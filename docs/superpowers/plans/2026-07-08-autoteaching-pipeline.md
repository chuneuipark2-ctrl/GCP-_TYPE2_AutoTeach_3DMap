# 오토티칭 셀 사이클 파이프라인화 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 오토티칭 셀 사이클을 [도착 → settle∥라이다 → 캡처 → 캡처응답 즉시 다음 이동 발행 → 이동 중 X→Z 추론]으로 재배열해 셀당 ~3초 이상 단축.

**Architecture:** 단일 스레드 협조적 파이프라인(깊이 1). 추론을 `Task.Run` 없이 await하지 않는 async 호출로 띄워 WPF 디스패처 인터리빙만으로 병렬화 — 멀티스레딩 없음, 비전 서버 HTTP는 전부 직렬 유지. 스펙: `docs/superpowers/specs/2026-07-08-autoteaching-pipeline-design.md`

**Tech Stack:** C# / .NET 6 WPF (`gcp_Wpf.csproj`), 수정 파일은 `PageAutoTeaching.xaml.cs` 단 1개.

**주의:** 이 저장소는 git 저장소가 아니다 — 커밋 단계 없음. 각 태스크의 게이트는 `dotnet build` 성공. 테스트 프로젝트가 없는 하드웨어 결합 WPF 코드라 자동 테스트 불가 — Task 4의 로그 시퀀스 검증으로 대체.

## Global Constraints (스펙 정합성 규칙 — 전 태스크 공통)

- R1: 라이다 스캔(셀 i)은 반드시 harvest(셀 i-1 추론 회수) **완료 후** 시작 (서버 측정값 단일 슬롯·120s)
- R2: `bay_pos`/`level_pos` 스냅샷은 settle 완료 후·캡처 직전 1회 → X·Z 추론까지 동일 값 (호출 직전 `CurTrav` 재읽기 금지)
- R3: 라이다 스캔·캡처는 크레인 정지 중(출발 게이트 이전)에만
- R4: `currentResults` Add 순서 = 셀 순서 (harvest가 유일한 지연 기록 지점, ZR CSV가 순서로 PLC 주소 배정)
- R5: 루프 탈출 모든 경로(정상/break/취소/예외)에서 pendingInfer 회수 후 SUMMARY·commit·저장
- R6: 이동 발행 지점마다 `gcpTxMode=2` 재강제 + `semiDest*` 기입 유지 (`IssueSemiAutoMove`가 담당)
- 재시도 전면 제거(사용자 결정): X/Z 추론 실패 = 즉시 실패 기록. `VisionRetry` 관련 코드 삭제.
- 스콥 밖: CALIB/VERIFY/수동 버튼, `MoveViaMaintAsync`(0x59), Phase1/1.5/1.7, 저장 포맷, UDP 프로토콜.

---

### Task 1: 이동 명령 발행/도착 대기 분리 (동작 불변)

**Files:**
- Modify: `PageAutoTeaching.xaml.cs:1258-1351` (`MoveViaSemiAutoAsync`)

**Interfaces:**
- Produces: `bool IssueSemiAutoMove(int row, int bay, int lev, int targetTravMm, int targetLiftMm)` — 안전트립 체크+발행, 트립 시 false
- Produces: `Task<bool> WaitSemiAutoArrivalAsync(int row, int bay, int lev, int targetTravMm, int targetLiftMm, CancellationToken ct)` — 기존 폴링 루프 그대로
- 기존 `MoveViaSemiAutoAsync`는 두 함수 위임 래퍼로 축소(Task 3에서 삭제) — 이 태스크 후 동작 완전 동일

- [ ] **Step 1: 분리 구현**

`MoveViaSemiAutoAsync`(1266행~)를 아래 3개로 교체. 폴링 루프 내용(sanity 가드·SKIP·stall·도착판정·ClearSemiJobFlag)은 기존 코드 그대로 유지하고 발행부만 뜯어낸다:

```csharp
/// <summary>반자동 이동(0x41) 발행만 수행 — 안전트립 체크 + gcpTxMode=2 재강제 + semiDest* 기입.
/// 파이프라인 출발 게이트에서 도착 대기 없이 호출된다. 트립이면 발행하지 않고 false.</summary>
private bool IssueSemiAutoMove(int row, int bay, int lev, int targetTravMm, int targetLiftMm)
{
    // [하드게이트] 안전입력 트립 시 0x41을 쏘지 않고 즉시 거부 (트립이면 MCU가 무응답 폐기 → 헛대기)
    if (IsSafetyTripped(out string safetyReason))
    {
        AddLog($"[SEMI][ERR] ★지상반 안전입력 트립 — 반자동 이동 거부: {safetyReason}");
        return false;
    }
    gClass.str.SrmState[gClass.srmNum].gcpState.gcpTxMode = 2;   // 반자동 강제 유지(MainWindow 키값 덮어쓰기 방지)
    SendCraneMoveCommand(row, bay, lev);
    AddLog($"[SEMI][0x41] 반자동 이동 송신 → R{row}-B{bay}-L{lev} (목표 trav={targetTravMm} lift={targetLiftMm})");
    return true;
}

/// <summary>반자동 이동 도착 대기 — 기존 MoveViaSemiAutoAsync의 폴링 루프 부분(발행은 IssueSemiAutoMove).</summary>
private async Task<bool> WaitSemiAutoArrivalAsync(int row, int bay, int lev, int targetTravMm, int targetLiftMm, CancellationToken ct)
{
    int srmNum = gClass.srmNum;
    const int TOL = 100;        // 목표 근처 판정(mm). 베이 간격보다 충분히 작아 이전 셀과 구분됨.
    const int TIMEOUT = 120000;
    const int STALL_MS = 15000; // 15초간 위치 무변화 + 미도착 = 미동작(안전트립/모드/시작OFF) 의심 → 조기 중단

    var sw = System.Diagnostics.Stopwatch.StartNew();
    int lastT = -999999, lastL = -999999;
    int stRefT = CurTrav;   // stall 기준 위치
    int stRefL = CurLift;
    long lastProgMs = 0;
    while (sw.ElapsedMilliseconds < TIMEOUT && !ct.IsCancellationRequested)
    {
        // …… 기존 1292~1346행 루프 본문을 문자 그대로 유지 ……
        // (Task.Delay(50, ct) / skipRequested / sanity 가드 cts.Cancel / 도착판정 / stall)
    }
    AddLog($"[SEMI][ERR] 반자동 이동 타임아웃/취소 ({sw.ElapsedMilliseconds}ms) — 지상반 키(반자동)/시작 ON/알람 확인");
    ClearSemiJobFlag(srmNum);
    return false;
}

/// <summary>(과도기 래퍼 — Task 3에서 삭제) 발행+대기 결합. 기존 호출부(:887) 동작 동일.</summary>
private async Task<bool> MoveViaSemiAutoAsync(int row, int bay, int lev, int targetTravMm, int targetLiftMm, CancellationToken ct)
{
    if (!IssueSemiAutoMove(row, bay, lev, targetTravMm, targetLiftMm)) return false;
    return await WaitSemiAutoArrivalAsync(row, bay, lev, targetTravMm, targetLiftMm, ct);
}
```

주의: 기존 XML doc 주석(1258-1265행, 도착판정 설명)은 `WaitSemiAutoArrivalAsync` 위로 이동. 루프 본문은 복사-이동만 하고 한 글자도 바꾸지 않는다.

- [ ] **Step 2: 빌드 검증**

Run: `dotnet build gcp_Wpf.csproj`
Expected: `Build succeeded.` / `0 Error(s)` (기존 경고는 무시)

---

### Task 2: CaptureCellAsync / InferCellAsync 신설 (기존 로직 이식, 아직 미호출)

**Files:**
- Modify: `PageAutoTeaching.xaml.cs` — `CaptureAndInferCellAsync`(1021행~) 아래에 신규 메서드 2개 + struct 1개 추가 (기존 메서드는 Task 3에서 삭제)

**Interfaces:**
- Produces: `struct CaptureOutcome { bool Ok; TeachingResult Fail; string CapFile, RawPath, CalPath; }`
- Produces: `Task<CaptureOutcome> CaptureCellAsync(string cameraId, int row, int bay, int lev, int capBayPos, int capLevPos, bool hasCargo, CancellationToken ct)`
- Produces: `Task<TeachingResult> InferCellAsync(string cameraId, int row, int bay, int lev, int capBayPos, int capLevPos, bool hasCargo, bool scanOk, string capFile, string capRaw, string capCal, CancellationToken ct)` — **모든 예외를 내부에서 실패 결과로 변환(절대 던지지 않음)**, harvest가 안전하게 await

- [ ] **Step 1: CaptureOutcome + CaptureCellAsync 추가**

기존 1027-1064행(캡처 부분)을 이식:

```csharp
// 파이프라인: 캡처 결과 전달용 (Ok=false면 Fail에 실패 TeachingResult)
private struct CaptureOutcome
{
    public bool Ok;
    public TeachingResult Fail;
    public string CapFile;
    public string RawPath;
    public string CalPath;
}

/// <summary>셀 1개 캡처만 수행 (크레인 정지 중). 실패 시 Ok=false + Fail 채움 — 재시도 없음(2026-07-08 정책 유지).</summary>
private async Task<CaptureOutcome> CaptureCellAsync(
    string cameraId, int row, int bay, int lev,
    int capBayPos, int capLevPos, bool hasCargo, CancellationToken ct)
{
    var captureReq = new CaptureRequest
    {
        Row = row, Bay = bay, BayPos = capBayPos,
        Level = lev, LevelPos = capLevPos, HasCargo = hasCargo
    };

    AddLog($"[TX] POST /api/gc/cmd/{cameraId}/capture  row={row} bay={bay} lev={lev} bay_pos={capBayPos} level_pos={capLevPos} has_cargo={hasCargo}");
    var sw = System.Diagnostics.Stopwatch.StartNew();
    CaptureResponse captureResp;
    try
    {
        captureResp = await visionApi.RequestCaptureAsync(cameraId, captureReq, ct);
        sw.Stop();
    }
    catch (Exception ex)
    {
        sw.Stop();
        AddLog($"[ERR] Capture 예외 ({sw.ElapsedMilliseconds}ms): {ex.Message}");
        AddLog($"[DEBUG-REQ] {visionApi.LastRequestJson}");
        AddLog($"[DEBUG-RES] {visionApi.LastResponseJson}");
        AddLog($"[DEBUG-HTTP] StatusCode={visionApi.LastHttpStatusCode}");
        return new CaptureOutcome { Ok = false, Fail = new TeachingResult { Row = row, Bay = bay, Level = lev, Success = false, Error = ex.Message, HasCargo = hasCargo, FailedStep = "capture" } };
    }

    if (!captureResp.Success)
    {
        AddLog($"[ERR] Capture 실패 ({sw.ElapsedMilliseconds}ms) HTTP={captureResp.HttpStatusCode}: {captureResp.Error}");
        AddLog($"[DEBUG-REQ] {visionApi.LastRequestJson}");
        AddLog($"[DEBUG-RES] {visionApi.LastResponseJson}");
        return new CaptureOutcome { Ok = false, Fail = new TeachingResult { Row = row, Bay = bay, Level = lev, Success = false, Error = captureResp.Error, HasCargo = hasCargo, FailedStep = "capture", FailedSubStep = captureResp.FailedStep } };
    }
    AddLog($"[RX] Capture OK ({sw.ElapsedMilliseconds}ms) HTTP={captureResp.HttpStatusCode} 주행={capBayPos}mm 승강={capLevPos}mm file={captureResp.Filename} server_elapsed={captureResp.ElapsedMs}ms");

    return new CaptureOutcome { Ok = true, CapFile = captureResp.Filename, RawPath = captureResp.RawPath, CalPath = captureResp.CalibratedPath };
}
```

- [ ] **Step 2: InferCellAsync 추가**

기존 1066-1184행(X→Z 추론)을 이식하되 ①라이다 스캔 호출(1113-1123행)을 `scanOk` 게이트로 대체 ②위치는 파라미터 동결값만 사용:

```csharp
/// <summary>
/// 셀 1개의 X→Z 추론 — 캡처 완료 후 크레인이 다음 셀로 이동 중일 수 있다(파이프라인 동시 진행 ②).
/// 위치(capBayPos/capLevPos)는 캡처 시점 스냅샷 동결값(R2) — 이 메서드 안에서 CurTrav/CurLift를 읽지 않는다.
/// scanOk=false(도착 시 라이다 스캔 실패)면 X추론까지만 하고 z_inference/lidar_scan 실패 반환.
/// ★모든 예외를 실패 결과로 변환해 반환(던지지 않음) — 사용자 STOP도 실패 기록 후 상위 루프가 취소 처리(현행 동일).
/// </summary>
private async Task<TeachingResult> InferCellAsync(
    string cameraId, int row, int bay, int lev,
    int capBayPos, int capLevPos, bool hasCargo, bool scanOk,
    string capFile, string capRaw, string capCal, CancellationToken ct)
{
    var inferReq = new CaptureRequest
    {
        Row = row, Bay = bay, BayPos = capBayPos,
        Level = lev, LevelPos = capLevPos, HasCargo = hasCargo
    };

    SetStatus("INFER", ClrInfo);
    AddLog($"[TX] X추론 시작  bay_pos={capBayPos} level_pos={capLevPos} (크레인 이동과 병렬)");

    var xSw = System.Diagnostics.Stopwatch.StartNew();
    TravelInferenceResponse travResp;
    try
    {
        travResp = await visionApi.RequestTravelInferenceAsync(cameraId, inferReq, ct);
        xSw.Stop();
    }
    catch (Exception ex)
    {
        xSw.Stop();
        AddLog($"[ERR] X추론 예외 ({xSw.ElapsedMilliseconds}ms): {ex.Message}");
        return new TeachingResult { Row = row, Bay = bay, Level = lev, Success = false, Error = ex.Message, HasCargo = hasCargo, FailedStep = "x_inference", CaptureOk = true,
            CapturedFile = capFile, RawPath = capRaw, CalibratedPath = capCal };
    }

    if (!travResp.Success)
    {
        AddLog($"[ERR] X추론 실패 ({xSw.ElapsedMilliseconds}ms) HTTP={travResp.HttpStatusCode}: step={travResp.FailedStep} error={travResp.Error}");
        return new TeachingResult { Row = row, Bay = bay, Level = lev, Success = false, Error = travResp.Error, HasCargo = hasCargo, FailedStep = "x_inference", CaptureOk = true, FailedSubStep = travResp.FailedStep,
            CapturedFile = capFile, RawPath = capRaw, CalibratedPath = capCal };
    }

    int inferredBayPos = (int)travResp.InferredBayPos;
    int bayDiff = capBayPos - inferredBayPos;
    AddLog($"[RX] X추론 OK ({xSw.ElapsedMilliseconds}ms) bay_pos={inferredBayPos} diff={bayDiff}mm server_elapsed={travResp.ElapsedMs}ms");

    Dispatcher.Invoke(() => { lbl_curTrav.Content = inferredBayPos.ToString(); });

    // [라이다 폴백] 라이다 미사용 모드 — 승강(Z)은 기존 캘리브(티칭)값 유지, Z추론 스킵.
    if (lidarFallback)
    {
        int existingLev = gClass.str.SrmInfo[gClass.srmNum].cellLev[lev - 1];
        AddLog($"[Z] 라이다 미사용(폴백) — 승강 기존값 유지 level_pos={existingLev}");
        Dispatcher.Invoke(() => { lbl_curLift.Content = existingLev.ToString(); });
        return new TeachingResult
        {
            Row = row, Bay = bay, Level = lev,
            BayPos = inferredBayPos, LevelPos = existingLev,
            Success = true, HasCargo = hasCargo,
            CaptureOk = true, XInferenceOk = true, ZInferenceOk = false,
            CapturedFile = capFile, RawPath = capRaw, CalibratedPath = capCal
        };
    }

    // 라이다 스캔은 도착 직후 settle과 병렬로 이미 완료(R1·R3) — 실패였으면 Z추론 불가(재스캔 불가: 크레인이 떠남).
    if (!scanOk)
    {
        return new TeachingResult { Row = row, Bay = bay, Level = lev, BayPos = inferredBayPos, Success = false,
            Error = "라이다 스캔 실패 (승강 추론 선행 필수)", HasCargo = hasCargo,
            FailedStep = "z_inference", CaptureOk = true, XInferenceOk = true, FailedSubStep = "lidar_scan",
            CapturedFile = capFile, RawPath = capRaw, CalibratedPath = capCal };
    }

    // Z추론 (X추론 완료 후 순차 — analysis.json 의존 + 라이다 저장값(120s) 사용)
    SetStatus("INFER", ClrInfo);
    var zReq = new CaptureRequest
    {
        Row = row, Bay = bay, Level = lev,
        BayPos = capBayPos, LevelPos = capLevPos,
        HasCargo = hasCargo
    };

    AddLog($"[TX] Z추론 시작");
    HoistInferenceResponse zResp;
    var zSw = System.Diagnostics.Stopwatch.StartNew();
    try
    {
        zResp = await visionApi.RequestHoistInferenceAsync(cameraId, zReq, ct);
        zSw.Stop();
    }
    catch (Exception ex)
    {
        zSw.Stop();
        AddLog($"[ERR] Z추론 예외 ({zSw.ElapsedMilliseconds}ms): {ex.Message}");
        return new TeachingResult { Row = row, Bay = bay, Level = lev, BayPos = inferredBayPos, Success = false, Error = ex.Message, HasCargo = hasCargo, FailedStep = "z_inference", CaptureOk = true, XInferenceOk = true,
            CapturedFile = capFile, RawPath = capRaw, CalibratedPath = capCal };
    }

    if (!zResp.Success)
    {
        AddLog($"[ERR] Z추론 실패 ({zSw.ElapsedMilliseconds}ms) HTTP={zResp.HttpStatusCode} step={zResp.FailedStep} error={zResp.Error}");
        // lidar_missing = 스캔 안 함/120s 초과/baseline 미설정 — :9000 status로 원인 구분해 로그
        if (zResp.FailedStep == "lidar_missing") await LogLidarDiagAsync(ct);
        return new TeachingResult { Row = row, Bay = bay, Level = lev, BayPos = inferredBayPos, Success = false, Error = zResp.Error, HasCargo = hasCargo, FailedStep = "z_inference", CaptureOk = true, XInferenceOk = true, FailedSubStep = zResp.FailedStep,
            CapturedFile = capFile, RawPath = capRaw, CalibratedPath = capCal };
    }

    int inferredLevPos = (int)zResp.InferredLevelPos;
    AddLog($"[RX] Z추론 OK (대기 {zSw.ElapsedMilliseconds}ms) level_pos={inferredLevPos} hoist_move_mm={zResp.HoistMoveMm:F1} ({zResp.Message ?? "N/A"}) server_elapsed={zResp.ElapsedMs}ms");
    AddLog($"[COMPARE-Z] 요청level_pos={capLevPos} → 추론level_pos={inferredLevPos} (차이={capLevPos - inferredLevPos}mm)");

    Dispatcher.Invoke(() => { lbl_curLift.Content = inferredLevPos.ToString(); });

    return new TeachingResult
    {
        Row = row, Bay = bay, Level = lev,
        BayPos = inferredBayPos, LevelPos = inferredLevPos,
        Success = true, HasCargo = hasCargo,
        CaptureOk = true, XInferenceOk = true, ZInferenceOk = true,
        CapturedFile = capFile, RawPath = capRaw, CalibratedPath = capCal
    };
}
```

- [ ] **Step 3: 빌드 검증**

Run: `dotnet build gcp_Wpf.csproj`
Expected: `Build succeeded.` / `0 Error(s)` (신규 메서드 미사용 상태 — private 메서드라 경고 없음)

---

### Task 3: Phase2 루프 파이프라인 재배열 + 구 코드 삭제

**Files:**
- Modify: `PageAutoTeaching.xaml.cs:814-1001` (셀 for 루프 전체 교체)
- Delete: `PageAutoTeaching.xaml.cs:46-49` (`VisionRetry`/`VisionRetryDelayMs` 상수+주석), `:121` (`cellSw` 필드 — 로컬로 대체), `CaptureAndInferCellAsync` 전체(1016-1185), `MoveViaSemiAutoAsync` 래퍼(Task 1에서 만든 과도기 래퍼)

**Interfaces:**
- Consumes: `IssueSemiAutoMove` / `WaitSemiAutoArrivalAsync` (Task 1), `CaptureCellAsync` / `InferCellAsync` / `CaptureOutcome` (Task 2)
- 서킷브레이커는 루프 top → harvest 직후(캡처 전, 크레인 정지)로 이동. PAUSE는 top 대기 유지 + 출발 게이트에서 발행 보류.

- [ ] **Step 1: for 루프 교체**

`for (int i = 0; i < targets.Count; i++)`(814행)부터 루프 닫는 `}`(1001행)까지를 아래로 교체. `cbFloor` 선언(812행)은 유지:

```csharp
            // ===== 파이프라인 상태 (깊이 1): 직전 셀의 추론 태스크 — harvest가 유일한 지연 기록 지점(R4) =====
            Task<TeachingResult> pendingTask = null;
            int pendingIdx = -1;
            string pendingName = null;
            System.Diagnostics.Stopwatch pendingSw = null;
            bool moveIssued = false;   // 현재 셀 이동이 이전 셀 출발 게이트에서 이미 발행됐는가

            // 직전 셀 추론 회수(harvest) — 결과 Add·표시. 셀 i의 어떤 기록보다 먼저 호출해 순서 보장(R4).
            async Task HarvestAsync()
            {
                if (pendingTask == null) return;
                TeachingResult r = await pendingTask;   // InferCellAsync는 예외를 던지지 않음(실패 결과로 변환)
                pendingTask = null;
                currentResults.Add(r);
                pendingSw?.Stop();
                if (pendingSw != null) cellDurMs.Add(pendingSw.ElapsedMilliseconds);
                pendingSw = null;
                PushRecent(r);
                if (!string.IsNullOrEmpty(r.RawPath))
                    Dispatcher.Invoke(() => imgRun_Snap.Source = LoadImageNoLock(r.CalibratedPath ?? r.RawPath));
                if (r.Success)
                    AddLog($"[OK] {pendingName}  bay={r.BayPos}mm  lev={r.LevelPos}mm");
                else
                    AddLog($"[FAIL] {pendingName} '{r.FailedStep}' — 실패 기록(재시도 없음), 계속");
                UpdateProgress(pendingIdx + 1, targets.Count, pendingName);
            }

            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    // M3.3: 셀 경계 협조적 PAUSE — 이동 발행 전에만 대기(출발 게이트가 보류한 발행은 아래에서 수행).
                    while (pauseRequested && !ct.IsCancellationRequested)
                    {
                        SetStatus("PAUSE", ClrWarn);
                        await Task.Delay(200, ct);
                    }

                    // 현재 셀용 SKIP 플래그 리셋 (이전 셀의 SKIP가 새 셀로 새지 않도록)
                    skipRequested = false;

                    curTargetIdx = i;
                    var (row, bay, lev) = targets[i];
                    string cellName = CellKey(row, bay, lev);
                    UpdateProgress(i, targets.Count, cellName);

                    var cellSw = System.Diagnostics.Stopwatch.StartNew();   // 셀 로컬 — 파이프라인에서 셀 구간이 겹치므로 필드 금지
                    SetStep("move");
                    UpdateRunStats(i, targets.Count, cellName);

                    AddLog($"────────────────────────────────────────");
                    AddLog($"[CELL {i + 1}/{targets.Count}] {cellName} 시작");

                    // Step 1: 셀 인덱스 → mm 변환 (cellBay/cellLev 1-based)
                    SetStatus("MOVING", ClrWarn);
                    var info = gClass.str.SrmInfo[gClass.srmNum];
                    if (info.cellBay == null || info.cellLev == null
                        || bay < 1 || bay > info.cellBay.Length
                        || lev < 1 || lev > info.cellLev.Length)
                    {
                        AddLog($"[ERR] cellBay/cellLev 미초기화 또는 인덱스 범위 초과 (bay={bay} lev={lev})");
                        await HarvestAsync();   // R4: 셀 i-1 결과 먼저
                        currentResults.Add(new TeachingResult { Row = row, Bay = bay, Level = lev, Success = false, Error = "cell index out of range", FailedStep = "move" });
                        moveIssued = false;
                        continue;
                    }
                    int targetTravMm = info.cellBay[bay - 1];
                    int targetLiftMm = info.cellLev[lev - 1];
                    AddLog($"[MOVE] Crane → {cellName} (Row={row} Bay={bay} Lev={lev}) → TRAV={targetTravMm}mm LIFT={targetLiftMm}mm");

                    // 이동 발행 — 이전 셀 출발 게이트에서 이미 발행됐으면 생략 (R6은 IssueSemiAutoMove가 담당)
                    if (!moveIssued && !IssueSemiAutoMove(row, bay, lev, targetTravMm, targetLiftMm))
                    {
                        await HarvestAsync();
                        currentResults.Add(new TeachingResult { Row = row, Bay = bay, Level = lev, Success = false, Error = "safety tripped", FailedStep = "move" });
                        continue;
                    }
                    moveIssued = false;   // 소비 — 다음 셀 발행 여부는 출발 게이트가 다시 결정

                    var moveSw = System.Diagnostics.Stopwatch.StartNew();
                    bool movedOk = await WaitSemiAutoArrivalAsync(row, bay, lev, targetTravMm, targetLiftMm, ct);
                    moveSw.Stop();

                    // ★ harvest: 이동과 병렬로 돌던 직전 셀 추론 회수 — 라이다 스캔(R1)·이번 셀 기록(R4)보다 반드시 먼저
                    await HarvestAsync();

                    int curBayPos = CurTrav;
                    int curLevPos = CurLift;
                    if (!movedOk)
                    {
                        AddLog($"[ERR] {cellName} 이동 실패 — 다음 셀로");
                        currentResults.Add(new TeachingResult { Row = row, Bay = bay, Level = lev, Success = false, Error = "semi move failed", FailedStep = "move" });
                        continue;
                    }
                    AddLog($"[ARRIVED] {cellName} ({moveSw.ElapsedMilliseconds}ms) 현재위치 trav={curBayPos}mm lift={curLevPos}mm");
                    SetStep("arrive");

                    // 서킷브레이커 — harvest 직후(셀 i-1까지 확정된 결과 기준·크레인 정지 상태). 추론 지연으로 최대 1셀 늦게 발화.
                    int recentFail = 0;
                    for (int k = currentResults.Count - 1; k >= cbFloor; k--)
                    {
                        if (currentResults[k].Success) break;
                        recentFail++;
                    }
                    if (recentFail >= MaxConsecutiveCellFail)
                    {
                        AddLog($"[PAUSE] 최근 {MaxConsecutiveCellFail}셀 연속 실패 — 운영자 확인 대기 (크레인 정지 상태)");
                        SetStatus("PAUSE", ClrErr);
                        var dec = Dispatcher.Invoke(() => MessageBox.Show(
                            $"최근 {MaxConsecutiveCellFail}셀 연속 실패.\n비전 서버 / 크레인 상태를 확인하세요.\n\n[예] 계속 진행   [아니오] 런 중단",
                            "Auto Teaching - 연속 실패", MessageBoxButton.YesNo, MessageBoxImage.Warning));
                        if (dec == MessageBoxResult.Yes)
                        {
                            cbFloor = currentResults.Count;
                            AddLog("[RESUME] 운영자 계속 선택 — 연속실패 카운터 리셋");
                        }
                        else
                        {
                            AddLog("[ABORT] 운영자 중단 선택 — 남은 셀 건너뜀");
                            break;
                        }
                    }

                    // Step 2b: 위치 검증 — 목표위치와 현재위치 비교 (기존 902-917행 그대로)
                    curBayPos = CurTrav;
                    curLevPos = CurLift;
                    int travDiff = Math.Abs(curBayPos - targetTravMm);
                    int liftDiff = Math.Abs(curLevPos - targetLiftMm);
                    AddLog($"[CHECK] 목표 trav={targetTravMm}mm lift={targetLiftMm}mm ↔ 현재 trav={curBayPos}mm lift={curLevPos}mm (차이: trav={travDiff}mm lift={liftDiff}mm)");
                    if (travDiff > 1 || liftDiff > 1)
                    {
                        AddLog($"[WARN] 위치 차이 과대 (허용 ±1mm) — 재대기 500ms 후 재확인");
                        await Task.Delay(500, ct);
                        curBayPos = CurTrav;
                        curLevPos = CurLift;
                        travDiff = Math.Abs(curBayPos - targetTravMm);
                        liftDiff = Math.Abs(curLevPos - targetLiftMm);
                        AddLog($"[CHECK] 재확인 trav={curBayPos}mm lift={curLevPos}mm (차이: trav={travDiff}mm lift={liftDiff}mm)");
                    }

                    // Step 2c: SRM Busy 해제 대기 (기존 920-939행 그대로)
                    var busySw = System.Diagnostics.Stopwatch.StartNew();
                    while (busySw.ElapsedMilliseconds < 10000 && !ct.IsCancellationRequested)
                    {
                        byte trOper = gClass.str.SrmState[gClass.srmNum].trav.trSt1OperState;
                        byte liOper = gClass.str.SrmState[gClass.srmNum].lift.liSt1OperState;
                        if (trOper == 0 && liOper == 0) break;
                        if (busySw.ElapsedMilliseconds % 1000 < 55)
                            AddLog($"[WAIT] SRM Busy 대기 중 (trav={trOper} lift={liOper}) {busySw.ElapsedMilliseconds}ms");
                        await Task.Delay(50, ct);
                    }
                    busySw.Stop();
                    if (busySw.ElapsedMilliseconds >= 10000)
                        AddLog($"[WARN] SRM Busy 10초 타임아웃 — 캡처 진행");
                    else if (busySw.ElapsedMilliseconds > 100)
                        AddLog($"[WAIT] SRM Busy 해제 ({busySw.ElapsedMilliseconds}ms)");

                    byte trOrg = gClass.str.SrmState[gClass.srmNum].trav.trSt1OriginPos;
                    byte liOrg = gClass.str.SrmState[gClass.srmNum].lift.liSt1OriginPos;
                    AddLog($"[ORIGIN] 정위치 신호 trav={trOrg} lift={liOrg} (curPos trav={curBayPos} lift={curLevPos})");

                    // ★ 동시 진행 ①: settle(잔진동 안정화) ∥ 라이다 스캔 — Busy OFF 후 시작(현행 의미 유지).
                    //   R1: 직전 셀 Z추론은 위 HarvestAsync에서 이미 소비 완료 → 여기서 스캔해도 서버 슬롯 안 덮임.
                    //   R3: 크레인 정지 중(출발 게이트 이전). WhenAll로 두 태스크 모두 관찰(취소 시 미관찰 태스크 방지).
                    int settleMs = GetCaptureSettleMs(lev);
                    SetStep("shoot");
                    AddLog($"[CAPTURE] SRM Busy OFF 후 안정화 {settleMs}ms ∥ 라이다 스캔 (Lev {lev})");
                    Task settleTask = settleMs > 0 ? Task.Delay(settleMs, ct) : Task.CompletedTask;
                    Task<bool> scanTask;
                    if (lidarFallback)
                    {
                        scanTask = Task.FromResult(false);   // 폴백: 스캔 생략 (InferCellAsync가 lidarFallback 분기로 처리)
                    }
                    else
                    {
                        SetStatus("LIDAR", ClrInfo);
                        scanTask = RunLidarScanAsync($"셀 R{row}-B{bay}-L{lev}", ct, verbose: false);
                    }
                    await Task.WhenAll(settleTask, scanTask);
                    bool scanOk = !lidarFallback && scanTask.Result;
                    if (!lidarFallback && !scanOk)
                        AddLog($"[WARN] {cellName} 라이다 스캔 실패 — 캡처·X추론은 진행, Z추론은 실패 처리 예정(재스캔 불가)");

                    // ★ R2: 캡처 직전 위치 재읽기(settle 중 크리프 반영) — 이 스냅샷이 X·Z추론까지 그대로 간다.
                    curBayPos = CurTrav;
                    curLevPos = CurLift;
                    bool hasCargo = false;
                    Dispatcher.Invoke(() => { hasCargo = chk_HasCargo.IsChecked == true; });

                    // Step 3: 캡처 (크레인 정지 중 마지막 순차 구간)
                    SetStatus("CAPTURE", Color.FromRgb(0x22, 0xB9, 0xAF));
                    var cap = await CaptureCellAsync(cameraId, row, bay, lev, curBayPos, curLevPos, hasCargo, ct);

                    // ★ 출발 게이트: 캡처 응답 수신 즉시 다음 셀 이동 발행 — 추론을 기다리지 않는다(타임라인 핵심).
                    //   캡처 실패여도 다음 행동은 어차피 이동(재시도 없음). PAUSE 중엔 발행 보류 → 다음 반복 top에서.
                    if (i + 1 < targets.Count && !pauseRequested && !ct.IsCancellationRequested)
                    {
                        var (nrow, nbay, nlev) = targets[i + 1];
                        if (nbay >= 1 && nbay <= info.cellBay.Length && nlev >= 1 && nlev <= info.cellLev.Length
                            && IssueSemiAutoMove(nrow, nbay, nlev, info.cellBay[nbay - 1], info.cellLev[nlev - 1]))
                        {
                            moveIssued = true;
                            AddLog($"[PIPE] 캡처 응답 → {CellKey(nrow, nbay, nlev)} 이동 즉시 발행 (추론은 이동 중 병렬)");
                        }
                        // 발행 실패(안전트립)·인덱스 불량 → moveIssued=false, 다음 반복 top에서 처리
                    }

                    if (!cap.Ok)
                    {
                        cellSw.Stop();
                        cellDurMs.Add(cellSw.ElapsedMilliseconds);
                        currentResults.Add(cap.Fail);   // harvest는 이미 완료 — R4 순서 보장
                        PushRecent(cap.Fail);
                        AddLog($"[FAIL] {cellName} 'capture' — 실패 기록(재시도 없음), 다음 셀로");
                        UpdateProgress(i + 1, targets.Count, cellName);
                        SetStep("");
                        continue;
                    }

                    // ★ 동시 진행 ②: 셀 i의 X→Z 추론을 await 없이 시작 — 다음 셀 이동과 병렬.
                    //   Task.Run 금지: 디스패처 컨텍스트 인터리빙으로만 병렬화(공유 상태 전부 UI 스레드 유지).
                    SetStep("analyze");
                    pendingTask = InferCellAsync(cameraId, row, bay, lev, curBayPos, curLevPos, hasCargo, scanOk,
                                                 cap.CapFile, cap.RawPath, cap.CalPath, ct);
                    pendingIdx = i;
                    pendingName = cellName;
                    pendingSw = cellSw;
                }

                // 정상 종료(끝까지 or break) — 마지막 셀 추론 회수(R5)
                await HarvestAsync();
            }
            finally
            {
                // R5: 취소·예외 경로에서도 in-flight 추론 회수 — 유령 태스크·결과 누락·조기 저장 방지.
                //   (ct 공유로 HTTP는 즉시 취소되고 InferCellAsync가 실패 결과로 변환해 곧 완료된다)
                if (pendingTask != null)
                {
                    try { await HarvestAsync(); }
                    catch (Exception hx) { AddLog($"[PIPE][WARN] 잔여 추론 회수 실패: {hx.Message}"); }
                }
            }
```

- [ ] **Step 2: 구 코드 삭제**

1. 46-49행 `VisionRetry`/`VisionRetryDelayMs` 상수와 주석 삭제 (참조는 재시도 루프뿐이었음 — 컴파일 에러로 잔존 참조 검출)
2. 121행 `private System.Diagnostics.Stopwatch cellSw;` 필드 삭제 (로컬로 대체됨)
3. `CaptureAndInferCellAsync` 메서드 전체 삭제 (1016-1185행 상당)
4. Task 1의 과도기 래퍼 `MoveViaSemiAutoAsync` 삭제 (885-886행 기존 주석 "본 루프: 반자동 이동(0x41)…"도 새 루프에 이미 반영돼 있으므로 함께 정리)

- [ ] **Step 3: 빌드 + 잔존 참조 검색**

Run: `dotnet build gcp_Wpf.csproj`
Expected: `Build succeeded.` / `0 Error(s)`
Run: `grep -n "VisionRetry\|MoveViaSemiAutoAsync\|CaptureAndInferCellAsync" PageAutoTeaching.xaml.cs`
Expected: 매치 0건

---

### Task 4: 검증 (빌드 + 로그 시퀀스 리뷰)

**Files:**
- 없음 (검증 전용)

- [ ] **Step 1: Release 빌드**

Run: `dotnet build gcp_Wpf.csproj -c Release`
Expected: `Build succeeded.` / `0 Error(s)`

- [ ] **Step 2: 코드 시퀀스 셀프 체크 (R1-R6)**

새 루프에서 아래를 눈으로 재확인 (스펙 정합성 규칙):
- R1: `RunLidarScanAsync` 호출이 `await HarvestAsync()` 뒤에만 존재
- R2: `curBayPos/curLevPos` 재읽기가 `WhenAll(settle, scan)` 뒤·`CaptureCellAsync` 앞 1회이고, `InferCellAsync`에 그 값이 그대로 전달
- R3: `IssueSemiAutoMove`(출발 게이트)가 `CaptureCellAsync` await 뒤에만 위치
- R4: `currentResults.Add`가 5곳(인덱스불량/발행거부/이동실패/캡처실패/HarvestAsync) 모두 harvest 이후 실행됨
- R5: try 정상 경로 끝 + finally 양쪽에 harvest 존재
- R6: 이동 발행이 `IssueSemiAutoMove` 경유뿐(gcpTxMode 재강제 포함 확인)

- [ ] **Step 3: 실기 로그 검증 절차 기록 (운영자/현장용)**

현장 런 1회 후 `SRM{n}\Teaching\Log\AutoTeachingLog_*.txt`에서:
1. 셀마다 `[RX] Z추론 OK`(셀 i-1) 로그가 `[LIDAR]`/`scan` (셀 i) 로그보다 **앞**인지 — R1 순서 전수 확인
2. `[PIPE] 캡처 응답 → … 이동 즉시 발행` 로그가 매 셀(마지막 제외) 존재
3. `[SUMMARY] 총소요`를 파이프라인 이전 런과 비교 — 셀당 ~3초 이상 단축 기대
4. STOP·PAUSE·연속실패 모달 각 1회 유도 후: 크레인 정지·부분 저장·`Pending` rollback 동작 확인
