# 오토티칭 라이다 · 비전 API 요청 로직 분석

> 분석일: 2026-07-03 · 대상: `gcp_type2-TP2_VER1` (지상반 WPF, .NET 6)
> 핵심 파일: [`commClass/VisionApiClient.cs`](../commClass/VisionApiClient.cs) (HTTP 클라이언트) · [`PageAutoTeaching.xaml.cs`](../PageAutoTeaching.xaml.cs) (오케스트레이션)
> 비전팀 인증 스펙(2026-07-02) 대조 완료. 6개 정독 에이전트 + 1개 적대적 검증(verdict: 일치) 기반.

---

## 0. 아키텍처 개요

```
지상반 WPF (VisionApiClient)
   │
   ├─ HTTP → 비전서버(Rust GC)   http://{VISIONIP}:{VISIONPORT}   (기본 127.0.0.1:3080)
   │        /api/gc/req/...  (상태 조회)
   │        /api/gc/cmd/...  (캡처·추론·캘리브·라이다 스캔)
   │
   └─ HTTP → 라이다 서비스        http://{VISIONIP}:9000            (포트 9000 고정, 별도 프로세스)
            /api/lidar/status    (상태 진단)
            /api/lidar/baseline  (기준높이 설정 — ※ WPF 미구현, 웹 UI로만)
```

- IP/포트 출처: `Config.ini` → `[SRMINFO_{srmNum}]` → `VISIONIP`(기본 `127.0.0.1`) / `VISIONPORT`(기본 `3080`).
- `SetBaseUrl(ip, port)` ([VisionApiClient.cs:69](../commClass/VisionApiClient.cs)) 이 **두 URL을 동시 세팅**:
  `_baseUrl = http://{ip}:{port}` (비전서버) / `_lidarSvcUrl = http://{ip}:9000` (라이다, **포트 9000 하드코딩**, ip만 공유).
- HttpClient 2개: `_client`(30s 전역) / `_lidarClient`(180s, 라이다 스캔 전용 — 최대 100프레임 대비).

---

## 1. 비전팀 스펙 ↔ 코드 대조 요약 (★ 가장 중요)

| 스펙 엔드포인트 | 코드 구현 | 일치 여부 |
|---|---|---|
| ① `POST :3080/api/gc/cmd/lidar/scan_start` (body `{frames:20}`, 5~100, 기본20) | `LidarScanStartAsync(int? frames)` [VisionApiClient.cs:516](../commClass/VisionApiClient.cs) | ✅ 경로·클램프(5~100)·기본20 일치. **단, 실호출은 전부 `null`(body 없음)** → 아래 ⚠️A |
| ② `POST :3080/api/gc/cmd/{cam}/start_z_inference` (승강추론, 라이다 필수) | `RequestHoistInferenceAsync()` [VisionApiClient.cs:306](../commClass/VisionApiClient.cs) | ✅ 경로·`failed_step` 6종(lidar_missing/lens_coefficients/calibrate_lens/travel/hoist/postprocess) 코드 주석과 정확히 일치 |
| ③ `POST :9000/api/lidar/baseline` (설치 1회, 50프레임, lidar.json 저장) | **없음** | ⚠️B **WPF 미구현** — [PageAutoTeaching.xaml.cs:2098](../PageAutoTeaching.xaml.cs) 안내 로그 문자열에만 언급 |
| ④ `GET :9000/api/lidar/status` (연결/워밍업/baseline 점검) | `LidarStatusAsync()` [VisionApiClient.cs:565](../commClass/VisionApiClient.cs) | ✅ 경로·응답필드(available/connected/warming_up/baseline_y_center/hoist_offset_mm/offset_smooth_mm/result) 완전 일치 |

### ⚠️ 유의점 A — `frames`는 정의만 있고 실사용 안 됨
코드 전 호출부가 `LidarScanStartAsync(null, ct)` ([PageAutoTeaching.xaml.cs:2047](../PageAutoTeaching.xaml.cs)) → **body 없이 전송 → 서버 기본 20프레임**.
`frames` 값(5~100)을 넘기는 호출부는 현재 코드에 **없다**. 프레임 수를 바꾸려면 `RunLidarScanAsync`/호출부에 인자를 추가해야 함.

> **✅ 갱신 (2026-07-03 해소)**: 수동 `라이다 스캔` 버튼에 한해 노출됨. `RunLidarScanAsync`에 `int? frames=null` 추가, `edit_LidarFrames` TextBox에서 파싱 전달. 자동 경로 3곳은 미수정 → 기본 20 유지. (계획: [superpowers/plans/2026-07-03-lidar-baseline-and-frames.md](superpowers/plans/2026-07-03-lidar-baseline-and-frames.md))

### ⚠️ 유의점 B — baseline 설정은 지상반에서 불가
스펙 ③ `POST :9000/api/lidar/baseline`에 대응하는 C# 메서드가 없다. 지상반은 baseline을 **설정하지 못하고**, 미설정을 **진단·안내**만 한다
([PageAutoTeaching.xaml.cs:2097-2098](../PageAutoTeaching.xaml.cs)):
> `★baseline 미설정 — 최초 설치/기구 재배치 후 1회 필수. 비전 웹 UI [현재 프레임을 기준으로] 또는 POST :9000/api/lidar/baseline`
→ 최초 설치/기구 재배치 시 **비전 웹 UI에서 baseline을 잡아야** 오토티칭의 승강(Z) 추론이 동작한다.

> **✅ 갱신 (2026-07-03 해소)**: 지상반에서 직접 설정 가능해짐. `VisionApiClient.LidarSetBaselineAsync`(POST `:9000/api/lidar/baseline`, body 없음) + `기준설정` 버튼(`Btn_LidarBaseline_Click`, 확인 다이얼로그+가드) + `RunLidarBaselineAsync` 래퍼 신설. 비전 웹 UI 없이도 baseline 설정 가능. (계획: [superpowers/plans/2026-07-03-lidar-baseline-and-frames.md](superpowers/plans/2026-07-03-lidar-baseline-and-frames.md))

---

## 2. 라이다 요청 로직 상세

### 2.1 라이다 스캔 공통 래퍼 — `RunLidarScanAsync(context, ct, verbose)` [PageAutoTeaching.xaml.cs:2042](../PageAutoTeaching.xaml.cs)

라이다 스캔 요청은 **전부 이 래퍼 1개**를 경유한다. 반환값 = 스캔 성공(bool).

```
RunLidarScanAsync
  → visionApi.LidarScanStartAsync(null, ct)          // POST {base}/api/gc/cmd/lidar/scan_start, body 없음(기본20), 180s 타임아웃
  → 성공 판정 = 응답 JSON의 success 필드
  → 성공 & verbose=false : 1줄 요약만 (셀 반복용, 로그 폭주 방지)
  → 실패/예외          : 상세 로그 + 응답 원문 + LogLidarDiagAsync(:9000 status 진단)
  → OperationCanceledException(사용자 STOP) : 그대로 re-throw (상위 취소 경로로)
```

- **성공 = "측정을 돌렸다"만 보증.** baseline 미설정이면 offset이 무효여도 scan_start는 성공할 수 있음 → 승강 추론 단계에서 뒤늦게 `lidar_missing` 실패.
- HTTP 실패코드: **502**(측정 실패) / **503**(비전서버 불통).

### 2.2 라이다 진단 — `LogLidarDiagAsync(ct)` [PageAutoTeaching.xaml.cs:2080](../PageAutoTeaching.xaml.cs)

`GET :9000/api/lidar/status`(`LidarStatusAsync`)를 조회해 실패 원인 후보를 로그로 남김. 진단 실패는 무해(로그만), 사용자 취소만 전파.

| 조건 | 로그 |
|---|---|
| status 조회 실패(null) | 라이다 서비스(:9000) 미기동/네트워크 불통 의심 |
| `!connected` | 라이다 미연결 — 센서/케이블/서비스 확인 |
| `connected && warming_up` | 워밍업 중 — 잠시 후 재시도 |
| `baseline_y_center == null` | ★baseline 미설정 — 설치 후 1회 필수 (connected/warming과 독립 체크) |

호출 지점 3곳: `RunLidarScanAsync` 예외경로(2052) · 비성공경로(2074) · 셀 Z추론이 `lidar_missing`일 때(1109).

### 2.3 `120초 stale` / `baseline` / `lidar_missing` 관계

- 측정 offset은 **응답에 안 담기고 서버 내부 저장(120초 유효)** — 승강(Z) 추론이 이 저장값을 사용.
- 그래서 **셀마다 Z추론 직전 재스캔 필수** ([PageAutoTeaching.xaml.cs:1074](../PageAutoTeaching.xaml.cs)) — 120s 초과 시 Z추론이 `lidar_missing`(502)으로 거부.
- Z추론 `failed_step == "lidar_missing"`의 3원인 (스펙 ②와 일치):
  1. `scan is missing` — scan_start 안 함
  2. `scan is stale (older than 120s)` — 스캔 후 120초 초과
  3. `measurement is invalid (baseline not set / no valid frames)` — baseline 미설정
- 3원인이 한 코드로 합쳐지므로, `lidar_missing` 감지 시 `LogLidarDiagAsync`가 `:9000 status`로 셋을 구분해 로깅.

### 2.4 `RunLidarScanAsync` 호출 4곳

| # | 위치 | context | verbose | 실패 시 정책 |
|---|---|---|---|---|
| 1 | 수동 [라이다 스캔] 버튼 `Btn_Lidar_Click` [2116](../PageAutoTeaching.xaml.cs) | `"수동 버튼"` | true | 로그만(반환값 미사용) |
| 2 | START Phase1.7 헬스체크 [1943](../PageAutoTeaching.xaml.cs) | `"START 헬스체크"` | true | [재시도]/[중단] 모달 — "라이다 없이 진행" 옵션 없음 |
| 3 | 캘리브레이션 `Phase1_5_CalibrationAsync` [605](../PageAutoTeaching.xaml.cs) | `"CALIB"` | true | **경고만 남기고 계속** (카메라 캘리브는 라이다 데이터 미사용) |
| 4 | 셀마다 Z추론 직전 `CaptureAndInferCellAsync` [1074](../PageAutoTeaching.xaml.cs) | `"셀 R{r}-B{b}-L{l}"` | **false** | 해당 셀 실패 처리 (`FailedStep=z_inference`, `FailedSubStep=lidar_scan`) |

---

## 3. 전체 API 엔드포인트 카탈로그 ([VisionApiClient.cs](../commClass/VisionApiClient.cs))

총 **15개 요청 메서드**. `{base}` = `http://{ip}:{port}` (비전서버), `{lidar}` = `http://{ip}:9000`.

| # | C# 메서드 | HTTP | 경로 | 클라이언트 | ct | 요청 | 응답 |
|---|---|---|---|---|---|---|---|
| 1 | `CheckCameraStatusAsync` | GET | `{base}/api/gc/req/cameras/status` | 30s | ✗ | — | CameraStatusResponse |
| 2 | `CheckRtspStatusAsync` | GET | `{base}/api/gc/req/rtsp_status` | 30s | ✗ | — | RtspStatusResponse |
| 3 | `HealthCheckAsync` | GET | `{base}/api/gc/req/cameras/status` | 30s | ✗ | — | bool |
| 4 | `ConnectRtspAsync` | POST | `{base}/api/gc/cmd/{cam}/connect` | 30s | ✗ | — | RtspResponse |
| 5 | `DisconnectRtspAsync` | POST | `{base}/api/gc/cmd/{cam}/disconnect` | 30s | ✗ | — | RtspResponse |
| 6 | `RequestCaptureAsync` | POST | `{base}/api/gc/cmd/{cam}/capture` | per-call 15s | ✓ | CaptureRequest | CaptureResponse |
| 7 | `RequestTravelInferenceAsync` (X주행) | POST | `{base}/api/gc/cmd/{cam}/start_x_inference` | per-call 15s | ✓ | CaptureRequest | TravelInferenceResponse |
| 8 | `RequestHoistInferenceAsync` (Z승강) | POST | `{base}/api/gc/cmd/{cam}/start_z_inference` | per-call 15s | ✓ | CaptureRequest | HoistInferenceResponse |
| 9 | `CalibrationCaptureAsync` | POST | `{base}/api/gc/cmd/{cam}/calibration/capture` | 30s | ✗ | CaptureRequest | CalibrationCaptureResponse |
| 10 | `CalibrationInferenceAsync` | POST | `{base}/api/gc/cmd/{cam}/calibration/inference` | 30s | ✗ | CalibrationInferenceRequest | CalibrationInferenceResponse |
| 11 | `CalibrationComputeAsync` | POST | `{base}/api/gc/cmd/calibration/compute` | 30s | ✗ | CalibrationComputeRequest | CalibrationComputeResponse |
| 12 | `CalibrationStatusAsync` | GET | `{base}/api/gc/cmd/calibration/status?camera_id={cam}` | 30s | ✗ | 쿼리 | CalibrationStatusResponse |
| 13 | `CalibrationCleanupAsync` | POST | `{base}/api/gc/cmd/calibration/cleanup` | 30s | ✗ | `{camera_id}` | CalibrationCleanupResponse |
| 14 | **`LidarScanStartAsync`** | POST | `{base}/api/gc/cmd/lidar/scan_start` | **180s** | ✓ | LidarScanRequest(선택) | LidarScanResponse |
| 15 | **`LidarStatusAsync`** | GET | `{lidar}/api/lidar/status` | 30s | ✓ | — | LidarStatusResponse |

**공통 요청 바디** `CaptureRequest` (캡처/X추론/Z추론 동일): `row, bay, bay_pos, level, level_pos, has_cargo, lens_calibration_file?`.
**디버그 필드**: `LastRequestJson` / `LastResponseJson` / `LastHttpStatusCode` / `LastError` — 실패 진단에 사용.
**per-call 타임아웃**: `PostWithTimeoutAsync`가 `PerCallTimeoutSeconds`(기본 15s)와 사용자 `ct`를 링크 → 비전서버 hang 시 빠른 Stop 반응성. 사용자 취소면 예외 그대로 전파, 타임아웃이면 실패 응답으로 변환.

---

## 4. 엔드투엔드 요청 시퀀스 — START 버튼 (`Btn_Start_Click` [1840](../PageAutoTeaching.xaml.cs))

```
START 클릭
 ├ BuildTargetList()      대상 셀 리스트 (ㄹ자/snake 순서) [325]
 ├ GetCameraId()          camera_id 결정 (기본 camera3) [380]
 ├ 확인 다이얼로그 + cts 재생성
 │
 ├ Phase1_InitAsync [391]  (실패 시 재시도/중단 모달)
 │   ├ SetBaseUrl(ip,port)
 │   ├ GET  cameras/status         (HealthCheck)
 │   ├ GET  cameras/status         (CheckCameraStatus, 선택 cam == "ok"?)
 │   └ POST {cam}/connect          (ConnectRtsp)
 │
 ├ Phase1.5 (START에선 파일 존재 확인만) [1903]
 │   └ GET  calibration/status?camera_id={cam}
 │        · 파일 있으면 진행 / 없으면 "CALIB 먼저 실행" 안내 후 중단
 │        · ※ 스윕(14점 캡처+compute)은 START가 아니라 CALIB 버튼에서만
 │
 ├ Phase1.7 라이다 헬스체크 [1936]
 │   └ POST lidar/scan_start        (RunLidarScanAsync "START 헬스체크", 실패 시 재시도/중단)
 │
 ├ [반자동 무장: gcpTxMode=2 → 자동모드 진입 → Start ON 0x50]
 │
 ├ Phase2_TeachingLoopAsync [717]   for each 셀:
 │   ├ MoveViaSemiAutoAsync (0x41 반자동 이동) → 위치검증 → Busy대기 → 안정화지연
 │   └ CaptureAndInferCellAsync [995]  (attempt 0..2, 최대 3회 재촬영)
 │        ├ POST {cam}/capture              (RequestCapture)
 │        ├ POST {cam}/start_x_inference    (X주행추론 → inferred_bay_pos)
 │        ├ POST lidar/scan_start           (라이다 재스캔, verbose=false) ★Z추론 필수 선행
 │        └ POST {cam}/start_z_inference    (Z승강추론 → inferred_level_pos)
 │             · lidar_missing 시 LogLidarDiagAsync(:9000 status)
 │
 └ Phase3_CleanupAsync [1149]
     └ (단일 셀/취소/예외) POST {cam}/disconnect  → 엑셀 저장 → DisarmCrane
```

### 셀 1개 비전 시퀀스 요약 (`CaptureAndInferCellAsync`)
`capture → start_x_inference → lidar/scan_start → start_z_inference` (순차·동기, 앞 단계 성공 시 진행).
- 요청 값 출처: `bay_pos = CurTrav`, `level_pos = CurLift` (캡처 직전 실측 위치 재읽기).
- 결과 사용: `InferredBayPos`(X) / `InferredLevelPos`(Z)만 티칭 결과로 저장. `travel_move_mm`/`hoist_move_mm`는 로그용.
- 크레인 이동을 포함하지 않으므로(이미 정지) 실패 시 메서드 통째 재호출 안전 → 재시도 경로에서도 라이다 자동 재스캔.

---

## 5. 수동/개발 버튼 경로

| 버튼 | 트리거 시퀀스 | 자동 흐름과 차이 | 라이다 |
|---|---|---|---|
| **CALIB** `Btn_Calib_Click` [2129](../PageAutoTeaching.xaml.cs) | SetBaseUrl → `Phase1_5_CalibrationAsync(force=true)` → finally disconnect | `force=true` → 기존 캘리브 무시·스윕(X 7점+Z 7점 capture)·compute 강제 재수행. 2단계 티칭 안 넘어감 | Phase1_5 내부 스캔 1회, 실패해도 경고만 |
| **라이다 스캔** `Btn_Lidar_Click` [2103](../PageAutoTeaching.xaml.cs) | SetBaseUrl → `RunLidarScanAsync("수동 버튼")` | 라이다만 단독 스캔 + 실패 진단 | 핵심(이 버튼의 전부) |
| **VERIFY** `Btn_Verify_Click`→`RunVerifyReinferAsync` [2276](../PageAutoTeaching.xaml.cs) | 셀별 0x59 정밀이동 → capture → start_x_inference → start_z_inference | 2단계 결과 자리로 재이동해 **잔차(≤3mm)만 재측정**, 새 좌표 산출 안 함 | **없음** (스캔 미호출) |
| **CLOSE** `Btn_Close_Click` [2399](../PageAutoTeaching.xaml.cs) | camera1·camera3 각각 disconnect | 페이지 이탈 시 양쪽 카메라 ffmpeg 해제 | 없음 |

---

## 6. 취소·재시도·타임아웃 정리

- **취소(Stop)**: `Btn_Stop_Click`이 `cts.Cancel()` → 하위 `ct`로 `OperationCanceledException` 전파. 라이다 래퍼·클라이언트 모두 사용자 취소는 re-throw, per-call/스캔 타임아웃만 실패 응답으로 변환.
- **비전 단계 재시도**: 셀당 `VisionRetry=2` → 이동 없이 같은 위치 최대 3회 재촬영. 라이다 재스캔도 이 재시도 안에서 반복됨.
- **서킷브레이커**: 연속 `MaxConsecutiveCellFail=8` 실패 시 운영자 [계속/중단] 모달.
- **타임아웃 계층**: 라이다 스캔 180s(`_lidarClient`) / 캡처·추론 per-call 15s / 그 외 30s 전역.

---

## 7. 결론 (요청하신 "라이다 API 요청 로직"의 위치)

- **라이다 스캔 요청**: [`VisionApiClient.LidarScanStartAsync`](../commClass/VisionApiClient.cs) (516행) ← 유일 래퍼 [`RunLidarScanAsync`](../PageAutoTeaching.xaml.cs) (2042행) ← 4곳에서 호출.
- **라이다 상태 요청**: [`VisionApiClient.LidarStatusAsync`](../commClass/VisionApiClient.cs) (565행) ← [`LogLidarDiagAsync`](../PageAutoTeaching.xaml.cs) (2080행) (진단 전용).
- **라이다가 필수인 곳**: 승강(Z) 추론 [`RequestHoistInferenceAsync`](../commClass/VisionApiClient.cs) (306행) — 매 셀 Z추론 직전 재스캔.
- **미구현/주의**: baseline 설정(스펙 ③)은 지상반에 없음(웹 UI 필요) · `frames`는 항상 기본 20 사용.
