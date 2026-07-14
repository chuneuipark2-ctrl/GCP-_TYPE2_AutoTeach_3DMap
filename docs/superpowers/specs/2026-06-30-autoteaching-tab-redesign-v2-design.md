# 오토티칭 탭 재설계 v2 — 설계 문서

- **작성일**: 2026-06-30
- **대상**: 지상반 WPF 앱 `gcp_Wpf` — `MainWindow`(권한) + `PageAutoTeaching`(탭 내용)
- **상태**: 설계 승인됨 → 스펙 검토 후 writing-plans
- **선행**: [오토티칭 UI 개편 v1](2026-06-29-autoteaching-ui-redesign-design.md)(A·B·C·D 구현 완료)의 후속. 이 문서는 v1의 Module B(탭 노출) 방식을 교체하고, 오토티칭 탭 내용을 목업대로 재설계한다.

---

## 1. 목표

1. **탭 권한 흐름 교체**: 평소 3탭(자동/수동/반자동). **로고 클릭 → 관리자 로그인** 시 4탭(+오토티칭). 관리자 해제(로고 재클릭/타임아웃) 시 다시 3탭.
2. **오토티칭 탭을 목업대로 정리**: 상태 기반 흐름 — **진행 화면**(런 중) ↔ **결과 확인·반영 화면**. MM POSITION MOVE는 정비용 **보조 서브탭**으로 격하.

## 2. 제약 (불변)

- **수정 범위는 `gcp_Wpf`뿐.** Vexi UDP·STM32 MCU·비전 프로그램 무수정.
- **런 엔진·안전 로직 100% 보존.** Phase1/1.5/2/3, 0x95 반영, 백업/복구(RP1~3), 서킷브레이커 등은 **로직 무변경** — 출력(UI 갱신 대상)만 새 패널로 재배선. 정상 동작 보존.
- 비전 IP/포트는 v1 Module A에서 이미 크레인 설정으로 이전됨(현행 유지).

## 3. 현재 구조 (재설계 대상)

- `Logo_Click`(MainWindow.xaml.cs:2202): 이미 관리자 모드 토글 — 일반 모드면 `WindowLogin` → 성공 시 `isAdminMode=true` + `adminModeStartTime`/`adminModeTimer`; 관리자 모드면 즉시 해제(`isAdminMode=false`, `Brd_AdminMode` 숨김, 각 설정 페이지 `SetPageMode(false)`). 타임아웃 자동 해제 경로도 존재.
- `Btn_AutoTeaching`(MainWindow, v1 Module B): 최상위 ToggleButton, `Visibility=Collapsed`. `RevealAutoTeachingTab()` 존재(노출만). **숨김 카운터파트 없음.**
- 진입 중복: `PageCraneSet.Btn_AutoTeaching_Click`(관리자 로그인 → `RevealAutoTeachingTab` + `Page_Change(PAGE_AUTOTEACHING)`).
- `PageAutoTeaching.xaml`: TabControl 3탭 — AUTO TEACHING(설정+로그 listBox_Log+위치라벨 lbl_curTrav/curLift/curFork/lbl_progress/lbl_result+progressBar) / MM POSITION MOVE / RESULT REVIEW(v1 Module C: grid_Review + 원본/보정 Image + 반영 버튼). 하단 공통 버튼바(START/STOP/SKIP/CALIB/SAVE/EXCEL/VERIFY/CLOSE).
- 런 엔진 UI 갱신 지점(재배선 대상): `SetStatus`, `AddLog`(listBox_Log), `UpdateProgress`, `UpdateResultCounts`(lbl_result), `lbl_curTrav/curLift` Dispatcher 갱신, `progressBar`.

## 4. 확정 결정

| 항목 | 결정 |
|---|---|
| 재설계 충실도 | **목업 그대로**(카드형 진행화면 + 정돈된 결과화면) |
| 화면 흐름 | **상태 기반**: 진행 ↔ 결과(런 중→진행, '결과 확인·반영하기'→결과) |
| MM POSITION MOVE | **보조 서브탭 유지**(평소 비노출/접근 가능) |
| 상세 이미지 탭 | **원본+보정 2개만**(검출 오버레이는 보류) |
| 일시정지(PAUSE) | **신규** — 셀 경계 협조적 일시정지 |
| 크레인설정 오토티칭 버튼 | **제거**(로고 로그인→탭이 유일 진입점) |

## 5. 아키텍처 — 4개 모듈

빌드 순서: **M1 → M2 → M3 → M4**.

### Module M1 — 탭 권한(로고 로그인 노출/숨김)
- `Logo_Click` 성공 분기(`isAdminMode=true`): `RevealAutoTeachingTab()` 호출.
- `Logo_Click` 해제 분기(`isAdminMode=false`) **그리고** `adminModeTimer` 타임아웃 해제 지점: 신규 `HideAutoTeachingTab()` 호출 — `Btn_AutoTeaching.Visibility=Collapsed`; 현재 페이지가 PAGE_AUTOTEACHING이면 안전 페이지(예: 자동/반자동)로 `Page_Change`(런 중이면 기존 `AbortAndDisarmForShutdown` 경유 — Page_Change가 이미 처리).
- **크레인설정 진입 버튼 제거**: `PageCraneSet.xaml`의 `btn_AutoTeaching` + `PageCraneSet.xaml.cs`의 `Btn_AutoTeaching_Click` 제거. (다른 참조 없음 확인 후.)
- 동작 보존: 관리자 토글·타임아웃·`Brd_AdminMode` 등 기존 흐름 무변경, 탭 노출/숨김만 부착.

### Module M2 — PageAutoTeaching 상태머신 셸
- TabControl 구조를 **상태 패널 + 보조 탭** 으로 재편:
  - 메인 영역: `pnl_Setup`(설정+START/CALIB), `pnl_Run`(진행 화면, M3), `pnl_Review`(결과 화면, M4) — 같은 자리에 겹쳐두고 Visibility로 전환.
  - `pnl_MmMove`(기존 MM POSITION MOVE 내용 이동) — 보조 서브탭/버튼으로 접근, 메인 흐름과 분리.
- `enum TeachView { Setup, Run, Review, MmMove }` + `SetView(TeachView)`(Visibility 토글). 전환: START→Run; **'결과 확인·반영하기' 버튼→Review**(런 중·완료 후 모두 가능, **운영자 주도 — 자동 강제전환 안 함**); Review 닫기/새 런→Setup; MM 진입/복귀 버튼.
- **엔진 배선 보존**: 기존 START/STOP/SKIP/CALIB/VERIFY 핸들러·Phase 메서드 로직 유지, UI 갱신 대상만 새 컨트롤로 교체. **진단 로그(listBox_Log + 파일 로그) 유지**(진행 화면 보조 영역 또는 토글로 접근 — 잃지 않음).
- 하단 버튼바를 상태별로: Setup=START/CALIB, Run=일시정지/중지/결과확인, Review=정상일괄선택/선택반영(+닫기). SAVE(엑셀)/VERIFY는 Review 액션으로 이동.

### Module M3 — 진행 화면 (목업 2) + PAUSE
- 레이아웃: 헤더(오토티칭 + 진행중 뱃지 + SRM#/비전 연결 상태) / 좌: 현재 셀(N열 M베이 L단) + **카메라 스냅샷**(셀마다 갱신, 캡처 후 `RawPath`/`CalibratedPath` 로드, 기존 `LoadImageNoLock` 재사용) + **스텝 표시**(이동·도착·촬영·분석) / 우: 전체 진행률 N/M + progressBar + **ETA**(완료 셀 평균시간×잔여) + 정상/실패 metric + **최근 처리 리스트**(최근 ~5셀 status·편차) / 하단: 일시정지·중지·결과확인.
- **파생 데이터(새 소스 없음)**: 스텝/스냅샷/ETA/최근목록은 전부 기존 Phase2 루프 진행 이벤트에서 갱신. 스텝은 루프의 이동(0x41/0x59)→도착→캡처→추론 시점에 `currentStep` 갱신. ETA는 셀 완료마다 누적 평균.
- **PAUSE(신규, 안전)**: `volatile bool pauseRequested`. Phase2 셀 루프의 **각 셀 시작 직전**(크레인 이동 전)에 `while (pauseRequested && !ct.IsCancellationRequested) await Task.Delay(200, ct);`. 일시정지→`pauseRequested=true`(다음 셀 경계에서 정지, 현재 셀의 이동·캡처는 완료), 재개→`false`. **이동/캡처 도중엔 절대 안 끊음.** 중지(STOP)는 기존 취소(cts) 그대로.

### Module M4 — 결과 확인·반영 화면 (목업 1) 정돈
v1 Module C(grid_Review + 원본/보정 이미지 + 반영 로직) 위에 정돈:
- 헤더: 제목 + 컨텍스트(측정·N열) + **편차 큰 순**(이미 기본 정렬) + **확인 필요** 뱃지(편차 임계 초과 셀 존재 시).
- 좌: 표(셀/기존값/측정값/편차/반영☑) — 기존 grid_Review.
- 우 상세: 원본/보정 이미지 탭(기존) + **승강 위치 before→after** + **주행 위치 before→after**(기존 lbl_ReviewDetail를 두 줄 카드로 정돈) + **이 셀 반려**(기존 Btn_RejectCell) + **이 셀 반영**(신규 단일 셀 반영 — `ApplySelectedCellsAsync`를 단일 셀로 호출하는 래퍼).
- 하단: "N개 셀 선택됨 · 반영 전 기존값 자동 백업" + 정상 셀 일괄 선택 + 선택 셀 반영(기존).
- **반영 로직 무변경**: `ApplySelectedCellsAsync`(백업→갱신→0x95→마커, 재진입가드/널가드/0mm가드 포함) 그대로. '이 셀 반영'은 해당 셀만 선택 상태로 만들어 같은 경로 호출.

## 6. 데이터 흐름 / 재사용
- 런: SETUP(설정) → START → Phase1/1.5/2(셀 루프, M3 진행화면 갱신)/3 → 완료 시 `SaveToExcel`+`SaveTeachingState`(기존), '결과 확인·반영하기' 버튼 활성 → 운영자가 눌러 REVIEW 진입.
- 반영: REVIEW에서 선택 → `ApplySelectedCellsAsync`(기존) → 0x95 ACK 시 `Rack.ini` 마커 + Pending=0.
- 반자동 스트립(v1 Module D)은 그대로 `TeachingState.ini`/`Rack.ini`를 읽음 — 본 재설계는 그 저장 포맷을 바꾸지 않음.

## 7. 테스트 전략
- 모듈별 빌드 검증(`dotnet build`, `: error` 0; `2>&1` 미사용).
- 동작 보존(수동): 로그인 시 4탭/해제 시 3탭·현재 오토티칭 페이지면 안전 이탈; 런 시작→진행화면 갱신(스텝/진행률/스냅샷/최근목록); 일시정지가 **셀 경계에서만** 멈추고 재개; 결과화면 표·이미지·승강/주행 표시·반려/반영; MM 보조탭 동작; 기존 자동·수동·반자동 무변경.
- **실기 게이트(크레인)**: 0x95 반영 ACK·마커·롤백; 카메라 스냅샷 실경로 렌더; 일시정지/재개 실런.

## 8. 범위 밖 (Phase 2)
- 상세 **검출(오버레이) 이미지 탭** — 비전이 오버레이를 파일로 저장하도록 추가해야 함(비전 수정). 지금은 원본+보정만.
- 라이브 비디오 스트리밍(현행 Mode1 정지 스냅샷 유지).
- 결과 자동 반영(운영자 게이트 유지).

## 9. 미해결 — 구현 단계 확인
1. `adminModeTimer` 타임아웃 자동 해제 지점의 정확한 위치 — `HideAutoTeachingTab()` 부착(M1).
2. MM POSITION MOVE 내용의 컨트롤 이름 충돌 없이 보조 탭으로 이동(M2).
3. 진행 화면 스텝 갱신을 위해 Phase2 루프에 삽입할 최소 상태표시 지점(M3, 로직 무변경 — 표시만).
