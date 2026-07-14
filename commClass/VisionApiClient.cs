using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace gcp_Wpf.commClass
{
    /// <summary>
    /// Vision API HTTP 클라이언트
    /// 지상반 → Rust GC 서버 간 HTTP 통신 담당
    /// GC API 경로 기준 (/api/gc/..., 포트 3080)
    /// </summary>
    public class VisionApiClient
    {
        private readonly HttpClient _client;
        private readonly HttpClient _lidarClient;   // 라이다 스캔 전용 — 프레임 최대 100이면 30s 전역상한을 넘길 수 있어 별도 클라이언트로 넉넉한 상한(180s) 부여. 기존 호출 동작 불변.
        private string _baseUrl;

        public string BaseUrl => _baseUrl;
        public bool IsConfigured => !string.IsNullOrEmpty(_baseUrl);

        // 디버깅용: 마지막 요청/응답 raw 데이터
        public string LastRequestJson { get; private set; } = "";
        public string LastResponseJson { get; private set; } = "";
        public int LastHttpStatusCode { get; private set; }

        /// <summary>HealthCheckAsync 등에서 실패 시 원인 메시지 (Connection refused, timeout 등)</summary>
        public string LastError { get; private set; } = "";

        private static string Truncate(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return "(empty)";
            return s.Length <= maxLen ? s : s.Substring(0, maxLen) + "...(truncated)";
        }

        /// <summary>캡처/추론 1건당 최대 대기(초). 비전 서버가 행(hang)일 때 전역 30s 대신 이 값으로 빨리 끊어 Stop 반응성을 확보.</summary>
        public int PerCallTimeoutSeconds { get; set; } = 15;

        public VisionApiClient()
        {
            _client = new HttpClient();
            _client.Timeout = TimeSpan.FromSeconds(30);   // 전역 상한 (per-call은 PerCallTimeoutSeconds로 별도 제어)

            _lidarClient = new HttpClient();
            _lidarClient.Timeout = TimeSpan.FromSeconds(180);   // 라이다 스캔(최대 100프레임)은 일반 호출보다 오래 걸릴 수 있어 넉넉한 상한
        }

        /// <summary>
        /// per-call 타임아웃 + 외부 취소(ct)를 합친 토큰으로 POST.
        /// ct(사용자 Stop)가 켜지면 OperationCanceledException 전파, per-call 타임아웃이면 호출부에서 실패 응답으로 변환.
        /// </summary>
        private async Task<HttpResponseMessage> PostWithTimeoutAsync(string url, HttpContent content, CancellationToken ct)
        {
            using (var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(PerCallTimeoutSeconds)))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token))
            {
                return await _client.PostAsync(url, content, linked.Token);
            }
        }

        // 라이다 서비스는 비전서버(3080)와 별도 프로세스 — 같은 호스트, 포트 9000 고정 (스펙 2026-07-02)
        private string _lidarSvcUrl = "http://127.0.0.1:9000";
        public string LidarSvcUrl => _lidarSvcUrl;

        public void SetBaseUrl(string ip, int port)
        {
            _baseUrl = $"http://{ip}:{port}";
            _lidarSvcUrl = $"http://{ip}:9000";
        }

        // ================================================================
        // 상태 확인 API (스펙 Rev.2 — 2026-03)
        // ================================================================

        /// <summary>
        /// GET /api/gc/req/cameras/status
        /// 각 카메라 IP에 ping을 보내 네트워크 도달 가능 여부 확인.
        /// 응답: 객체 (키: camera_id, 값: "ok" | "unreachable")
        /// </summary>
        public async Task<CameraStatusResponse> CheckCameraStatusAsync()
        {
            var response = await _client.GetAsync($"{_baseUrl}/api/gc/req/cameras/status");
            var json = await response.Content.ReadAsStringAsync();
            LastResponseJson = json;
            LastHttpStatusCode = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Camera status API error ({(int)response.StatusCode}): {json}");
            return JsonSerializer.Deserialize<CameraStatusResponse>(json) ?? new CameraStatusResponse();
        }

        /// <summary>
        /// GET /api/gc/req/rtsp_status
        /// RTSP 커넥션 풀 연결 상태. 응답: 객체 (키: camera_id, 값: "connected" | "disconnected")
        /// </summary>
        public async Task<RtspStatusResponse> CheckRtspStatusAsync()
        {
            var response = await _client.GetAsync($"{_baseUrl}/api/gc/req/rtsp_status");
            var json = await response.Content.ReadAsStringAsync();
            LastResponseJson = json;
            LastHttpStatusCode = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"RTSP status API error ({(int)response.StatusCode}): {json}");
            return JsonSerializer.Deserialize<RtspStatusResponse>(json) ?? new RtspStatusResponse();
        }

        /// <summary>
        /// 서버 헬스체크 — cameras/status 응답 = 살아있음으로 판정.
        /// GC API에 별도 /health 엔드포인트는 없음.
        /// 실패 시 LastError에 원인 메시지 저장 (Connection refused, timeout, HTTP status 등).
        /// </summary>
        public async Task<bool> HealthCheckAsync()
        {
            LastError = "";
            try
            {
                var response = await _client.GetAsync($"{_baseUrl}/api/gc/req/cameras/status");
                LastHttpStatusCode = (int)response.StatusCode;
                if (response.IsSuccessStatusCode) return true;

                string body = "";
                try { body = await response.Content.ReadAsStringAsync(); } catch { }
                LastError = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}" +
                            (string.IsNullOrWhiteSpace(body) ? "" : $" body=[{Truncate(body, 200)}]");
                return false;
            }
            catch (HttpRequestException ex)
            {
                // 연결 거부 / DNS 실패 / 소켓 종료 등
                string inner = ex.InnerException?.Message ?? "";
                LastError = $"HttpRequestException: {ex.Message}" +
                            (string.IsNullOrWhiteSpace(inner) ? "" : $" | inner: {inner}");
                return false;
            }
            catch (TaskCanceledException ex)
            {
                // 30초 타임아웃 등
                LastError = $"Timeout: {ex.Message}";
                return false;
            }
            catch (Exception ex)
            {
                LastError = $"{ex.GetType().Name}: {ex.Message}";
                return false;
            }
        }

        // ================================================================
        // RTSP 연결 API
        // ================================================================

        /// <summary>
        /// POST /api/gc/cmd/{camera_id}/connect
        /// RTSP 풀에 연결 유지
        /// </summary>
        public async Task<RtspResponse> ConnectRtspAsync(string cameraId)
        {
            var response = await _client.PostAsync($"{_baseUrl}/api/gc/cmd/{cameraId}/connect", null);
            var json = await response.Content.ReadAsStringAsync();
            LastResponseJson = json;
            LastHttpStatusCode = (int)response.StatusCode;

            RtspResponse result;
            try
            {
                result = JsonSerializer.Deserialize<RtspResponse>(json);
            }
            catch (JsonException)
            {
                result = new RtspResponse
                {
                    Success = response.IsSuccessStatusCode,
                    Message = json
                };
            }
            if (result == null)
                result = new RtspResponse { Success = response.IsSuccessStatusCode, Message = json };
            result.HttpStatusCode = (int)response.StatusCode;
            return result;
        }

        /// <summary>
        /// POST /api/gc/cmd/{camera_id}/disconnect
        /// RTSP 연결 해제
        /// </summary>
        public async Task<RtspResponse> DisconnectRtspAsync(string cameraId)
        {
            var response = await _client.PostAsync($"{_baseUrl}/api/gc/cmd/{cameraId}/disconnect", null);
            var json = await response.Content.ReadAsStringAsync();
            LastResponseJson = json;
            LastHttpStatusCode = (int)response.StatusCode;

            RtspResponse result;
            try
            {
                result = JsonSerializer.Deserialize<RtspResponse>(json);
            }
            catch (JsonException)
            {
                result = new RtspResponse
                {
                    Success = response.IsSuccessStatusCode,
                    Message = json
                };
            }
            if (result == null)
                result = new RtspResponse { Success = response.IsSuccessStatusCode, Message = json };
            result.HttpStatusCode = (int)response.StatusCode;
            return result;
        }

        // ================================================================
        // 캡처 API
        // ================================================================

        /// <summary>
        /// POST /api/gc/cmd/{camera_id}/capture
        /// 영상 캡처 저장
        /// </summary>
        public async Task<CaptureResponse> RequestCaptureAsync(string cameraId, CaptureRequest request, CancellationToken ct = default)
        {
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            LastRequestJson = json;

            HttpResponseMessage response;
            try { response = await PostWithTimeoutAsync($"{_baseUrl}/api/gc/cmd/{cameraId}/capture", content, ct); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return new CaptureResponse { Success = false, Error = $"per-call 타임아웃 {PerCallTimeoutSeconds}s 초과 — 비전 서버 무응답" };
            }
            var responseJson = await response.Content.ReadAsStringAsync();
            LastResponseJson = responseJson;
            LastHttpStatusCode = (int)response.StatusCode;

            CaptureResponse result;
            try
            {
                result = JsonSerializer.Deserialize<CaptureResponse>(responseJson);
            }
            catch (JsonException ex)
            {
                result = new CaptureResponse
                {
                    Success = false,
                    Error = $"JSON 파싱 실패: {ex.Message} | raw={Truncate(responseJson, 300)}"
                };
            }
            // 본문이 리터럴 "null"이면 Deserialize가 예외 없이 null 반환 → 아래 대입에서 NRE. 명확한 실패객체로 치환.
            if (result == null)
                result = new CaptureResponse { Success = false, Error = $"빈/null 응답 본문 (HTTP {(int)response.StatusCode}) raw={Truncate(responseJson, 300)}" };
            result.HttpStatusCode = (int)response.StatusCode;
            return result;
        }

        // ================================================================
        // 추론 API
        // ================================================================

        /// <summary>
        /// POST /api/gc/cmd/{camera_id}/start_x_inference
        /// 주행(X) 추론
        /// </summary>
        public async Task<TravelInferenceResponse> RequestTravelInferenceAsync(string cameraId, CaptureRequest request, CancellationToken ct = default)
        {
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            LastRequestJson = json;

            HttpResponseMessage response;
            try { response = await PostWithTimeoutAsync($"{_baseUrl}/api/gc/cmd/{cameraId}/start_x_inference", content, ct); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return new TravelInferenceResponse { Success = false, Error = $"per-call 타임아웃 {PerCallTimeoutSeconds}s 초과 — 비전 서버 무응답" };
            }
            var responseJson = await response.Content.ReadAsStringAsync();
            LastResponseJson = responseJson;
            LastHttpStatusCode = (int)response.StatusCode;

            TravelInferenceResponse result;
            try
            {
                result = JsonSerializer.Deserialize<TravelInferenceResponse>(responseJson);
            }
            catch (JsonException ex)
            {
                result = new TravelInferenceResponse
                {
                    Success = false,
                    Error = $"JSON 파싱 실패: {ex.Message} | raw={Truncate(responseJson, 300)}"
                };
            }
            if (result == null)
                result = new TravelInferenceResponse { Success = false, Error = $"빈/null 응답 본문 (HTTP {(int)response.StatusCode}) raw={Truncate(responseJson, 300)}" };
            result.HttpStatusCode = (int)response.StatusCode;
            return result;
        }

        /// <summary>
        /// POST /api/gc/cmd/{camera_id}/start_z_inference
        /// 승강(Z) 추론
        /// </summary>
        public async Task<HoistInferenceResponse> RequestHoistInferenceAsync(string cameraId, CaptureRequest request, CancellationToken ct = default)
        {
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            LastRequestJson = json;

            HttpResponseMessage response;
            try { response = await PostWithTimeoutAsync($"{_baseUrl}/api/gc/cmd/{cameraId}/start_z_inference", content, ct); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return new HoistInferenceResponse { Success = false, Error = $"per-call 타임아웃 {PerCallTimeoutSeconds}s 초과 — 비전 서버 무응답" };
            }
            var responseJson = await response.Content.ReadAsStringAsync();
            LastResponseJson = responseJson;
            LastHttpStatusCode = (int)response.StatusCode;

            HoistInferenceResponse result;
            try
            {
                result = JsonSerializer.Deserialize<HoistInferenceResponse>(responseJson);
            }
            catch (JsonException ex)
            {
                result = new HoistInferenceResponse
                {
                    Success = false,
                    Error = $"JSON 파싱 실패: {ex.Message} | raw={Truncate(responseJson, 300)}"
                };
            }
            if (result == null)
                result = new HoistInferenceResponse { Success = false, Error = $"빈/null 응답 본문 (HTTP {(int)response.StatusCode}) raw={Truncate(responseJson, 300)}" };
            result.HttpStatusCode = (int)response.StatusCode;
            result.Source = "lidar";
            return result;
        }

        /// <summary>
        /// POST /api/gc/cmd/{camera_id}/start_z_inference_vision — 승강(Z) '비전 모듈' 추론.
        /// ★ 2026-07-08 스캐폴드: 비전 서버 아직 미구현. Z 모듈 우선순위(세팅 선택) 대비용 클라이언트 뼈대.
        ///   실제 엔드포인트명/파라미터/응답 스펙은 비전팀 확정 후 조정. 현재는 서버 미구현이라 호출 시 404 등 실패 →
        ///   상위 ResolveHoistResolvedAsync가 라이다 결과로 폴백(디폴트 visionHoistEnabled=false면 아예 호출 안 함).
        /// </summary>
        public async Task<HoistInferenceResponse> RequestHoistInferenceVisionAsync(string cameraId, CaptureRequest request, CancellationToken ct = default)
        {
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            LastRequestJson = json;

            HttpResponseMessage response;
            try { response = await PostWithTimeoutAsync($"{_baseUrl}/api/gc/cmd/{cameraId}/start_z_inference_vision", content, ct); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return new HoistInferenceResponse { Success = false, Error = $"per-call 타임아웃 {PerCallTimeoutSeconds}s 초과 — 비전 서버 무응답", Source = "vision" };
            }
            var responseJson = await response.Content.ReadAsStringAsync();
            LastResponseJson = responseJson;
            LastHttpStatusCode = (int)response.StatusCode;

            HoistInferenceResponse result;
            try { result = JsonSerializer.Deserialize<HoistInferenceResponse>(responseJson); }
            catch (JsonException ex)
            {
                result = new HoistInferenceResponse { Success = false, Error = $"JSON 파싱 실패: {ex.Message} | raw={Truncate(responseJson, 300)}" };
            }
            if (result == null)
                result = new HoistInferenceResponse { Success = false, Error = $"빈/null 응답 본문 (HTTP {(int)response.StatusCode}) raw={Truncate(responseJson, 300)}" };
            result.HttpStatusCode = (int)response.StatusCode;
            result.Source = "vision";
            return result;
        }

        // ================================================================
        // Vision 서버 시작/종료는 불필요 (항상 실행중)
        // 하위 호환용 더미 메서드
        // ================================================================

        public Task<bool> StartVisionServerAsync() => HealthCheckAsync();
        public Task<bool> StopVisionServerAsync() => Task.FromResult(true);

        // ================================================================
        // Calibration API (스펙 §10 — 세팅·회귀)
        // ================================================================

        /// <summary>
        /// POST /api/gc/cmd/{camera_id}/calibration/capture
        /// 세팅 폴더 캡처 + 렌즈 보정 + 주행(세팅)
        /// </summary>
        public async Task<CalibrationCaptureResponse> CalibrationCaptureAsync(string cameraId, CaptureRequest request)
        {
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            LastRequestJson = json;

            var response = await _client.PostAsync($"{_baseUrl}/api/gc/cmd/{cameraId}/calibration/capture", content);
            var responseJson = await response.Content.ReadAsStringAsync();
            LastResponseJson = responseJson;
            LastHttpStatusCode = (int)response.StatusCode;

            CalibrationCaptureResponse result;
            try { result = JsonSerializer.Deserialize<CalibrationCaptureResponse>(responseJson); }
            catch (JsonException ex)
            {
                result = new CalibrationCaptureResponse
                {
                    Success = false,
                    Error = $"JSON 파싱 실패: {ex.Message} | raw={Truncate(responseJson, 300)}"
                };
            }
            if (result == null)
                result = new CalibrationCaptureResponse { Success = false, Error = $"빈/null 응답 본문 (HTTP {(int)response.StatusCode}) raw={Truncate(responseJson, 300)}" };
            result.HttpStatusCode = (int)response.StatusCode;
            return result;
        }

        /// <summary>
        /// POST /api/gc/cmd/{camera_id}/calibration/inference
        /// 세팅 폴더 기준 렌즈 + run_travel
        /// </summary>
        public async Task<CalibrationInferenceResponse> CalibrationInferenceAsync(string cameraId, CalibrationInferenceRequest request)
        {
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            LastRequestJson = json;

            var response = await _client.PostAsync($"{_baseUrl}/api/gc/cmd/{cameraId}/calibration/inference", content);
            var responseJson = await response.Content.ReadAsStringAsync();
            LastResponseJson = responseJson;
            LastHttpStatusCode = (int)response.StatusCode;

            CalibrationInferenceResponse result;
            try { result = JsonSerializer.Deserialize<CalibrationInferenceResponse>(responseJson); }
            catch (JsonException ex)
            {
                result = new CalibrationInferenceResponse
                {
                    Success = false,
                    Error = $"JSON 파싱 실패: {ex.Message} | raw={Truncate(responseJson, 300)}"
                };
            }
            if (result == null)
                result = new CalibrationInferenceResponse { Success = false, Error = $"빈/null 응답 본문 (HTTP {(int)response.StatusCode}) raw={Truncate(responseJson, 300)}" };
            result.HttpStatusCode = (int)response.StatusCode;
            return result;
        }

        /// <summary>
        /// POST /api/gc/cmd/calibration/compute
        /// 좌/우 샘플 데이터로 회귀·zero offset 산출
        /// </summary>
        public async Task<CalibrationComputeResponse> CalibrationComputeAsync(CalibrationComputeRequest request)
        {
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            LastRequestJson = json;

            var response = await _client.PostAsync($"{_baseUrl}/api/gc/cmd/calibration/compute", content);
            var responseJson = await response.Content.ReadAsStringAsync();
            LastResponseJson = responseJson;
            LastHttpStatusCode = (int)response.StatusCode;

            CalibrationComputeResponse result;
            try { result = JsonSerializer.Deserialize<CalibrationComputeResponse>(responseJson); }
            catch (JsonException ex)
            {
                result = new CalibrationComputeResponse
                {
                    Success = false,
                    Error = $"JSON 파싱 실패: {ex.Message} | raw={Truncate(responseJson, 300)}"
                };
            }
            if (result == null)
                result = new CalibrationComputeResponse { Success = false, Error = $"빈/null 응답 본문 (HTTP {(int)response.StatusCode}) raw={Truncate(responseJson, 300)}" };
            result.HttpStatusCode = (int)response.StatusCode;
            return result;
        }

        /// <summary>
        /// GET /api/gc/cmd/calibration/status
        /// 캘리브레이션 파일 존재/요약 조회
        /// </summary>
        public async Task<CalibrationStatusResponse> CalibrationStatusAsync(string cameraId)
        {
            LastRequestJson = $"?camera_id={cameraId}";

            var response = await _client.GetAsync($"{_baseUrl}/api/gc/cmd/calibration/status?camera_id={cameraId}");
            var responseJson = await response.Content.ReadAsStringAsync();
            LastResponseJson = responseJson;
            LastHttpStatusCode = (int)response.StatusCode;

            CalibrationStatusResponse result;
            try { result = JsonSerializer.Deserialize<CalibrationStatusResponse>(responseJson); }
            catch (JsonException ex)
            {
                result = new CalibrationStatusResponse
                {
                    Error = $"JSON 파싱 실패: {ex.Message} | raw={Truncate(responseJson, 300)}"
                };
            }
            if (result == null)
                result = new CalibrationStatusResponse { Error = $"빈/null 응답 본문 (HTTP {(int)response.StatusCode}) raw={Truncate(responseJson, 300)}" };
            result.HttpStatusCode = (int)response.StatusCode;
            return result;
        }

        /// <summary>
        /// POST /api/gc/cmd/calibration/cleanup
        /// setting_images/{camera_id} 파일 삭제
        /// </summary>
        public async Task<CalibrationCleanupResponse> CalibrationCleanupAsync(string cameraId)
        {
            var body = new { camera_id = cameraId };
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            LastRequestJson = json;

            var response = await _client.PostAsync($"{_baseUrl}/api/gc/cmd/calibration/cleanup", content);
            var responseJson = await response.Content.ReadAsStringAsync();
            LastResponseJson = responseJson;
            LastHttpStatusCode = (int)response.StatusCode;

            CalibrationCleanupResponse result;
            try { result = JsonSerializer.Deserialize<CalibrationCleanupResponse>(responseJson); }
            catch (JsonException ex)
            {
                result = new CalibrationCleanupResponse
                {
                    Error = $"JSON 파싱 실패: {ex.Message} | raw={Truncate(responseJson, 300)}"
                };
            }
            if (result == null)
                result = new CalibrationCleanupResponse { Error = $"빈/null 응답 본문 (HTTP {(int)response.StatusCode}) raw={Truncate(responseJson, 300)}" };
            result.HttpStatusCode = (int)response.StatusCode;
            return result;
        }

        // ================================================================
        // LiDAR API
        // ================================================================

        /// <summary>
        /// POST /api/gc/cmd/lidar/scan_start — 승강(Z) 추론 전 필수 (스펙 2026-07-02 확정).
        /// frames 생략(null) → body 없이 전송 → 서버 기본 20프레임. frames 지정 → 5~100으로 클램프하여 { "frames": N } 전송.
        /// ★측정 결과(offset)는 응답에 안 담기고 서버 내부저장(120초 유효) — Z추론이 이 저장값을 쓰므로
        ///   셀마다 Z추론 직전 재스캔 필요(120s 초과 시 Z추론이 failed_step=lidar_missing으로 502 거부).
        /// 실패: 502(측정 실패) / 503(비전서버 불통). raw는 LastResponseJson에 항상 저장.
        /// </summary>
        public async Task<LidarScanResponse> LidarScanStartAsync(int? frames = null, CancellationToken ct = default)
        {
            HttpContent content;
            if (frames.HasValue)
            {
                int f = frames.Value;
                if (f < 5) f = 5;
                else if (f > 100) f = 100;
                var bodyJson = JsonSerializer.Serialize(new LidarScanRequest { Frames = f });
                content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
                LastRequestJson = bodyJson;
            }
            else
            {
                // 서버가 Content-Type: application/json 필수 → null content(Content-Type 헤더 없음)면 HTTP 415.
                // frames 미지정 시 빈 객체 {}를 application/json으로 전송(frames 키 없음 → 서버 기본 20프레임).
                content = new StringContent("{}", Encoding.UTF8, "application/json");
                LastRequestJson = "{} (frames 미지정 — 서버 기본 20프레임)";
            }

            HttpResponseMessage response;
            try
            {
                // _lidarClient.Timeout(180s)가 전역 상한. ct(사용자 취소)는 그대로 전파.
                response = await _lidarClient.PostAsync($"{_baseUrl}/api/gc/cmd/lidar/scan_start", content, ct);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return new LidarScanResponse { Success = false, Error = "라이다 스캔 타임아웃 초과 — 라이다/비전 서버 무응답" };
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            LastResponseJson = responseJson;
            LastHttpStatusCode = (int)response.StatusCode;

            LidarScanResponse result;
            try { result = JsonSerializer.Deserialize<LidarScanResponse>(responseJson); }
            catch (JsonException ex)
            {
                result = new LidarScanResponse { Success = false, Error = $"JSON 파싱 실패: {ex.Message} | raw={Truncate(responseJson, 300)}" };
            }
            if (result == null)
                result = new LidarScanResponse { Success = false, Error = $"빈/null 응답 본문 (HTTP {(int)response.StatusCode}) raw={Truncate(responseJson, 300)}" };
            result.HttpStatusCode = (int)response.StatusCode;
            return result;
        }

        /// <summary>
        /// GET http://{host}:9000/api/lidar/status — 라이다 서비스(포트 9000, 비전서버와 별도) 상태.
        /// 연결/워밍업/baseline 설정 여부 점검용 — 스캔·Z추론 실패 진단에 사용.
        /// 진단 보조라 실패 시 예외 대신 null 반환(원인은 LastError에 저장). 사용자 취소(ct)는 전파.
        /// </summary>
        public async Task<LidarStatusResponse> LidarStatusAsync(CancellationToken ct = default)
        {
            try
            {
                var response = await _client.GetAsync($"{_lidarSvcUrl}/api/lidar/status", ct);
                var json = await response.Content.ReadAsStringAsync();
                LastResponseJson = json;
                LastHttpStatusCode = (int)response.StatusCode;
                var result = JsonSerializer.Deserialize<LidarStatusResponse>(json);
                if (result != null) result.HttpStatusCode = (int)response.StatusCode;
                return result;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return null;
            }
        }

        /// <summary>
        /// POST http://{host}:9000/api/lidar/baseline — 라이다 기준높이(baseline) 설정 (스펙 2026-07-02 §③).
        /// 정지 상태에서 서버가 고정 50프레임 측정 → 기준 중심높이/기울기를 lidar.json에 저장.
        /// baseline 미설정 시 scan_start는 성공해도 offset 무효 → Z추론이 lidar_missing으로 실패.
        /// 최초 설치/기구 재배치 후 1회 필수. 빈 본문 {}를 application/json으로 전송(Content-Type 필수). 사용자 취소(ct)는 전파.
        /// </summary>
        public async Task<LidarBaselineResponse> LidarSetBaselineAsync(CancellationToken ct = default)
        {
            // scan_start와 동일하게 서버가 Content-Type: application/json을 요구할 수 있어 빈 객체 {}를 전송(필드 없음 → 서버 고정 50프레임).
            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            LastRequestJson = "{} (서버 고정 50프레임 baseline)";

            HttpResponseMessage response;
            try
            {
                // baseline은 status와 같은 라이다 서비스(:9000). 50프레임 측정이 30s를 넘길 수 있어 _lidarClient(180s) 사용.
                response = await _lidarClient.PostAsync($"{_lidarSvcUrl}/api/lidar/baseline", content, ct);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return new LidarBaselineResponse { Success = false, Error = "라이다 baseline 타임아웃 초과 — 라이다 서비스 무응답" };
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            LastResponseJson = responseJson;
            LastHttpStatusCode = (int)response.StatusCode;

            LidarBaselineResponse result;
            try { result = JsonSerializer.Deserialize<LidarBaselineResponse>(responseJson); }
            catch (JsonException ex)
            {
                result = new LidarBaselineResponse { Success = false, Error = $"JSON 파싱 실패: {ex.Message} | raw={Truncate(responseJson, 300)}" };
            }
            if (result == null)
                result = new LidarBaselineResponse { Success = false, Error = $"빈/null 응답 본문 (HTTP {(int)response.StatusCode}) raw={Truncate(responseJson, 300)}" };
            result.HttpStatusCode = (int)response.StatusCode;
            return result;
        }
    }

    // ================================================================
    // Request/Response Models (GC API 문서 §1~§2 기준)
    // ================================================================

    /// <summary>
    /// 공통 요청 Body (스펙 Rev.2 §5)
    /// capture / start_x_inference / start_z_inference 모두 동일
    /// </summary>
    public class CaptureRequest
    {
        [JsonPropertyName("row")]
        public int Row { get; set; }

        [JsonPropertyName("bay")]
        public int Bay { get; set; }

        [JsonPropertyName("bay_pos")]
        public long BayPos { get; set; }

        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("level_pos")]
        public long LevelPos { get; set; }

        [JsonPropertyName("has_cargo")]
        public bool HasCargo { get; set; }

        /// <summary>
        /// 선택: 렌즈 보정 계수 JSON 파일명만 (경로 X).
        /// 비우거나 생략 시 GC가 기본 계수 경로 사용.
        /// </summary>
        [JsonPropertyName("lens_calibration_file")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LensCalibrationFile { get; set; }
    }

    /// <summary>
    /// RTSP connect/disconnect 응답
    /// </summary>
    public class RtspResponse
    {
        [JsonPropertyName("camera_id")]
        public string? CameraId { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("failed_step")]
        public string? FailedStep { get; set; }

        [JsonPropertyName("elapsed_ms")]
        public double ElapsedMs { get; set; }

        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }

        [JsonIgnore]
        public int HttpStatusCode { get; set; }
    }

    /// <summary>
    /// 캡처 응답 (스펙 Rev.2 §2-1)
    /// 성공: success=true, raw_path 항상, lens_calibrated/calibrated_path/lens_error 선택
    /// 실패: success=false, error=string, failed_step="capture"
    /// </summary>
    public class CaptureResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("camera_id")]
        public string? CameraId { get; set; }

        [JsonPropertyName("filename")]
        public string? Filename { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("failed_step")]
        public string? FailedStep { get; set; }

        [JsonPropertyName("elapsed_ms")]
        public double ElapsedMs { get; set; }

        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }

        // 요청 echo 필드
        [JsonPropertyName("row")]
        public int Row { get; set; }

        [JsonPropertyName("bay")]
        public int Bay { get; set; }

        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("bay_pos")]
        public long BayPos { get; set; }

        [JsonPropertyName("level_pos")]
        public long LevelPos { get; set; }

        [JsonPropertyName("has_cargo")]
        public bool HasCargo { get; set; }

        [JsonPropertyName("raw_path")]
        public string? RawPath { get; set; }

        // 신규 — 운영 렌즈 보정 (Rev.2)
        [JsonPropertyName("lens_calibrated")]
        public bool? LensCalibrated { get; set; }

        [JsonPropertyName("calibrated_path")]
        public string? CalibratedPath { get; set; }

        [JsonPropertyName("lens_error")]
        public string? LensError { get; set; }

        [JsonIgnore]
        public int HttpStatusCode { get; set; }
    }

    /// <summary>
    /// 주행(X) 추론 응답 (스펙 Rev.2 §2-2 start_x_inference)
    /// 성공: travel_move_mm + inferred_bay_pos.
    ///   inferred_bay_pos ≈ cur_bay_pos + round(travel_move_mm)
    /// 실패: failed_step ∈ { "lens_coefficients", "inference", "postprocess" }
    /// </summary>
    public class TravelInferenceResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("camera_id")]
        public string? CameraId { get; set; }

        [JsonPropertyName("cell_id")]
        public string? CellId { get; set; }

        /// <summary>요청 시점 주행 위치(요청의 bay_pos echo)</summary>
        [JsonPropertyName("cur_bay_pos")]
        public long CurBayPos { get; set; }

        /// <summary>요청 시점 승강 위치(요청의 level_pos echo)</summary>
        [JsonPropertyName("cur_level_pos")]
        public long CurLevelPos { get; set; }

        /// <summary>회귀·zero_offset 적용 후 주행 보정 이동량(mm) — 음수 가능</summary>
        [JsonPropertyName("travel_move_mm")]
        public double TravelMoveMm { get; set; }

        /// <summary>목표 주행 좌표(반올림). 정상 동작 시 cur_bay_pos + round(travel_move_mm)</summary>
        [JsonPropertyName("inferred_bay_pos")]
        public long InferredBayPos { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("failed_step")]
        public string? FailedStep { get; set; }

        [JsonPropertyName("elapsed_ms")]
        public double ElapsedMs { get; set; }

        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }

        /// <summary>?debug=1 일 때만 포함되는 단계별 ms 객체 (lens_coeff_check_ms 등)</summary>
        [JsonPropertyName("debug_timings")]
        public System.Text.Json.JsonElement? DebugTimings { get; set; }

        [JsonIgnore]
        public int HttpStatusCode { get; set; }
    }

    /// <summary>
    /// 승강(Z) 추론 응답 (스펙 Rev.2 §2-3 start_z_inference + 2026-07-02 라이다 개정)
    /// 성공: hoist_move_mm(라이다 보정값) + inferred_level_pos. message="Hoist inference done (lidar)".
    /// 실패: failed_step ∈ { "lidar_missing", "lens_coefficients", "calibrate_lens", "travel", "hoist", "postprocess" }
    /// ★lidar_missing(502) 사유 3종: 스캔 안 함 / 스캔 후 120초 초과(stale) / baseline 미설정·유효프레임 없음.
    /// </summary>
    public class HoistInferenceResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("camera_id")]
        public string? CameraId { get; set; }

        [JsonPropertyName("cell_id")]
        public string? CellId { get; set; }

        /// <summary>요청 시점 주행 위치</summary>
        [JsonPropertyName("cur_bay_pos")]
        public long CurBayPos { get; set; }

        /// <summary>요청 시점 승강 위치</summary>
        [JsonPropertyName("cur_level_pos")]
        public long CurLevelPos { get; set; }

        /// <summary>Y축 회귀·zero_offset 적용 후 승강 보정량(mm)</summary>
        [JsonPropertyName("hoist_move_mm")]
        public double HoistMoveMm { get; set; }

        /// <summary>목표 승강 좌표(반올림)</summary>
        [JsonPropertyName("inferred_level_pos")]
        public long InferredLevelPos { get; set; }

        /// <summary>[클라이언트 태그, 서버 응답 아님] 이 결과를 낸 모듈 — "lidar" | "vision". Z 모듈 우선순위 로직용.</summary>
        [JsonIgnore]
        public string Source { get; set; } = "lidar";

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("failed_step")]
        public string? FailedStep { get; set; }

        [JsonPropertyName("elapsed_ms")]
        public double ElapsedMs { get; set; }

        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }

        [JsonIgnore]
        public int HttpStatusCode { get; set; }
    }

    /// <summary>
    /// GET /api/gc/req/cameras/status 응답 (스펙 Rev.2 §3-1)
    /// 값: "ok" | "unreachable"
    /// </summary>
    public class CameraStatusResponse
    {
        [JsonPropertyName("camera1")]
        public string? Camera1 { get; set; }

        [JsonPropertyName("camera2")]
        public string? Camera2 { get; set; }

        [JsonPropertyName("camera3")]
        public string? Camera3 { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    /// <summary>
    /// GET /api/gc/req/rtsp_status 응답 (스펙 Rev.2 §3-2)
    /// 값: "connected" | "disconnected"
    /// </summary>
    public class RtspStatusResponse
    {
        [JsonPropertyName("camera1")]
        public string? Camera1 { get; set; }

        [JsonPropertyName("camera2")]
        public string? Camera2 { get; set; }

        [JsonPropertyName("camera3")]
        public string? Camera3 { get; set; }
    }

    // ================================================================
    // Calibration Models (스펙 §10)
    // ================================================================

    /// <summary>
    /// POST /api/gc/cmd/{camera_id}/calibration/capture 응답
    /// </summary>
    public class CalibrationCaptureResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("camera_id")]
        public string? CameraId { get; set; }

        [JsonPropertyName("filename")]
        public string? Filename { get; set; }

        [JsonPropertyName("raw_path")]
        public string? RawPath { get; set; }

        [JsonPropertyName("elapsed_ms")]
        public double ElapsedMs { get; set; }   // 서버가 소수 ms(예 578.89) 전송 → long이면 JSON 파싱예외→거짓 실패. 나머지 응답과 동일하게 double.

        [JsonPropertyName("pipeline")]
        public JsonElement? Pipeline { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("failed_step")]
        public string? FailedStep { get; set; }

        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }

        [JsonIgnore]
        public int HttpStatusCode { get; set; }
    }

    /// <summary>
    /// POST /api/gc/cmd/{camera_id}/calibration/inference 요청
    /// </summary>
    public class CalibrationInferenceRequest
    {
        [JsonPropertyName("row")]
        public int Row { get; set; }

        [JsonPropertyName("bay")]
        public int Bay { get; set; }

        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("bay_pos")]
        public int BayPos { get; set; }

        [JsonPropertyName("level_pos")]
        public int LevelPos { get; set; }

        [JsonPropertyName("has_cargo")]
        public bool HasCargo { get; set; }

        [JsonPropertyName("lens_calibration_file")]
        public string? LensCalibrationFile { get; set; }
    }

    /// <summary>
    /// POST /api/gc/cmd/{camera_id}/calibration/inference 응답
    /// </summary>
    public class CalibrationInferenceResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("travel_move_mm")]
        public int TravelMoveMm { get; set; }

        [JsonPropertyName("travel_offset_mm")]
        public int TravelOffsetMm { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("elapsed_ms")]
        public double ElapsedMs { get; set; }   // 서버가 소수 ms(예 578.89) 전송 → long이면 JSON 파싱예외→거짓 실패. 나머지 응답과 동일하게 double.

        [JsonIgnore]
        public int HttpStatusCode { get; set; }
    }

    /// <summary>
    /// POST /api/gc/cmd/calibration/compute 요청
    /// 좌3 우3 — reference 기준 좌/우 오프셋 위치의 bay_positions 배열
    /// </summary>
    public class CalibrationComputeRequest
    {
        [JsonPropertyName("camera_id")]
        public string CameraId { get; set; } = "";

        [JsonPropertyName("row")]
        public int Row { get; set; }

        [JsonPropertyName("bay")]
        public int Bay { get; set; }

        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("reference_bay_pos")]
        public int ReferenceBayPos { get; set; }

        [JsonPropertyName("bay_positions")]
        public int[] BayPositions { get; set; } = Array.Empty<int>();

        [JsonPropertyName("reference_level_pos")]
        public int ReferenceLevelPos { get; set; }

        [JsonPropertyName("level_positions")]
        public int[] LevelPositions { get; set; } = Array.Empty<int>();
    }

    /// <summary>
    /// POST /api/gc/cmd/calibration/compute 응답
    /// </summary>
    public class CalibrationComputeResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("zero_offset_x_mm")]
        public double ZeroOffsetXMm { get; set; }

        [JsonPropertyName("zero_offset_y_mm")]
        public double ZeroOffsetYMm { get; set; }

        [JsonPropertyName("regression_x_r2")]
        public double RegressionXR2 { get; set; }

        [JsonPropertyName("regression_y_r2")]
        public double RegressionYR2 { get; set; }

        [JsonPropertyName("samples_x")]
        public int SamplesX { get; set; }

        [JsonPropertyName("samples_y")]
        public int SamplesY { get; set; }

        [JsonPropertyName("saved_path")]
        public string? SavedPath { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("elapsed_ms")]
        public double ElapsedMs { get; set; }   // 서버가 소수 ms(예 578.89) 전송 → long이면 JSON 파싱예외→거짓 실패. 나머지 응답과 동일하게 double.

        [JsonIgnore]
        public int HttpStatusCode { get; set; }
    }

    /// <summary>
    /// GET /api/gc/cmd/calibration/status 응답
    /// </summary>
    public class CalibrationStatusResponse
    {
        [JsonPropertyName("calibration_exists")]
        public bool CalibrationExists { get; set; }

        [JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }

        [JsonPropertyName("zero_offset_x_mm")]
        public double ZeroOffsetXMm { get; set; }

        [JsonPropertyName("zero_offset_y_mm")]
        public double ZeroOffsetYMm { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonIgnore]
        public int HttpStatusCode { get; set; }
    }

    /// <summary>
    /// POST /api/gc/cmd/calibration/cleanup 응답
    /// </summary>
    public class CalibrationCleanupResponse
    {
        [JsonPropertyName("deleted_count")]
        public int DeletedCount { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonIgnore]
        public int HttpStatusCode { get; set; }
    }

    // ================================================================
    // LiDAR Models
    // ================================================================

    /// <summary>
    /// POST /api/gc/cmd/lidar/scan_start 요청 Body.
    /// frames 미지정 시 클라이언트는 이 Body를 보내지 않음(서버 기본 20프레임). 지정 시 5~100.
    /// </summary>
    public class LidarScanRequest
    {
        [JsonPropertyName("frames")]
        public int Frames { get; set; }
    }

    /// <summary>
    /// POST /api/gc/cmd/lidar/scan_start 응답.
    /// 응답 스펙 미확정 — 다른 응답들과 공통인 필드만 typed로 두고, 그 외 서버 추가 필드는 Extra로 보존.
    /// </summary>
    public class LidarScanResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("camera_id")]
        public string? CameraId { get; set; }

        /// <summary>실제 사용된 프레임 수(서버 echo). body 없이 보냈다면 기본 20.</summary>
        [JsonPropertyName("frames")]
        public int? Frames { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("failed_step")]
        public string? FailedStep { get; set; }

        [JsonPropertyName("elapsed_ms")]
        public double ElapsedMs { get; set; }

        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }

        /// <summary>스펙 미확정 응답 필드 보존(point_count·output_path 등 서버가 추가로 주는 키).</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }

        [JsonIgnore]
        public int HttpStatusCode { get; set; }
    }

    /// <summary>
    /// GET http://{host}:9000/api/lidar/status 응답 (스펙 2026-07-02).
    /// baseline_y_center가 null이면 기준높이 미설정 — scan_start는 성공해도 offset 무효 →
    /// Z추론이 lidar_missing("measurement is invalid")으로 실패한다. 설치/기구 재배치 후 1회 baseline 필수.
    /// </summary>
    public class LidarStatusResponse
    {
        [JsonPropertyName("available")]
        public bool Available { get; set; }

        [JsonPropertyName("connected")]
        public bool Connected { get; set; }

        [JsonPropertyName("warming_up")]
        public bool WarmingUp { get; set; }

        /// <summary>기준 중심높이 — null = baseline 미설정</summary>
        [JsonPropertyName("baseline_y_center")]
        public double? BaselineYCenter { get; set; }

        [JsonPropertyName("hoist_offset_mm")]
        public double? HoistOffsetMm { get; set; }

        [JsonPropertyName("offset_smooth_mm")]
        public double? OffsetSmoothMm { get; set; }

        /// <summary>최근 측정 결과 { y_center, length, pass } — 구조 유동적이라 raw 보존</summary>
        [JsonPropertyName("result")]
        public JsonElement? Result { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }

        [JsonIgnore]
        public int HttpStatusCode { get; set; }
    }

    /// <summary>
    /// POST http://{host}:9000/api/lidar/baseline 응답 (스펙 미확정 — 관대 파싱).
    /// 공통 필드만 typed로 두고 서버 추가 필드는 Extra로 보존. baseline_y_center는 설정된 기준 중심높이(있으면 로그 확인용).
    /// </summary>
    public class LidarBaselineResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        /// <summary>설정된 기준 중심높이 — 응답에 있으면 로그로 확인</summary>
        [JsonPropertyName("baseline_y_center")]
        public double? BaselineYCenter { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("failed_step")]
        public string? FailedStep { get; set; }

        [JsonPropertyName("elapsed_ms")]
        public double ElapsedMs { get; set; }

        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }

        [JsonIgnore]
        public int HttpStatusCode { get; set; }
    }
}
