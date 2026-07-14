# 라이다 baseline 설정 + 스캔 frames 노출 — 구현 계획

> **For agentic workers:** 이 계획은 3개 태스크로 구성됩니다. 각 태스크는 독립적으로 `dotnet build` 성공으로 검증됩니다.
> **프로젝트 특성 주의:** WPF(.NET 6) HMI + 물리 라이다/비전 서버 대상 코드라 단위 테스트 스위트가 없고, git 저장소도 아닙니다. 따라서 TDD/커밋 단계 대신 **완료 게이트 = `dotnet build` 컴파일 성공 + 수동 기능 확인**을 사용합니다.

**Goal:** 지상반에서 라이다 baseline을 직접 설정하는 버튼을 추가하고, 수동 라이다 스캔의 frames(5~100)를 UI에서 지정할 수 있게 한다.

**Architecture:** `VisionApiClient`에 baseline POST 메서드/모델을 신설(기존 라이다 메서드와 동일한 관대 파싱). `PageAutoTeaching`에 baseline 버튼·핸들러·실행 래퍼를 추가하고, 수동 스캔 버튼에 frames 입력칸을 붙여 `RunLidarScanAsync`의 새 옵션 파라미터로 전달한다. 자동 티칭/CALIB/헬스체크 경로는 손대지 않아 기본 20프레임 동작이 그대로 유지된다.

**Tech Stack:** C# / .NET 6 (net6.0-windows) / WPF / System.Text.Json / HttpClient.

## Global Constraints

- 대상 파일: `commClass/VisionApiClient.cs`, `PageAutoTeaching.xaml`, `PageAutoTeaching.xaml.cs` (그 외 무수정).
- 기존 API 메서드·모델·자동 경로 라이다 스캔(기본 20프레임)은 **비변경**. `RunLidarScanAsync`의 기존 호출부 3곳(605/1074/1943)은 수정하지 않는다.
- baseline 엔드포인트는 **라이다 서비스(:9000, `_lidarSvcUrl`)**, body 없이 POST, `_lidarClient`(180s) 사용.
- frames 클램프(5~100)는 이미 `LidarScanStartAsync`가 처리 — UI에서 중복 클램프하지 않는다.
- 빌드 명령(프로젝트 루트에서): `dotnet build "gcp_Wpf.sln"` — 완료 게이트.
- 코드 스타일: 기존 라이다 메서드/버튼 핸들러 패턴(로그 태그 `[LIDAR]`/`[BASELINE]`, `AddLog`, `lidarBusy` 가드, `cIniAccess.Read`)을 그대로 따른다.

---

### Task 1: VisionApiClient — baseline POST 메서드 + 응답 모델

**Files:**
- Modify: `commClass/VisionApiClient.cs` (메서드는 `LidarStatusAsync` 정의 뒤 ~L583 다음, 모델은 `LidarStatusResponse` 클래스 뒤 ~L1160 다음)

**Interfaces:**
- Consumes: 기존 필드 `_lidarClient`, `_lidarSvcUrl`, `Truncate()`, `LastRequestJson/LastResponseJson/LastHttpStatusCode`.
- Produces:
  - `public async Task<LidarBaselineResponse> LidarSetBaselineAsync(CancellationToken ct = default)`
  - `public class LidarBaselineResponse` — `Success(bool)`, `Message(string?)`, `BaselineYCenter(double?)`, `Error(string?)`, `FailedStep(string?)`, `ElapsedMs(double)`, `Timestamp(string?)`, `Extra`, `HttpStatusCode(int)`

- [ ] **Step 1: `LidarSetBaselineAsync` 메서드 추가** (`LidarStatusAsync` 메서드 닫는 `}` 다음, `// LiDAR API` 리전 안)

```csharp
/// <summary>
/// POST http://{host}:9000/api/lidar/baseline — 라이다 기준높이(baseline) 설정 (스펙 2026-07-02 §③).
/// 정지 상태에서 서버가 고정 50프레임 측정 → 기준 중심높이/기울기를 lidar.json에 저장.
/// baseline 미설정 시 scan_start는 성공해도 offset 무효 → Z추론이 lidar_missing으로 실패.
/// 최초 설치/기구 재배치 후 1회 필수. body 없이 전송(스펙 §③). 사용자 취소(ct)는 전파.
/// </summary>
public async Task<LidarBaselineResponse> LidarSetBaselineAsync(CancellationToken ct = default)
{
    LastRequestJson = "(no body — 서버 고정 50프레임 baseline)";

    HttpResponseMessage response;
    try
    {
        // baseline은 status와 같은 라이다 서비스(:9000). 50프레임 측정이 30s를 넘길 수 있어 _lidarClient(180s) 사용.
        response = await _lidarClient.PostAsync($"{_lidarSvcUrl}/api/lidar/baseline", null, ct);
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
```

- [ ] **Step 2: `LidarBaselineResponse` 모델 추가** (`LidarStatusResponse` 클래스 닫는 `}` 다음, `// LiDAR Models` 리전 안, namespace 닫기 전)

```csharp
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
```

- [ ] **Step 3: 빌드로 검증**

Run: `dotnet build "gcp_Wpf.sln"`
Expected: 빌드 성공(0 errors). `LidarSetBaselineAsync`/`LidarBaselineResponse` 컴파일됨.

---

### Task 2: baseline 버튼 + 핸들러 + 실행 래퍼

**Files:**
- Modify: `PageAutoTeaching.xaml` (버튼 행 StackPanel, L130~137)
- Modify: `PageAutoTeaching.xaml.cs` (핸들러/래퍼는 `Btn_Lidar_Click` L2103~2127 뒤에 추가)

**Interfaces:**
- Consumes: Task 1의 `visionApi.LidarSetBaselineAsync(ct)`, `LidarBaselineResponse`. 기존 `visionApi.LidarSvcUrl`, `LogLidarDiagAsync`, `lidarBusy`, `AddLog`, `cIniAccess.Read`, `gClass.srmNum`, `isRunning`, `visionApi.SetBaseUrl`, `visionApi.LastResponseJson`.
- Produces: `Btn_LidarBaseline_Click`(이벤트 핸들러), `RunLidarBaselineAsync(CancellationToken)`.

- [ ] **Step 1: XAML에 baseline 버튼 추가** — `btn_Lidar` 바로 다음 줄에 삽입 (L131 다음)

```xml
                        <Button x:Name="btn_LidarBaseline" Content="기준설정" Click="Btn_LidarBaseline_Click" Height="38" MinWidth="100" Background="#FFB8860B" Foreground="#FFB8D8FC" BorderBrush="#FF555151" FontWeight="Bold" FontSize="13" Margin="4" ToolTip="라이다 baseline 설정 — 최초 설치/기구 재배치 후 1회. 크레인 정지 상태에서 현재 프레임을 기준으로 등록(POST :9000/api/lidar/baseline). 미설정 시 승강 추론이 lidar_missing으로 실패"/>
```

- [ ] **Step 2: 핸들러 + 래퍼 추가** — `Btn_Lidar_Click`의 닫는 `}`(L2127) 다음에 삽입

```csharp
        // 수동 [기준설정] 버튼 — 라이다 baseline 설정(POST :9000/api/lidar/baseline). 티칭/캘리브 실행 중 차단.
        private async void Btn_LidarBaseline_Click(object sender, RoutedEventArgs e)
        {
            if (isRunning) { AddLog("[BASELINE] 오토티칭/캘리브레이션 실행 중에는 설정 불가"); return; }
            if (lidarBusy) return;

            var confirm = MessageBox.Show(
                "크레인이 정지한 상태에서 현재 라이다 프레임을 기준(baseline)으로 설정합니다.\n설치/기구 재배치 후 1회만 수행하세요. 계속할까요?",
                "LiDAR Baseline", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            lidarBusy = true;
            btn_LidarBaseline.IsEnabled = false;
            try
            {
                string visIni = AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini";
                string ip = cIniAccess.Read(visIni, "SRMINFO_" + gClass.srmNum, "VISIONIP", "127.0.0.1").Trim();
                int port = int.TryParse(cIniAccess.Read(visIni, "SRMINFO_" + gClass.srmNum, "VISIONPORT", "3080").Trim(), out int p) ? p : 3080;
                visionApi.SetBaseUrl(ip, port);

                await RunLidarBaselineAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                AddLog($"[BASELINE][ERR] 예외: {ex.Message}");
            }
            finally
            {
                lidarBusy = false;
                btn_LidarBaseline.IsEnabled = true;
            }
        }

        // 라이다 baseline 실행 — POST :9000/api/lidar/baseline + 로깅. 반환 = success. RunLidarScanAsync와 동일 구조.
        private async Task<bool> RunLidarBaselineAsync(CancellationToken ct)
        {
            AddLog($"[BASELINE] set baseline 요청 → {visionApi.LidarSvcUrl}/api/lidar/baseline (body 없음 = 서버 고정 50프레임)");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            LidarBaselineResponse res;
            try { res = await visionApi.LidarSetBaselineAsync(ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                AddLog($"[BASELINE][ERR] 예외: {ex.Message}");
                await LogLidarDiagAsync(ct);
                return false;
            }
            sw.Stop();

            AddLog($"[BASELINE] HTTP={res.HttpStatusCode} ({sw.ElapsedMilliseconds}ms) success={res.Success} elapsed_ms={res.ElapsedMs}");
            if (!string.IsNullOrEmpty(res.Message)) AddLog($"[BASELINE] message: {res.Message}");
            if (res.BaselineYCenter.HasValue) AddLog($"[BASELINE] baseline_y_center={res.BaselineYCenter.Value:F1}");
            if (!string.IsNullOrEmpty(res.Error)) AddLog($"[BASELINE][ERR] error: {res.Error} (failed_step={res.FailedStep ?? "N/A"})");

            string raw = visionApi.LastResponseJson ?? "(없음)";
            if (raw.Length > 1000) raw = raw.Substring(0, 1000) + $" ...(+{raw.Length - 1000}자 생략)";
            AddLog($"[BASELINE] 응답 원문: {raw}");

            if (!res.Success) await LogLidarDiagAsync(ct);
            return res.Success;
        }
```

- [ ] **Step 3: 빌드로 검증**

Run: `dotnet build "gcp_Wpf.sln"`
Expected: 빌드 성공(0 errors). XAML의 `btn_LidarBaseline`/`Btn_LidarBaseline_Click` 바인딩이 코드비하인드와 일치.

- [ ] **Step 4: 수동 확인(장비 환경, 사용자)**
  - 설정 패널 버튼 행에 주황색 `기준설정` 버튼이 보인다.
  - 클릭 → Yes/No 다이얼로그 → 예 → 로그에 `[BASELINE]` TX/RX + (응답에 있으면) `baseline_y_center` 출력, `:9000/api/lidar/baseline` 호출.
  - 오토티칭/CALIB 실행 중에는 차단된다.

---

### Task 3: frames 노출 (수동 스캔 버튼만)

**Files:**
- Modify: `PageAutoTeaching.xaml` (버튼 행 StackPanel — `btn_Lidar` 앞에 라벨+입력칸)
- Modify: `PageAutoTeaching.xaml.cs` (`RunLidarScanAsync` 시그니처/본문 L2042~2047, `Btn_Lidar_Click` L2116)

**Interfaces:**
- Consumes: 기존 `visionApi.LidarScanStartAsync(int? frames, CancellationToken)`, `visionApi.BaseUrl`.
- Produces: `RunLidarScanAsync(string, CancellationToken, bool, int?)` (파라미터 1개 추가 — 기존 3곳 호출부는 기본값으로 호환).

- [ ] **Step 1: XAML에 frames 입력칸 추가** — 버튼 행 StackPanel(L130) 안, `btn_Lidar`(L131) **앞**에 삽입

```xml
                        <Label Content="프레임" Style="{StaticResource LblStyle}" VerticalContentAlignment="Center" Margin="4,0,2,0"/>
                        <TextBox x:Name="edit_LidarFrames" Style="{StaticResource EditStyle}" Width="48" Height="30" VerticalContentAlignment="Center" Margin="0,0,4,0" ToolTip="비우면 기본 20프레임, 지정 시 5~100 (범위 밖은 자동 보정)"/>
```

- [ ] **Step 2: `RunLidarScanAsync` 시그니처에 `int? frames = null` 추가** (L2042)

변경 전:
```csharp
        private async Task<bool> RunLidarScanAsync(string context, CancellationToken ct, bool verbose = true)
```
변경 후:
```csharp
        private async Task<bool> RunLidarScanAsync(string context, CancellationToken ct, bool verbose = true, int? frames = null)
```

- [ ] **Step 3: 시작 로그(L2044) frames 유무로 분기**

변경 전:
```csharp
            if (verbose) AddLog($"[LIDAR] ({context}) scan_start 요청 → {visionApi.BaseUrl}/api/gc/cmd/lidar/scan_start (body 없음 = 서버 기본 20프레임)");
```
변경 후:
```csharp
            if (verbose) AddLog($"[LIDAR] ({context}) scan_start 요청 → {visionApi.BaseUrl}/api/gc/cmd/lidar/scan_start ({(frames.HasValue ? $"frames={frames.Value}" : "body 없음 = 서버 기본 20프레임")})");
```

- [ ] **Step 4: 스캔 호출(L2047)에 frames 전달**

변경 전:
```csharp
            try { res = await visionApi.LidarScanStartAsync(null, ct); } // 클라이언트 타임아웃 180s
```
변경 후:
```csharp
            try { res = await visionApi.LidarScanStartAsync(frames, ct); } // 클라이언트 타임아웃 180s. frames=null이면 서버 기본 20
```

- [ ] **Step 5: `Btn_Lidar_Click`에서 frames 파싱·전달** (L2116)

변경 전:
```csharp
                await RunLidarScanAsync("수동 버튼", CancellationToken.None);
```
변경 후:
```csharp
                int? frames = int.TryParse(edit_LidarFrames.Text?.Trim(), out int fv) ? fv : (int?)null;
                await RunLidarScanAsync("수동 버튼", CancellationToken.None, frames: frames);
```

- [ ] **Step 6: 빌드로 검증**

Run: `dotnet build "gcp_Wpf.sln"`
Expected: 빌드 성공(0 errors). 기존 자동 호출부 3곳(605/1074/1943)은 `frames` 미지정이라 그대로 컴파일(회귀 없음).

- [ ] **Step 7: 수동 확인(장비 환경, 사용자)**
  - 버튼 행 맨 앞에 `프레임 [__]` 입력칸이 보인다.
  - 50 입력 후 `라이다 스캔` → 로그 `frames=50`, 응답 echo `frames=50` 확인.
  - 빈칸이면 기존처럼 기본 20.
  - 자동 티칭 셀 스캔은 여전히 기본 20(회귀 없음).

---

## 검증 요약

| 게이트 | 방법 |
|---|---|
| 컴파일 | 각 태스크 끝 `dotnet build "gcp_Wpf.sln"` 성공 |
| 기능(baseline) | Task 2 Step 4 수동 확인 |
| 기능(frames) | Task 3 Step 7 수동 확인 |
| 회귀(자동 20프레임) | Task 3 Step 6/7 — 자동 경로 무수정 확인 |
