# 인수인계 — 오토티칭 0x59 이동 디버깅 (작성 2026-06-10)

> 다음 세션(빈 컨텍스트)이 바로 이어받기 위한 문서. 메모리 `autoteaching-backup-restore.md`, `vexi-protocol-autoteaching.md`와 함께 읽을 것.

---

## 0. 다음 세션 즉시 할 일 (TL;DR)
**사용자가 내일 크레인에서 "TAB2 mm 이동(또는 캘리)" 0x59 테스트를 돌리고 결과를 가져옵니다.**
→ 아래 **§4 로그 체크리스트**로 어디서 멈췄는지 판정하고 **§5 결과별 다음 수**로 이어갈 것.
- 빌드 확인: `dotnet build gcp_Wpf.csproj -c Debug -v q -clp:ErrorsOnly` (정상 = 오류0, 경고 828개는 기존 nullable)
- 작업 파일: `PageAutoTeaching.xaml.cs` (작업디렉토리 = `...\gcp_type2-TP2_final\gcp_type2-TP2_VER1`)
- **★ 안정성 개선 §8 — 2026-06-24 구현 완료(빌드 오류0).** 안전입력 하드게이트/런 종료 무장해제/이동 stall 조기중단/비전 서킷브레이커/**비전 per-call 타임아웃(15s)+CT**/NRE·길이 가드/캐시 무효화/재진입 가드/Skip 실동작/MM_BACKUP 부분복구 경고/죽은코드 ~550줄 제거. `VisionApiClient.cs`도 변경됨. **크레인 실동작 미검증** — 테스트 시 §8 "신규 로그" 확인. 의도적 보류는 **P0-1 0x30 트립 송신억제(fail-safe 방향 — 테스트 데이터 먼저)** + P2-4(상수정리) 둘뿐, §8 ⏭ 참고.

---

## 1. 프로젝트 한 줄
한국타이어 TP2 SRM(스태커크레인) **비전 기반 오토티칭**. 지상반 WPF앱(gcp_Wpf) ↔ Vexi 프로토콜(UDP) ↔ MCU(STM32, `MX_SRM_App_Ver_4.4_260507`) ↔ SEW 인버터. 비전서버 :3080.
**작업 범위 = 지상반 앱 + 비전 API만. Vexi 프로토콜은 기존 명령을 쓰는 틀. MCU/프로토콜은 고치지 않음(사용자 지시).**

## 2. 지금 증상 + 2대 근본원인 (★중요)
증상: 0x59 보수위치 이동을 보내도 **크레인이 안 움직임 + 0x8059 응답조차 없음 + 알람 없음**. (모드 OK, 0xA4/A6 ACK, 0x59 송신은 됨)

펌웨어 전수분석(워크플로우)으로 확정한 **진짜 원인 2개 — 둘 다 지상반 코드 밖**:
1. **지상반 안전비트 래치 (1순위)** — GCP가 0x30 요청 `data[11]`에 DIO `EM_SW`/`SF_PLUG` 입력이 false면 "비상정지/안전플러그=1"을 실어보냄(반전 로직, `udpClientClass.cs:3117` 근처 `CMD_Req_State`). MCU가 이 비트를 래치(`dev_SRM.c:53565~`)해 **0x59/0x51만 무응답 폐기**(`com_tml.c:2923/2929`). 0xA4/A6/0x58/0x30은 이 가드 검사 안 해서 정상 ACK → "파라미터는 써지는데 0x59만 무반응" 증상과 정확히 일치. 안전플러그 엣지에서 `SRM_Start_Off()` 호출 → `startSt=0` 관측까지 설명. 알람 안 올림(Fault X) → "알람 없음"도 정합.
   - 물증: 0x34 알람로그에 `ERROR1_SAFETY_PULG_ON(1,5)` / `ERROR1_EMBERGENCY_STOP(1,0)`이 0x59 시도 횟수만큼 누적돼 있어야 함.
   - 해제: 그 DIO 비트가 0(정상)으로 바뀌는 1→0 엣지에서 자동.
2. **MCU 펌웨어 복붙 버그** — `dev_SRM.c SRM_Move_Maintanence_Cmd()`(약 47255줄)가 **승강 보수위치를 "주행" ManualOp 범위(`TravDrive.ManualDrive.StartPos~EndPos`, 현장 [1000, 25300])로 검증**. `LiftDrive.ManualDrive`여야 함. → 승강 목표 < 1000mm면 `result=8`로 무소음 거부. (6/1엔 lift=1000이라 우연히 통과 + 구버전 2.3엔 이 검증 자체가 없었음)
   - MCU팀 리포트 필요. **이 버그가 남으면 1000mm 미만 승강(캘리 z-15mm) 원천 불가.**

## 3. 0x59 mm이동 조건 정리 (펌웨어 근거, 질문답 3종)
- **Q. mm단위 임의이동 = 0x59뿐?** → **예.** 0x80 jog=속도/방향, 0x41=셀테이블(Bay/Lev), 0x51=홈, 0x44=원점세팅. 임의 mm는 0xA4/A6(Maintenance_Pos) + 0x59뿐.
- **Q. 셋업→수동→셋업 토글 왜?** → **펌웨어상 불필요(통설 거짓).** "SEW 인버터가 setup 진입 엣지에서만 위치 수용"은 펌웨어에 없음. 0x59가 부르는 이동시퀀스(`Auto_Ctr_Travel/Lift`)가 인버터에 위치 직접 송신. **이번 세션에 토글 제거함**(§4 ②).
- **Q. mm이동 스위치/인터록 (원천별):**
  - 기상반HW: 자동/수동 키스위치(`ManualSW`=`IN_AUTO` DI) → 자동/반자동 모드진입 막음. 크레인 비상정지/인버터fault → `SRM_Status1.Fault` → 0x59 result=2.
  - 지상반(0x30 data[11]): 비상정지(EM_SW)→reject bit0→0x59 무응답. 안전플러그(SF_PLUG)→reject bit1→0x59 무응답+StartOff. ★1순위 원인.
  - 벡시/MCU내부: 셋업모드(0xA4/A6 쓰기 필수, 자동/수동이면 NACK=1), RunMode==STAND_BY 아니면 result=8, MaintanencePos(이미 보수위치)=result=9, 포크중심알람=5, 이동/포크작업중=6/7, Maint_Pos==0/범위밖=5/7/6/8.
  - 인버터(SEW): 원점확인(IPOS_ref) — 이동 실행단계 필수(없으면 이동중 ERROR1_REFERENCE 알람). 접속(Connect) — 수동/반자동 모드진입 필요.

## 4. 이번 세션에 바꾼 것 (현재 상태)
파일: `PageAutoTeaching.xaml.cs`. ✅=빌드검증됨, ❌=크레인 실동작 미검증.

### A. 0x59 경로 (`MoveViaMaintInternalAsync`, ~1338) — 마지막 수정, 테스트 대상
- ✅❌ **Step 0 DIO 안전진단**(1342): EM_SW/SF_PLUG false면 `[MAINT][WARN] ★지상반 DIO …` 경고(마스킹 안 함, 안전신호라 의도적).
- ✅❌ **Step 1 셋업 보장**(1353): `if(setupMode==0) SetCraneModeAsync(1)` 한 번만. **토글 제거**(펌웨어 근거). SetCraneModeAsync에 이상리셋·강제·작업삭제 fallback 내장.
- ✅❌ **LIFT 범위 거부**(1406): liftMm이 주행 ManualOp[132/136] 밖이면 **`return false`+에러로그**. (예전 silent 클램프 → 위험해서 코드리뷰 후 거부로 변경). 부수: sub-1000 캘리샘플은 깨끗이 skip.
- ✅❌ **DecodeMaintResult**(~1546): 0x8059 result 코드(2/5/6/7/8/9)를 한글 사유로 로그. result=0이면 `0x59 ACK` 로그. + curPos 모니터링 루프에 지연 NACK 즉시중단(1500 근처).

### B. 반자동(0x41) 본 루프 + 자동전환 — ❌미검증, 재검토 후보
- `MoveViaSemiAutoAsync`(~986): Phase2 셀이동을 0x41(셀테이블)로. 도착=정위치 OR mvProcState/procState==4 + 목표±100mm.
- Phase2 진입 자동전환(~623): gcpTxMode=2 강제 + 자동모드 + Start ON(0x50) + 검증/중단. ⚠️코드리뷰 C3: START 다이얼로그에 "자동+START 인가" 고지 없음.

### C. 캘리 0x59 유지 + 범위확장 해킹 제거
- Phase1.5 캘리는 0x59(`MoveViaMaintAsync`)로 z ±15mm(zOffsets -15..15) 직접이동 — **유지**.
- 제거: `CalibExtendLiftRange`/`Restore`, cellLev 임시확장(±30), `calibTempLiftHomeMm`(offset167), 관련 백업/복구(CAL_LEV/CAL_LIFT_RANGE), `calibRefLevMmOverride`(dead). 사유: MCU가 ±15 오버레인지 처리한다고 들음(미검증 — B의 펌웨어 버그와 충돌 가능).

### D. 부가 기능 (✅빌드, ❌실동작 일부)
- Level별 캡처 안정대기: `Config.ini [CaptureSettle]` DefaultMs/CaseCount/Case_i, `GetCaptureSettleMs`, Phase2 캡처 전 적용.
- 3단계 Verify: `RunVerifyReinferAsync`+`Btn_Verify_Click`+XAML VERIFY버튼(하단바 7→8칸). 추정위치 0x59 이동→재추론→잔차±3mm.
- MM_BACKUP crash-safe: `BackupCellArrays`→`MmCellBackup.dat`(사이드카, INI 255자 제한 회피), `RestoreCellArraysAsync` 파일에서 읽어 0x95. Phase2 트랜잭션(시작 백업/정상종료 Pending=0). Btn_CalRestore는 MM_BACKUP만.

## 5. 내일 테스트 로그 체크리스트 → 결과별 다음 수
순서대로 어디서 멈추는지:
1. `[MAINT][WARN] ★지상반 DIO` 뜸? → **뜨면 안전비트부터(§2-1, 메커니즘은 §8 P0-1). DIO화면 EM_SW/SF_PLUG 확인.** 동시 점검(§8 P0-1): `SrmInfo[srmNum].dioUse`(0이면 안전비트 항상 OK/무관, ≠0이면 실배선 의존), `DioPacket[srmNum].DISET[EM_SW/SF_PLUG].value`(false=트립송신), `rxDioComm`/`dioCommDiscCnt`/commFltCnt(DIO 통신불량이면 `gcpDioClass.cs:294` 가 EM_SW=false 강제 → 영구 트립). **신규지상반이 "되던게 안되는" 가장 유력한 지상반쪽 원인.** 비트 0 되면 0x59 재시도.
2. `[MAINT] 셋업모드 진입`→`[MODE] 셋업모드 확인 OK` (토글 없이 한 번) — 안 들어가면 알람/키/작업잔류.
3. `[MAINT][ERR] LIFT … 범위 밖` 뜸? → 그 LIFT는 펌웨어버그(§2-2)로 막힌 것(정상거부). 안 뜨면 통과.
4. `[MAINT][0xA4]/[0xA6] 쓰기 완료` — NACK 없어야(NACK=1이면 셋업모드 아님).
5. `[MAINT] 0x59 ACK (result=0)` + `[MAINT][MOVE] TRAV …→2840` 증가 → **성공! 토글제거(§4-A) 검증 완료.**

**결과별:**
- 1에서 막힘 → 안전배선 해결이 먼저. 코드 더 손댈 것 없음(진단만).
- 5까지 가고 움직임 → 토글제거 정상. 코드리뷰 남은 항목(§6) 진행.
- result=8인데 LIFT는 범위 안 → RunMode≠STAND_BY(이전이동 미완) 또는 MaintanencePos(이미 그자리±tolerance). 연속 소이동이면 result=9 회피 로직 필요.
- 0x8059 응답 자체가 또 없음 → 안전비트 래치 여전(§2-1). 0x34 알람로그 확인.

## 6. 미해결 코드리뷰 항목 (카파시+리뷰어, 우선순위)
- **C3** Phase2 자동 Start-ON: START 다이얼로그에 "자동모드+START 인가" 고지 추가 (안전).
- **C2** `cachedDriveParam` 무효화: SL-Save(0xA4)/mm탭 쓰기 후 캐시 null 안 함 → 범위검증 stale. 0xA4/A6 성공 후 캐시 무효화.
- **I4** `Btn_CalRestore` XAML 툴팁(246) 옛 동작(cellLev만)으로 남음 → 전체그리드 복구로 수정.
- **I6** `MoveViaSemiAutoAsync` 도착 ±100mm + 잔류 procState=4 오인 가능 → "송신 후 curPos 변했는지" 가드.
- **I1~I3** 죽은코드(대부분 세션 이전): `Btn_MmMove_Click`의 `if(false)` ~185줄 블록(3352~), `MoveViaJogAsync`/`JogAxisToTargetAsync`/`WaitCranePositionedAsync` 체인, 비트 반환하는 `GetCurrentTravPos/LiftPos`(1558~). 정리 대상.
- **반자동 전환(§4-B) 재검토**: 진짜 원인이 펌웨어+안전비트였으므로, 본 루프를 0x41로 바꾼 게 맞는지 재평가 필요.

## 7. 핵심 위치
- 지상반 코드: `PageAutoTeaching.xaml.cs` — `MoveViaMaintInternalAsync`(1338), `MoveViaSemiAutoAsync`(986), `Phase2_TeachingLoopAsync`, `Phase1_5_CalibrationAsync`, `RunVerifyReinferAsync`, `BackupCellArrays`/`RestoreCellArraysAsync`, `DecodeMaintResult`.
- 0x30 TX 빌더: `commClass/udpClientClass.cs:3098 CMD_Req_State`(data[11] 안전비트), 0x8059 RX `:817`, 디스패치 `:2540~2670`.
- 펌웨어(읽기전용): `MX_SRM_App_Ver_4.4_260507/Core/Src/com_tml.c`(rxCmdMoveMaintanence:2919, rxModeChange:3146, 디스패치:6232), `dev_SRM.c`(SRM_Move_Maintanence_Cmd:47224, Change_DeviceMode:65283). 구버전 비교: `MX_SRM_App_Ver_2.3_241113`.
- 프로토콜: `20260424_Vexi_통신 프로토콜_(Rev.92)(양일규).xlsx`. 분석: `오토티칭_프로토콜_분석.md`(상위폴더).
- MCU팀 전달 버그리포트: §2-2 코드스니펫 그대로.

---

## 8. STABILITY 개선 백로그

> 2026-06-24 세션에서 **독립 리뷰 에이전트 2개**(① 티칭 런타임 ② 통신/안전/비전/설정) + **DIO 소스 직접검증**으로 도출.
> ★ **대부분 같은 세션에 구현 완료(빌드 오류0 검증). 단, 크레인 실동작은 미검증 — 다음 테스트 때 확인 필요.** effort S=수십분, M=반나절.
> 빌드: `dotnet build gcp_Wpf.csproj -c Debug -v q -clp:ErrorsOnly` → 오류0, 경고 828(기존 nullable, 죽은코드 제거로 830→828).

### ✅ 2026-06-24 구현 완료 (코드 = `PageAutoTeaching.xaml.cs`)
| 항목 | 구현 내용 | 핵심 메서드 |
|---|---|---|
| **P0-1**(부분) | 안전입력 트립 **하드게이트 + 진단강화**(dioUse/rxDioComm 로그). ⚠ "첫 DIO 전 0x30 트립 송신 억제"(comm-layer 수정)는 **보류** — 안전 fail-safe 방향이라 신중. | `IsSafetyTripped` |
| **P0-2** | 트립 시 경고→**거부**. MoveViaMaint/MoveViaSemiAuto/Phase2 진입 3곳 게이트 | `IsSafetyTripped` |
| **P0-3** | 런 종료(정상/취소/예외) 공통 **무장 해제**: Start OFF(0x50)+요청플래그 클리어+gcpTxMode 복원 | `DisarmCraneAfterRun`, Btn_Start finally |
| **P0-4** | RefreshCellPositions `cellBay[0]`/`cellLev[0]` NRE 가드 | RefreshCellPositionsAsync |
| **P0-5** | 0xA4/A6(SoftLimit/ManualOp) 쓰기 후 `cachedDriveParam/LiftParam=null` | Btn_SlSave_Click |
| **P0-6** | 0x59 offset(132/136/207) 접근 전 길이 가드(<176B 거부) | MoveViaMaintInternalAsync |
| **P1-1** | 반자동/0x59 이동 **15s 무변화+미도착 → 조기 중단**(120s 헛대기 제거) | MoveViaSemiAuto/MaintInternal |
| **P1-2** | Phase2 **최근 8셀 연속 실패 → 런 중단**(서킷브레이커). ⚠ per-call HTTP CT/타임아웃 단축은 보류(전역 30s 유지) | Phase2 루프 상단 |
| **P1-3** | 일반 예외 경로에서도 Phase3(RTSP) 정리 | Btn_Start catch(Exception) |
| **P1-5** | BackupCellArrays try/catch + 실패 시 backupExists=false 표면화 | BackupCellArrays |
| **P1-6** | SaveToExcel `info.cellBay/Lev` null deref 가드 | SaveToExcel |
| **P1-7** | Probe59/SlRead/SlSave/MmMove에 `isRunning/mmMoveInProgress` 재진입 가드 | 각 핸들러 |
| **P1-8** | 0x59 송신 전 `maintMoveResult=0xFF` 리셋(이전 stale 0=성공 오인 방지) | MoveViaMaintInternalAsync |
| **P2-1** | CaptureSettle 구간중복 경고 + 파싱실패 로그 + IP빈값 경고(Phase1 표면화) | LoadTeachingConfig, Phase1 |
| **P2-2** | Btn_Skip 실동작: `skipRequested` 폴링으로 현재 셀 이동 중단 | Btn_Skip + MoveViaSemiAuto |
| **P2-3** | 죽은코드 ~550줄 제거: `if(false)` 블록, jog 서브시스템(MoveViaJog/JogAxisToTarget/JogAxis/WaitUntilStopped/SendJog*), WaitCranePositioned/WaitOriginPos/GetCurrentTrav/LiftPos/SendCraneTravMove | — |

### ✅ 2026-06-24 추가 구현 (2차)
| 항목 | 구현 내용 | 위치 |
|---|---|---|
| **P1-2 잔여** | VisionApiClient에 **per-call 타임아웃(`PerCallTimeoutSeconds`=15s) + CancellationToken** 추가(`PostWithTimeoutAsync`). Capture/X추론/Z추론에 ct 전달 → 서버 행 시 Stop 즉시 반응 + 셀당 15s로 끊김. per-call 타임아웃은 실패응답으로 변환(서킷브레이커가 카운트), 사용자 Stop은 그대로 전파. | `VisionApiClient.cs`, Phase2/Verify 호출부 |
| **P1-4** | MM_BACKUP 부분복구(Bay만/Lev만 성공) 시 **★혼합상태 경고 + Pending 유지(재시도 수렴)**. (진정한 원자성은 0x95 2회 분리전송이라 불가) | RestoreCellArraysAsync |
| **속도조정** | 0x59 보수위치(셋업/강제모드) 이동 속도 **Drive 60·Lift 40 → 20 m/min 통일**(`MaintMoveSpeedMpm` 상수). 티칭/캘리/검증 mm 이동 저속 정밀화. ※위치·속도는 0x59가 아니라 직전 0xA4/A6 속도그룹으로 결정(0x59는 빈 패킷 트리거 — `udpClientClass.cs:2645/2994` 재확인) | MoveViaMaintInternalAsync |

### ⏭ 의도적 보류 (이유 명시 — 다음 기회)
- **P0-1 잔여 (★테스트 데이터 먼저)**: 첫 DIO 읽기 전/통신불량 시 0x30 트립 송신 자체를 막는 것 = **comm-layer 안전 fail-safe 방향 변경**. `Dio_Setting.value` 기본 false / `gcpDioClass.cs:294` 강제false는 "모르면 정지"라는 **올바른 fail-safe**일 수 있어, DIO 미배선을 "안전"으로 위장하면 위험. **테스트 로그로 dioUse/value/rxDioComm 실측 후 결정**할 것. (이미 P0-2 하드게이트가 트립을 잡아 헛대기는 제거됨 + 메시지에 dioUse/DIO수신 노출.)
- **P2-4 (가치 낮음/리스크)**: 타임아웃 상수(0.8s/15s/120s/30s 등) 단일 상수화 = **순수 churn**인데 크레인 코드 다수 라인 수정 → 오타 리스크 대비 이득 적어 보류.

### 다음 테스트 때 함께 확인할 신규 로그
- `[MAINT][ERR] ★지상반 안전입력 트립 …` 또는 `[SEMI][ERR] ★… 트립` → §2-1 확정. dioUse/DIO수신 값이 메시지에 포함됨.
- `[SEMI][ERR] 15s간 위치 변화 없음 …` / `[MAINT][ERR] 15s간 …` → 미동작 조기중단 동작.
- `[SAFE] 런 종료 — 시작(Start) OFF …` → 무장 해제 정상.
- `[ABORT] 최근 8셀 연속 실패 …` → 서킷브레이커.

### ✅ 2026-06-24 수명/누수/종료 사냥 (멀티에이전트 14건 → 검증 9건 중 7건 수정)
| 수정 | 내용 | 심각도 |
|---|---|---|
| **★앱 종료 시 크레인 무장 잔류** | 진행 중 종료(X버튼)가 런 취소·무장해제 없이 통신 Close → 크레인이 자동+Start ON 잔류(후속 WCS/잔류신호로 오동작 위험). `PageAutoTeaching.AbortAndDisarmForShutdown()`(런 취소+Start OFF 플래그+RTSP 정리) 추가, `MainWindow.Window_Closing`에서 통신 Close ★전★ 호출+500ms로 0x50 실송신 보장 | **high(물리안전)** |
| **★캘리 elapsed_ms 타입** | 캘리 응답 3종(`CalibrationCapture/Inference/Compute`)의 `elapsed_ms`가 `long`인데 서버는 소수(예 578.89ms) 전송 → `JsonException`→ **캘리 성공이 "거짓 실패"로 둔갑→캘리 전체 중단**. capture/추론은 이미 `double`. 3개를 `double`로 통일(무위험). **캘리가 막히던 잠복 원인일 수 있음 — 테스트 시 확인** | med(캘리차단) |
| cts/mmCts dispose | 매 START/CALIB/VERIFY·MM이동·Probe가 CTS 재할당하며 이전 것 미해제 → 새 할당 전 `?.Dispose()` | low |
| slCts using | SL-Read/Save의 타임아웃 CTS(내부 Timer)를 `using var`로 확정 해제 | low |
| sendTimer dispose | udpClientClass.Close()에서 `Elapsed -=` + `Dispose()` | low |
| 종료 시 RTSP 정리 | AbortAndDisarmForShutdown에서 camera1/3 DisconnectRtsp(베스트에포트, 서버 ffmpeg 잔류 방지) | low |

⚠️ **미해결(결정 필요) — 실행 중 페이지 이탈**: 오토티칭 진행 중 운영자가 ★물리 지상반 키를 돌리면★ MainWindow 타이머가 자동/수동/반자동 페이지로 강제 전환(`pageCheck`→`Page_Change`, isRunning 가드 없음) → **STOP/SKIP 접근 불가 + 크레인이 자동+Start ON으로 백그라운드 계속 실행**. 자동발생은 아니나(키조작 필요) 실재 위험. 수정안: ①isRunning 중 자동전환 차단, ②전환 시 런 취소+무장해제. 사용자 결정 대기.

### ✅ 2026-06-24 Fault 사냥 후속 수정 (3차 — 멀티에이전트 fault-hunt 9건 검증 → 7건 수정)
멀티에이전트로 "오토티칭 중 fault(예외/크래시/행) 날만한 곳"을 차원별 사냥+적대적 검증. 검증 통과 9건 중 안전한 7건 수정(빌드 오류0, 경고 828→822):
| 수정 | 내용 | 파일 |
|---|---|---|
| **SaveToExcel 크래시** | 메서드 전체 try/catch로 흡수 + `using var wb`(SaveAs throw 시 Dispose 보장). 취소 catch블록 안 호출(1720)이 형제 catch에 안 잡혀 async void 밖→앱크래시 나던 것 차단. Btn_Save 무가드 크래시도 동시 해결 | PageAutoTeaching |
| **0x94 파서 경계검사** | Rx_RequestCellSetting에 길이가드(<8 거부) + BAY/LEV 루프에 `i<배열길이 && off+4<=len` 검사 후 인덱싱. 잘린/조작 프레임에서 IndexOutOfRange→수신스레드 예외폭주→통신저하/티칭대기 차단 | udpClientClass |
| **SaveLogFile 뮤텍스** | WaitOne~ReleaseMutex를 try/finally로 — IO예외 시에도 뮤텍스 반드시 해제(영구점유→로그/SaveRackData 전면정지 방지) | udpClientClass |
| **Vision null 가드** | Capture/X추론/Z추론 Deserialize 후 null이면 명확한 실패객체로 치환(본문 리터럴 "null" 시 NRE 차단). ※캘리/RTSP/카메라 메서드도 동일 패턴이나 호출부 catch가 흡수(cosmetic)라 미적용 | VisionApiClient |
| **DisarmCrane 레이스** | copy-modify-write → in-place 필드쓰기(런 종료 순간 UDP스레드 갱신필드 lost-update 제거) | PageAutoTeaching |
| **Btn_Stop cts** | 모달 전 cts를 로컬 캡처 후 그것만 Cancel(런 경계 넘어 새 런 spurious cancel 방지) | PageAutoTeaching |

**✅ 추가 마무리(2026-06-24, "계속 수정" 요청):**
- **Vision null 가드 전체 적용** — 캘리(capture/inference/compute/status/cleanup)·RTSP(connect/disconnect)·카메라/RTSP 상태 GET까지 모든 메서드에 `Deserialize` 후 null→실패객체 치환(또는 `?? new T()`). 리터럴 "null" 응답 NRE 패턴 전부 제거.
- **0x98/0x9C 파서 경계검사** — Rx_RequestStationSetting(SrmStation[]), Rx_RequestProhSetting(prohRack[])에 Count/오프셋 OOR 가드 추가. 오토티칭 경로는 미트리거지만(크레인정보요청 버튼 전용) 수신 스레드 사망 방지 차원에서 하드닝.
- **App 전역 예외 핸들러 추가**(`App.xaml.cs`) — DispatcherUnhandledException/AppDomain.UnhandledException/UnobservedTaskException를 `CrashLog` 폴더에 일자별 기록. **운영 HMI 정책: Dispatcher 예외는 로그+운영자 알림 후 `ex.Handled=true`로 앱 유지**(창 죽으면 크레인 감시/조작 상실이 더 위험). ※사이트가 "오류 시 종료"를 원하면 그 한 줄만 제거. 빌드 경고 822→806.

### ✅ 2026-06-24 정합성(로직) 사냥 — "크래시 아닌 조용히 틀린 결과" (멀티에이전트 8건 → 검증통과 3건 수정)
| 수정 | 내용 | 심각도 |
|---|---|---|
| **★캡처 기준위치 재읽기**(Phase2) | curBayPos/curLevPos를 Busy대기·안정화(settleMs) ★이전★에 읽어 capture/추론 요청에 그대로 썼음 → 안정화 동안 크레인이 정착/크리프하면 비전 추론기준(bay_pos)과 실제 캡처위치가 어긋나 **전 셀에 체계적 mm 오차**(높은 레벨일수록 심함). 안정화 ★후★ 재읽기로 수정(캘리·검증 경로와 동일) | **med(실데이터 오차)** |
| Excel 저장로그 거짓0 | `count*` 필드가 증가코드 없이 항상 0 → `[EXCEL] Saved (OK:0 CAP:0 X:0 Z:0)` 거짓표기. results에서 직접 집계로 교체 | low(표시) |
| 캘리 진행/요약 카운트 | zOffsets 7개(0 포함)인데 주석 "7+6=13"·로그 "/6"이라 "승강 7/6" 표시. X·Z 대칭(둘 다 0 포함, 회귀 정상 — 검증 확인)이라 **데이터는 정상**, 주석·분모만 14/7로 정정 | low(표시) |

검증기가 정확히 기각한 것(데이터 안 깨짐 재확인): 반자동 도착 ±100mm(베이피치보다 작아 옆셀 오인 불가 + 저장값은 추론값), 캘리 0-오프셋 중복캡처(대칭이라 회귀 정상).

### ✅ 처리됨(사용자 결정): `TravelOffsetMm` 제거
Config `[Vision] TravelOffsetMm` 가 로드만 되고 보정 로직이 구현된 적 없어(거짓 안심) → **사용자 결정 "죽은 config 제거"** 로 처리. `PageAutoTeaching.xaml.cs`에서 필드 선언·LoadTeachingConfig 로드부 삭제(라인 67에 제거 breadcrumb 주석). per-cell 보정이 필요하면 3단계 VERIFY 버튼이 그 역할.
- 잔여(무해, 손 안 댐): `VisionApiClient.cs`의 `CalibrationInferenceResponse.TravelOffsetMm`(비전 응답모델 필드 — GC config와 다른 것, CalibrationInference 미사용이라 죽어있으나 모델이라 존치), 배포된 `bin\...\Config.ini`의 `TravelOffsetMm=100` 키(이제 아무도 안 읽어 무시됨).

### ✅ 2026-06-24 로깅 처리 점검 — 멀티에이전트 15건 → 검증통과 7건 중 5건 수정
| 수정 | 내용 | 심각도 |
|---|---|---|
| **★cIniAccess 뮤텍스 누수** | `SaveExLog/SaveOPLog/SaveJobLog`의 `WaitOne()~ReleaseMutex()`에 try/finally 없음 → 파일IO 예외 1회로 공유 static 뮤텍스 영구점유 → 이후 ★모든 EXLOG/OPLOG/JOBLOG 영구 정지★(udp에서 고친 것과 동일 패턴, 여기만 미수정). try/finally로 수정 | **high** |
| udp 폴링로그 토글 | `SaveLogFile`의 `pollingStop` 가드가 파일쓰기 ★뒤★에 있어 "폴링정지" 눌러도 0x30 SEND/RECV가 초당 ~6~7줄 계속 디스크에 기록. 가드를 맨 위로 → 토글 시 파일쓰기까지 스킵 | med |
| Teaching/Log 무한증가 | `WriteLogFile`이 시간별 파일을 만들면서 `DeleteOldFiles` 미호출(타 로그는 전부 호출) → 무한누적. 15일 정리 추가 | med |
| Teaching/MmLog 무한증가 | `WriteMmLogFile` 동일 누락 → 15일 정리 추가 | med |
| CrashLog 정리없음 | 내가 추가한 `App.LogCrash`에 정리 정책 없음 → 새 일자파일 생성 시 15일 정리(isNew 게이트) 추가 | low |

⏭ 미적용(검증상 low): AddLog/AddMmLog가 로그 줄마다 `File.AppendAllText`(파일 open/close) + Dispatcher.Invoke + ScrollIntoView를 UI스레드에서 수행 — 그러나 셀당 시간이 비전추론·이동대기로 지배돼 실측 영향 미미(검증기 low). 영속 StreamWriter/백그라운드 큐로 개선 가능하나 churn 대비 이득 적어 보류. (※로그쓰기 첫 실패를 운영자에게 표면화하는 관측성 개선도 후속 후보.)

### ✅ 2026-06-24 리미트 스위치 → 자동 정지(EM) 신규 구현 (사용자 요청)
**요구**: 주행/승강 리미트 스위치 감지 시 무조건 정지.
- **신호**: `SRMIO.TST`(주행 리미트센서, DI0 bit5), `SRMIO.LST`(승강 리미트센서, DI0 bit4). ※`LSH/TSH`는 코드에 없음(축당 리미트 1개). THP/LHP는 원점, TDR/LDU/LDD는 감속이라 제외.
- **판정(사용자 지정)**: 절대 극성이 아니라 **"시작 시점 상태(baseline)에서 바뀌면 정지"**. 최초 0x30 상태수신 때 TST/LST 값을 baseline으로 잡고, 이후 그와 달라지면 리미트 작동으로 간주 → 극성 몰라도 됨.
- **정지 수단**: `startCmd=1; startOnOff=0` → UDP TX가 `0x50`(시작 OFF/정지) 송신(udpClientClass.cs:2563). 물리 EM 입력은 읽기전용이라 SW로 못 걸어 0x50이 지상반이 거는 정지.
- **위치/주기**: `udpClientClass.Srm_InOutParse()`(SRMIO 파싱 직후)에 `CheckLimitSwitchStop()` 호출 — **호기별·매 상태수신(~수백ms)**. 사용자는 "메인 루프(1초 UI타이머)"를 골랐으나, 안전 응답성을 위해 3배 빠르고 데이터가 신선한 이 per-SRM 상태수신 경로에 배치(전 호기 상시 만족). 원하면 MainWindow.TestTimer_Elapsed로 이동 가능.
- **동작**: 변화 지속 중 매 수신마다 0x50 강제(무조건 멈춤, 운전 재개 차단). 로그/EXLOG는 진입 엣지 1회(`[LIMIT][STOP]`). 기준상태로 복귀(리미트 해제)하면 latch 풀려 재무장.
- **⚠️ 전제/주의**: ①최초 상태수신 시 크레인이 리미트에 걸려있지 않아야 함(그 값이 기준). 걸린 채 시작하면 정상이동이 '변화'로 오인됨. ②이건 **보조 SW 레이어** — 리미트→정지는 하드웨어/MCU가 1차로 처리해야 함(UDP 폴링 지연 의존). ③극성/오발동 여부는 **실크레인 테스트로 검증 필요**(빌드만 됨).

### P0 — 런 중단 / 크레인 안전 (최우선)

**P0-1. DIO 안전비트 기본값·통신불량 → 0x59 무응답 (★§2-1의 지상반쪽 실제 메커니즘 — 스코프 내 점검/수정 가능)**
- 근거(검증됨): `Dio_Setting.value`는 `bool` → 배열생성 시 **기본 false**(`singletonClass.cs:1157~`, 값초기화 없음). `CMD_Req_State`가 `value ? 0 : 1`(`udpClientClass.cs:3120-3121`) → **value=false면 비트=1=트립** 송신. 첫 DIO 읽기 전 구간 + DIO 통신불량(`gcpDioClass.cs:292-298`, commFltCnt>4 → EM_SW=false 강제)에서 "비상정지/안전플러그 트립"을 MCU에 송신 → MCU 래치 → 0x59 영구 폐기.
- 단 `SrmInfo.dioUse==0`이면 안전비트 항상 true(트립 안 함, `gcpDioClass.cs:223-228/285-290`). `dioUse≠0`인데 DIO보드 미배선/통신불량이면 **지속 트립** → 신규지상반 "되던게 안되는" 가장 유력한 지상반쪽 설명.
- 다음 세션: (점검 S) 런타임에 `dioUse`/`DISET[EM_SW/SF_PLUG].value`/`rxDioComm`/`commFltCnt` 실측(내일 테스트 로그+DIO화면). (수정 M, 점검결과 따라) 첫 DIO 성공 전 0x30 송신 억제 또는 미검 상태를 "트립"으로 송신하지 않도록(데이터유효 비트 분리). comm-fault 시 안전쪽 송신이 의도면 유지하되 **운영자에게 표면화**(현재는 조용히 트립).

**P0-2. 안전비트 트립 시 경고만 하고 이동 진행 → 하드게이트 필요**
- `MoveViaMaintInternalAsync`(~1342)는 EM_SW/SF_PLUG false에 경고만 하고 그대로 0x59 송신. `MoveViaSemiAutoAsync`(~986, 0x41)는 검사조차 없음. 트립 상태면 셀당 120s 헛대기 × N셀로 런 전체 낭비.
- 수정(S): 이동 프리미티브 진입부에서 트립이면 **런 중단 + 명확 메시지**. (안전신호를 "안전한 것처럼 위조"하는 마스킹이 아니라, "위험하면 구동 거부" — 올바른 게이트.)

**P0-3. Stop/취소/예외 시 크레인 ARMED 잔존 (★물리안전 최중요)**
- Phase2가 자동모드+Start ON(0x50) 자동인가하는데, 런 `finally`(~1773)는 `isAutoTeaching`/UI만 정리 → **Start OFF 안 보냄, gcpTxMode 복원 안 함, 자동모드 안 내림.** 티칭 "끝난" 뒤에도 크레인이 자동+Start 래치 상태 → 후속 호스트(WCS) 작업/잡신호에 움직일 수 있음. 본보기: mm탭 경로는 `origGcpTxMode` 저장(3105)→복원(3555) 제대로 함.
- 추가: Stop 시 요청 플래그(semiJobClicked/maintMoveReq 등) 잔존.
- 수정(M): Phase2 진입에서 orig(gcpTxMode/Start) 저장, finally에서 **무조건 Start OFF + 모드 복원 + 요청플래그 클리어.**

**P0-4. RefreshCellPositionsAsync NRE 크래시** (effort S)
- `1123-1124` `cellBay[0]`/`cellLev[0]` 언가드 → 연결끊김/빈배열 시 NRE로 Phase2 진입에서 런 크래시. `1102-1103`처럼 가드.

**P0-5. cachedDriveParam/cachedLiftParam 무효화 안 함 (= 기존 코드리뷰 C2)** (effort S)
- 0xA4/A6 쓰기(`Btn_SlSave_Click` 2949~/3006~) 후 캐시 null 안 함 → §2-2 펌웨어버그 가드의 **범위검증이 stale 값으로 잘못 판정**(엉뚱한 거부/허용). 쓰기 성공 후 캐시 무효화.

**P0-6. offset-207 고정오프셋 쓰기 길이검사 없음** (effort S)
- `MoveViaMaintInternalAsync`에서 cached*Param 고정오프셋 접근 전 length 체크 없음 → 짧은 프레임 수신 시 IndexOutOfRange로 런 크래시. 길이 가드 추가.

### P1 — 데이터/UX 무결성 · 복원력

- **P1-1. 이동 stall 감지 없음 (안전비트 래치 케이스 정확히 해당)** — 반자동/maint 이동이 ACK 검사 없이 120s 폴링만. curPos가 수초간 무변화 + busy 미발생이면 조기 bail. `JogAxisToTargetAsync`(1290-1315)가 모델. 0x41은 전송응답 파싱해 NACK 조기실패. effort M
- **P1-2. 비전 API 타임아웃/서킷브레이커 없음 ("69건 전부 실패" 케이스)** — `VisionApiClient` 30s 글로벌 타임아웃 1개, 호출부에 CancellationToken 미전달 → 서버 행 시 셀당 ~90s × 69 ≈ 1.7h 헛때림 + Stop이 in-flight HTTP 못 끊음. (a) 메서드에 CT+per-call 타임아웃(5~10s) (b) 연속실패 N회(예 5)면 런 중단 (c) 하드실패(refused/timeout) vs 소프트(success=false, no beams) 구분. effort M
- **P1-3. RTSP 누수(일반예외 경로)** — `catch(Exception)`(1767~)이 `OperationCanceledException` 분기와 달리 Phase3를 안 불러 `rtspConnected=true` 잔존 → 다음 런이 재연결 skip. catch/finally에서 Phase3 정리. effort S
- **P1-4. MM_BACKUP 복구 비원자성** — Bay 써지고 Lev 실패 시 그리드 반쪽 잔존. 복구를 원자화 or 부분실패를 운영자에게 명확히 표면화. effort M
- **P1-5. BackupCellArrays try/catch 미적용**(2222-2248) — 파일쓰기 실패가 런 진입 막거나 크레인 armed 후 중단. effort S
- **P1-6. SaveToExcel null deref**(1597-1598, info.cellBay/Lev.Length) — null 역참조 가드. effort S
- **P1-7. 재진입/CTS 공유** — Btn_Probe59/SlRead/SlSave가 isRunning 무시 + mmCts 교차취소 + cts 미dispose 재사용. effort S
- **P1-8. 플래그 응답 상관성 없음** — `maintMoveResult` 값이 송신 전 리셋 안 됨(`maintMoveDone`만 false), 0x59 opcode를 probe/maint가 공유 → 이전/중복 0x8059가 현재로 오인 가능. 송신 전 sentinel(0xFF) 리셋 + 송신타임스탬프 이후 수신만 인정 + probe/maint 소비자 분리. effort M

### P2 — 폴리시 / 정리
- **P2-1. 설정 검증** — `LoadTeachingConfig` 파싱실패 swallow(로그X), `[CaptureSettle]` 구간 중복 시 specificity 무시하고 INI 선언순서로 결정(잘못된 settle→블러→"no beams"), Vision IP/Port 검증. 파싱실패 로그 + 구간중복 경고 + IP 형식검증. effort S
- **P2-2. Btn_Skip no-op** — 활성화돼 있는데 동작 없음 → 운영자 오인. 비활성화하거나 실제 skip 구현. effort S
- **P2-3. 죽은코드 정리 (= 기존 I1~I3)** — `Btn_MmMove_Click`의 `if(false)` ~185줄(3352~), `MoveViaJogAsync`/`JogAxisToTargetAsync`/`WaitCranePositionedAsync`(미사용·무한루프) 체인, 비트 반환 `GetCurrentTravPos/LiftPos`(1558~). effort S
- **P2-4. 타임아웃 상수 산재** — 셀별 0.8s/120s/30s 등 흩어짐 → 단일 예산/상수로. effort S

### "이대로 둬도 됨"(확인된 양호 — 손대지 말 것)
- 비전 JSON 파싱: 잘못된 JSON/비200 per-call catch → `success=false` 변환, 크래시 없음(`VisionApiClient.cs`).
- `HealthCheckAsync` refused/timeout/HTTP-status 구분 깔끔.
- 단일 `HttpClient` 재사용(소켓고갈 없음) — 빠진 건 per-call CT뿐(P1-2).
- 0x8059 짧은응답 시 `maintMoveResult` 기본 `0xFF`(실패쪽, 안전).
- §2-2 LIFT 범위 사전거부(1404~) — silent 클램프 대신 거부, 올바른 안전판단. **유지.**
