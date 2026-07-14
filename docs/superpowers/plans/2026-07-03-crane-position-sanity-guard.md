# 크레인 위치 sanity 가드 — 구현 계획

> **For agentic workers:** 2개 태스크, 단일 파일(`PageAutoTeaching.xaml.cs`). 각 태스크 완료 게이트 = `dotnet build "gcp_Wpf.sln"` 성공. 이 프로젝트는 단위 테스트 스위트·git이 없어 TDD/커밋 단계 대신 빌드+수동 확인을 쓴다.

**Goal:** 0x41 반자동 티칭 이동 중 크레인이 보고한 위치가 유효 범위를 벗어나면 즉시 Start OFF(0x50)로 세우고 런 전체를 중단한다.

**Architecture:** 런 시작 시 소프트리밋(0xA3/A5)으로 유효 주행/승강 범위(envelope)를 1회 계산(실패 시 셀그리드 min~max ±100mm 폴백)해 필드에 저장. `MoveViaSemiAutoAsync` 폴링 루프에서 매 주기 `curPos`가 envelope 밖이면 Start OFF + `cts.Cancel()`로 기존 취소 경로를 태워 런을 중단한다.

**Tech Stack:** C# / .NET 6 / WPF. 기존 헬퍼 `ReadDriveParamAsync`/`ReadLiftParamAsync`, 기존 무장해제 필드(`startOnOff` 등), 기존 `cts` 취소 플러밍 재사용.

## Global Constraints

- 변경 파일: `PageAutoTeaching.xaml.cs` 하나. 다른 파일·API·모델·0x59 경로 무수정.
- 적용 범위: `MoveViaSemiAutoAsync`(0x41)만, 주행+승강 두 축.
- envelope 기준: SL Home~End(성공) / 셀그리드 min~max ± 100mm(폴백). 폴백 margin 상수 = 100.
- STOP = `startOnOff=0`(0x50, 기존 `DisarmCraneAfterRun`과 동일 방식). 런 중단 = `cts.Cancel()` + `ct.ThrowIfCancellationRequested()`(기존 취소 경로).
- 필드 쓰기는 코드베이스 표준인 in-place 필드쓰기(`gClass.str.SrmPacket[sn].X = ...`) 사용.
- 빌드 명령: `dotnet build "gcp_Wpf.sln"`.

---

### Task 1: envelope 필드 + 런시작 로더 헬퍼

**Files:**
- Modify: `PageAutoTeaching.xaml.cs` (필드: `CancellationTokenSource cts;` 선언 뒤 ~L23 / 헬퍼: `ClearSemiJobFlag` 메서드 뒤 ~L1299)

**Interfaces:**
- Consumes: 기존 `ReadDriveParamAsync(int minLen, int timeoutMs, CancellationToken ct)`, `ReadLiftParamAsync(...)` (둘 다 `Task<bool>`, 각각 `SrmPacket[sn].driveParamData`/`liftParamData` 채움), `gClass.str.SrmInfo[sn].cellBay`/`cellLev`(int[]), `AddLog`.
- Produces: 필드 `envReady`(bool), `envTravLo/envTravHi/envLiftLo/envLiftHi`(long), 상수 `GUARD_FALLBACK_MARGIN_MM`(int=100); 메서드 `LoadPositionGuardEnvelopeAsync(List<(int row,int bay,int lev)> targets, CancellationToken ct)`.

- [ ] **Step 1: envelope 필드 추가** — `CancellationTokenSource cts;`(L23) 바로 다음 줄에 삽입

```csharp
        // [위치 sanity 가드] 런 시작 1회 계산하는 유효 위치 범위(mm). curPos가 이 밖이면 폭주로 보고 STOP+런중단.
        private bool envReady = false;
        private long envTravLo, envTravHi, envLiftLo, envLiftHi;
        private const int GUARD_FALLBACK_MARGIN_MM = 100; // SL 읽기 실패 시 셀그리드 min~max에 적용할 여유
```

- [ ] **Step 2: 로더 헬퍼 추가** — `ClearSemiJobFlag` 메서드의 닫는 `}`(L1299) 다음에 삽입

```csharp
        // [위치 sanity 가드] 런 시작 1회: 소프트리밋(0xA3/A5)으로 유효 주행/승강 범위를 잡는다.
        //   읽기 성공 → SL Home~End. 실패 → 이번 런 타겟 셀 그리드 min~max ± GUARD_FALLBACK_MARGIN_MM.
        //   가드(MoveViaSemiAutoAsync)는 이 envelope만 비교한다.
        private async Task LoadPositionGuardEnvelopeAsync(List<(int row, int bay, int lev)> targets, CancellationToken ct)
        {
            envReady = false;
            int sn = gClass.srmNum;
            try
            {
                if (await ReadDriveParamAsync(187, 10000, ct) && await ReadLiftParamAsync(187, 10000, ct))
                {
                    byte[] dd = gClass.str.SrmPacket[sn].driveParamData;
                    byte[] ld = gClass.str.SrmPacket[sn].liftParamData;
                    envTravLo = BitConverter.ToUInt32(dd, 179);
                    envTravHi = BitConverter.ToUInt32(dd, 183);
                    envLiftLo = BitConverter.ToInt32(ld, 179);
                    envLiftHi = BitConverter.ToInt32(ld, 183);
                    if (envTravHi > envTravLo && envLiftHi > envLiftLo)
                    {
                        envReady = true;
                        AddLog($"[SAFETY] 소프트리밋 로드 — 주행 {envTravLo}~{envTravHi}mm, 승강 {envLiftLo}~{envLiftHi}mm (위치 가드 기준)");
                        return;
                    }
                    AddLog("[SAFETY][WARN] 소프트리밋 값 비정상(End<=Home) — 폴백으로 전환");
                }
                else AddLog("[SAFETY][WARN] 소프트리밋 읽기 실패 — 폴백으로 전환");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { AddLog($"[SAFETY][WARN] 소프트리밋 읽기 예외: {ex.Message} — 폴백으로 전환"); }

            // 폴백: 이번 런 타겟 셀 그리드 min~max ± margin
            var info = gClass.str.SrmInfo[sn];
            if (info.cellBay == null || info.cellLev == null || targets == null || targets.Count == 0)
            {
                AddLog("[SAFETY][WARN] 셀 배열/타겟 미로드 — 위치 가드 비활성");
                return;
            }
            long tLo = long.MaxValue, tHi = long.MinValue, lLo = long.MaxValue, lHi = long.MinValue;
            foreach (var (row, bay, lev) in targets)
            {
                if (bay - 1 < 0 || bay - 1 >= info.cellBay.Length || lev - 1 < 0 || lev - 1 >= info.cellLev.Length) continue;
                int tv = info.cellBay[bay - 1], lvp = info.cellLev[lev - 1];
                if (tv < tLo) tLo = tv;
                if (tv > tHi) tHi = tv;
                if (lvp < lLo) lLo = lvp;
                if (lvp > lHi) lHi = lvp;
            }
            if (tLo > tHi || lLo > lHi)
            {
                AddLog("[SAFETY][WARN] 그리드 범위 계산 실패 — 위치 가드 비활성");
                return;
            }
            envTravLo = tLo - GUARD_FALLBACK_MARGIN_MM; envTravHi = tHi + GUARD_FALLBACK_MARGIN_MM;
            envLiftLo = lLo - GUARD_FALLBACK_MARGIN_MM; envLiftHi = lHi + GUARD_FALLBACK_MARGIN_MM;
            envReady = true;
            AddLog($"[SAFETY] 폴백 위치 범위 — 주행 {envTravLo}~{envTravHi}mm, 승강 {envLiftLo}~{envLiftHi}mm (그리드±{GUARD_FALLBACK_MARGIN_MM})");
        }
```

- [ ] **Step 3: 빌드 검증**

Run: `dotnet build "gcp_Wpf.sln"`
Expected: 빌드 성공(0 errors). 헬퍼는 아직 미호출이라 CS0168/사용안됨 경고 없이 컴파일(필드는 다음 태스크에서 사용).

---

### Task 2: 런시작 호출 + 폴링 가드

**Files:**
- Modify: `PageAutoTeaching.xaml.cs` (호출: `Phase2_TeachingLoopAsync` 내 L722 직후 / 가드: `MoveViaSemiAutoAsync` 폴링 루프 L1256 직후)

**Interfaces:**
- Consumes: Task 1의 `LoadPositionGuardEnvelopeAsync`, `envReady`, `envTravLo/Hi`, `envLiftLo/Hi`. 기존 `cts`(필드), `ClearSemiJobFlag`, `gClass.str.SrmPacket[sn]`의 `startCmd/startOnOff/semiJobClicked/maintMoveReq`.

- [ ] **Step 1: 런 시작 시 envelope 로드 호출** — `Phase2_TeachingLoopAsync`에서 `await RefreshCellPositionsAsync(ct);` 다음에 삽입

변경 전:
```csharp
            // ★ 셀 위치 0x94 재조회 — Vexi에서 변경한 셀 정위치 반영 (앱 시작 캐시 무효화)
            await RefreshCellPositionsAsync(ct);
```
변경 후:
```csharp
            // ★ 셀 위치 0x94 재조회 — Vexi에서 변경한 셀 정위치 반영 (앱 시작 캐시 무효화)
            await RefreshCellPositionsAsync(ct);

            // [위치 sanity 가드] 런 시작 1회 — 유효 위치 범위 계산 (SL 우선, 실패 시 그리드±margin)
            await LoadPositionGuardEnvelopeAsync(targets, ct);
```

- [ ] **Step 2: 폴링 루프에 가드 블록 삽입** — `MoveViaSemiAutoAsync`에서 `int curT = s.trav.curPos, curL = s.lift.curPos;`(L1256) 다음에 삽입

변경 전:
```csharp
                var s = gClass.str.SrmState[srmNum];
                int curT = s.trav.curPos, curL = s.lift.curPos;
                int errT = Math.Abs(curT - targetTravMm), errL = Math.Abs(curL - targetLiftMm);
```
변경 후:
```csharp
                var s = gClass.str.SrmState[srmNum];
                int curT = s.trav.curPos, curL = s.lift.curPos;

                // [위치 sanity 가드] curPos가 유효 범위 밖 = 위치 폭주/엔코더 폴트 → 즉시 STOP + 런 전체 중단
                if (envReady && (curT < envTravLo || curT > envTravHi || curL < envLiftLo || curL > envLiftHi))
                {
                    AddLog($"[SEMI][SAFETY] ★위치 범위 이탈 — trav={curT}(허용 {envTravLo}~{envTravHi}) lift={curL}(허용 {envLiftLo}~{envLiftHi}) → 크레인 STOP + 런 중단");
                    gClass.str.SrmPacket[srmNum].startCmd = 1;
                    gClass.str.SrmPacket[srmNum].startOnOff = 0;      // 0x50 Start OFF (즉시 정지)
                    gClass.str.SrmPacket[srmNum].semiJobClicked = false;
                    gClass.str.SrmPacket[srmNum].maintMoveReq = false;
                    ClearSemiJobFlag(srmNum);
                    cts?.Cancel();                                    // 런 전체 중단 (기존 취소 경로 재사용)
                    ct.ThrowIfCancellationRequested();                // OCE 전파 → Btn_Start catch → Phase3 + finally DisarmCrane
                }
                int errT = Math.Abs(curT - targetTravMm), errL = Math.Abs(curL - targetLiftMm);
```

- [ ] **Step 3: 빌드 검증**

Run: `dotnet build "gcp_Wpf.sln"`
Expected: 빌드 성공(0 errors).

- [ ] **Step 4: 수동 확인(장비, 사용자)**
  1. 정상 티칭 런에서 `[SAFETY] 소프트리밋 로드 …` 로그가 뜨고, 가드 오탐 없이 완주(회귀 없음).
  2. (가능 시, 저속·안전확보) 위치를 범위 밖으로 만들면 `[SEMI][SAFETY] ★위치 범위 이탈 …` + Start OFF + 런 중단 확인.

---

## 검증 요약

| 게이트 | 방법 |
|---|---|
| 컴파일 | 각 태스크 끝 `dotnet build "gcp_Wpf.sln"` |
| 회귀 없음 | Task 2 Step 4-1 정상 런 완주 |
| 가드 동작 | Task 2 Step 4-2 (장비 안전확보 후) |
