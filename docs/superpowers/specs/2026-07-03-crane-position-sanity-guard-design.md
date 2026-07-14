# 크레인 위치 sanity 가드 — 설계 스펙

> 작성일: 2026-07-03 · 대상: `gcp_type2-TP2_VER1` (지상반 WPF, .NET 6)
> 계기: 오토티칭 런에서 셀7 주행 위치가 `9957→22758→26706mm`(목표 11237)로 폭주 → 15초 stall로 뒤늦게 중단. 위치 파싱은 무결(크레인/PLC 측 값), 앱에 위치 sanity 체크가 없음.

## 1. 목적

`MoveViaSemiAutoAsync`(0x41 반자동 티칭 이동) 중 크레인이 보고하는 `curPos`(주행·승강)가 **유효 범위를 벗어나면 즉시 크레인을 세우고(Start OFF) 런 전체를 중단**한다. 위치 폭주/엔코더 폴트 시 15초 기다리지 않고 빠르게 fail-safe.

## 2. 확정 결정사항 (사용자 승인)

| 항목 | 결정 |
|---|---|
| 감지 시 동작 | **즉시 중단 + 크레인 STOP 송신** |
| 중단 범위 | **런 전체 중단** (해당 셀만 아님) |
| 판정 기준 | **소프트리밋(SL) 절대범위** — curPos가 SL Home~End 밖이면 이상 |
| SL 확보 | **런 시작 1회 읽기**(0xA3/A5), 실패 시 폴백 |
| 폴백 기준 | **셀그리드 최소~최대 ± 100mm** |
| 적용 범위 | **0x41 반자동(티칭) 이동만**, 주행+승강 둘 다 |

## 3. 상세 설계

### 3.1 유효 범위(envelope) — 런 시작 1회 계산

런 시작(셀 루프 진입 전, 크레인 무장 전)에 아래 순서로 계산해 필드에 저장하고, 이후 가드는 분기 없이 이 필드만 비교한다.

- **기본(SL 로드 성공):** 기존 `ReadDriveParamAsync(187, 10000, ct)`(0xA3) / `ReadLiftParamAsync(187, 10000, ct)`(0xA5)로 파라미터를 읽어
  - 주행: `envTravLo = ToUInt32(driveParamData, 179)`(SL Home), `envTravHi = ToUInt32(driveParamData, 183)`(SL End)
  - 승강: `envLiftLo = ToInt32(liftParamData, 179)`(SL Home), `envLiftHi = ToInt32(liftParamData, 183)`(SL End)
  - 물리 가동 전범위라 정상 위치는 항상 안, 26706 같은 폭주는 밖.
- **폴백(SL 읽기 실패):** 이번 런 타겟 셀 그리드에서 계산
  - 각 타겟 `(row,bay,lev)`의 `cellBay[bay-1]`(주행 mm), `cellLev[lev-1]`(승강 mm)로 min/max 산출
  - `envTravLo = min(cellBay 타겟들) - 100`, `envTravHi = max(...) + 100`
  - `envLiftLo = min(cellLev 타겟들) - 100`, `envLiftHi = max(...) + 100`
  - ⚠️ **왜 "목표±100"이 아니라 "그리드 min~max ±100"인가:** 셀마다 목표±margin을 쓰면 긴 이동 시작 시 직전 셀 위치가 새 목표에서 margin 이상 떨어져 있어 오탐한다. 그리드 전체 범위 기준이면 이동 중에도 오탐 없이 폭주만 잡는다.
- envelope 필드 타입: `long`(주행 SL은 UInt32라 int 초과 대비). `curPos`(int)와 비교 시 자동 승격.
- `envReady`(bool) 플래그: 계산 완료 시 true. 가드는 `envReady`일 때만 동작(방어적).

### 3.2 감지 시 동작 (`MoveViaSemiAutoAsync` 폴링 루프)

폴링 루프에서 `curT=s.trav.curPos`, `curL=s.lift.curPos`를 읽은 직후 검사:

```
if (envReady && (curT < envTravLo || curT > envTravHi || curL < envLiftLo || curL > envLiftHi))
{
    AddLog("[SEMI][SAFETY] ★위치 범위 이탈 — trav={curT}(허용 {envTravLo}~{envTravHi}) lift={curL}(허용 {envLiftLo}~{envLiftHi}) → 크레인 STOP + 런 중단");
    // 즉시 정지: Start OFF (기존 무장해제 수단 재사용)
    startCmd=1; startOnOff=0; semiJobClicked=false; maintMoveReq=false;
    ClearSemiJobFlag(srmNum);
    cts?.Cancel();                    // 런 전체 중단 (기존 취소 플러밍 재사용)
    ct.ThrowIfCancellationRequested(); // OCE 전파 → Btn_Start catch → Phase3 + finally DisarmCrane
}
```

- STOP은 기존 `DisarmCraneAfterRun`과 동일한 `startOnOff=0`(0x50 Start OFF) 방식.
- 런 중단은 기존 `cts` 취소 경로 재사용 → 새 제어 흐름 최소화. Btn_Start의 `finally`가 `DisarmCraneAfterRun`로 재차 무장해제(중복이나 무해).
- `[SEMI][SAFETY]` 로그가 원인을 명확히 남김(취소 로그와 구분).

### 3.3 적용 범위

- `MoveViaSemiAutoAsync`(0x41)만. `MoveViaMaintAsync`(0x59)는 이번 범위 밖.
- 주행·승강 두 축 모두 검사.

## 4. 변경 파일

`PageAutoTeaching.xaml.cs`만:
- envelope 필드(`envTravLo/Hi`, `envLiftLo/Hi`: long / `envReady`: bool) 추가
- `LoadPositionGuardEnvelopeAsync(List<(int row,int bay,int lev)> targets, CancellationToken ct)` 헬퍼 추가 (SL 읽기 → 실패 시 그리드 폴백)
- 런 시작(셀 루프 진입 전, 무장 전)에서 위 헬퍼 1회 호출
- `MoveViaSemiAutoAsync` 폴링 루프에 가드 블록 삽입

기존 API/모델/다른 이동 경로(0x59)·다른 파일은 무수정.

## 5. 알려진 동작(fail-safe 엣지)

- **폴백 ±100은 타이트**하다. SL 읽기 실패 + 크레인이 티칭 그리드 밖(예: 홈)에 주차된 상태에서 첫 이동 시작 시 오탐으로 STOP될 수 있다. 이는 (a) SL 읽기 실패라는 드문 경우에 한하고, (b) "이상이면 세운다"는 안전 방향의 fail-safe다. SL 로드 성공(일반 경로)은 물리 전범위 기준이라 이 이슈 없음.
- 운영자 노트: 폴백 상황에서 START 전 크레인을 티칭 범위 내에 두면 오탐 회피.

## 6. 검증 기준

- **자동(이 세션):** `dotnet build "gcp_Wpf.sln"` 성공(컴파일).
- **로직 리뷰:** STOP(startOnOff=0) + cts.Cancel() 경로가 기존 무장해제/취소와 일치.
- **기능(장비, 사용자):**
  1. 정상 티칭 런에서 가드가 **오탐 없이** 통과(회귀 없음).
  2. SL 로드 로그(`[SAFETY] 소프트리밋 로드 — 주행 …~… mm`) 확인.
  3. (가능 시, 저속·주의) 위치를 인위적으로 범위 밖으로 만들면 `[SEMI][SAFETY]` + Start OFF + 런 중단 확인.
- ⚠️ 실제 폭주 상황 테스트는 장비 안전 확보 후에만.
