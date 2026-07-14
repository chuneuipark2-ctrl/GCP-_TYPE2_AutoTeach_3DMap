# 오토티칭 UI 개편 — 설계 문서 (기안)

- **작성일**: 2026-06-29
- **대상**: 지상반 WPF 앱 `gcp_Wpf` (오토티칭 + 반자동 + 크레인 설정)
- **상태**: 설계 승인 대기 → 승인 후 구현 계획(writing-plans) 작성

---

## 1. 목표

오토티칭 기능을 운영자 친화적 UI로 개편한다.

1. 오토티칭을 **자동/수동/반자동과 동급인 최상위 탭**으로 노출(관리자 로그인 시).
2. **반자동 그리드 위에 카메라+적용여부 스트립**을 추가해, 운영자가 목적 셀의 비전 영상·편차·적용여부를 그 자리에서 확인.
3. **비전 IP/포트를 크레인 설정으로 이전**(기본값 하드코딩).
4. 오토티칭 결과를 **확인·반영(결과 확인·반영 화면)** 으로 운영자가 검토 후 선택 적용.

## 2. 제약 (불변)

- **수정 범위는 `gcp_Wpf` 지상반 앱뿐.** Vexi UDP 프로토콜·STM32 MCU 펌웨어는 건드리지 않는다.
- **이번 v1은 비전 프로그램도 수정하지 않는다.** (아래 §3-1로 가능해짐)
- **정상(성공) 동작 100% 보존.** 모든 변경은 *추가(additive)* 이거나 *기본값으로 현행과 동일* 해야 한다.

## 3. 탐색으로 확정된 사실 (설계의 근거)

### 3-1. 비전 서버는 localhost — 이미지 API 불필요 ✅
- 모든 로그가 `Vision API BaseUrl=http://127.0.0.1:3080`. 비전 서버는 **지상반과 같은 PC**에서 동작.
- `CaptureResponse`(VisionApiClient.cs:568)는 `raw_path`(항상) + `calibrated_path`(렌즈 보정 시)를 반환. 캡처 파일명 예: `camera3_01_001_01_02839_01000_empty.jpg` (카메라·열·베이·단·주행pos·승강pos·적재여부 인코딩).
- **현재 코드는 이 경로를 버리고 `Filename`만 로그**(PageAutoTeaching.xaml.cs:902, :437)에 찍는다.
- → 결론: 캡처 이미지는 로컬 디스크에 있으니 **WPF `Image`에 파일 경로를 직접 바인딩**하면 된다. **신규 비전 엔드포인트 0개.**
- **구현 선행 확인 1건**: `raw_path`가 절대경로인지 상대경로인지 런타임에 1회 확인. 상대경로면 "캡처 베이스 디렉토리"를 크레인 설정에 1개 추가(§Module A에 합류).

### 3-2. "적용여부"가 가리킬 상태 저장소가 없음 → 신규 필요
- 측정값은 SRM에 자동 기록되지 않음. `currentResults`(메모리)→엑셀로만 남고, 0x95 반영은 수동(TAB2 / [CAL 복구]).
- 셀별 "적용됨/미적용" 플래그 자체가 없다 → **신규 상태 저장**(§Module C).
- 단, *"반영 전 기존값 자동 백업"* 은 기존 `BackupCellArrays`/`Pending` 메커니즘(PageAutoTeaching.xaml.cs:2268~)을 그대로 재사용.

### 3-3. 반자동 페이지에 여유 공간 없음 + XAML 의심
- 루트 Grid 3행(20\*/50\*/10\*)이 꽉 참. 카메라 스트립을 넣으려면 **행 재배분** 필요.
- 루트는 0~2행인데 `Grid.Row="3"` 참조, 1행 그리드 안에서 `Grid.Row=9/10` 참조 등 **의심 XAML** 존재. WPF는 행을 자동 생성하지 않고 마지막 행으로 클램프 → 현재 겹쳐 렌더 중일 수 있음.
- **구현 선행 확인 2건**: 행 재배분 전, 현행 반자동 화면이 실제로 어떻게 렌더되는지 검증(현행 동작 보존 위해).

### 3-4. 위치 모델은 베이별·단별 분리
- `cellBay[256]`(베이 인덱스별 주행 위치), `cellLev[128]`(단 인덱스별 승강 위치), 1-based.
- 셀 1-7-4의 위치 = `cellBay[7]` + `cellLev[4]`. 즉 **하나의 셀을 반영하면 같은 베이/같은 단을 쓰는 다른 셀에도 영향**.
- → 적용상태는 **베이 인덱스·단 인덱스 단위**로 추적하고, 셀별 뱃지는 그 셀의 (bay,lev) 인덱스로부터 유도(§Module C).

## 4. 확정 결정 사항

| 항목 | 결정 |
|---|---|
| 이미지 | 로컬 폴더에서 직접 로드(`raw_path`/`calibrated_path` → `Image` 바인딩). 신규 API 없음 |
| 비교 모드 | **Mode 1만** — 정지영상. v1은 `원본`(raw) + `보정`(calibrated) 2장 |
| 탭 노출 | 관리자 로그인 성공 시 최상위 오토티칭 탭 노출 |
| 탭 위치 | **자동/수동/반자동과 동급 최상위 4번째 탭** |
| 비전 IP/포트 | 크레인 설정 이전 + 기본값 `127.0.0.1:3080` 하드코딩 |
| 적용상태 저장 | `Rack.ini`에 베이/단 인덱스별 `applied` + 타임스탬프, **0x95 ACK 시 기록** |
| 반영 전 백업 | 기존 `BackupCellArrays`/`Pending` 재사용 |

## 5. 아키텍처 — 4개 모듈

권장 빌드 순서: **A → B → C → D** (C가 본체, D는 C의 산출물을 재사용).

### Module A — 비전 설정 이전 (소규모·독립)
- **현행**: PageAutoTeaching.xaml의 `edit_VisionIP`(기본 `127.0.0.1`) / `edit_VisionPort`(기본 `3080`) 입력 필드 → `Phase1_InitAsync`/CALIB에서 `visionApi.SetBaseUrl(ip, port)`.
- **변경**:
  - 크레인 설정(PageCraneSet)에 비전 IP/포트(+ 필요 시 캡처 베이스 디렉토리) 필드 추가, ini 저장.
  - 오토티칭 코드의 `SetBaseUrl` 입력 소스를 설정값으로 교체. **설정이 비어 있으면 하드코딩 기본값 `127.0.0.1:3080` 사용 → 현행과 동일.**
  - 오토티칭 탭의 IP/포트 입력 필드 제거(또는 읽기전용 표시).
- **동작 보존**: 기본값이 현행과 동일하므로 설정 미변경 시 행동 불변.

### Module B — 오토티칭 탭화 (네비게이션)
- **현행**: `MainWindow` 하단 ToggleButton(자동/수동/반자동/숨김 메인) → `Mode_Click` → `Page_Change(index)`. 오토티칭(`PAGE_AUTOTEACHING=14`)은 PageCraneSet의 `btn_AutoTeaching` → 관리자 로그인 → `Page_Change(14)`로만 진입.
- **변경**:
  - `MainWindow`에 최상위 ToggleButton `Btn_AutoTeaching` 추가(예약된 숨김 컬럼 슬롯 활용), 기존 ToggleButton 스타일(터쿼이즈 `#FF22B9AF`, CornerRadius 15) 그대로.
  - 기본 `Visibility=Collapsed`. **기존 PageCraneSet의 관리자 로그인 성공을 "잠금 해제"로 재사용** → 성공 시 탭을 `Visible`로 노출(+선택적으로 즉시 이동).
  - `Mode_Click`에 `Btn_AutoTeaching → Page_Change(PAGE_AUTOTEACHING)` 라우팅 추가.
- **동작 보존**: 기존 PageCraneSet 진입 경로는 유지(중복 허용). 로그인 전에는 탭이 보이지 않음(현행 보안 강도 유지).

### Module C — 결과 확인·반영 화면 (핵심·최대)
오토티칭 실행 후, 운영자가 셀별 결과를 검토하고 선택 반영하는 화면. (목업: "결과 확인·반영")

- **데이터 소스**: 기존 `currentResults: List<TeachingResult>`. `TeachingResult`에 **이미지 경로 필드 추가**(`RawPath`, `CalibratedPath`) — 현재 버리는 `CaptureResponse.RawPath/CalibratedPath`를 `CaptureAndInferCellAsync`에서 채운다.
- **표 영역**: 셀(`R{row}-B{bay:D3}-L{lev:D2}`), 기존값(`cellBay[bay]`/`cellLev[lev]`), 측정값(`BayPos`/`LevelPos`), 편차(측정−기존), 반영 체크박스. 편차 큰 순 정렬.
- **상세 영역**: 선택 셀의 이미지 — v1은 `원본`(raw_path) / `보정`(calibrated_path) 2탭. 승강/주행 위치 before→after.
- **반영 동작**:
  1. [이 셀 반영] / [선택 셀 반영] / [정상 셀 일괄 선택].
  2. 반영 직전 **기존 `BackupCellArrays`로 자동 백업**(Pending=1).
  3. 선택 셀의 `cellBay[bay]`/`cellLev[lev]`를 측정값으로 갱신 → **기존 0x95 `WriteCellRangeAsync`** 호출.
  4. 0x95 ACK 성공 시 `Rack.ini`에 해당 베이/단 인덱스의 `applied=1` + 타임스탬프 기록(§3-2). ACK 실패 시 반영 안 함(상태도 미기록).
  5. 성공 시 백업 커밋(Pending=0).
- **영속화 — 두 저장소로 분리**(적용상태는 인덱스 단위, 이미지는 셀 단위라 분리한다):
  - (a) **적용 플래그(권위)**: `Rack.ini`에 베이 인덱스·단 인덱스별 `applied` + 타임스탬프 — **0x95 ACK 성공 시에만** 기록. 기존 `RACK_BAY{i}`/`RACK_LEV{i}` 기록 경로(udpClientClass.cs:212/220)에 얹는다. "SRM에 실제로 써졌는가"의 단일 진실. 자동 롤백(Pending) 시 `applied`가 남지 않도록 백업 복구 경로와 일관 처리.
  - (b) **셀별 티칭 레코드**: `SRM{N}/Teaching/TeachingState.ini`(신규)에 셀 키(`R{row}-B{bay:D3}-L{lev:D2}`) → {측정 bay/lev pos, `raw_path`, `calibrated_path`, run 타임스탬프}. 티칭 런 완료 시 기록. 이미지·측정값의 출처로, C(검토)와 D(스트립)가 공유.
  - **적용여부 판정** = 셀의 (bay,lev) 인덱스가 둘 다 `Rack.ini`에서 `applied=1` **그리고** (b)의 측정값이 현재 `cellBay/cellLev`와 일치.
- **동작 보존**: 기존 자동 엑셀 저장(SaveToExcel), TAB2/[CAL 복구] 경로는 그대로. 본 화면은 *추가 게이트*이며 기존 수동 반영을 막지 않는다.

### Module D — 반자동 카메라+적용여부 스트립 (C 의존)
- **위치**: PageSemiAuto 루트 Grid 최상단에 신규 행 추가(행 재배분, §3-3 검증 후).
- **내용**: 운영자가 포크 필드에 입력한 **목적 셀의** 비교영상 2장(원본/보정) + 편차 텍스트 + 적용여부 뱃지(적용됨✓ / 미적용).
- **연동**: 입력된 목적(To) 셀의 Bay/Level(다중 포크 시 활성 포크) → §Module C (b) `TeachingState.ini`에서 이미지 경로·측정값, `Rack.ini`에서 적용여부 조회. 티칭 이력이 없는 셀이면 "데이터 없음" 표시.
- **동작 보존**: 기존 반자동 작업선택·포크 입력·전송/초기화 동작 불변. 스트립은 순수 표시(읽기).

## 6. 공통 구현 노트
- **이미지 로드**: `BitmapImage`를 `BitmapCacheOption.OnLoad` + `CreateOptions.IgnoreImageCache`로 로드 → 파일 잠금 방지(비전 서버가 덮어써도 무방). 경로 없음/파일 없음 시 플레이스홀더.
- **스레드**: 이미지 로드/상태표시는 UI 스레드. Phase2 백그라운드 컨티뉴에이션에서 갱신 시 `Dispatcher.Invoke`(기존 패턴).
- **localization**: 기존 `{lx:Loc ...}` 패턴 따름.

## 7. 테스트 전략
- **빌드 검증**: 각 모듈 후 `dotnet build gcp_Wpf.csproj` 오류 0 확인(2>&1 미사용 — PowerShell 255 오탐 주의).
- **동작 보존 검증(모듈별)**:
  - A: 설정 미변경 시 `BaseUrl`이 `127.0.0.1:3080`로 동일한지 로그 확인.
  - B: 로그인 전 탭 비노출 / 로그인 후 노출, 기존 PageCraneSet 진입 경로도 동작.
  - C: 반영 전 백업 생성 → 0x95 ACK → `applied` 기록 → 백업 커밋의 순서. ACK 실패 시 미반영·미기록·롤백. 기존 엑셀 저장 영향 없음.
  - D: 반자동 기존 포크 동작 불변, 스트립은 표시만.
- **실기 검증**: 크레인 실동작(0x95 반영, 이미지 경로 실값)은 하드웨어 필요 → 빌드 검증 후 실기 라운드에서 확인.

## 8. 범위 밖 (YAGNI / Phase 2)
- **Mode 2 라이브 스트리밍**(현재 위치 RTSP 스트림): WPF RTSP 디코더 신규 의존성 필요 → 보류.
- **`검출` 오버레이 이미지 / `정위치` 골든 레퍼런스 이미지**: 현재 비전 API가 디스크에 저장한다고 확인되지 않음. v1은 `원본`+`보정`만. (디스크에 이미 존재하면 그때 배선; 신규 생성은 비전 수정이라 v1 제외.)
- 결과 자동 반영(운영자 게이트 없는 자동 0x95): 안전상 도입 안 함.

## 9. 미해결 — 구현 단계에서 확인할 선행 항목
1. `raw_path` 절대/상대 경로 형식(§3-1) — 상대면 캡처 베이스 디렉토리 설정 추가.
2. 반자동 의심 XAML 현행 렌더 검증(§3-3) — 재배분 전 현상태 확정.
3. 저장 스키마 확정 — `Rack.ini` 적용 플래그(인덱스 키 + applied + ts)와 `TeachingState.ini` 셀별 레코드(측정값 + 이미지경로 + ts) — Module C 착수 시.
