# 오토티칭 셀 사이클 파이프라인화 — 설계

> 작성일: 2026-07-08 · 대상: `PageAutoTeaching.xaml.cs` Phase2 티칭 루프
> 근거: 타임라인 요구(오토티칭_타임라인.html) + 4영역 정독·24개 위험 교차검증(전부 실재 확정)
> 사용자 결정: ①추론 실패 시 재시도 없이 실패 기록 ②라이다 스캔은 안정화와 병렬 ③폴백 토글 없이 파이프라인 고정

## 목표 타임라인

```
이동(~6s) → [도착] settle ∥ 라이다 스캔 → 캡처(~0.4s)
→ 캡처 응답 즉시 다음 셀 이동 발행 (추론을 기다리지 않음)
→ 이동 중 X추론 → Z추론 백그라운드 실행 → 다음 셀 도착 시 결과 회수(harvest)
```

셀당 순차 구간을 "settle + 캡처"로 줄이고, X추론+라이다+Z추론(~3.5–6s)을 이동 뒤로 숨긴다.

## 접근법: 단일 스레드 협조적 파이프라인 (깊이 1)

추론을 `Task.Run` 없이 **await하지 않는 async 호출**로 띄운다. Phase2는 WPF 디스패처
컨텍스트에서 돌므로 모든 continuation이 UI 스레드에서 실행 — async 인터리빙만으로
병렬화된다. 따라서:

- `SrmPacket` struct copy-modify-write 경합, `currentResults` List 동시접근,
  `VisionApiClient.Last*` 디버그 필드 오염이 **원천적으로 발생하지 않는다** (멀티스레딩 없음).
- 비전 서버 HTTP 호출은 전부 직렬 유지(아래 순서 보장) — 서버 동시성 미지 영역을 건드리지 않는다.

파이프라인 깊이는 1 (`pendingInfer` 태스크 1개 + `pendingInferIdx`). 라이다 측정값이
서버측 "셀 구분 없는 단일 슬롯(120s 유효)"이므로 더 깊게 만들 수 없다.

## 새 셀 사이클 (셀 i)

```
[루프 top]   ct 체크 → PAUSE 대기 → skipRequested 리셋
             → (이번 셀 이동 미발행이면) IssueMove(i)      ← 첫 셀·PAUSE 보류분
             → movedOk = await WaitArrivalAsync(...)
[도착 직후]  harvest: pendingInfer가 있으면 await 후 currentResults.Add + 로그/표시
             → 이동 실패면: 실패 기록 후 continue (harvest는 이미 끝남 — 순서 보장)
             → 위치검증(±1mm)·Busy 해제 대기 (현행 유지)
             → settleTask = Task.Delay(GetCaptureSettleMs(lev))  ← Busy OFF 후 시작(현행 의미 유지)
               scanTask = RunLidarScanAsync(...)  (lidarFallback이면 생략)
             → await settleTask + scanTask                     ← 동시 진행 ①
[정지 중]    위치 스냅샷(CurTrav/CurLift) → 캡처 요청(await)
[캡처 응답]  출발 게이트: 서킷브레이커 평가 → 트리거면 모달(크레인 정지 상태) →
             '계속'이면 발행 / '중단'이면 break
             pauseRequested면 발행 보류(다음 루프 top에서 발행)
             아니면 IssueMove(i+1)  (캡처 성공/실패 무관 — 다음 행동은 어차피 이동)
             → 캡처 실패면 실패 기록 후 continue
             → pendingInfer = InferCellAsync(...)  ← await 없이 시작, 동시 진행 ②
[루프 후]    finally: pendingInfer 회수(await) 후 기록
             → SUMMARY → Pending=0 commit → 일괄 저장 (모든 경로에서 회수 선행)
```

### 코드 구조 변경

1. **`MoveViaSemiAutoAsync` 분리**: `IssueSemiAutoMove(row,bay,lev)`(안전트립 체크 +
   `gcpTxMode=2` 재강제 + `SendCraneMoveCommand`) / `WaitSemiAutoArrivalAsync(...)`(기존
   폴링 루프: 도착판정·sanity 가드·stall·SKIP·타임아웃 그대로). 기존 호출부 형태는
   두 함수 순차 호출과 동일.
2. **`CaptureAndInferCellAsync` 분해**: 캡처 부분은 루프 인라인(또는 `CaptureCellAsync`),
   X→Z 추론은 `InferCellAsync(cameraId, row, bay, lev, capBayPos, capLevPos, hasCargo,
   scanOk, capPaths, ct)`로 신설. 스냅샷 값·캡처 파일경로를 파라미터로 동결해 전달.
   내부는 현행 X추론→(lidarFallback 분기)→Z추론 로직 그대로, 라이다 스캔 호출만 빠짐
   (`scanOk=false`면 X추론까지만 하고 `FailedStep=z_inference, FailedSubStep=lidar_scan`).
3. **재시도 루프(attempt 0..VisionRetry) 제거**: 전제("같은 자리 재촬영")가 성립 불가.
   `VisionRetry`/`VisionRetryDelayMs` 상수와 재시도 로그는 삭제 대상.

## 정합성 규칙 (위반 시 침묵 오염 — 구현·리뷰 시 필수 확인)

| # | 규칙 | 이유 |
|---|------|------|
| R1 | 스캔(i)은 반드시 harvest(i-1) **완료 후** 시작 | 라이다 서버 슬롯이 단일값 — Z(i-1)이 소비하기 전에 덮으면 셀 i-1 승강이 셀 i 높이로 오염(에러 없이 오답) |
| R2 | bay_pos/level_pos 스냅샷은 settle 완료 후·캡처 직전 1회, X·Z까지 동일 값 사용 | 서버 계약 inferred = bay_pos + move_mm. VERIFY식 "호출 직전 재읽기" 패턴 금지 |
| R3 | 스캔·캡처는 크레인 정지 중(출발 게이트 이전)에만 | 물리 측정/촬영 |
| R4 | currentResults Add 순서 = 셀 순서. harvest가 유일한 지연 기록 지점 | ZR CSV가 "결과 순서=셀 순서"로 PLC 주소 배정 |
| R5 | 루프 탈출 모든 경로(정상/break/취소/예외)에서 pendingInfer 회수 후 commit·저장 | 미회수 시 마지막 셀 결과 누락·조기 commit·유령 태스크 |
| R6 | 발행 지점마다 `gcpTxMode=2` 재강제 + `semiDest*` 기입 유지 | MainWindow가 물리 키값으로 되돌림 → 0x41이 게이트에서 조용히 막혀 STALL 오탐 |

## 운영자 제어 (의미 보존)

- **서킷브레이커**: 평가를 루프 top → 출발 게이트로 이동. 모달 시점에 크레인 정지 유지.
  추론 확정이 1셀 뒤라 발화가 최대 1셀 지연될 수 있음(사용자 수용).
- **PAUSE**: 출발 게이트에서 발행 보류 → 루프 top의 기존 대기 후 발행. "셀 경계 정지" 유지.
- **SKIP**: 현행대로 이동 대기 루프에서만 소비(이동만 중단). 추론은 계속.
- **STOP/안전가드**: 변경 없음. in-flight 추론은 ct 공유로 즉시 취소되고 finally에서 회수.

## 실패 정책 (재시도 전면 제거)

| 실패 | 처리 |
|---|---|
| 이동 실패 | (harvest 후) 실패 기록, continue — 현행 동일 |
| 라이다 스캔 실패 | 캡처·X추론은 진행, `z_inference/lidar_scan` 실패 기록 (현행 결과 형태 유지) |
| 캡처 실패 | 실패 기록, 다음 이동은 이미 발행됨 — 현행(재시도 없음) 동일 |
| X/Z 추론 실패 | harvest 시점에 실패 기록. 재시도 없음 (사용자 결정) |

## 표시 (기능 무관)

- `cellSw`: 셀 시작~해당 셀 결과 harvest까지. `cellDurMs`/ETA 계산식 유지.
- `SetStep`/`SetStatus`: 크레인 쪽 단계 우선. 추론은 harvest 로그로 확인.
- `[OK]`/`[FAIL]` 셀 결과 로그가 한 셀 늦게 출력됨(셀명 포함이라 추적 가능 — 사용자 수용).

## 비변경 (스콥 밖)

CALIB/VERIFY/수동 버튼 경로, `MoveViaMaintAsync`(0x59), Phase1/1.5/1.7, 저장 포맷,
UDP 프로토콜(0x41 실송신 ~300–600ms 지연은 수용), 비전 서버.

## 검증

1. 빌드 통과 (`dotnet build`).
2. 로그 시퀀스 검증: "Z추론(i-1) OK → 라이다 스캔(i)" 순서가 전 셀에서 성립,
   `[SUMMARY] 총소요` 전후 비교(셀당 ~3초 이상 단축 기대).
3. STOP·PAUSE·서킷브레이커 경로: 취소 시 크레인 정지·부분 저장·rollback(Pending) 동작 확인.
