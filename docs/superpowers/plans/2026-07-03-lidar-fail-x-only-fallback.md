# 라이다 실패 시 주행(X)만 티칭 폴백 — 구현 계획

> **For agentic workers:** 2개 태스크, 단일 파일(`PageAutoTeaching.xaml.cs`). 완료 게이트 = `dotnet build "gcp_Wpf.sln"` 성공. 테스트 스위트·git 없음 → 빌드+수동 확인.

**Goal:** START 라이다 스캔이 자동 2회 실패하면 운영자 확인 후 "주행(X)만 티칭, 승강(Z)은 기존 캘리브값 유지" 폴백 모드로 런을 진행한다.

**Architecture:** 런 플래그 `lidarFallback`을 Phase 1.7에서 세팅. per-cell(`CaptureAndInferCellAsync`)에서 이 플래그면 X추론 뒤 라이다·Z추론을 스킵하고 기존 `cellLev[lev-1]`를 승강값으로 반환. 정상 라이다면 플래그 false로 기존 흐름 100% 불변.

**Tech Stack:** C# / .NET 6 / WPF.

## Global Constraints

- 변경 파일: `PageAutoTeaching.xaml.cs` 하나. 기존 API/모델/다른 파일 무수정.
- 정상 라이다 경로(자동 2회 중 성공) 동작 불변.
- 폴백 승강값 = 기존 `cellLev[lev-1]`(변경 없음). `ZInferenceOk=false` 표식.
- Phase 1.7 프롬프트: YesNoCancel — 예=X만 계속, 취소=재시도, 아니오=중단.
- 빌드: `dotnet build "gcp_Wpf.sln"`.

---

### Task 1: 폴백 플래그 + Phase 1.7 자동 2회/프롬프트

**Files:**
- Modify: `PageAutoTeaching.xaml.cs` (필드 ~L27 / Btn_Start 리셋 ~L1941 / Phase 1.7 루프 L2015~2032)

**Interfaces:**
- Produces: 필드 `private bool lidarFallback`. Phase 1.7가 자동 2회 실패+운영자 [예] 시 `lidarFallback=true` 설정.
- Consumes: 기존 `RunLidarScanAsync`, `SetStatus`, `AddLog`, `MessageBox`.

- [ ] **Step 1: 필드 추가** — `private const int GUARD_FALLBACK_MARGIN_MM = 100;` 줄 다음에 삽입

```csharp
        // [라이다 폴백] START 라이다 2회 실패 후 운영자가 'X만 계속' 선택 시 true. 셀마다 라이다·Z추론 스킵, 승강 기존값 유지.
        private bool lidarFallback = false;
```

- [ ] **Step 2: Btn_Start_Click에서 런마다 리셋** — `origGcpTxMode`/`cts` 재생성 블록에 추가

변경 전:
```csharp
            byte origGcpTxMode = gClass.str.SrmState[gClass.srmNum].gcpState.gcpTxMode;
            cts?.Dispose(); cts = new CancellationTokenSource();
            currentResults.Clear();
```
변경 후:
```csharp
            byte origGcpTxMode = gClass.str.SrmState[gClass.srmNum].gcpState.gcpTxMode;
            cts?.Dispose(); cts = new CancellationTokenSource();
            lidarFallback = false;   // 새 런: 라이다 폴백 상태 리셋
            currentResults.Clear();
```

- [ ] **Step 3: Phase 1.7 while 루프 개정** — L2015~2032 전체 교체

변경 전:
```csharp
                // Phase 1.7: 라이다 스캔 — START 시 무조건 1회 실행 (2026-07-02 사용자 요청).
                //   성격 = 헬스체크: 측정값은 120초만 유효하므로 셀 진행 중 신선도는 셀마다 Z추론 직전
                //   재스캔(CaptureAndInferCellAsync)이 담당한다. 여기서 실패 = 라이다 계통 자체가 죽은 것 →
                //   승강 추론이 전 셀 lidar_missing으로 전멸하므로 '라이다 없이 계속' 옵션은 없다(재시도/중단만).
                SetStatus("LIDAR", ClrInfo);
                while (true)
                {
                    if (await RunLidarScanAsync("START 헬스체크", cts.Token)) break;

                    SetStatus("LIDAR FAIL", ClrErr);
                    var lr = MessageBox.Show(
                        "라이다 스캔 실패 (라이다/비전 서버 확인 — 로그의 [LIDAR][DIAG] 참조).\n" +
                        "라이다 없이는 승강(Z) 추론이 불가능합니다.\n\n[예] 재시도   [아니오] START 중단",
                        "Auto Teaching - 라이다", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (lr == MessageBoxResult.Yes) { AddLog("[RETRY] 라이다 스캔 재시도 (운영자 선택)"); SetStatus("LIDAR", ClrInfo); continue; }
                    AddLog("[ERR] 라이다 스캔 실패 — 운영자 중단");
                    return;
                }
```
변경 후:
```csharp
                // Phase 1.7: 라이다 스캔 — START 시 자동 2회 시도.
                //   성격 = 헬스체크: 측정값은 120초만 유효하므로 셀 진행 중 신선도는 셀마다 Z추론 직전
                //   재스캔(CaptureAndInferCellAsync)이 담당한다. 자동 2회 실패 시 운영자가 'X만 계속'을 고르면
                //   라이다 없이 주행(X)만 티칭하고 승강(Z)은 기존 캘리브값을 유지한다(lidarFallback=true).
                SetStatus("LIDAR", ClrInfo);
                while (true)
                {
                    bool lidarOk = false;
                    for (int attempt = 1; attempt <= 2; attempt++)   // 자동 2회 시도
                    {
                        if (await RunLidarScanAsync("START 헬스체크", cts.Token)) { lidarOk = true; break; }
                        if (attempt < 2) AddLog("[LIDAR] 자동 재시도...");
                    }
                    if (lidarOk) break;   // 라이다 정상 → lidarFallback=false 유지

                    SetStatus("LIDAR FAIL", ClrErr);
                    var lr = MessageBox.Show(
                        "라이다 검출 실패 (자동 2회 시도 — 로그의 [LIDAR][DIAG] 참조).\n" +
                        "승강(Z)은 기존 캘리브레이션 값을 유지하고 주행(X)만 티칭합니다.\n\n[예] X만 계속   [취소] 다시 시도   [아니오] START 중단",
                        "Auto Teaching - 라이다", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                    if (lr == MessageBoxResult.Cancel) { AddLog("[RETRY] 라이다 재시도 (운영자 선택)"); SetStatus("LIDAR", ClrInfo); continue; }
                    if (lr == MessageBoxResult.Yes)
                    {
                        lidarFallback = true;
                        AddLog("[LIDAR] 운영자 선택 — 라이다 미사용, 주행(X)만 티칭 / 승강 기존값 유지");
                        break;
                    }
                    AddLog("[ERR] 라이다 스캔 실패 — 운영자 중단");
                    return;
                }
```

- [ ] **Step 4: 빌드 검증**

Run: `dotnet build "gcp_Wpf.sln"`
Expected: 빌드 성공(0 errors). `lidarFallback`은 세팅만 되고 아직 소비 안 됨(다음 태스크).

---

### Task 2: per-cell 폴백 분기 + 완료 로그

**Files:**
- Modify: `PageAutoTeaching.xaml.cs` (`CaptureAndInferCellAsync` X추론 뒤 ~L1076 / 완료 로그 ~L2047)

**Interfaces:**
- Consumes: Task 1의 `lidarFallback`. 기존 `inferredBayPos`, `capFile/capRaw/capCal`(캡처 후 확보), `gClass.str.SrmInfo[sn].cellLev`, `lbl_curLift`, `TeachingResult`(필드 BayPos/LevelPos/Success/HasCargo/CaptureOk/XInferenceOk/ZInferenceOk/CapturedFile/RawPath/CalibratedPath).

- [ ] **Step 1: per-cell 폴백 분기 삽입** — X추론 뒤 lbl_curTrav 갱신과 "Step 4b-0: 라이다 스캔" 사이에 삽입

변경 전:
```csharp
            Dispatcher.Invoke(() => { lbl_curTrav.Content = inferredBayPos.ToString(); });

            // Step 4b-0: 라이다 스캔 — 승강(Z) 추론 필수 선행 (스펙 2026-07-02: 측정값 서버 내부저장 120초 유효).
```
변경 후:
```csharp
            Dispatcher.Invoke(() => { lbl_curTrav.Content = inferredBayPos.ToString(); });

            // [라이다 폴백] 라이다 미사용 모드 — 승강(Z)은 기존 캘리브(티칭)값 유지, 라이다·Z추론 스킵.
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

            // Step 4b-0: 라이다 스캔 — 승강(Z) 추론 필수 선행 (스펙 2026-07-02: 측정값 서버 내부저장 120초 유효).
```

- [ ] **Step 2: 완료 로그에 폴백 표식 추가** — `[COMPLETE]` 로그 다음 줄

변경 전:
```csharp
                AddLog($"[COMPLETE] {okCnt}/{targets.Count} cells done");
```
변경 후:
```csharp
                AddLog($"[COMPLETE] {okCnt}/{targets.Count} cells done");
                if (lidarFallback) AddLog("[LIDAR] 이번 런: 라이다 미사용(주행 X만 티칭, 승강 미보정)");
```

- [ ] **Step 3: 빌드 검증**

Run: `dotnet build "gcp_Wpf.sln"`
Expected: 빌드 성공(0 errors).

- [ ] **Step 4: 수동 확인(장비, 사용자)**
  1. 라이다 정상: 자동 2회 중 성공 → 기존과 동일 진행(회귀 없음).
  2. 라이다 실패: 자동 2회 실패 후 [예] X만 계속 → 셀마다 `[Z] 라이다 미사용(폴백)` + 주행만 갱신/승강 기존값. [취소]=재시도, [아니오]=중단.
  3. 폴백 완주 시 `[LIDAR] 이번 런: 라이다 미사용…` 로그, 결과의 승강값이 기존과 동일.

---

## 검증 요약

| 게이트 | 방법 |
|---|---|
| 컴파일 | 각 태스크 끝 `dotnet build "gcp_Wpf.sln"` |
| 회귀 없음 | Task 2 Step 4-1 |
| 폴백 동작 | Task 2 Step 4-2/3 (장비) |
