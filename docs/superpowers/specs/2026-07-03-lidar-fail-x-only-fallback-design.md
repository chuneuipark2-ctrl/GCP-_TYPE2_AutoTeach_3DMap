# 라이다 실패 시 주행(X)만 티칭 폴백 — 설계 스펙

> 작성일: 2026-07-03 · 대상: `gcp_type2-TP2_VER1` (지상반 WPF, .NET 6)
> 계기: START 헬스체크 라이다 스캔이 502 "유효 프레임 부족(0개) — 빔 미검출"로 실패 시 현재는 재시도/중단만 가능. 라이다가 안 될 때도 주행(X) 티칭은 진행하고 싶다.

## 1. 배경 · 핵심 제약

- 승강(Z) 보정은 **라이다 기반 `start_z_inference` 하나뿐**. 캘리브 계열(`calibration/inference`)은 응답이 `travel_move_mm`뿐 — **주행 전용, 승강 보정 없음**. START이 읽는 캘리브 파일 `offset_y`는 0.
- 따라서 이 시스템엔 **라이다 없이 승강을 보정할 서버 경로가 없음**. "원래 쓰던 캘리브레이션 파일 값 사용" = 이 시스템에선 **승강은 기존 캘리브(티칭)값을 유지하고 주행만 재티칭**과 동일 결과(offset_y=0).
- 현재 Phase 1.7(2019~2032)은 라이다 실패 시 재시도/중단만 제공(주석 2018: "라이다 없이 계속 옵션 없음"). 이걸 **폴백(주행만 티칭) 옵션 추가**로 개정.

## 2. 확정 결정사항 (사용자 승인)

| 항목 | 결정 |
|---|---|
| START 라이다 재시도 | **자동 2회** |
| 2회 실패 후 | **운영자 확인 후** X-only 폴백 전환 |
| 폴백 시 승강(Z) | **기존 캘리브(티칭)값 유지**(재보정 안 함). 서버 라이다-없는 승강보정 없음 |
| 폴백 시 주행(X) | 정상대로 `start_x_inference` 재티칭 |
| 범위 | 폴백은 **런 전체**에 적용(라이다 계통 다운 확정) |

## 3. 상세 설계

### 3.1 런 플래그
- 필드 `private bool lidarFallback = false;` 추가.
- `Btn_Start_Click`에서 런 시작 시(cts 재생성 부근) `lidarFallback = false;`로 리셋 → 이전 런 상태 누수 방지.

### 3.2 Phase 1.7 개정 (현재 while 루프 2019~2032 대체)

```
SetStatus("LIDAR")
while (true):
    ok = false
    for attempt in 1..2:                       # 자동 2회
        if RunLidarScanAsync("START 헬스체크", cts.Token): ok = true; break
        if attempt < 2: AddLog("[LIDAR] 자동 재시도...")
    if ok: break                               # 라이다 정상 → lidarFallback=false 유지

    # 자동 2회 실패 → 운영자 선택
    lr = MessageBox("라이다 검출 실패 (자동 2회 시도).\n승강(Z)은 기존 캘리브값 유지, 주행(X)만 티칭합니다.\n\n[예] X만 계속   [취소] 다시 시도   [아니오] START 중단",
                    YesNoCancel, Warning)
    if lr == Cancel: continue                   # 재시도
    if lr == Yes:    lidarFallback = true; AddLog("[LIDAR] 운영자 선택 — 라이다 미사용, 주행(X)만 티칭/승강 기존값 유지"); break
    else:            AddLog("[ERR] 라이다 스캔 실패 — 운영자 중단"); return
```

- 버튼 매핑: **예=X만 계속(폴백), 취소=재시도, 아니오=중단.**

### 3.3 Phase 2 per-cell 폴백 분기 (`CaptureAndInferCellAsync`, 정의 1003~)

X추론 성공 직후(현재 "Step 4b-0: 라이다 스캔" `RunLidarScanAsync` 호출 바로 앞)에 분기 삽입:

```csharp
if (lidarFallback)
{
    int existingLev = gClass.str.SrmInfo[gClass.srmNum].cellLev[lev - 1];  // 기존 캘리브(티칭)값
    AddLog($"[Z] 라이다 미사용(폴백) — 승강 기존값 유지 level_pos={existingLev}");
    Dispatcher.Invoke(() => { lbl_curLift.Content = existingLev.ToString(); });
    return new TeachingResult {
        Row = row, Bay = bay, Level = lev,
        BayPos = inferredBayPos, LevelPos = existingLev,
        Success = true, HasCargo = hasCargo,
        CaptureOk = true, XInferenceOk = true, ZInferenceOk = false,
        CapturedFile = capFile, RawPath = capRaw, CalibratedPath = capCal
    };
}
```

- **라이다 스캔·`start_z_inference` 미호출** → 셀당 9s 라이다 타임아웃 낭비 없음.
- 결과: `BayPos`=신규 X추론값, `LevelPos`=기존 `cellLev[lev-1]`(변경 없음 → 반영 시 승강 no-op), `ZInferenceOk=false`(폴백 표식).
- `capFile/capRaw/capCal`, `inferredBayPos`는 분기 지점에서 이미 확보돼 있음(캡처·X추론 완료 후).

### 3.4 완료 로그
- 폴백 런이면 `[COMPLETE]`/`[SUMMARY]` 부근에 `[LIDAR] 이번 런: 라이다 미사용(X만 티칭, 승강 미보정)` 1줄 추가로 운영자에게 명시.

## 4. 변경 파일

`PageAutoTeaching.xaml.cs`만:
- 필드 `lidarFallback` 추가 + `Btn_Start_Click` 리셋
- Phase 1.7 while 루프 개정(자동 2회 + YesNoCancel 폴백)
- `CaptureAndInferCellAsync` X추론 뒤 폴백 분기
- 완료 로그 1줄

기존 API/모델/다른 파일 무수정. 정상 라이다 경로는 **동작 100% 불변**(lidarFallback=false).

## 5. 비변경 보장 · 유의

- 라이다 정상(성공) 시: 기존 흐름 그대로(자동 2회 중 1회라도 성공하면 즉시 정상 진행).
- 폴백 셀은 `ZInferenceOk=false`로 기록 → 결과 확인/엑셀에서 승강 미보정 여부 구분 가능(기존 필드 활용, 리뷰 UI 자체는 무수정).
- 폴백은 "승강 재티칭 안 함"이지 "승강 무시"가 아님 — 기존 캘리브 승강값을 그대로 보존한다.

## 6. 검증 기준

- **자동(이 세션):** `dotnet build "gcp_Wpf.sln"` 성공.
- **기능(장비, 사용자):**
  1. 라이다 정상: 기존과 동일하게 진행(회귀 없음), 자동 2회 재시도 로그만 추가 관찰.
  2. 라이다 실패(빔 미검출 재현): 자동 2회 실패 후 [예] X만 계속 → 셀마다 `[Z] 라이다 미사용(폴백)` + 주행만 갱신, 승강 기존값 유지, `[아니오]` 중단·`[취소]` 재시도 동작.
  3. 폴백 완주 후 결과의 승강값이 기존과 동일(변경 없음), 주행은 갱신됨.
