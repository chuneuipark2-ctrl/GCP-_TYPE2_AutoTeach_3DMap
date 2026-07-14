using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClosedXML.Excel;
using gcp_Wpf.commClass;
// SEW MOVI-C 직접 연결이 불가능한 환경에서는
// VEXI 프로토콜 CMD2_80 수동 조깅 + curPos 모니터링으로 mm 포지션 제어

namespace gcp_Wpf
{
    public partial class PageAutoTeaching : Page
    {
        singletonClass gClass;
        MainWindow pMain;
        VisionApiClient visionApi = new VisionApiClient();

        CancellationTokenSource cts;

        // [위치 sanity 가드] 런 시작 1회 계산하는 유효 위치 범위(mm). curPos가 이 밖이면 폭주로 보고 STOP+런중단.
        private bool envReady = false;
        private long envTravLo, envTravHi, envLiftLo, envLiftHi;
        private const int GUARD_FALLBACK_MARGIN_MM = 100; // SL 읽기 실패 시 셀그리드 min~max에 적용할 여유
        private const int GUARD_LIFT_LOW_MARGIN_MM = 10;   // SL 승강하한은 정상 하강정위치가 SL값보다 낮게 나올 수 있어 여유 필요(2026-07-06 실측 970 vs SL 980)

        // [라이다 폴백] START 라이다 2회 실패 후 운영자가 'X만 계속' 선택 시 true. 셀마다 라이다·Z추론 스킵, 승강 기존값 유지.
        private bool lidarFallback = false;

        // [2026-07-08 스캐폴드] 승강(Z) 모듈 우선순위 — 라이다/비전 각각 결과 중 현장에 유리한 쪽 채택. 디폴트=라이다 우선.
        //   주행(X)은 비전 전담(변경 없음). 비전-Z는 서버 미구현 → visionHoistEnabled=false면 라이다 단독(현재 동작 동일).
        //   Config.ini [Vision] HoistModulePriority=lidar|vision, VisionHoistEnabled=0|1 로 세팅. 비전팀 준비 후 on.
        private enum HoistModulePriority { LidarFirst, VisionFirst }
        private HoistModulePriority hoistPriority = HoistModulePriority.LidarFirst;
        private bool visionHoistEnabled = false;

        // cts.Cancel()을 부른 주체 — catch(OperationCanceledException)에서 STOP 로그 문구를 분기하기 위함
        //   (기존엔 위치가드 발동도 무조건 "cancelled by user"로 찍혀서 실제 원인을 오해하기 쉬웠음, 2026-07-06).
        private string stopReason = "user";   // "user"(Stop버튼) / "safety"(위치가드) / "shutdown"(앱종료)
        bool isRunning = false;
        // 관리자 5분 타임아웃 등으로 탭을 숨길 때, 진행 중인 티칭 런을 죽이지 않도록 외부에서 런 활성 여부 조회용
        public bool IsTeachingRunActive => isRunning;
        bool rtspConnected = false;   // RTSP 연결 상태 추적 (연속 테스트 시 재연결 방지)
        volatile bool skipRequested = false;   // SKIP 버튼 → 현재 셀 이동/대기 중단 후 다음 셀로

        // 비전 연속 실패 서킷브레이커: 최근 N셀 연속 실패면 런 전체를 조기 중단 (서버다운/크레인트립 시 헛돌이 방지)
        const int MaxConsecutiveCellFail = 8;

        // (비전 재시도 제거 2026-07-08: 파이프라인화로 캡처 직후 크레인이 다음 셀로 출발 —
        //  '같은 위치 재촬영' 전제가 성립하지 않아 X/Z 추론 실패는 즉시 실패 기록.)

        // 0x59 보수위치 이동(셋업/강제모드) 속도(m/min). 0xA4/A6 속도그룹에 써서 인버터 이동속도를 결정.
        //   티칭/캘리/검증 mm 이동은 정밀·안전 우선이라 저속(~20)으로. (기존 Drive 60 / Lift 40 → 20 통일)
        const ushort MaintMoveSpeedMpm = 20;

        // 티칭 결과 저장
        struct TeachingResult
        {
            public int Row;
            public int Bay;
            public int Level;
            public int BayPos;       // 주행 추론 결과 (mm) — X추론 실패 시 capture 시점 cur_bay_pos
            public int LevelPos;     // 승강 추론 결과 (mm) — Z추론 실패 시 capture 시점 cur_level_pos
            public bool Success;     // 전체 OK (capture + x_infer + z_infer 모두 성공)
            public string Error;     // 실패 메시지

            // 단계별 결과 추적 (어디서 실패했는지 명확히)
            public bool HasCargo;        // 캡처 시 화물 있음 체크 여부 (지상반 입력)
            public string FailedStep;    // "capture" / "x_inference" / "z_inference" / "" (성공)
            public bool CaptureOk;       // 캡처 성공
            public bool XInferenceOk;    // 주행 추론 성공
            public bool ZInferenceOk;    // 승강 추론 성공
            public string? FailedSubStep; // 비전 측 failed_step (예: "lens_coefficients", "postprocess")
            public string CapturedFile;  // 캡처 파일명 (디버깅용 — 4종 이미지 뷰어 URL 만들 때)
            public string? RawPath;          // 비전 캡처 원본 경로 (localhost 디스크)
            public string? CalibratedPath;   // 렌즈 보정 이미지 경로 (있을 때만)
        }

        // 실시간 카운트
        int countOk = 0;
        int countFailCapture = 0;
        int countFailX = 0;
        int countFailZ = 0;

        List<TeachingResult> currentResults = new List<TeachingResult>();
        string lastExcelPath = null;
        int curTargetIdx = 0;
        int totalTargets = 0;

        // (TravelOffsetMm 제거됨 2026-06-24: 로드만 되고 보정 로직이 구현된 적 없어 거짓 안심만 줌.
        //  per-cell 추론위치 보정이 필요하면 3단계 VERIFY 버튼이 그 역할을 함.)

        // Level별 캡처 전 안정 대기시간(ms). 크레인이 높을수록 Busy 해제 후에도 잔진동이 커서
        // 레벨 구간별로 캡처 전 대기시간을 따로 줄 수 있게 함. Config.ini [CaptureSettle] 에서 로드:
        //   DefaultMs=1000
        //   CaseCount=3
        //   Case1=1,10,800      (Lev 1~10 → 800ms)
        //   Case2=11,20,1500
        //   Case3=21,40,2500
        int captureSettleDefaultMs = 1000;
        readonly List<(int startLev, int endLev, int ms)> captureSettleCases = new List<(int, int, int)>();

        // LoadTeachingConfig에서 발견한 설정 경고(파싱실패/구간중복 등). 생성자 시점엔 로그창이 비어있어 보관했다가 Phase1에서 출력.
        readonly List<string> _configWarnings = new List<string>();

        // 셀 키 = "R{row}-B{bay:D3}-L{lev:D2}"
        string CellKey(int row, int bay, int lev) => $"R{row}-B{bay:D3}-L{lev:D2}";

        // 현재 주행/승강 위치(mm) 읽기 단축 접근자 — 동일 표현 47곳 중복 제거(읽기 전용이라 struct 함정 없음)
        private int CurTrav => gClass.str.SrmState[gClass.srmNum].trav.curPos;
        private int CurLift => gClass.str.SrmState[gClass.srmNum].lift.curPos;

        // 상태 표시 색상 상수 — 반복되던 매직 RGB 제거(의미 자명화)
        private static readonly Color ClrWarn  = Color.FromRgb(0xFF, 0xC8, 0x00);  // 경고/진행(앰버)
        private static readonly Color ClrErr   = Color.FromRgb(0xB4, 0x45, 0x45);  // 오류/정지(레드)
        private static readonly Color ClrCalib = Color.FromRgb(0xE0, 0x82, 0xFF);  // 캘리브레이션(퍼플)
        private static readonly Color ClrDone  = Color.FromRgb(0x36, 0xCC, 0x7B);  // 완료(그린)
        private static readonly Color ClrInfo  = Color.FromRgb(0x90, 0xCA, 0xF9);  // 정리/정보(블루)

        // ===== M3: 진행 화면 갱신(표시 전용) + 셀-경계 PAUSE =====
        private readonly System.Collections.Generic.List<long> cellDurMs = new();   // ETA용 셀 소요시간
        private volatile bool pauseRequested = false;   // 셀 경계 협조적 일시정지(이동/캡처/추론 도중엔 절대 멈추지 않음)

        // [2026-07-08] 2축 분리 — 파이프라인은 '이동(다음 셀)'과 '추론(현재 셀)'이 동시 진행되므로
        //   크레인 축(이동/도착)과 비전 축(촬영/분석/라이다/승강)을 서로 끄지 않게 독립 갱신한다.
        //   (예: 루프top SetStep("move")가 직전 셀의 '분석/승강' 칩을 끄지 않음 → 이동+추론 둘 다 점등)
        private void SetStep(string active)   // "move"/"arrive"(크레인축) · "shoot"/"analyze"/"lidar"/"z"(비전축) · ""(전체 클리어)
        {
            Dispatcher.Invoke(() =>
            {
                Brush on = new SolidColorBrush(Color.FromRgb(0x22, 0xB9, 0xAF)), off = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
                switch (active)
                {
                    case "move":                                  // 크레인 축만
                        stepMove.Background = on; stepArrive.Background = off; break;
                    case "arrive":
                        stepArrive.Background = on; stepMove.Background = off; break;
                    case "shoot":                                 // 비전 축만 (크레인 칩 유지)
                        stepShoot.Background = on; stepAnalyze.Background = off; stepLidar.Background = off; stepZ.Background = off; break;
                    case "analyze":
                        stepAnalyze.Background = on; stepShoot.Background = off; stepLidar.Background = off; stepZ.Background = off; break;
                    case "lidar":
                        stepLidar.Background = on; stepShoot.Background = off; stepAnalyze.Background = off; stepZ.Background = off; break;
                    case "z":
                        stepZ.Background = on; stepShoot.Background = off; stepAnalyze.Background = off; stepLidar.Background = off; break;
                    default:                                      // "" — 전체 클리어(셀 종료·런 종료)
                        stepMove.Background = stepArrive.Background = stepShoot.Background =
                            stepAnalyze.Background = stepLidar.Background = stepZ.Background = off; break;
                }
            });
        }

        private void UpdateRunStats(int current, int total, string cellName)
        {
            // Phase2 스레드에서 읽음 — 유일 writer(.Add)와 동일 스레드라 동시변경 없음
            int ok = currentResults.Count(r => r.Success);
            int fail = currentResults.Count(r => !r.Success);
            double avg = cellDurMs.Count > 0 ? cellDurMs.Average() : 0;
            int remain = Math.Max(0, total - current);
            string eta = avg > 0 ? $"예상 남은 시간 약 {Math.Ceiling(avg * remain / 60000.0)}분" : "예상 남은 시간 —";
            Dispatcher.Invoke(() =>
            {
                lblRun_Cell.Content = $"현재 셀 — {cellName}";
                lblRun_Progress.Content = $"{current} / {total}";
                progRun.Value = total > 0 ? (double)current / total * 100 : 0;
                lblRun_Ok.Content = ok.ToString();
                lblRun_Fail.Content = fail.ToString();
                lblRun_Eta.Content = eta;
            });
        }

        /// <summary>런 대시보드의 OK/FAIL/ETA만 갱신(현재 셀·진행바는 안 건드림). 파이프라인 harvest 시점 카운트 반영용 —
        /// 루프top UpdateRunStats(i)의 '현재 셀=i' 표시를 되돌리지 않도록 분리.</summary>
        private void UpdateRunCounts(int total)
        {
            int ok = currentResults.Count(r => r.Success);
            int fail = currentResults.Count(r => !r.Success);
            double avg = cellDurMs.Count > 0 ? cellDurMs.Average() : 0;
            int remain = Math.Max(0, total - currentResults.Count);
            string eta = avg > 0 ? $"예상 남은 시간 약 {Math.Ceiling(avg * remain / 60000.0)}분" : "예상 남은 시간 —";
            Dispatcher.Invoke(() =>
            {
                lblRun_Ok.Content = ok.ToString();
                lblRun_Fail.Content = fail.ToString();
                lblRun_Eta.Content = eta;
            });
        }

        private static string FailLabel(string step) => step switch
        {
            "move"        => "이동 실패",
            "capture"     => "촬영 실패",
            "x_inference" => "주행 미검출",
            "z_inference" => "승강 미검출",
            _             => string.IsNullOrEmpty(step) ? "실패" : step
        };

        private void PushRecent(TeachingResult r)
        {
            string txt;
            if (r.Success)
            {
                var info = gClass.str.SrmInfo[gClass.srmNum];
                int existBay = (info.cellBay != null && r.Bay >= 1 && r.Bay <= info.cellBay.Length) ? info.cellBay[r.Bay - 1] : 0;
                int existLev = (info.cellLev != null && r.Level >= 1 && r.Level <= info.cellLev.Length) ? info.cellLev[r.Level - 1] : 0;
                int devBay = r.BayPos - existBay;
                int devLev = r.LevelPos - existLev;
                // [2026-07-08] 성공 셀: 주행/승강 각각 실측 mm + 편차를 축별로 별도 표기(요청).
                txt = $"{CellKey(r.Row, r.Bay, r.Level)}   정상 · 주행 {r.BayPos}mm(편차 {(devBay >= 0 ? "+" : "")}{devBay}mm) 승강 {r.LevelPos}mm(편차 {(devLev >= 0 ? "+" : "")}{devLev}mm)";
            }
            else
            {
                txt = $"{CellKey(r.Row, r.Bay, r.Level)}   실패 · {FailLabel(r.FailedStep)}";
            }
            bool ok = r.Success;
            Dispatcher.Invoke(() =>
            {
                // [2026-07-08] 성공=초록/실패=빨강 색상 구분 (문자열 대신 ListBoxItem으로 삽입).
                lstRun_Recent.Items.Insert(0, new ListBoxItem
                {
                    Content = txt,
                    Foreground = new SolidColorBrush(ok ? Color.FromRgb(0x8B, 0xE0, 0x8B) : Color.FromRgb(0xFF, 0x6B, 0x6B))
                });
                while (lstRun_Recent.Items.Count > 6) lstRun_Recent.Items.RemoveAt(lstRun_Recent.Items.Count - 1);
            });
        }

        private void Btn_Pause_Click(object sender, RoutedEventArgs e)
        {
            pauseRequested = !pauseRequested;
            Dispatcher.Invoke(() => btnRun_Pause.Content = pauseRequested ? "▶ 재개" : "⏸ 일시정지");
            AddLog(pauseRequested ? "[PAUSE] 일시정지 요청 — 현재 셀 완료 후 정지" : "[RESUME] 재개");
        }

        public PageAutoTeaching(MainWindow parent)
        {
            gClass = singletonClass.Instance;
            InitializeComponent();
            pMain = parent;

            InitRowCombo();
            LoadTeachingConfig();
            UpdateCameraLabel();      // Row 기준 카메라 표시
            UpdateTargetPreview();    // 대상 셀 수 + CARGO 경고

            // [2026-07-08] Run 화면 현재 주행/승강 mm 실시간 갱신(200ms) — Run 패널 보일 때만.
            _runPosTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _runPosTimer.Tick += (s, e) =>
            {
                if (pnl_Run.Visibility != Visibility.Visible) return;
                lblRun_curTrav.Content = CurTrav.ToString();
                lblRun_curLift.Content = CurLift.ToString();
            };
            _runPosTimer.Start();
        }

        private System.Windows.Threading.DispatcherTimer _runPosTimer;

        // Row가 카메라를 결정(Row1=camera3, Row2=camera1) — 표시 라벨 갱신.
        private void UpdateCameraLabel()
        {
            try { lbl_camAuto.Content = $"{GetCameraId()} (Row{(combo_Row.SelectedItem is ListBoxItem s && int.TryParse(s.Content.ToString(), out int r) ? r : 1)})"; }
            catch { }
        }

        // START 전 대상 셀 수 미리보기 + CARGO 경고(실시간). 범위 오타로 0셀/전체 도는 사고 방지.
        private void UpdateTargetPreview()
        {
            try
            {
                int n = BuildTargetList().Count;
                string cargo = (chk_HasCargo.IsChecked == true) ? "   ⚠ CARGO 체크 — 라이다 승강보정 불가(빈 셀 권장)" : "";
                lbl_targetCount.Content = $"총 {n}셀 티칭 예정{cargo}";
                lbl_targetCount.Foreground = new SolidColorBrush(chk_HasCargo.IsChecked == true ? Color.FromRgb(0xFF, 0xC1, 0x07)
                                                                : n == 0 ? Color.FromRgb(0xFF, 0x6B, 0x6B) : Color.FromRgb(0x22, 0xB9, 0xAF));
            }
            catch { }
        }

        private void Combo_Row_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        { UpdateCameraLabel(); UpdateTargetPreview(); }
        private void Range_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e) => UpdateTargetPreview();
        private void Cargo_Changed(object sender, RoutedEventArgs e) => UpdateTargetPreview();

        /// <summary>
        /// 지상반 측 비전 설정 로드 (travel_offset_mm 등).
        /// SRM{N}/Teaching/Config.ini의 [Vision] 섹션에서 읽음.
        /// 기본값 0 = 모든 차이를 보정 (오차 0 목표).
        /// </summary>
        private void LoadTeachingConfig()
        {
            _configWarnings.Clear();
            try
            {
                string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "SRM" + gClass.srmNum, "Teaching", "Config.ini");

                // Level별 캡처 전 안정 대기시간 로드 ([CaptureSettle])
                captureSettleCases.Clear();
                string defMs = cIniAccess.Read(iniPath, "CaptureSettle", "DefaultMs", "1000");
                if (int.TryParse(defMs, out int dm) && dm >= 0 && dm <= 60000) captureSettleDefaultMs = dm;
                else if (defMs != "1000")
                    _configWarnings.Add($"[CONFIG][WARN] CaptureSettle.DefaultMs '{defMs}' 무시(0~60000) — 기본 {captureSettleDefaultMs}ms 사용");
                string cntStr = cIniAccess.Read(iniPath, "CaptureSettle", "CaseCount", "0");
                if (int.TryParse(cntStr, out int cnt) && cnt > 0)
                {
                    for (int i = 1; i <= cnt && i <= 32; i++)
                    {
                        string cv = cIniAccess.Read(iniPath, "CaptureSettle", "Case" + i, "nowrite");
                        if (cv == "nowrite") continue;   // 키 자체가 없음 — 조용히 스킵
                        var parts = cv.Split(',');
                        if (parts.Length == 3
                            && int.TryParse(parts[0].Trim(), out int s)
                            && int.TryParse(parts[1].Trim(), out int e)
                            && int.TryParse(parts[2].Trim(), out int ms)
                            && s >= 1 && e >= s && ms >= 0 && ms <= 60000)
                            captureSettleCases.Add((s, e, ms));
                        else
                            _configWarnings.Add($"[CONFIG][WARN] CaptureSettle.Case{i} '{cv}' 형식오류 무시 (형식: 시작레벨,끝레벨,ms)");
                    }
                }

                // 구간 중복 감지 — 겹치면 GetCaptureSettleMs가 '먼저 선언된' 것을 반환(specificity 무시)하므로 의도와 다를 수 있음.
                for (int a = 0; a < captureSettleCases.Count; a++)
                    for (int b = a + 1; b < captureSettleCases.Count; b++)
                        if (captureSettleCases[a].startLev <= captureSettleCases[b].endLev &&
                            captureSettleCases[b].startLev <= captureSettleCases[a].endLev)
                            _configWarnings.Add($"[CONFIG][WARN] CaptureSettle 구간 중복: [{captureSettleCases[a].startLev}-{captureSettleCases[a].endLev}] ↔ [{captureSettleCases[b].startLev}-{captureSettleCases[b].endLev}] — 먼저 선언된 것이 적용됨(의도 확인)");
                // [Vision] 승강(Z) 모듈 우선순위 (2026-07-08 스캐폴드) — lidar(디폴트)/vision + 비전-Z 사용여부.
                string zp = cIniAccess.Read(iniPath, "Vision", "HoistModulePriority", "lidar").Trim().ToLower();
                hoistPriority = zp == "vision" ? HoistModulePriority.VisionFirst : HoistModulePriority.LidarFirst;
                if (zp != "lidar" && zp != "vision")
                    _configWarnings.Add($"[CONFIG][WARN] Vision.HoistModulePriority '{zp}' 무시(lidar/vision) — 기본 lidar");
                string vze = cIniAccess.Read(iniPath, "Vision", "VisionHoistEnabled", "0").Trim().ToLower();
                visionHoistEnabled = vze == "1" || vze == "true";
            }
            catch (Exception ex)
            {
                _configWarnings.Add($"[CONFIG][ERR] 설정 로드 예외: {ex.Message} — 기본값 사용");
            }
        }

        // [2026-07-08 스캐폴드] 승강(Z) 결과 결정 — 라이다/비전 모듈 결과 중 우선순위·성공여부로 채택.
        //   visionHoistEnabled=false(디폴트)면 라이다 단독(기존 동작과 100% 동일).
        //   true면 두 모듈 다 호출해 AdoptBetterHoist로 채택. 반환 타입은 기존 HoistInferenceResponse 그대로라
        //   호출부(InferCellAsync)의 성공/실패·InferredLevelPos 처리 로직 무변경.
        private async Task<HoistInferenceResponse> RequestHoistResolvedAsync(string cameraId, CaptureRequest zReq, CancellationToken ct)
        {
            var lidar = await visionApi.RequestHoistInferenceAsync(cameraId, zReq, ct);
            lidar.Source = "lidar";
            if (!visionHoistEnabled)
                return lidar;   // 비전-Z 미사용(서버 미구현/미설정) → 라이다 단독

            HoistInferenceResponse vision;
            try { vision = await visionApi.RequestHoistInferenceVisionAsync(cameraId, zReq, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { vision = new HoistInferenceResponse { Success = false, Error = ex.Message, Source = "vision" }; }

            var adopted = AdoptBetterHoist(lidar, vision);
            AddLog($"[Z][MODULE] lidar(ok={lidar.Success} lev={lidar.InferredLevelPos}) / vision(ok={vision.Success} lev={vision.InferredLevelPos}) → 채택={adopted.Source} (우선={hoistPriority})");
            return adopted;
        }

        // 승강(Z) 두 모듈 결과 중 채택 규칙(스캐폴드). 현재: 우선 모듈이 성공이면 그것, 실패면 다른 모듈로 폴백.
        //   TODO(비전팀): '현장에 더 유리한' 판정(가로바/랙빔 타펫 기울기, 신뢰도 스코어 등)을 반영해 정교화.
        //     통상 타펫 기준 라이다가 유리 → 디폴트 우선순위를 라이다로 둔 이유.
        private HoistInferenceResponse AdoptBetterHoist(HoistInferenceResponse lidar, HoistInferenceResponse vision)
        {
            var (primary, secondary) = hoistPriority == HoistModulePriority.VisionFirst ? (vision, lidar) : (lidar, vision);
            if (primary.Success) return primary;   // 우선 모듈 성공 → 채택
            if (secondary.Success) return secondary; // 우선 실패 → 다른 모듈로 폴백
            return primary;                          // 둘 다 실패 → 우선 모듈 에러를 대표로
        }

        /// <summary>Level별 캡처 전 안정 대기시간(ms). 매칭되는 CASE가 없으면 DefaultMs.</summary>
        private int GetCaptureSettleMs(int lev)
        {
            foreach (var c in captureSettleCases)
                if (lev >= c.startLev && lev <= c.endLev) return c.ms;
            return captureSettleDefaultMs;
        }

        private void InitRowCombo()
        {
            combo_Row.Items.Clear();
            int maxRow = gClass.str.SrmInfo[gClass.srmNum].row;
            if (maxRow <= 0) maxRow = 2;
            for (int i = 1; i <= maxRow; i++)
            {
                var item = new ListBoxItem { Content = i.ToString(), Foreground = new SolidColorBrush(Colors.White) };
                combo_Row.Items.Add(item);
            }
            if (combo_Row.Items.Count > 0)
                combo_Row.SelectedIndex = 0;
        }

        public async void PageInit()
        {
            InitRowCombo();
            ResetUI();
            await CheckAndRestorePendingMmBackup();
            PopulateReview();
            SetView(TeachView.Setup);
        }

        // ===== M2: 상태머신 셸 (SETUP/RUN/REVIEW/MM 4패널 전환) =====
        private enum TeachView { Setup, Run, Review, MmMove }

        private void SetView(TeachView v)
        {
            Dispatcher.Invoke(() =>
            {
                pnl_Setup.Visibility   = v == TeachView.Setup  ? Visibility.Visible : Visibility.Collapsed;
                pnl_Run.Visibility     = v == TeachView.Run    ? Visibility.Visible : Visibility.Collapsed;
                pnl_Review.Visibility  = v == TeachView.Review ? Visibility.Visible : Visibility.Collapsed;
                pnl_MmMove.Visibility  = v == TeachView.MmMove ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        private void Btn_Mm_Click(object sender, RoutedEventArgs e)     => SetView(TeachView.MmMove);
        private void Btn_MmBack_Click(object sender, RoutedEventArgs e) => SetView(TeachView.Setup);
        private void Btn_Review_Click(object sender, RoutedEventArgs e) => SetView(TeachView.Review);

        private void ResetUI()
        {
            lbl_status.Content = "IDLE";
            ellStatus.Fill = new SolidColorBrush(Color.FromRgb(0xAD, 0xAD, 0xAD));
            lbl_curTarget.Content = "-";
            lbl_curTrav.Content = "0";
            lbl_curLift.Content = "0";
            lbl_curFork.Content = "CENTER";
            lbl_progress.Content = "0 / 0";
            lbl_result.Content = "-";
            progressBar.Value = 0;
            btn_Start.IsEnabled = true;
            btn_Stop.IsEnabled = false;
            btn_Skip.IsEnabled = false;
            btn_Save.IsEnabled = false;
            countOk = countFailCapture = countFailX = countFailZ = 0;
        }

        /// <summary>UI에 단계별 결과 카운트 실시간 표시 (RESULT 라벨에)</summary>
        private void UpdateResultCounts()
        {
            Dispatcher.Invoke(() =>
            {
                int total = countOk + countFailCapture + countFailX + countFailZ;
                lbl_result.Content = $"OK:{countOk} CAP:{countFailCapture} X:{countFailX} Z:{countFailZ} ({total})";
                lbl_result.Foreground = countOk > 0 && (countFailCapture + countFailX + countFailZ) == 0
                    ? new SolidColorBrush(Colors.LightGreen)
                    : new SolidColorBrush(ClrWarn);
            });
        }

        // ================================================================
        // 대상 셀 목록 생성
        // ================================================================

        private List<(int row, int bay, int lev)> BuildTargetList()
        {
            var list = new List<(int, int, int)>();

            int row = 1;
            if (combo_Row.SelectedItem is ListBoxItem sel)
                int.TryParse(sel.Content.ToString(), out row);

            int maxBay = gClass.str.SrmInfo[gClass.srmNum].bay;
            if (maxBay <= 0) maxBay = 1;
            int maxLev = gClass.str.SrmInfo[gClass.srmNum].lev;
            if (maxLev <= 0) maxLev = 1;

            int bayStart = 1, bayEnd = maxBay;
            ParseRange(edit_BayRange.Text, ref bayStart, ref bayEnd, maxBay);
            int levStart = 1, levEnd = maxLev;
            ParseRange(edit_LevRange.Text, ref levStart, ref levEnd, maxLev);

            // ㄹ자형 snake 패턴: 레벨별로 베이를 순회, 짝수 레벨은 역방향
            bool forward = true;
            for (int l = levStart; l <= levEnd; l++)
            {
                if (forward)
                    for (int b = bayStart; b <= bayEnd; b++)
                        list.Add((row, b, l));
                else
                    for (int b = bayEnd; b >= bayStart; b--)
                        list.Add((row, b, l));
                forward = !forward;
            }

            return list;
        }

        private void ParseRange(string text, ref int start, ref int end, int maxVal)
        {
            text = text.Trim().ToUpper().Replace("MAX", maxVal.ToString());
            if (text.Contains("-"))
            {
                var parts = text.Split('-');
                if (parts.Length == 2)
                {
                    int.TryParse(parts[0].Trim(), out start);
                    int.TryParse(parts[1].Trim(), out end);
                }
            }
            else if (int.TryParse(text, out int val))
            {
                start = val; end = val;
            }
            start = Math.Max(1, start);
            end = Math.Min(maxVal, end);
            if (start > end) start = end;
        }

        // [2026-07-08] Row별 카메라 분리 — 선택한 Row에 따라 카메라 자동 선택(Row1=camera3, Row2=camera1).
        //   런은 단일 Row(combo_Row)라 한 런은 한 카메라. combo_Camera 수동선택은 이제 무시됨(Row가 결정).
        //   ※ camera1은 비전 서버 렌즈 캘리브가 선행돼야 사용 가능(현재 camera3만 셋업됨).
        private string GetCameraId()
        {
            int row = 1;
            if (combo_Row.SelectedItem is ListBoxItem sel && int.TryParse(sel.Content.ToString(), out int r))
                row = r;
            return row == 1 ? "camera3" : "camera1";
        }

        // ================================================================
        // Phase 1: 초기화 (카메라확인 → 서버시작 → RTSP연결)
        // ================================================================

        private async Task<bool> Phase1_InitAsync(string cameraId, CancellationToken ct)
        {
            // 1. Vision IP/Port 설정
            string visIni = AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini";
            string ip = cIniAccess.Read(visIni, "SRMINFO_" + gClass.srmNum, "VISIONIP", "127.0.0.1").Trim();
            int port = int.TryParse(cIniAccess.Read(visIni, "SRMINFO_" + gClass.srmNum, "VISIONPORT", "3080").Trim(), out int p) ? p : 3080;
            visionApi.SetBaseUrl(ip, port);
            AddLog($"[INIT] Vision API BaseUrl={visionApi.BaseUrl}");
            if (string.IsNullOrWhiteSpace(ip))
                AddLog("[INIT][WARN] Vision IP가 비어있음 — 헬스체크 실패 예상 (IP 입력 확인)");
            foreach (var w in _configWarnings) AddLog(w);   // Config.ini 파싱/구간중복 경고 표면화
            AddLog($"[INIT] SRM#{gClass.srmNum} srmID={gClass.str.SrmInfo[gClass.srmNum].srmID} gcpTxMode={gClass.str.SrmState[gClass.srmNum].gcpState.gcpTxMode}");

            // 2. 헬스체크
            SetStatus("CHECK", ClrWarn);
            AddLog("[TX] GET /api/gc/req/cameras/status (health)");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                bool alive = await visionApi.HealthCheckAsync();
                sw.Stop();
                AddLog($"[RX] health={( alive ? "OK" : "FAIL")} ({sw.ElapsedMilliseconds}ms)");
                if (!alive)
                {
                    AddLog("[ERR] Vision server not responding — 비전PC 연결 또는 GC서버 구동 상태 확인 필요");
                    return false;
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                AddLog($"[ERR] Health check failed ({sw.ElapsedMilliseconds}ms): {ex.Message}");
                return false;
            }

            ct.ThrowIfCancellationRequested();

            // 3. 카메라 연결 확인
            SetStatus("CHECK CAM", ClrWarn);
            AddLog("[TX] GET /api/gc/req/cameras/status (camera)");
            sw.Restart();
            try
            {
                var camStatus = await visionApi.CheckCameraStatusAsync();
                sw.Stop();
                AddLog($"[RX] camera1={camStatus.Camera1 ?? "null"}, camera3={camStatus.Camera3 ?? "null"} ({sw.ElapsedMilliseconds}ms)");
                string status = cameraId == "camera1" ? camStatus.Camera1 : camStatus.Camera3;
                lbl_camStatus.Content = status;

                if (status != "ok")
                {
                    AddLog($"[ERR] {cameraId} unreachable — 카메라 IP ping 실패");
                    return false;
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                AddLog($"[ERR] Camera check failed ({sw.ElapsedMilliseconds}ms): {ex.Message}");
                return false;
            }

            ct.ThrowIfCancellationRequested();

            // 4. RTSP 연결 (이미 연결 상태면 skip)
            if (rtspConnected)
            {
                AddLog($"[INIT] RTSP 이미 연결됨 — connect skip");
            }
            else
            {
                AddLog($"[TX] POST /api/gc/cmd/{cameraId}/connect");
                sw.Restart();
                try
                {
                    var rtspResp = await visionApi.ConnectRtspAsync(cameraId);
                    sw.Stop();
                    AddLog($"[RX] RTSP connect={( rtspResp.Success ? "OK" : "FAIL")} ({sw.ElapsedMilliseconds}ms)");
                    if (!rtspResp.Success)
                    {
                        AddLog($"[ERR] RTSP 연결 실패 — {rtspResp.Error ?? "카메라 RTSP 스트림 확인 필요"}");
                        return false;
                    }
                    rtspConnected = true;
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    AddLog($"[ERR] RTSP connect failed ({sw.ElapsedMilliseconds}ms): {ex.Message}");
                    return false;
                }
            }

            AddLog("[INIT] Phase1 초기화 완료");
            return true;
        }

        // ================================================================
        // Phase 1.5: Calibration 세팅 (주행 ±3mm, 승강 ±3mm)
        // 첫 실행 시 calibration/capture × 14 → compute
        // 이후에는 기존 calibration 파일 재사용
        // ================================================================

        /// <summary>
        /// 캘리 스윕 1점 캡처(주행·승강 공통): 보수위치 이동 → 정밀위치 도달 대기 → Busy해제+안정화 → 캡처.
        /// 성공 시 측정값(recordTrav=true면 측정 trav, false면 측정 lift)을 positions에 추가. 이동타겟 축·기록값만 호출부가 지정.
        /// </summary>
        private async Task CalibSweepPointAsync(string cameraId, int targetTrav, int targetLift, string label,
            int stepNum, int totalSteps, int refRow, int refBay, int refLev,
            List<int> positions, bool recordTrav, System.Diagnostics.Stopwatch sw, CancellationToken ct)
        {
            if (!await MoveViaMaintAsync(targetTrav, targetLift, ct))
            {
                AddLog($"[CAL-ERR] {label} 이동 실패 — 건너뜀");
                return;
            }

            // 0mm 오차 검증: 정확히 타겟 위치 도달 대기 (±1mm 허용)
            if (!await WaitExactPositionAsync(targetTrav, targetLift, 30000, ct))
            {
                AddLog($"[CAL-ERR] {label} 위치 미달성 (목표 trav={targetTrav} lift={targetLift}, 현재 trav={CurTrav} lift={CurLift}) — 건너뜀");
                return;
            }

            // SRM Busy 해제 대기 + 추가 안정화 100ms
            await WaitSrmIdleAsync(10000, ct);
            await Task.Delay(100, ct);

            int curBay = CurTrav, curLev = CurLift;
            AddLog($"[CAL] [{stepNum}/{totalSteps}] {label} 위치확인 trav={curBay}mm lift={curLev}mm (오차: trav={Math.Abs(curBay - targetTrav)}mm lift={Math.Abs(curLev - targetLift)}mm)");

            var capReq = new CaptureRequest
            {
                Row = refRow, Bay = refBay, BayPos = curBay,
                Level = refLev, LevelPos = curLev, HasCargo = false
            };

            sw.Restart();
            try
            {
                var capResp = await visionApi.CalibrationCaptureAsync(cameraId, capReq);
                sw.Stop();
                if (capResp.Success)
                {
                    AddLog($"[CAL] [{stepNum}/{totalSteps}] {label} 캡처 OK ({sw.ElapsedMilliseconds}ms) file={capResp.Filename}");
                    positions.Add(recordTrav ? curBay : curLev);
                }
                else
                {
                    AddLog($"[CAL-ERR] [{stepNum}/{totalSteps}] {label} 캡처 실패 ({sw.ElapsedMilliseconds}ms): {capResp.Error}");
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                AddLog($"[CAL-ERR] [{stepNum}/{totalSteps}] {label} 캡처 예외 ({sw.ElapsedMilliseconds}ms): {ex.Message}");
            }
        }

        private async Task<bool> Phase1_5_CalibrationAsync(string cameraId, List<(int row, int bay, int lev)> targets, CancellationToken ct, bool forceRecalibrate = false)
        {
            // 1. 기존 캘리브레이션 상태 확인 (강제 재캘리브레이션 시 스킵 안 함)
            AddLog("[CAL] Calibration 상태 확인...");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var status = await visionApi.CalibrationStatusAsync(cameraId);
                sw.Stop();
                AddLog($"[CAL] status 응답 ({sw.ElapsedMilliseconds}ms) exists={status.CalibrationExists} offset_x={status.ZeroOffsetXMm}mm offset_y={status.ZeroOffsetYMm}mm");

                if (status.CalibrationExists && !forceRecalibrate)
                {
                    AddLog($"[CAL] 기존 캘리브레이션 사용 (생성: {status.CreatedAt ?? "N/A"})");
                    return true;  // 이미 있으면 스킵 (Auto Start 흐름)
                }
                if (status.CalibrationExists && forceRecalibrate)
                {
                    AddLog($"[CAL] 기존 캘리브레이션 존재(생성: {status.CreatedAt ?? "N/A"}) — 강제 재캘리브레이션 진행");
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                AddLog($"[CAL] status 조회 실패 ({sw.ElapsedMilliseconds}ms): {ex.Message}");
            }

            ct.ThrowIfCancellationRequested();

            // 2. 기준 위치 결정 — 현재 SRM 위치 사용 (셀 좌표 무시)
            //    사용자가 SRM을 원하는 위치로 미리 옮겨놓고 CALIB 시작하면 그 자리 기준 ±15mm
            //    이렇게 하면 lift floor(1000mm) 미만으로 강제 이동할 필요 자체가 없음
            var (refRow, refBay, refLev) = targets[0]; // capReq의 셀 메타데이터용으로만 사용
            var info = gClass.str.SrmInfo[gClass.srmNum];

            int refBayMm = CurTrav;
            int refLevMm = CurLift;
            AddLog($"[CAL] 기준 위치 = 현재 SRM 위치 trav={refBayMm}mm lift={refLevMm}mm (셀 Row{refRow}/Bay{refBay}/Lev{refLev} 메타데이터로만 사용)");

            // 3. 세팅 이미지 정리
            AddLog("[CAL] 이전 세팅 이미지 정리...");
            try
            {
                var cleanup = await visionApi.CalibrationCleanupAsync(cameraId);
                AddLog($"[CAL] cleanup: deleted={cleanup.DeletedCount}");
            }
            catch (Exception ex)
            {
                AddLog($"[CAL] cleanup 실패 (무시): {ex.Message}");
            }

            // 3.5 라이다 스캔 — 캘리브레이션 과정에 포함 (2026-07-02 사용자 요청). 기준 위치(정지 상태)에서 1회.
            //   실패해도 카메라 캘리브(스윕/compute)는 라이다 데이터를 쓰지 않으므로 경고만 남기고 계속 진행.
            ct.ThrowIfCancellationRequested();
            SetStatus("CAL-LIDAR", ClrCalib);
            if (!await RunLidarScanAsync("CALIB", ct))
                AddLog("[CAL][WARN] 라이다 스캔 실패 — 카메라 캘리브레이션(스윕)은 계속 진행");

            // 4. 주행(X) ±15mm + 승강(Z) ±15mm 캡처 (5mm 간격, 각 7회)
            // vision API compute가 X축 7장(±15,±10,±5,0) + Y축 7장(±15,±10,±5,0) 별도 요구
            int[] xOffsets = { -15, -10, -5, 0, 5, 10, 15 };
            int[] zOffsets = { -15, -10, -5, 0, 5, 10, 15 };
            var bayPositions = new List<int>();
            int totalSteps = xOffsets.Length + zOffsets.Length; // 7 + 7 = 14 (X·Z 둘 다 0 포함 대칭)
            int stepNum = 0;

            AddLog($"[CAL] ── 주행(X) ±15mm 캡처 시작 (5mm간격, 기준 trav={refBayMm}mm, 고정 lift={refLevMm}mm) ──");

            for (int idx = 0; idx < xOffsets.Length; idx++)
            {
                ct.ThrowIfCancellationRequested();
                stepNum++;

                int offset = xOffsets[idx];
                int targetBayMm = refBayMm + offset;
                string label = offset == 0 ? "기준" : $"주행{(offset > 0 ? "+" : "")}{offset}mm";

                AddLog($"[CAL] [{stepNum}/{totalSteps}] {label} → trav={targetBayMm}mm lift={refLevMm}mm");
                SetStatus($"CAL-X {label}", ClrCalib);

                // 공통 스윕(이동→정밀위치 대기→Busy해제→캡처). X는 trav를 움직이고 측정 trav를 기록.
                await CalibSweepPointAsync(cameraId, targetBayMm, refLevMm, label, stepNum, totalSteps,
                    refRow, refBay, refLev, bayPositions, recordTrav: true, sw, ct);
            }

            // 5. 승강(Z) ±15mm 캡처 (5mm 간격): -15, -10, -5, 0, +5, +10, +15 = 7회 (0 포함, X 스윕과 대칭 — 비전 회귀에 정상 입력)
            // MoveViaMaintAsync가 내부에서 X→Z 분리하므로 별도 복귀 불필요
            var levelPositions = new List<int>();
            AddLog($"[CAL] ── 승강(Z) ±15mm 캡처 시작 (5mm간격, 고정 trav={refBayMm}mm, 기준 lift={refLevMm}mm) ──");

            for (int idx = 0; idx < zOffsets.Length; idx++)
            {
                ct.ThrowIfCancellationRequested();
                stepNum++;

                int offset = zOffsets[idx];
                int targetLevMm = refLevMm + offset;
                string label = $"승강{(offset > 0 ? "+" : "")}{offset}mm";

                AddLog($"[CAL] [{stepNum}/{totalSteps}] {label} → trav={refBayMm}mm lift={targetLevMm}mm (셋업 0x59 mm 이동)");
                SetStatus($"CAL-Z {label}", ClrCalib);

                // 공통 스윕. Z는 lift를 움직이고 측정 lift를 기록.
                await CalibSweepPointAsync(cameraId, refBayMm, targetLevMm, label, stepNum, totalSteps,
                    refRow, refBay, refLev, levelPositions, recordTrav: false, sw, ct);
            }

            // 6. 검증
            AddLog($"[CAL] 캡처 결과: 주행(X) {bayPositions.Count}/7, 승강(Z) {levelPositions.Count}/7");
            if (bayPositions.Count < 3)
            {
                AddLog($"[CAL-ERR] 주행 캡처 {bayPositions.Count}개 — 최소 3개 필요, calibration 실패");
                return false;
            }
            if (levelPositions.Count < 3)
            {
                AddLog($"[CAL-ERR] 승강 캡처 {levelPositions.Count}개 — 최소 3개 필요, calibration 실패");
                return false;
            }

            ct.ThrowIfCancellationRequested();

            // 7. compute — 회귀·zero offset 산출
            AddLog($"[CAL] compute 요청 (주행 {bayPositions.Count}개, 승강 {levelPositions.Count}개)...");
            SetStatus("CAL COMPUTE", ClrCalib);
            sw.Restart();
            try
            {
                var compReq = new CalibrationComputeRequest
                {
                    CameraId = cameraId,
                    Row = refRow, Bay = refBay, Level = refLev,
                    ReferenceBayPos = refBayMm,
                    BayPositions = bayPositions.ToArray(),
                    ReferenceLevelPos = refLevMm,
                    LevelPositions = levelPositions.ToArray()
                };
                var compResp = await visionApi.CalibrationComputeAsync(compReq);
                sw.Stop();

                if (compResp.Success)
                {
                    AddLog($"[CAL] compute OK ({sw.ElapsedMilliseconds}ms)");
                    AddLog($"[CAL]   offset_x={compResp.ZeroOffsetXMm}mm  offset_y={compResp.ZeroOffsetYMm}mm");
                    AddLog($"[CAL]   R²_x={compResp.RegressionXR2:F4}  R²_y={compResp.RegressionYR2:F4}");
                    AddLog($"[CAL]   samples: x={compResp.SamplesX} y={compResp.SamplesY}");
                    AddLog($"[CAL]   saved → {compResp.SavedPath}");
                    return true;
                }
                else
                {
                    AddLog($"[CAL-ERR] compute 실패 ({sw.ElapsedMilliseconds}ms): {compResp.Error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                AddLog($"[CAL-ERR] compute 예외 ({sw.ElapsedMilliseconds}ms): {ex.Message}");
                return false;
            }
        }

        // ================================================================
        // Phase 2: 셀 반복 (이동 → 캡처 → 주행추론 → 승강추론)
        // ================================================================

        private async Task Phase2_TeachingLoopAsync(string cameraId, List<(int row, int bay, int lev)> targets, CancellationToken ct)
        {
            var totalSw = System.Diagnostics.Stopwatch.StartNew();

            // ★ 셀 위치 0x94 재조회 — Vexi에서 변경한 셀 정위치 반영 (앱 시작 캐시 무효화)
            await RefreshCellPositionsAsync(ct);

            // [위치 sanity 가드] 런 시작 1회 — 유효 위치 범위 계산 (SL 우선, 실패 시 그리드±margin)
            await LoadPositionGuardEnvelopeAsync(targets, ct);

            // [하드게이트] 안전입력 트립이면 자동모드/Start ON 무장 자체를 하지 않고 중단.
            if (IsSafetyTripped(out string safetyReason))
            {
                AddLog($"[ERR] ★지상반 안전입력 트립 — 티칭 중단(무장 안 함): {safetyReason}");
                return;
            }

            // 본 루프는 반자동 이동(0x41)으로 타겟 셀 이동 — 셋업모드/0x59 토글 불필요(빠름).
            // 1단계(셋업/0x59) → 2단계(반자동) 모드를 최대한 자동으로 전환한다.
            //   반자동 이동(0x41) 실행 조건(PageSemiAuto 기준): 장비 자동모드 + 지상반 반자동(gcpTxMode==2) + 시작 ON.
            {
                int sn = gClass.srmNum;
                // a) 지상반 반자동(gcpTxMode=2) 강제 (물리 키가 반자동이 아니면 MainWindow가 되돌릴 수 있음)
                gClass.str.SrmState[sn].gcpState.gcpTxMode = 2;
                // b) 장비 자동모드 진입 (셋업 등에서 빠져나옴 — 반자동 잡은 자동모드에서 실행)
                if (gClass.str.SrmState[sn].autoMode == 0)
                {
                    AddLog("[MODE] 장비 자동모드 진입 시도 (반자동 이동 실행 조건)");
                    if (!await SetCraneModeAsync(2, 5000))
                        AddLog("[MODE][WARN] 자동모드 진입 실패 — 홈위치/알람/작업잔류 확인 필요할 수 있음");
                    await Task.Delay(300, ct);
                }
                // c) 시작(Start) ON (0x50)
                if (gClass.str.SrmState[sn].dSt1StartSt == 0)
                {
                    AddLog("[MODE] 시작(Start) ON 송신 (0x50)");
                    var spkt = gClass.str.SrmPacket[sn];
                    spkt.startCmd = 1; spkt.startOnOff = 1;
                    gClass.str.SrmPacket[sn] = spkt;
                    await WaitFlagAsync(() => gClass.str.SrmState[sn].dSt1StartSt > 0, 5000, ct);
                }
                // d) 최종 확인 — 안 되면 안내 후 중단
                var s = gClass.str.SrmState[sn];
                if (s.autoMode == 0 || s.gcpState.gcpTxMode != 2 || s.dSt1StartSt == 0)
                {
                    AddLog($"[ERR] 반자동 준비 실패 (자동모드={s.autoMode}, gcpTxMode={s.gcpState.gcpTxMode}, 시작={s.dSt1StartSt}) — 지상반 키=반자동 + 자동모드 + 시작 ON 확인 후 재실행. 중단");
                    return;
                }
                AddLog($"[INIT] 반자동 자동전환 완료 (자동모드={s.autoMode}, gcpTxMode={s.gcpState.gcpTxMode}, 시작={s.dSt1StartSt}) — 셋업 불필요");
            }

            // ★ 셀 그리드 원본 백업 (트랜잭션 begin) — 방금 0x94로 재조회한 실제 SRM 그리드를 파일에 저장.
            //   여기 이후에 셀을 SRM에 쓰더라도(0x95), 런이 비정상 종료되면 원본으로 되돌릴 수 있다.
            //   정상 종료 시 아래에서 Pending=0 으로 commit(백업 폐기) → 좋은 결과가 부팅 시 되돌려지지 않음.
            {
                var bInfo = gClass.str.SrmInfo[gClass.srmNum];
                // 이전 런이 commit 못한 채(=Pending=1) 다시 시작한 경우: 기존 원본 백업을 보존(덮어쓰기 금지).
                // 그렇지 않으면 중단된 런의 부분 수정값이 새 "원본"으로 굳어져 rollback 불가능해진다.
                bool alreadyPending = cIniAccess.Read(MmBackupIniPath, "MM_BACKUP", "Pending", "nowrite") == "1";
                if (alreadyPending)
                {
                    AddLog("[BACKUP] 이전 미완료(rollback 대기) 백업 유지 — 새 백업 생략");
                }
                else if (bInfo.cellBay != null && bInfo.cellBay.Length > 0 && bInfo.cellLev != null && bInfo.cellLev.Length > 0)
                {
                    AddLog("[BACKUP] 셀 그리드 원본 백업 (중단/크래시 시 [CAL 복구]·부팅 자동복구로 rollback)");
                    BackupCellArrays();
                }
                else
                {
                    AddLog("[BACKUP][WARN] 셀 배열 미로드 — 원본 백업 건너뜀 (rollback 불가)");
                }
            }

            // 서킷브레이커 리셋 기준점 — 운영자가 '계속'을 선택한 지점. 그 이전 실패는 다시 세지 않는다(즉시 재트리거 방지).
            int cbFloor = 0;

            // ===== 파이프라인 상태 (깊이 1): 직전 셀의 추론 태스크 — harvest가 유일한 지연 기록 지점 =====
            //   추론은 Task.Run 없이 await하지 않는 async 호출로 실행(디스패처 인터리빙) — 공유 상태 전부 UI 스레드 유지.
            //   라이다 측정값이 서버측 단일 슬롯(120s)이라 깊이 1이 상한: 스캔(i)은 반드시 Z추론(i-1) 소비 후.
            Task<TeachingResult> pendingTask = null;
            int pendingIdx = -1;
            string pendingName = null;
            System.Diagnostics.Stopwatch pendingSw = null;
            bool moveIssued = false;   // 현재 셀 이동이 이전 셀 출발 게이트에서 이미 발행됐는가

            // 직전 셀 추론 회수(harvest) — 결과 Add·표시. 셀 i의 어떤 기록보다 먼저 호출해 순서 보장(ZR CSV = 순서 기반).
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
                UpdateRunCounts(targets.Count);   // ★ 대시보드 OK/FAIL/ETA는 harvest 시점 갱신(마지막 셀 누락 방지). 현재셀·진행바는 루프top 담당.
            }

            // 연속실패 서킷브레이커 평가 — recentFail>=Max면 모달, '중단'이면 true 반환(호출부가 break).
            //   ★ 도착 경로뿐 아니라 이동실패(스톨/타임아웃)·안전트립·인덱스불량 continue 경로에서도 호출해야
            //     크레인 결함 시 조기중단이 동작한다. 이 지점들은 크레인 정지 상태(도착/스톨/타임아웃/트립/미이동)라
            //     모달의 '크레인 정지' 전제가 성립. (SKIP은 의도 조작이라 호출부에서 제외 — 크레인 이동 중일 수 있음)
            async Task<bool> CircuitBreakerAbortAsync()
            {
                int recentFail = 0;
                for (int k = currentResults.Count - 1; k >= cbFloor; k--)
                {
                    if (currentResults[k].Success) break;
                    recentFail++;
                }
                if (recentFail < MaxConsecutiveCellFail) return false;

                AddLog($"[PAUSE] 최근 {MaxConsecutiveCellFail}셀 연속 실패 — 운영자 확인 대기 (크레인 정지 상태)");
                SetStatus("PAUSE", ClrErr);
                var dec = Dispatcher.Invoke(() => MessageBox.Show(
                    $"최근 {MaxConsecutiveCellFail}셀 연속 실패.\n비전 서버 / 크레인 상태를 확인하세요.\n\n[예] 계속 진행   [아니오] 런 중단",
                    "Auto Teaching - 연속 실패", MessageBoxButton.YesNo, MessageBoxImage.Warning));
                if (dec == MessageBoxResult.Yes)
                {
                    cbFloor = currentResults.Count;   // 지금까지의 실패는 확인 처리 → 카운터 리셋 (즉시 재트리거 방지)
                    AddLog("[RESUME] 운영자 계속 선택 — 연속실패 카운터 리셋");
                    return false;
                }
                AddLog("[ABORT] 운영자 중단 선택 — 남은 셀 건너뜀");
                return true;
            }

            try
            {
            for (int i = 0; i < targets.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                // M3.3: 셀 경계 협조적 PAUSE — 이동 발행 전에만 대기(출발 게이트가 보류한 발행은 아래 IssueSemiAutoMove에서).
                //   STOP은 기존 cts.Cancel()로 ct가 취소되어 Task.Delay(ct)에서 즉시 탈출.
                while (pauseRequested && !ct.IsCancellationRequested)
                {
                    SetStatus("PAUSE", ClrWarn);
                    await Task.Delay(200, ct);
                }

                // 현재 셀용 SKIP 플래그 리셋 (이전 셀의 SKIP가 새 셀로 새지 않도록)
                skipRequested = false;

                // (연속실패 서킷브레이커는 도착·harvest 직후로 이동 — 파이프라인에서 셀 결과가 확정되는 지점이 그곳)

                curTargetIdx = i;
                var (row, bay, lev) = targets[i];
                string cellName = CellKey(row, bay, lev);
                UpdateProgress(i, targets.Count, cellName);

                // M3.2: 셀 시작(이동 단계) — 표시 전용. 파이프라인에서 셀 구간이 겹치므로 셀 로컬 스톱워치 사용.
                var cellSw = System.Diagnostics.Stopwatch.StartNew();
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
                    await HarvestAsync();   // 순서 보장: 셀 i-1 결과 먼저
                    currentResults.Add(new TeachingResult { Row = row, Bay = bay, Level = lev, Success = false, Error = "cell index out of range", FailedStep = "move" });
                    moveIssued = false;
                    if (await CircuitBreakerAbortAsync()) break;
                    continue;
                }
                int targetTravMm = info.cellBay[bay - 1];
                int targetLiftMm = info.cellLev[lev - 1];
                AddLog($"[MOVE] Crane → {cellName} (Row={row} Bay={bay} Lev={lev}) → TRAV={targetTravMm}mm LIFT={targetLiftMm}mm");

                // 이동 발행 — 이전 셀 출발 게이트에서 이미 발행됐으면 생략. (0x41 반자동, 셋업/0x59 토글 불필요)
                // (캘리브레이션·1000mm 미만 캡처는 여전히 0x59 MoveViaMaintAsync 사용)
                if (!moveIssued && !IssueSemiAutoMove(row, bay, lev, targetTravMm, targetLiftMm))
                {
                    await HarvestAsync();   // 순서 보장: 셀 i-1 결과 먼저
                    currentResults.Add(new TeachingResult { Row = row, Bay = bay, Level = lev, Success = false, Error = "safety tripped", FailedStep = "move" });
                    if (await CircuitBreakerAbortAsync()) break;
                    continue;
                }
                moveIssued = false;   // 소비 — 다음 셀 발행 여부는 출발 게이트가 다시 결정

                var moveSw = System.Diagnostics.Stopwatch.StartNew();
                bool movedOk = await WaitSemiAutoArrivalAsync(row, bay, lev, targetTravMm, targetLiftMm, ct);
                moveSw.Stop();
                // 이동실패 사유를 harvest await '전'에 캡처 — 아래 await가 양보하는 사이의 늦은 SKIP 클릭이
                //   진짜 stall/timeout을 SKIP로 오분류하지 않도록(브레이커 오억제 방지).
                bool movedFailWasSkip = !movedOk && skipRequested;

                // ★ harvest: 이동과 병렬로 돌던 직전 셀 추론 회수 — 라이다 스캔(서버 단일슬롯)·이번 셀 기록보다 반드시 먼저
                await HarvestAsync();

                int curBayPos = CurTrav;
                int curLevPos = CurLift;
                if (!movedOk)
                {
                    AddLog($"[ERR] {cellName} 이동 실패 — 다음 셀로");
                    currentResults.Add(new TeachingResult { Row = row, Bay = bay, Level = lev, Success = false, Error = "semi move failed", FailedStep = "move" });
                    // SKIP(운영자 의도 조작)은 '연속 실패'가 아니다 — 브레이커 대신 카운터 리셋(오탐 모달 방지).
                    //   또 SKIP 시엔 크레인이 유효 타겟으로 아직 이동 중일 수 있어 '크레인 정지' 전제도 안 맞음.
                    if (movedFailWasSkip)
                        cbFloor = currentResults.Count;
                    else if (await CircuitBreakerAbortAsync())
                        break;
                    continue;
                }
                AddLog($"[ARRIVED] {cellName} ({moveSw.ElapsedMilliseconds}ms) 현재위치 trav={curBayPos}mm lift={curLevPos}mm");
                SetStep("arrive");   // M3.2: 도착 단계 — 표시 전용

                // 비전 연속 실패 서킷브레이커 — harvest 직후(셀 i-1까지 확정된 결과 기준, 크레인 정지 상태).
                //   파이프라인이라 추론 확정이 1셀 뒤 → 발화가 최대 1셀 늦을 수 있음(수용).
                if (await CircuitBreakerAbortAsync()) break;

                // Step 2b: 위치 검증 — 목표위치와 현재위치 비교
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

                // Step 2c: SRM Busy 해제 대기 (주행+승강 동작상태 = 0)
                var busySw = System.Diagnostics.Stopwatch.StartNew();
                while (busySw.ElapsedMilliseconds < 10000 && !ct.IsCancellationRequested)
                {
                    byte trOper = gClass.str.SrmState[gClass.srmNum].trav.trSt1OperState;
                    byte liOper = gClass.str.SrmState[gClass.srmNum].lift.liSt1OperState;
                    if (trOper == 0 && liOper == 0) break;
                    if (busySw.ElapsedMilliseconds % 1000 < 55)   // 폴링 50ms에 맞춰 로그 가드 조정(초당 동일 횟수)
                        AddLog($"[WAIT] SRM Busy 대기 중 (trav={trOper} lift={liOper}) {busySw.ElapsedMilliseconds}ms");
                    await Task.Delay(50, ct);   // Busy 해제 감지 폴링(100→50ms) — 동작 동일, 해제를 더 빨리 인지
                }
                busySw.Stop();
                if (busySw.ElapsedMilliseconds >= 10000)
                    AddLog($"[WARN] SRM Busy 10초 타임아웃 — 캡처 진행");
                else if (busySw.ElapsedMilliseconds > 100)
                    AddLog($"[WAIT] SRM Busy 해제 ({busySw.ElapsedMilliseconds}ms)");

                // 정위치 신호 즉시 체크 (대기 없이 로그만)
                byte trOrg = gClass.str.SrmState[gClass.srmNum].trav.trSt1OriginPos;
                byte liOrg = gClass.str.SrmState[gClass.srmNum].lift.liSt1OriginPos;
                AddLog($"[ORIGIN] 정위치 신호 trav={trOrg} lift={liOrg} (curPos trav={curBayPos} lift={curLevPos})");

                // ★ 동시 진행 ①: settle(잔진동 안정화) ∥ 라이다 스캔 — SRM Busy OFF 후 시작.
                //   직전 셀 Z추론은 위 HarvestAsync에서 이미 측정값을 소비 완료 → 여기서 스캔해도 서버 슬롯 안 덮임.
                //   크레인 정지 중(출발 게이트 이전) 측정 — WhenAll로 두 태스크 모두 관찰(취소 시 미관찰 태스크 방지).
                int settleMs = GetCaptureSettleMs(lev);
                SetStep("shoot");   // M3.2: 촬영 단계 — 표시 전용
                AddLog($"[CAPTURE] SRM Busy OFF 후 안정화 {settleMs}ms ∥ 라이다 스캔 (Lev {lev})");
                Task settleTask = settleMs > 0 ? Task.Delay(settleMs, ct) : Task.CompletedTask;
                Task<bool> scanTask;
                if (lidarFallback)
                {
                    scanTask = Task.FromResult(false);   // 폴백: 스캔 생략 (InferCellAsync가 lidarFallback 분기로 처리)
                }
                else
                {
                    SetStatus("LIDAR", ClrInfo); SetStep("lidar");
                    scanTask = RunLidarScanAsync($"셀 R{row}-B{bay}-L{lev}", ct, verbose: false);
                }
                await Task.WhenAll(settleTask, scanTask);
                bool scanOk = !lidarFallback && scanTask.Result;
                if (!lidarFallback && !scanOk)
                    AddLog($"[WARN] {cellName} 라이다 스캔 실패 — 캡처·X추론은 진행, Z추론은 실패 처리 예정(크레인 출발 후 재스캔 불가)");

                // ★ 캡처 직전 위치 재읽기 — 안정화 대기 동안 크레인이 미세 정착/크리프할 수 있으므로,
                //   캡처 이미지의 실제 위치와 추론 기준(bay_pos/level_pos)을 일치시킨다.
                //   (비전: inferred = bay_pos + travel_move_mm 이므로 기준이 어긋나면 전 셀이 그 드리프트만큼 체계적 오차.
                //    이 스냅샷이 X·Z추론까지 동일 값으로 동결돼 전달된다 — 이동 중 추론에도 오염 없음.)
                curBayPos = CurTrav;
                curLevPos = CurLift;
                bool hasCargo = false;
                Dispatcher.Invoke(() => { hasCargo = chk_HasCargo.IsChecked == true; });

                // Step 3: 캡처 (크레인 정지 중 마지막 순차 구간 — 재시도 없음)
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
                    currentResults.Add(cap.Fail);   // harvest는 이미 완료 — 순서 보장
                    PushRecent(cap.Fail);
                    AddLog($"[FAIL] {cellName} 'capture' — 실패 기록(재시도 없음), 다음 셀로");
                    UpdateProgress(i + 1, targets.Count, cellName);
                    SetStep("");   // M3.2: 셀 종료 — 표시 전용
                    continue;
                }

                // ★ 동시 진행 ②: 셀 i의 X→Z 추론을 await 없이 시작 — 다음 셀 이동과 병렬.
                //   Task.Run 금지: 디스패처 컨텍스트 인터리빙으로만 병렬화(공유 상태 전부 UI 스레드 유지).
                //   결과 기록은 다음 도착 시 HarvestAsync에서 (마지막 셀은 루프 종료 후).
                SetStep("analyze");   // M3.2: 분석(추론) 단계 — 표시 전용
                pendingTask = InferCellAsync(cameraId, row, bay, lev, curBayPos, curLevPos, hasCargo, scanOk,
                                             cap.CapFile, cap.RawPath, cap.CalPath, ct);
                pendingIdx = i;
                pendingName = cellName;
                pendingSw = cellSw;
            }

            // 정상 종료(끝까지 or break) — 마지막 셀 추론 회수
            await HarvestAsync();
            }
            finally
            {
                // 취소·예외 경로에서도 in-flight 추론 회수 — 유령 태스크·결과 누락·조기 저장 방지.
                //   (ct 공유로 HTTP는 즉시 취소되고 InferCellAsync가 실패 결과로 변환해 곧 완료된다)
                if (pendingTask != null)
                {
                    try { await HarvestAsync(); }
                    catch (Exception hx) { AddLog($"[PIPE][WARN] 잔여 추론 회수 실패: {hx.Message}"); }
                }
                SetStep("");   // 단계 표시등 클리어 (성공 경로 마지막 셀은 다음 iteration이 없어 잔존하므로)
            }

            // ★ 마지막 셀 캡처/추론 중 STOP은 OCE가 루프 밖으로 안 나와 '정상 종료'로 오인된다(캡처는 흡수·추론은 실패변환).
            //   여기서 취소를 명시 확인해 commit(Pending=0)을 건너뛰고 catch(OCE)로 보내 rollback(Pending=1 유지)되게 한다.
            ct.ThrowIfCancellationRequested();

            totalSw.Stop();
            int okCnt = currentResults.Count(r => r.Success);
            int failCnt = currentResults.Count(r => !r.Success);
            UpdateRunCounts(targets.Count);   // 완결 지점 확정 — 마지막 셀이 비추론 단계(캡처/이동)에서 실패해도 대시보드 카운트 반영
            // [2026-07-08] 진행률 최종 확정 — 루프top UpdateRunStats(i)가 0-based라 마지막이 (N-1)/N에 멈추던 버그 수정.
            //   실제 처리 셀 수(currentResults.Count; 정상완주=N, 중단시=처리분)로 표시.
            Dispatcher.Invoke(() =>
            {
                lblRun_Progress.Content = $"{currentResults.Count} / {targets.Count}";
                progRun.Value = targets.Count > 0 ? (double)currentResults.Count / targets.Count * 100 : 0;
            });
            AddLog($"────────────────────────────────────────");
            AddLog($"[SUMMARY] 전체={targets.Count} 성공={okCnt} 실패={failCnt} 총소요={totalSw.Elapsed:mm\\:ss}");

            // ★ 트랜잭션 commit — 루프가 끝까지(취소/예외 아님) 돌았으므로 원본 백업 폐기.
            //   취소(ThrowIfCancellationRequested)·예외 시 이 줄에 도달하지 않아 Pending=1 유지
            //   → 다음 페이지 진입(부팅 자동복구) 또는 [CAL 복구] 버튼이 원본으로 rollback.
            cIniAccess.Write(MmBackupIniPath, "MM_BACKUP", "Pending", "0");
            AddLog("[BACKUP] 정상 종료 — 셀 원본 백업 commit (Pending=0)");
        }

        // ================================================================
        // 파이프라인 셀 처리: 캡처(정지 중) / X→Z 추론(이동과 병렬)
        // ================================================================

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

        /// <summary>
        /// 셀 1개의 X→Z 추론 — 캡처 완료 후 크레인이 다음 셀로 이동 중일 수 있다(파이프라인 동시 진행 ②).
        /// 위치(capBayPos/capLevPos)는 캡처 시점 스냅샷 동결값 — 이 메서드 안에서 CurTrav/CurLift를 읽지 않는다.
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

            // (SetStatus는 여기서 안 건다 — 이 메서드는 크레인 이동 중 백그라운드로 돌아, 상태표시는 루프의 크레인 단계가 우선)
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

            // 라이다 스캔은 도착 직후 settle과 병렬로 이미 완료 — 실패였으면 라이다-Z 불가(재스캔 불가: 크레인이 떠남).
            //   ※ 비전-Z 사용(visionHoistEnabled) 시엔 라이다 스캔 실패해도 비전 모듈로 시도 가능 → 게이트 완화.
            //     (리졸버 내부에서 라이다-Z는 lidar_missing으로 실패하고 비전-Z 결과를 채택)
            if (!scanOk && !visionHoistEnabled)
            {
                return new TeachingResult { Row = row, Bay = bay, Level = lev, BayPos = inferredBayPos, Success = false,
                    Error = "라이다 스캔 실패 (승강 추론 선행 필수)", HasCargo = hasCargo,
                    FailedStep = "z_inference", CaptureOk = true, XInferenceOk = true, FailedSubStep = "lidar_scan",
                    CapturedFile = capFile, RawPath = capRaw, CalibratedPath = capCal };
            }

            // Z추론 (X추론 완료 후 순차 — analysis.json 의존 + 라이다 저장값(120s) 사용). SetStatus는 크레인 우선이라 생략.
            var zReq = new CaptureRequest
            {
                Row = row, Bay = bay, Level = lev,
                BayPos = capBayPos, LevelPos = capLevPos,
                HasCargo = hasCargo
            };

            AddLog($"[TX] Z추론 시작");
            SetStep("z");   // 진행 단계 칩: 승강(Z) 추론 (파이프라인상 다음 셀 이동과 병렬이라 짧게 표시될 수 있음)
            HoistInferenceResponse zResp;
            var zSw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                zResp = await RequestHoistResolvedAsync(cameraId, zReq, ct);   // 라이다/비전 모듈 우선순위 채택(디폴트 라이다 단독)
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
                //   ★ LogLidarDiagAsync는 취소 시 OCE를 재던짐 → 이 메서드의 no-throw 계약(harvest가 신뢰)을 깨므로 반드시 삼킨다.
                //     진단 로그 누락은 무해; 취소 자체는 상위 루프의 await 지점에서 관측된다.
                if (zResp.FailedStep == "lidar_missing")
                {
                    try { await LogLidarDiagAsync(ct); }
                    catch (Exception dx) { AddLog($"[LIDAR][DIAG] 진단 생략({dx.GetType().Name})"); }
                }
                return new TeachingResult { Row = row, Bay = bay, Level = lev, BayPos = inferredBayPos, Success = false, Error = zResp.Error, HasCargo = hasCargo, FailedStep = "z_inference", CaptureOk = true, XInferenceOk = true, FailedSubStep = zResp.FailedStep,
                    CapturedFile = capFile, RawPath = capRaw, CalibratedPath = capCal };
            }

            int inferredLevPos = (int)zResp.InferredLevelPos;
            AddLog($"[RX] Z추론 OK (대기 {zSw.ElapsedMilliseconds}ms) module={zResp.Source} level_pos={inferredLevPos} hoist_move_mm={zResp.HoistMoveMm:F1} ({zResp.Message ?? "N/A"}) server_elapsed={zResp.ElapsedMs}ms");
            AddLog($"[COMPARE-Z] 요청level_pos={capLevPos} → 추론level_pos={inferredLevPos} (차이={capLevPos - inferredLevPos}mm)");

            Dispatcher.Invoke(() =>
            {
                lbl_curLift.Content = inferredLevPos.ToString();
            });

            return new TeachingResult
            {
                Row = row, Bay = bay, Level = lev,
                BayPos = inferredBayPos, LevelPos = inferredLevPos,
                Success = true, HasCargo = hasCargo,
                CaptureOk = true, XInferenceOk = true, ZInferenceOk = true,
                CapturedFile = capFile, RawPath = capRaw, CalibratedPath = capCal
            };
        }

        // ================================================================
        // Phase 3: 종료 (RTSP 해제 → 서버 종료)
        // ================================================================

        /// <summary>
        /// Phase3: forceDisconnect=true → RTSP 해제 (단일 셀 테스트 / 수동 해제)
        ///          forceDisconnect=false → RTSP 유지 (연속 테스트 시 재사용)
        /// </summary>
        private async Task Phase3_CleanupAsync(string cameraId, bool forceDisconnect)
        {
            AddLog("[CLEANUP] Phase3 정리 시작");
            if (forceDisconnect)
            {
                try
                {
                    AddLog($"[TX] POST /api/gc/cmd/{cameraId}/disconnect");
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var dcResp = await visionApi.DisconnectRtspAsync(cameraId);
                    sw.Stop();
                    AddLog($"[RX] RTSP disconnect {(dcResp.Success ? "OK" : "FAIL")} ({sw.ElapsedMilliseconds}ms)");
                    rtspConnected = false;
                }
                catch (Exception ex) { AddLog($"[WARN] RTSP disconnect 실패: {ex.Message}"); }
            }
            else
            {
                AddLog("[CLEANUP] RTSP 연결 유지 (연속 테스트용)");
            }
            AddLog("[CLEANUP] Phase3 정리 완료");
        }

        // ================================================================
        // 크레인 이동 (기존 TCP 프로토콜 활용)
        // ================================================================

        private void SendCraneMoveCommand(int row, int bay, int lev)
        {
            var packet = gClass.str.SrmPacket[gClass.srmNum];

            // 반자동 이동 명령 설정 (PageSemiAuto MOVE 기준)
            packet.reqWcsCodeFk1 = 1;           // 작업종류: 이동
            packet.reqJobCodeFk1 = 0x01;        // 전송코드: 이동
            packet.reqJobNoFk1 = 30000;         // 작업번호: 반자동 이동

            packet.reqJobStepFk1 = 0;
            packet.reqFromStFk1 = 0;
            packet.reqFromRowFk1 = 0;
            packet.reqFromBayFk1 = 0;
            packet.reqFromLevFk1 = 0;

            packet.reqToStFk1 = 0;
            packet.reqToRowFk1 = (byte)row;
            packet.reqToBayFk1 = (ushort)bay;
            packet.reqToLevFk1 = (byte)lev;

            // ★[2026-06-30 버그수정 — 크레인 무동작 진범] 0x41 이동명령(reqJobCode==0x01)의 TX는 To 좌표를
            //   reqTo*가 아니라 semiDest* 버퍼에서 읽는다(udpClientClass.cs:3340~3351 "이동 명령만 Dest 버퍼를 To 영역으로").
            //   수동 반자동은 Srm_JobEnableParse가 semiDest*=semiTo*로 채워주지만, 티칭은 그 경로를 안 타서
            //   semiDest*가 비어(stale)→크레인이 빈/엉뚱한 목적지를 받아 안 움직였음(reqTo*는 이동명령에선 무시됨).
            //   여기서 직접 채운다(이동작업은 To 좌표만 필요).
            packet.semiDestSt = 0;
            packet.semiDestRow = (byte)row;
            packet.semiDestBay = (ushort)bay;
            packet.semiDestLev = (byte)lev;

            packet.resMainCode = 0;
            packet.semiJobClicked = true;

            gClass.str.SrmPacket[gClass.srmNum] = packet;
        }

        /// <summary>
        /// 반자동 이동(0x41) 발행만 수행 — 안전트립 체크 + gcpTxMode=2 재강제 + semiDest* 기입.
        /// 파이프라인 출발 게이트에서 도착 대기 없이 호출된다. 트립이면 발행하지 않고 false.
        /// </summary>
        private bool IssueSemiAutoMove(int row, int bay, int lev, int targetTravMm, int targetLiftMm)
        {
            // [하드게이트] 안전입력 트립 시 0x41을 쏘지 않고 즉시 거부 (트립이면 MCU가 무응답 폐기 → 헛대기)
            if (IsSafetyTripped(out string safetyReason))
            {
                AddLog($"[SEMI][ERR] ★지상반 안전입력 트립 — 반자동 이동 거부: {safetyReason}");
                return false;
            }

            // 0x41 반자동 이동 송신 (reqJobNo=30000, 완료 시 SRM이 자동삭제)
            gClass.str.SrmState[gClass.srmNum].gcpState.gcpTxMode = 2;   // 반자동 강제 유지(MainWindow 키값 덮어쓰기 방지)
            SendCraneMoveCommand(row, bay, lev);
            AddLog($"[SEMI][0x41] 반자동 이동 송신 → R{row}-B{bay}-L{lev} (목표 trav={targetTravMm} lift={targetLiftMm})");
            return true;
        }

        /// <summary>
        /// 반자동 이동(0x41) 도착 대기 — 발행은 IssueSemiAutoMove가 담당.
        /// 전제: 지상반 반자동(gcpTxMode==2) + 시작 ON (Phase2 진입부에서 1회 확인).
        /// 도착 판정: (정위치 OR 반자동 작업완료) AND 목표 근처(±TOL).
        ///   - 정위치   : trav/lift OriginPos 둘 다 1
        ///   - 작업완료 : fork1.mvProcState==4(이동작업) 또는 procState==4(반송작업)
        ///   - 목표 근처: 이전 셀의 정위치/완료 잔류값을 도착으로 오인하는 것 방지
        /// </summary>
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
                await Task.Delay(50, ct);   // 도착 감지 폴링(100→50ms) — 동작 동일, 도착을 더 빨리 인지

                if (skipRequested)
                {
                    AddLog($"[SEMI][SKIP] 사용자 SKIP — R{row}-B{bay}-L{lev} 이동 중단");
                    ClearSemiJobFlag(srmNum);
                    return false;
                }

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
                    stopReason = "safety";                            // catch(OCE)에서 "사용자 취소"로 오기록되지 않도록
                    cts?.Cancel();                                    // 런 전체 중단 (기존 취소 경로 재사용)
                    ct.ThrowIfCancellationRequested();                // OCE 전파 → Btn_Start catch → Phase3 + finally DisarmCrane
                }
                int errT = Math.Abs(curT - targetTravMm), errL = Math.Abs(curL - targetLiftMm);
                bool near = errT <= TOL && errL <= TOL;
                bool pos = s.trav.trSt1OriginPos == 1 && s.lift.liSt1OriginPos == 1;
                bool jobDone = s.fork1.mvProcState == 4 || s.fork1.procState == 4;

                if (Math.Abs(curT - lastT) >= 50 || Math.Abs(curL - lastL) >= 50)
                {
                    AddLog($"[SEMI][MOVE] TRAV={curT}mm(Δ{errT}) LIFT={curL}mm(Δ{errL}) 정위치={pos} mvProc={s.fork1.mvProcState}");
                    lastT = curT; lastL = curL;
                }

                // 도착: (정위치 OR 작업완료) + 목표 근처(잔류값 오인 방지)
                if ((pos || jobDone) && near)
                {
                    AddLog($"[SEMI][ARRIVE] R{row}-B{bay}-L{lev} ({sw.ElapsedMilliseconds}ms) 정위치={pos} 작업완료={jobDone} trav={curT}(Δ{errT}) lift={curL}(Δ{errL})");
                    ClearSemiJobFlag(srmNum);
                    return true;
                }

                // stall 감지: 위치가 계속 바뀌면 기준 갱신, 멈춰있고 아직 목표 근처도 아니면 STALL_MS 후 조기 중단
                if (Math.Abs(curT - stRefT) >= 2 || Math.Abs(curL - stRefL) >= 2)
                {
                    stRefT = curT; stRefL = curL; lastProgMs = sw.ElapsedMilliseconds;
                }
                else if (!near && sw.ElapsedMilliseconds - lastProgMs > STALL_MS)
                {
                    AddLog($"[SEMI][ERR] {STALL_MS / 1000}s간 위치 변화 없음 + 미도착 — 크레인 미동작(안전트립/모드/시작OFF) 의심, 조기 중단 trav={curT} lift={curL}");
                    ClearSemiJobFlag(srmNum);
                    return false;
                }
            }
            AddLog($"[SEMI][ERR] 반자동 이동 타임아웃/취소 ({sw.ElapsedMilliseconds}ms) — 지상반 키(반자동)/시작 ON/알람 확인");
            ClearSemiJobFlag(srmNum);
            return false;
        }

        /// <summary>반자동(0x41) 잔류 송신 플래그(semiJobClicked) 정리.</summary>
        private void ClearSemiJobFlag(int srmNum)
        {
            var pkt = gClass.str.SrmPacket[srmNum];
            pkt.semiJobClicked = false;
            gClass.str.SrmPacket[srmNum] = pkt;
        }

        // [위치 sanity 가드] 런 시작 1회: 소프트리밋(0xA3/A5)으로 유효 주행/승강 범위를 잡는다.
        //   읽기 성공 → SL Home~End. 실패 → 이번 런 타겟 셀 그리드 min~max ± GUARD_FALLBACK_MARGIN_MM.
        //   가드(WaitSemiAutoArrivalAsync)는 이 envelope만 비교한다.
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
                    envLiftLo = BitConverter.ToInt32(ld, 179) - GUARD_LIFT_LOW_MARGIN_MM;
                    envLiftHi = BitConverter.ToInt32(ld, 183);
                    if (envTravHi > envTravLo && envLiftHi > envLiftLo)
                    {
                        envReady = true;
                        AddLog($"[SAFETY] 소프트리밋 로드 — 주행 {envTravLo}~{envTravHi}mm, 승강 {envLiftLo}~{envLiftHi}mm (승강하한 SL-{GUARD_LIFT_LOW_MARGIN_MM}mm 여유 포함, 위치 가드 기준)");
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

        /// <summary>
        /// 0x59 보수위치 이동: 셋업모드 전제. 0xA3/A5 읽고 → 0xA4/A6에 Maintance_Position만 변경하여
        /// 쓴 뒤 → 0x59 명령으로 이동시키고 curPos 모니터링으로 도착을 확인한다.
        /// 셋업모드 진입은 호출자가 보장한다 (한 번만 전환).
        /// </summary>
        // 드라이브/승강 파라미터 캐시 (0xA3/0xA5 반복 읽기 방지)
        private byte[] cachedDriveParam = null;
        private byte[] cachedLiftParam = null;

        /// <summary>0xA3/0xA5 파라미터에서 속도 그룹 값을 로그에 출력</summary>
        private void LogSpeedParams(string label, byte[] param)
        {
            if (param == null || param.Length < 176) return;
            string[] names = { "자동정방", "자동역방", "자동정방2", "수동역방", "수동정방", "경감속", "크리프", "정위치확인" };
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[SPEED] {label} 속도 파라미터:");
            for (int g = 0; g < names.Length && g * 8 + 7 < param.Length; g++)
            {
                int off = g * 8;
                ushort vel = BitConverter.ToUInt16(param, off);
                ushort acc = BitConverter.ToUInt16(param, off + 2);
                ushort dec = BitConverter.ToUInt16(param, off + 4);
                ushort jrk = BitConverter.ToUInt16(param, off + 6);
                sb.Append($"  {names[g]}: 속도={vel}m/min 가속={acc}mm/s² 감속={dec}mm/s² 저크={jrk}ms");
                if (g < names.Length - 1) sb.AppendLine();
            }
            AddLog(sb.ToString());
        }

        private string UdpGap()
        {
            var last = gClass.str.SrmPacket[gClass.srmNum].lastUdpReceiveTime;
            if (last == DateTime.MinValue) return "UDP=없음";
            return $"UDP갭={(DateTime.Now - last).TotalMilliseconds:F0}ms";
        }

        private async Task<bool> WaitSrmIdleAsync(int timeoutMs, CancellationToken ct)
        {
            int srmNum = gClass.srmNum;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs && !ct.IsCancellationRequested)
            {
                byte trOper = gClass.str.SrmState[srmNum].trav.trSt1OperState;
                byte liOper = gClass.str.SrmState[srmNum].lift.liSt1OperState;
                if (trOper == 0 && liOper == 0) return true;
                if (sw.ElapsedMilliseconds % 1000 < 110)
                    AddLog($"[IDLE] SRM 동작 대기 중 (trav={trOper} lift={liOper}) {sw.ElapsedMilliseconds}ms");
                await Task.Delay(100, ct);
            }
            AddLog($"[IDLE][ERR] SRM Idle 대기 타임아웃 ({timeoutMs}ms)");
            return false;
        }

        /// <summary>
        /// 0x94 셀 위치 재조회 — SRM(Vexi) 최신 셀 정위치를 info.cellBay/cellLev에 갱신.
        /// 오토티칭 시작 전 호출해야 Vexi에서 변경한 셀 위치가 반영됨 (앱 시작 시 로드한 Rack.ini 캐시 무효화).
        /// </summary>
        private async Task RefreshCellPositionsAsync(CancellationToken ct)
        {
            int srmNum = gClass.srmNum;
            var info = gClass.str.SrmInfo[srmNum];
            int beforeBay0 = (info.cellBay != null && info.cellBay.Length > 0) ? info.cellBay[0] : -1;
            int beforeLev0 = (info.cellLev != null && info.cellLev.Length > 0) ? info.cellLev[0] : -1;
            AddLog($"[CELL] 셀 위치 0x94 재조회 시작 (Vexi 최신값 반영) — 기존 cellBay[0]={beforeBay0}mm cellLev[0]={beforeLev0}mm");

            var pkt = gClass.str.SrmPacket[srmNum];
            pkt.rackRequest = true;
            pkt.rackReqType = 1;       // BAY부터 (응답 후 자동 LEV 요청)
            pkt.rackReqCount = 255;
            gClass.str.SrmPacket[srmNum] = pkt;

            // BAY 응답 → 자동 LEV 요청 → LEV 응답까지 대기 (rackRequest false 되거나 최대 3초)
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 3000 && !ct.IsCancellationRequested)
            {
                await Task.Delay(100, ct);
                // rackRequest가 false면 BAY+LEV 둘 다 송신 완료된 것 (LEV 응답은 약간 더 대기)
                if (!gClass.str.SrmPacket[srmNum].rackRequest && sw.ElapsedMilliseconds > 800)
                    break;
            }
            await Task.Delay(500, ct); // LEV 응답 안정화

            int afterBay0 = (info.cellBay != null && info.cellBay.Length > 0) ? info.cellBay[0] : -1;
            int afterLev0 = (info.cellLev != null && info.cellLev.Length > 0) ? info.cellLev[0] : -1;
            AddLog($"[CELL] 재조회 완료 ({sw.ElapsedMilliseconds}ms) — cellBay[0] {beforeBay0}→{afterBay0}mm, cellLev[0] {beforeLev0}→{afterLev0}mm");
        }

        private async Task<bool> WaitExactPositionAsync(int targetTrav, int targetLift, int timeoutMs, CancellationToken ct, int tolMm = 1)
        {
            // tolMm: 허용 오차 (기본 ±1mm — 인코더 떨림 허용)
            int srmNum = gClass.srmNum;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs && !ct.IsCancellationRequested)
            {
                int curTrav = CurTrav;
                int curLift = CurLift;
                int errT = Math.Abs(curTrav - targetTrav);
                int errL = Math.Abs(curLift - targetLift);
                if (errT <= tolMm && errL <= tolMm) return true;
                if (sw.ElapsedMilliseconds % 2000 < 110)
                    AddLog($"[POS] 정밀 위치 대기 trav={curTrav}(err={errT}) lift={curLift}(err={errL}) tol=±{tolMm} {sw.ElapsedMilliseconds}ms");
                await Task.Delay(100, ct);
            }
            int finalTrav = CurTrav;
            int finalLift = CurLift;
            AddLog($"[POS][ERR] 정밀 위치 타임아웃 trav={finalTrav}(목표={targetTrav}) lift={finalLift}(목표={targetLift}) tol=±{tolMm}");
            return false;
        }

        private async Task<bool> MoveViaMaintAsync(int travMm, int liftMm, CancellationToken ct)
        {
            int curTrav = CurTrav;
            int curLift = CurLift;

            // [2026-07-08] 2축 동시 이동으로 단일화 — 펌웨어 소스 확인 결과 0x59는 한 워크아이템에 주행·승강
            //   목표를 함께 받아 동시 구동한다(dev_SRM.c SRM_Move_Maintanence_Cmd → Store_Work_Data(to_tpos, to_lpos)).
            //   기존 "두 축 동시 이동 거부" 분리 로직은 구펌웨어의 승강범위 거부(result=8)를 오인한 것으로 판단해 제거.
            //   (0xA4/0xA6 파라미터 '쓰기'는 Internal에서 여전히 순차 — 동시 쓰기 충돌 주의는 유지)
            if (curTrav != travMm && curLift != liftMm)
                AddLog($"[MAINT] 2축 동시 이동: 주행 {curTrav}→{travMm}mm + 승강 {curLift}→{liftMm}mm (단일 0x59)");
            return await MoveViaMaintInternalAsync(travMm, liftMm, ct);
        }

        /// <summary>
        /// 지상반 안전 입력(비상정지/안전플러그) 트립 여부. tripped=true면 이동을 금지해야 한다.
        /// GCP는 0x30 상태요청 data[11]에 EM_SW/SF_PLUG 입력이 false면 "트립=1"을 실어 보내고(CMD_Req_State, udpClientClass.cs:3120),
        /// MCU는 그 비트를 래치해 0x59/0x41/0x51을 ★무응답★으로 버린다(com_tml.c). DIO 미수신/통신불량(gcpDioClass.cs:294)도 트립으로 송신됨.
        /// → 트립 상태에서 이동을 쏘면 셀당 타임아웃까지 헛대기하므로, 쏘지 말고 즉시 거부한다(안전신호 위조 아님 — 위험하면 구동 거부).
        /// </summary>
        private bool IsSafetyTripped(out string reason)
        {
            int sn = gClass.srmNum;
            bool emOk = gClass.str.DioPacket[sn].DISET[(int)DISTATE.EM_SW].value;
            bool plugOk = gClass.str.DioPacket[sn].DISET[(int)DISTATE.SF_PLUG].value;
            if (emOk && plugOk) { reason = null; return false; }
            int dioUse = gClass.str.SrmInfo[sn].dioUse;
            bool rxAlive = gClass.str.SrmPacket[sn].rxDioComm;
            reason = $"비상정지={(emOk ? "정상" : "동작(입력OFF)")} 안전플러그={(plugOk ? "정상" : "해제(입력OFF)")} " +
                     $"(dioUse={dioUse} DIO수신={(rxAlive ? "OK" : "없음")}) — MCU가 0x59/0x41을 무응답 거부합니다. 지상반 배선/플러그 확인 필요";
            return true;
        }

        /// <summary>
        /// 런 종료/중단 시 크레인 무장 해제: 시작(Start) OFF(0x50) + 잔류 요청 플래그 클리어 + gcpTxMode 복원.
        /// Phase2가 자동모드+Start ON으로 무장하므로, 런이 끝나면 반드시 해제해 후속 호스트(WCS) 작업/잔류 신호에 의한 오동작을 막는다.
        /// </summary>
        private void DisarmCraneAfterRun(byte origGcpTxMode)
        {
            try
            {
                int sn = gClass.srmNum;
                // ★ in-place 필드쓰기(코드베이스 표준). 전체 struct copy-modify-write는 읽기~되쓰기 사이에
                //   UDP스레드가 갱신한 에러/통신 카운터 필드를 통째 revert(lost update)할 수 있어 직접 필드만 건드린다.
                gClass.str.SrmPacket[sn].startCmd = 1;
                gClass.str.SrmPacket[sn].startOnOff = 0;          // 0x50 시작 OFF
                gClass.str.SrmPacket[sn].semiJobClicked = false;  // 반자동(0x41) 잔류 송신 취소
                gClass.str.SrmPacket[sn].maintMoveReq = false;    // 0x59 잔류 요청 취소
                gClass.str.SrmState[sn].gcpState.gcpTxMode = origGcpTxMode;
                AddLog("[SAFE] 런 종료 — 시작(Start) OFF + 요청 플래그 클리어 (크레인 무장 해제)");
            }
            catch (Exception ex) { AddLog($"[SAFE][WARN] 무장 해제 중 예외: {ex.Message}"); }
        }

        /// <summary>
        /// 앱 종료 시(MainWindow.Window_Closing) ★통신 Close 직전★ 호출 — 진행 중 런 취소 + 크레인 무장해제(Start OFF) + RTSP 정리.
        /// 런 종료 finally는 통신이 끊긴 뒤라 신뢰성이 없으므로, 여기서 동기적으로 Start OFF 플래그를 세팅한다(실제 0x50 전송은 호출측이 잠깐 대기).
        /// </summary>
        public void AbortAndDisarmForShutdown()
        {
            stopReason = "shutdown";
            try { cts?.Cancel(); } catch { }
            try { mmCts?.Cancel(); } catch { }
            try
            {
                int sn = gClass.srmNum;
                gClass.str.SrmPacket[sn].startCmd = 1;
                gClass.str.SrmPacket[sn].startOnOff = 0;          // 0x50 시작 OFF
                gClass.str.SrmPacket[sn].semiJobClicked = false;
                gClass.str.SrmPacket[sn].maintMoveReq = false;
                gClass.str.SrmPacket[sn].isAutoTeaching = false;
                isRunning = false;
            }
            catch { }
            // 서버측 RTSP/ffmpeg 정리 — 베스트에포트(종료를 막지 않도록 백그라운드+짧은 타임아웃, 데드락 회피 위해 Task.Run)
            try
            {
                if (rtspConnected)
                    foreach (var cam in new[] { "camera1", "camera3" })
                        try { Task.Run(() => visionApi.DisconnectRtspAsync(cam)).Wait(1500); } catch { }
                rtspConnected = false;
            }
            catch { }
        }

        private async Task<bool> MoveViaMaintInternalAsync(int travMm, int liftMm, CancellationToken ct)
        {
            int srmNum = gClass.srmNum;

            // Step 0: [하드게이트] 지상반 비상정지/안전플러그 트립 시 0x59를 쏘지 않고 즉시 거부.
            //   트립 상태면 MCU가 어차피 0x59를 무응답 폐기 → 쏘면 120초 헛대기. 안전신호 위조(마스킹) 아님, 위험하면 구동 거부.
            if (IsSafetyTripped(out string safetyReason))
            {
                AddLog($"[MAINT][ERR] ★지상반 안전입력 트립 — 0x59 이동 거부: {safetyReason}");
                return false;
            }

            // Step 1(모드): 셋업 선진입 제거 — 2026-07-07 신펌웨어가 모드 제약(0xA4/0x59 셋업 요구)을 완화했는지
            //   실제 시도로 확인하기 위해, 현재 모드(오토/반자동/수동) 그대로 먼저 시도하고 거부 시에만 셋업 폴백한다(Step 2 뒤).
            //   ※ 펌웨어 분석 결과: "manual→setup 엣지에서만 인버터가 위치 수용"이라는 통설은 펌웨어에 근거 없음.

            // Step 2: 0xA3/0xA5 읽기 — 캐시 있으면 스킵, 없으면 병렬 읽기 (0xA3/A5 읽기는 모드 무관 — 셋업 전 수행 가능)
            if (cachedDriveParam == null || cachedLiftParam == null)
            {
                AddLog("[MAINT] 0xA3+0xA5 파라미터 병렬 읽기...");
                // 드라이브/리프트 요청을 동시에 띄워 병렬 대기 (각 헬퍼가 요청 플래그 세팅 후 완료 대기)
                var driveTask = ReadDriveParamAsync(176, 10000, ct);
                var liftTask = ReadLiftParamAsync(176, 10000, ct);
                bool[] readResults = await Task.WhenAll(driveTask, liftTask);
                if (!readResults[0]) { AddLog("[MAINT][ERR] 0xA3 Drive 읽기 타임아웃"); return false; }
                if (!readResults[1]) { AddLog("[MAINT][ERR] 0xA5 Lift 읽기 타임아웃"); return false; }

                cachedDriveParam = (byte[])gClass.str.SrmPacket[srmNum].driveParamData.Clone();
                cachedLiftParam = (byte[])gClass.str.SrmPacket[srmNum].liftParamData.Clone();

                // 현재 속도 파라미터 로그 (각 그룹: 속도(2)+가속(2)+감속(2)+저크(2) = 8bytes)
                LogSpeedParams("주행(0xA3)", cachedDriveParam);
                LogSpeedParams("승강(0xA5)", cachedLiftParam);
            }

            // [길이 가드] 고정 오프셋(132/136 읽기, 207 쓰기) 접근 전 캐시 길이 확인 — 짧은 프레임 수신 시 IndexOutOfRange 크래시 방지.
            if (cachedDriveParam.Length < 176 || cachedLiftParam.Length < 176)
            {
                AddLog($"[MAINT][ERR] 파라미터 길이 부족 (drive={cachedDriveParam.Length}B lift={cachedLiftParam.Length}B, 176B 필요) — 0x59 거부");
                return false;
            }
            // [MCU 4.4 펌웨어 버그 — 2026-07-07 사전'차단'→'경고 후 시도'로 완화] 구펌웨어 SRM_Move_Maintanence_Cmd()는
            //   "승강" 보수위치를 "주행" ManualOp 범위로 검증(dev_SRM.c 복붙 버그) → 범위 밖이면 result=8 거부(이동 없음 = 안전).
            //   신펌웨어가 이 버그를 고쳤는지 실제 응답으로 확인하기 위해 앱에서 막지 않는다. 구펌웨어여도 거부만 될 뿐 크레인은 움직이지 않는다.
            //   ★ 클램프(명령 위치를 조용히 바꿔 이동) 금지 원칙은 유지 — 값을 바꾸지 않고 그대로 보내서 성공/거부를 받는다.
            int travMoStart = BitConverter.ToInt32(cachedDriveParam, 132);
            int travMoEnd = BitConverter.ToInt32(cachedDriveParam, 136);
            if (liftMm < travMoStart || liftMm > travMoEnd)
                AddLog($"[MAINT][WARN] LIFT {liftMm}mm 가 주행 ManualOp 범위 [{travMoStart},{travMoEnd}] 밖 — " +
                       $"구펌웨어는 0x59 result=8로 거부(이동 없음). 신펌웨어 수정 확인을 위해 시도함.");

            // [2026-07-07] 모드 낙관 시도: 셋업 전환 없이 현재 모드에서 0xA4/0xA6+0x59를 먼저 시도.
            //   구펌웨어면 0xA4가 NACK(10=셋업모드 아님)로 즉시 거부(이동 없음 = 안전) → 셋업모드로 폴백해 재시도.
            //   단 0x59 result=8(승강범위 거부)은 모드와 무관한 검증(2026-06-30 셋업모드에서도 거부됨) → 셋업 폴백 무의미, 즉시 실패.
            if (gClass.str.SrmState[srmNum].setupMode == 0)
            {
                AddLog($"[MAINT] 셋업 전환 없이 시도 (현재 모드 유지: auto={gClass.str.SrmState[srmNum].autoMode} manual={gClass.str.SrmState[srmNum].manualMode}) — 거부 시 셋업모드 폴백");
                if (await MaintWriteAndMove59Async(travMm, liftMm, ct)) return true;
                ct.ThrowIfCancellationRequested();
                if (gClass.str.SrmPacket[srmNum].maintMoveDone && gClass.str.SrmPacket[srmNum].maintMoveResult == 8)
                {
                    AddLog("[MAINT] 0x59 result=8(승강 보수위치 범위 거부)은 모드 무관 — 셋업 폴백 생략, 실패 처리");
                    return false;
                }
                AddLog($"[MAINT] 현재 모드에서 거부/실패 — 셋업모드 폴백 진입 (setup=0 auto={gClass.str.SrmState[srmNum].autoMode})");
                if (!await SetCraneModeAsync(1, 5000))
                {
                    AddLog("[MAINT][ERR] 셋업모드 진입 실패 — 진행 중단 (알람/키/작업잔류 확인)");
                    return false;
                }
                await Task.Delay(150, ct);
            }
            return await MaintWriteAndMove59Async(travMm, liftMm, ct);
        }

        /// <summary>0xA4/0xA6(저속 속도그룹+보수위치) 쓰기 → 0x59 이동 → 도착 모니터링 1회 시도. 모드 전환 없음(호출측 책임).</summary>
        private async Task<bool> MaintWriteAndMove59Async(int travMm, int liftMm, CancellationToken ct)
        {
            int srmNum = gClass.srmNum;
            // 직전 이동의 stale 0x8059 결과가 호출측 폴백 판단(maintMoveResult==8)을 오염시키지 않도록 시도 시작 시 리셋
            gClass.str.SrmPacket[srmNum].maintMoveResult = 0xFF;
            gClass.str.SrmPacket[srmNum].maintMoveDone = false;

            // Step 3: 0xA4 Drive 쓰기 준비 (VER1 원본 CtrlFlag 사용 — 모든 속도 그룹 활성)
            byte[] driveCTRL = new byte[35 + cachedDriveParam.Length];
            driveCTRL[0] = 0x00; driveCTRL[1] = 0xFF; driveCTRL[2] = 0x07; driveCTRL[3] = 0xFF; driveCTRL[4] = 0x07;
            Array.Copy(cachedDriveParam, 0, driveCTRL, 35, cachedDriveParam.Length);
            // 모든 속도 그룹(8개: offset 0,8,16,24,32,40,48,56)을 저속으로 통일 (셋업/강제모드 보수위치 이동 = 저속 정밀)
            for (int g = 0; g < 8; g++)
                Buffer.BlockCopy(BitConverter.GetBytes(MaintMoveSpeedMpm), 0, driveCTRL, 35 + g * 8, 2);
            Buffer.BlockCopy(BitConverter.GetBytes((uint)travMm), 0, driveCTRL, 207, 4);
            AddLog($"[MAINT][SPEED] Drive CtrlFlag={driveCTRL[0]:X2}-{driveCTRL[1]:X2}-{driveCTRL[2]:X2}-{driveCTRL[3]:X2}-{driveCTRL[4]:X2} 전 속도그룹={MaintMoveSpeedMpm}m/min");

            // Step 3b: 0xA6 Lift 쓰기 준비 (VER1 원본 CtrlFlag 사용)
            byte[] liftCTRL = new byte[35 + cachedLiftParam.Length];
            liftCTRL[0] = 0x00; liftCTRL[1] = 0xFF; liftCTRL[2] = 0x1F; liftCTRL[3] = 0xFF; liftCTRL[4] = 0x0F;
            Array.Copy(cachedLiftParam, 0, liftCTRL, 35, cachedLiftParam.Length);
            // 모든 속도 그룹(8개)을 저속으로 통일 (셋업/강제모드 보수위치 이동 = 저속 정밀)
            for (int g = 0; g < 8; g++)
                Buffer.BlockCopy(BitConverter.GetBytes(MaintMoveSpeedMpm), 0, liftCTRL, 35 + g * 8, 2);
            Buffer.BlockCopy(BitConverter.GetBytes((uint)liftMm), 0, liftCTRL, 207, 4);
            AddLog($"[MAINT][SPEED] Lift CtrlFlag={liftCTRL[0]:X2}-{liftCTRL[1]:X2}-{liftCTRL[2]:X2}-{liftCTRL[3]:X2}-{liftCTRL[4]:X2} 전 속도그룹={MaintMoveSpeedMpm}m/min");

            // Step 4: 0xA4 + 0xA6 쓰기 (순차 — 동시 쓰기는 SRM 펌웨어에서 충돌 가능)
            AddLog($"[MAINT][0xA4] Drive 쓰기 시작 ({UdpGap()})");
            if (!await WriteDriveParamAsync(driveCTRL, 10000, ct))
            {
                AddLog("[MAINT][ERR] 0xA4 Drive 쓰기 타임아웃");
                return false;
            }
            AddLog($"[MAINT][0xA4] Drive 쓰기 완료 ({UdpGap()})");
            if (gClass.str.SrmPacket[srmNum].driveParamWriteResult != 0)
            {
                byte dr = gClass.str.SrmPacket[srmNum].driveParamWriteResult;
                AddLog($"[MAINT][ERR] 0xA4 Drive 쓰기 NACK result={dr}{(dr == 10 ? " (셋업모드 아님 — 펌웨어가 현재 모드 거부)" : "")}");
                return false;
            }

            AddLog($"[MAINT][0xA6] Lift 쓰기 시작 ({UdpGap()})");
            if (!await WriteLiftParamAsync(liftCTRL, 10000, ct))
            {
                AddLog("[MAINT][ERR] 0xA6 Lift 쓰기 타임아웃");
                return false;
            }
            AddLog($"[MAINT][0xA6] Lift 쓰기 완료 ({UdpGap()})");
            if (gClass.str.SrmPacket[srmNum].liftParamWriteResult != 0)
            {
                byte lr = gClass.str.SrmPacket[srmNum].liftParamWriteResult;
                AddLog($"[MAINT][ERR] 0xA6 Lift 쓰기 NACK result={lr}{(lr == 10 ? " (셋업모드 아님 — 펌웨어가 현재 모드 거부)" : "")}");
                return false;
            }

            // Step 6: 0x59 보수위치 이동
            AddLog($"[MAINT][0x59] 송신 TRAV={travMm}mm LIFT={liftMm}mm ({UdpGap()})");
            gClass.str.SrmPacket[srmNum].probeData = null;
            gClass.str.SrmPacket[srmNum].maintMoveResult = 0xFF;  // 이전 이동의 stale result(0=성공)를 현재 ACK로 오인하지 않도록 송신 전 리셋
            gClass.str.SrmPacket[srmNum].maintMoveDone = false;
            gClass.str.SrmPacket[srmNum].maintMoveReq = true;
            // 0x59 응답은 거의 안 오므로 짧게 대기 후 바로 curPos 모니터링 (5초→0.8초)
            if (!await WaitFlagAsync(() => gClass.str.SrmPacket[srmNum].maintMoveDone, 800, ct))
            {
                AddLog($"[MAINT] 0x59 응답 미수신 — curPos 모니터링 진행 ({UdpGap()})");
            }
            else if (gClass.str.SrmPacket[srmNum].maintMoveResult != 0)
            {
                byte r = gClass.str.SrmPacket[srmNum].maintMoveResult;
                AddLog($"[MAINT][ERR] 0x59 거부 result={r} ({DecodeMaintResult(r)})");
                return false;
            }
            else
            {
                AddLog("[MAINT] 0x59 ACK (result=0 성공)");
            }

            // Step 7: curPos 모니터링 (최대 120초)
            var moveSw = System.Diagnostics.Stopwatch.StartNew();
            bool arrived = false;
            int lastT = -99999, lastL = -99999;
            int stRefT = CurTrav, stRefL = CurLift;
            long lastProgMs = 0;
            const int MAINT_STALL_MS = 15000; // 15초 무변화+미도착 = 0x59 미동작(안전트립/펌웨어거부/모드) → 조기 중단
            while (moveSw.ElapsedMilliseconds < 120000 && !ct.IsCancellationRequested)
            {
                // 0x8059가 뒤늦게 도착해 거부(result≠0)로 판명되면 120초 기다리지 않고 즉시 중단
                if (gClass.str.SrmPacket[srmNum].maintMoveDone && gClass.str.SrmPacket[srmNum].maintMoveResult != 0)
                {
                    byte lateR = gClass.str.SrmPacket[srmNum].maintMoveResult;
                    AddLog($"[MAINT][ERR] 0x59 거부 (지연 응답) result={lateR} ({DecodeMaintResult(lateR)})");
                    return false;
                }

                int curT = CurTrav;
                int curL = CurLift;
                int errT = Math.Abs(curT - travMm);
                int errL = Math.Abs(curL - liftMm);

                if ((Math.Abs(curT - lastT) >= 50 || Math.Abs(curL - lastL) >= 50))
                {
                    AddLog($"[MAINT][MOVE] TRAV={curT}mm(Δ{errT}) LIFT={curL}mm(Δ{errL})");
                    lastT = curT; lastL = curL;
                }

                if (errT <= 1 && errL <= 1)
                {
                    await Task.Delay(120, ct); // 정지 확인용 짧은 안정화 (300→120ms)
                    int curT2 = CurTrav;
                    int curL2 = CurLift;
                    if (Math.Abs(curT2 - curT) <= 1 && Math.Abs(curL2 - curL) <= 1)
                    {
                        arrived = true;
                        AddLog($"[MAINT][ARRIVE] TRAV={curT2}mm LIFT={curL2}mm ({moveSw.ElapsedMilliseconds}ms)");
                        break;
                    }
                }

                // stall 감지: 위치가 계속 바뀌면 기준 갱신, 멈춰있고 미도착이면 MAINT_STALL_MS 후 조기 중단(120초 헛대기 방지)
                if (Math.Abs(curT - stRefT) >= 2 || Math.Abs(curL - stRefL) >= 2)
                {
                    stRefT = curT; stRefL = curL; lastProgMs = moveSw.ElapsedMilliseconds;
                }
                else if ((errT > 1 || errL > 1) && moveSw.ElapsedMilliseconds - lastProgMs > MAINT_STALL_MS)
                {
                    AddLog($"[MAINT][ERR] {MAINT_STALL_MS / 1000}s간 위치 변화 없음 + 미도착 — 0x59 미동작(안전트립/펌웨어 거부/모드) 의심, 조기 중단 TRAV={curT} LIFT={curL}");
                    return false;
                }
                await Task.Delay(60, ct); // 모니터링 폴링 (100→60ms)
            }

            if (!arrived)
            {
                int curT = CurTrav;
                int curL = CurLift;
                AddLog($"[MAINT][TIMEOUT] {moveSw.ElapsedMilliseconds}ms TRAV={curT}mm(목표{travMm}) LIFT={curL}mm(목표{liftMm})");
            }
            return arrived;
        }

        /// <summary>
        /// 0x8059 result 코드 해석 — MCU 4.4 펌웨어 기준
        /// (com_tml.c rxCmdMoveMaintanence + dev_SRM.c SRM_Move_Maintanence_Cmd, 코드값 일부 중복 사용)
        /// </summary>
        private static string DecodeMaintResult(byte r) => r switch
        {
            0 => "성공",
            2 => "장비 Fault",
            5 => "포크 중심 아님 / 주행 보수위치=0",
            6 => "이동작업 수행중 / 주행 보수위치 범위 밖",
            7 => "포크작업 수행중 / 승강 보수위치=0",
            8 => "시퀀스 busy / 승강 보수위치 범위 밖(★펌웨어가 '주행' ManualOp 범위로 검사하는 버그)",
            9 => "이미 보수위치 상태",
            _ => "미정의",
        };

        // ================================================================
        // 엑셀 저장 (타임스탬프 파일명, 매 실행마다 새 파일)
        // ================================================================

        private void SaveToExcel(List<TeachingResult> results)
        {
          // 저장 실패(디스크풀/권한/직렬화)가 호출부로 던져지면 — 특히 취소 catch 블록(1720) 안에서 호출되면
          // 형제 catch가 못 잡아 async void 밖→앱 크래시. 여기서 모두 흡수한다.
          try
          {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SRM" + gClass.srmNum, "Teaching");
            Directory.CreateDirectory(dir);
            string filePath = Path.Combine(dir, $"AutoTeaching_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

            using var wb = new XLWorkbook();   // SaveAs throw 시에도 Dispose 보장(핸들 누수 방지)
            var ws = wb.AddWorksheet("Teaching");

            // 헤더
            string[] headers = { "Cell", "Row", "Bay", "Level", "Param bay_mm", "Param lev_mm",
                                 "추론 bay_pos", "추론 lev_pos", "bay_diff", "lev_diff",
                                 "status", "fail_step", "cargo", "timestamp" };
            for (int c = 0; c < headers.Length; c++)
                ws.Cell(1, c + 1).Value = headers[c];

            var headerRange = ws.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.SteelBlue;
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var info = gClass.str.SrmInfo[gClass.srmNum];
            string ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                int row = i + 2;

                int paramBay = (info.cellBay != null && r.Bay >= 1 && r.Bay <= info.cellBay.Length) ? info.cellBay[r.Bay - 1] : 0;
                int paramLev = (info.cellLev != null && r.Level >= 1 && r.Level <= info.cellLev.Length) ? info.cellLev[r.Level - 1] : 0;

                ws.Cell(row, 1).Value = CellKey(r.Row, r.Bay, r.Level);
                ws.Cell(row, 2).Value = r.Row;
                ws.Cell(row, 3).Value = r.Bay;
                ws.Cell(row, 4).Value = r.Level;
                ws.Cell(row, 5).Value = paramBay;
                ws.Cell(row, 6).Value = paramLev;

                if (r.Success)
                {
                    ws.Cell(row, 7).Value = r.BayPos;
                    ws.Cell(row, 8).Value = r.LevelPos;
                    ws.Cell(row, 9).Value = r.BayPos - paramBay;
                    ws.Cell(row, 10).Value = r.LevelPos - paramLev;
                    ws.Cell(row, 11).Value = "OK";
                    ws.Cell(row, 11).Style.Font.FontColor = XLColor.DarkGreen;
                    ws.Cell(row, 12).Value = "";
                }
                else
                {
                    if (r.CaptureOk)
                    {
                        ws.Cell(row, 7).Value = r.BayPos;
                        ws.Cell(row, 8).Value = r.LevelPos;
                        ws.Cell(row, 9).Value = r.BayPos - paramBay;
                        ws.Cell(row, 10).Value = r.LevelPos - paramLev;
                    }
                    else
                    {
                        ws.Cell(row, 7).Value = "-";
                        ws.Cell(row, 8).Value = "-";
                        ws.Cell(row, 9).Value = "-";
                        ws.Cell(row, 10).Value = "-";
                    }
                    string statusLabel = r.FailedStep switch
                    {
                        "capture" => "FAIL-CAPTURE",
                        "x_inference" => "FAIL-X",
                        "z_inference" => "FAIL-Z",
                        _ => "FAIL"
                    };
                    ws.Cell(row, 11).Value = statusLabel;
                    ws.Cell(row, 11).Style.Font.FontColor = XLColor.Red;
                    ws.Cell(row, 11).Style.Font.Bold = true;

                    string detail = r.FailedSubStep ?? "";
                    if (!string.IsNullOrEmpty(r.Error))
                        detail = string.IsNullOrEmpty(detail)
                            ? r.Error.Substring(0, Math.Min(r.Error.Length, 80))
                            : $"{detail}: {r.Error.Substring(0, Math.Min(r.Error.Length, 60))}";
                    ws.Cell(row, 12).Value = detail;
                    ws.Cell(row, 12).Style.Font.FontColor = XLColor.Red;
                }

                ws.Cell(row, 13).Value = r.HasCargo ? "Y" : "N";
                ws.Cell(row, 14).Value = ts;

                ws.Range(row, 1, row, headers.Length).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            ws.Columns().AdjustToContents();

            wb.SaveAs(filePath);
            lastExcelPath = filePath;

            // 단계별 집계는 결과 리스트에서 직접 계산(과거 count* 필드는 증가 코드가 없어 항상 0이었음 — 거짓 표기 제거)
            int okCnt2 = results.Count(r => r.Success);
            int capF = results.Count(r => !r.Success && r.FailedStep == "capture");
            int xF = results.Count(r => !r.Success && r.FailedStep == "x_inference");
            int zF = results.Count(r => !r.Success && r.FailedStep == "z_inference");
            AddLog($"[EXCEL] Saved → {filePath} (OK:{okCnt2} CAP:{capF} X:{xF} Z:{zF})");
          }
          catch (Exception ex)
          {
            AddLog($"[EXCEL][ERR] 저장 실패 — {ex.Message}");
            try { cIniAccess.SaveExLog(gClass.srmNum, "SaveToExcel: " + ex.Message); } catch { }
          }
        }

        // [2026-07-08] 결과 저장 진입점 — xlsx 우선, 실패 시 CSV 폴백.
        //   ClosedXML/DocumentFormat.OpenXml DLL 누락이면 SaveToExcel이 JIT/어셈블리로드 단계에서 던지는데,
        //   그 예외는 SaveToExcel '안'의 try/catch가 못 잡고 호출부로 전파된다(호출 지점에서 발생). 여기서 잡아 CSV로 대체.
        private void SaveResults(List<TeachingResult> results)
        {
            try { SaveToExcel(results); }
            catch (Exception ex)
            {
                AddLog($"[EXCEL][ERR] xlsx 저장 실패({ex.Message}) → CSV로 대체 저장 (실행폴더에 ClosedXML/DocumentFormat.OpenXml DLL 확인 필요)");
                SaveToCsv(results);
            }
        }

        // ClosedXML 없이 System.IO만으로 저장하는 폴백. xlsx와 동일 컬럼. UTF-8 BOM(엑셀에서 한글 정상).
        private void SaveToCsv(List<TeachingResult> results)
        {
            try
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SRM" + gClass.srmNum, "Teaching");
                Directory.CreateDirectory(dir);
                string filePath = Path.Combine(dir, $"AutoTeaching_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                var info = gClass.str.SrmInfo[gClass.srmNum];
                string ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Cell,Row,Bay,Level,Param bay_mm,Param lev_mm,추론 bay_pos,추론 lev_pos,bay_diff,lev_diff,status,fail_step,cargo,timestamp");
                foreach (var r in results)
                {
                    int paramBay = (info.cellBay != null && r.Bay >= 1 && r.Bay <= info.cellBay.Length) ? info.cellBay[r.Bay - 1] : 0;
                    int paramLev = (info.cellLev != null && r.Level >= 1 && r.Level <= info.cellLev.Length) ? info.cellLev[r.Level - 1] : 0;
                    bool hasPos = r.Success || r.CaptureOk;
                    string bp = hasPos ? r.BayPos.ToString() : "-";
                    string lp = hasPos ? r.LevelPos.ToString() : "-";
                    string bd = hasPos ? (r.BayPos - paramBay).ToString() : "-";
                    string ld = hasPos ? (r.LevelPos - paramLev).ToString() : "-";
                    string status = r.Success ? "OK" : (r.FailedStep switch
                    {
                        "capture" => "FAIL-CAPTURE",
                        "x_inference" => "FAIL-X",
                        "z_inference" => "FAIL-Z",
                        _ => "FAIL"
                    });
                    string detail = ((r.FailedSubStep ?? "") + " " + (r.Error ?? "")).Replace(",", ";").Replace("\n", " ").Replace("\r", " ").Trim();
                    sb.AppendLine($"{CellKey(r.Row, r.Bay, r.Level)},{r.Row},{r.Bay},{r.Level},{paramBay},{paramLev},{bp},{lp},{bd},{ld},{status},{detail},{(r.HasCargo ? "Y" : "N")},{ts}");
                }
                File.WriteAllText(filePath, sb.ToString(), new System.Text.UTF8Encoding(true));
                lastExcelPath = filePath;
                AddLog($"[CSV] Saved → {filePath} (xlsx 대체 저장)");
            }
            catch (Exception ex) { AddLog($"[CSV][ERR] 저장 실패 — {ex.Message}"); }
        }

        // ================================================================
        // UI Event Handlers
        // ================================================================

        private async void Btn_Start_Click(object sender, RoutedEventArgs e)
        {
            if (isRunning) return;

            var targets = BuildTargetList();
            if (targets.Count == 0)
            {
                MessageBox.Show("Teaching target is empty.", "Auto Teaching", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string cameraId = GetCameraId();
            var result = MessageBox.Show(
                $"Start Auto Teaching?\nCamera: {cameraId}\nTargets: {targets.Count} cells",
                "Auto Teaching", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (result != MessageBoxResult.OK) return;

            isRunning = true;
            skipRequested = false;
            gClass.str.SrmPacket[gClass.srmNum].isAutoTeaching = true;
            // Phase2가 자동모드+Start ON으로 무장하므로, 런 종료 시 복원할 원래 지상반 모드를 저장.
            byte origGcpTxMode = gClass.str.SrmState[gClass.srmNum].gcpState.gcpTxMode;
            cts?.Dispose(); cts = new CancellationTokenSource();
            lidarFallback = false;   // 새 런: 라이다 폴백 상태 리셋
            stopReason = "user";     // 새 런: STOP 사유 리셋(기본값 = 사용자)
            currentResults.Clear();
            totalTargets = targets.Count;
            curTargetIdx = 0;
            listBox_Log.Items.Clear();

            btn_Start.IsEnabled = false;
            btn_Stop.IsEnabled = true;
            btn_Skip.IsEnabled = true;
            btn_Save.IsEnabled = false;
            btn_Excel.IsEnabled = false;

            cIniAccess.SaveJobLog(gClass.srmNum, "Auto Teaching Start");
            AddLog("[START] Auto Teaching");

            // M2.2: 런 시작 → RUN 뷰로 전환, 결과확인 버튼 비활성
            SetView(TeachView.Run);
            Dispatcher.Invoke(() => btnRun_Review.IsEnabled = false);

            // M3.3: 런 시작 시 PAUSE 상태 리셋 + 버튼 라벨 복원
            pauseRequested = false;
            Dispatcher.Invoke(() => btnRun_Pause.Content = "⏸ 일시정지");

            try
            {
                // Phase 1: 초기화 — 실패 시 운영자에게 [재시도/중단] 선택 (막다른 종료 방지).
                //   (비전 서버/카메라/RTSP는 일시적 네트워크 깜빡임이 잦아 한 번 실패로 런을 죽이지 않는다.)
                while (true)
                {
                    SetStatus("INIT", ClrWarn);
                    if (await Phase1_InitAsync(cameraId, cts.Token)) break;

                    SetStatus("INIT FAIL", ClrErr);
                    var r = MessageBox.Show(
                        "초기화 실패 (비전 서버 / 카메라 / RTSP 연결 확인).\n\n[예] 재시도   [아니오] 중단",
                        "Auto Teaching - 초기화", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (r == MessageBoxResult.Yes) { AddLog("[RETRY] 초기화 재시도 (운영자 선택)"); continue; }
                    AddLog("[ERR] Initialization failed - 운영자 중단");
                    return;
                }

                // Phase 1.5: 캘리브레이션 파일 존재 여부만 확인 (START에서는 캘리브 스윕을 돌리지 않음 — 2026-06-30 정책 변경).
                //   파일 있으면 그걸로 진행. 없으면 중단하고 수동 [캘리브레이션] 버튼으로 먼저 하도록 안내.
                //   (실제 재캘리/스윕은 Btn_Calib_Click → Phase1_5_CalibrationAsync(force=true) 경로에서만 수행.)
                //   배경: 0x59 보수이동이 lift<주행ManualOpStart(통상1000mm)에서 펌웨어버그로 거부되므로,
                //         START가 매번 스윕을 강제하면 저단 셀에서 티칭 자체가 막힘. 기존 파일 재사용으로 분리.
                SetStatus("CALIBRATION", ClrCalib);
                CalibrationStatusResponse calSt = null;
                try { calSt = await visionApi.CalibrationStatusAsync(cameraId); }
                catch (Exception ex) { AddLog($"[CAL] status 조회 실패: {ex.Message}"); }

                if (calSt != null && calSt.CalibrationExists)
                {
                    AddLog($"[CAL] 캘리브레이션 파일 확인됨 (생성: {calSt.CreatedAt ?? "N/A"}, offset_x={calSt.ZeroOffsetXMm}mm offset_y={calSt.ZeroOffsetYMm}mm, status={calSt.Status ?? "N/A"}) — START는 캘리브 미실행, 파일만 사용");

                    // 가벼운 유효성 점검(비차단 경고) — 파일은 있으나 값이 수상하면 로그로 알리고 그대로 진행.
                    //   · 양축 offset 모두 정확히 0 = 캘리브가 비어있을(미산출) 가능성. ※offset_y 단독 0은 정상값이라 '양축 0'만 본다.
                    //   · |offset| 비정상적으로 큼 = 손상/오산출 의심.
                    const double OffsetSaneLimitMm = 100.0;
                    if (calSt.ZeroOffsetXMm == 0 && calSt.ZeroOffsetYMm == 0)
                        AddLog("[CAL][WARN] 캘리브 offset이 양축 모두 0 — 캘리브가 비어있을 수 있음. 재캘리브레이션 권장(START는 계속 진행).");
                    if (Math.Abs(calSt.ZeroOffsetXMm) > OffsetSaneLimitMm || Math.Abs(calSt.ZeroOffsetYMm) > OffsetSaneLimitMm)
                        AddLog($"[CAL][WARN] 캘리브 offset이 비정상적으로 큼 (|x|={Math.Abs(calSt.ZeroOffsetXMm):F1}mm |y|={Math.Abs(calSt.ZeroOffsetYMm):F1}mm > {OffsetSaneLimitMm:F0}mm) — 손상 의심. 확인 권장(START는 계속 진행).");
                }
                else
                {
                    SetStatus("CAL 없음", ClrErr);
                    AddLog("[CAL-ERR] 캘리브레이션 파일 없음/확인불가 — START 중단. 먼저 [캘리브레이션] 버튼으로 캘리브레이션 후 다시 시작하세요.");
                    MessageBox.Show(
                        "캘리브레이션 파일이 없습니다.\n\n먼저 [캘리브레이션] 버튼으로 캘리브레이션을 수행한 뒤 다시 시작하세요.",
                        "Auto Teaching - 캘리브레이션", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Phase 1.7: 라이다 스캔 — START 시 자동 2회 시도.
                //   성격 = 헬스체크: 측정값은 120초만 유효하므로 셀 진행 중 신선도는 셀마다 Z추론 직전
                //   재스캔(셀 루프의 settle ∥ 라이다 스캔)이 담당한다. 자동 2회 실패 시 운영자가 'X만 계속'을 고르면
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

                // Phase 2: 셀 반복 티칭
                await Phase2_TeachingLoopAsync(cameraId, targets, cts.Token);

                // Phase 3: 종료 — 단일 셀이면 disconnect, 연속이면 RTSP 유지
                bool singleCell = targets.Count <= 1;
                SetStatus("CLEANUP", ClrInfo);
                await Phase3_CleanupAsync(cameraId, singleCell);

                // 완료
                int okCnt = currentResults.Count(r => r.Success);
                SetStatus("DONE", ClrDone);
                lbl_result.Content = $"OK ({okCnt}/{targets.Count})";
                lbl_result.Foreground = new SolidColorBrush(Colors.LightGreen);
                AddLog($"[COMPLETE] {okCnt}/{targets.Count} cells done");
                if (lidarFallback) AddLog("[LIDAR] 이번 런: 라이다 미사용(주행 X만 티칭, 승강 미보정)");

                // 자동 저장 (xlsx 우선, 실패 시 CSV 폴백)
                if (currentResults.Any(r => r.Success))
                {
                    SaveResults(currentResults);
                    SaveTeachingState();
                }
            }
            catch (OperationCanceledException)
            {
                AddLog(stopReason switch
                {
                    "safety" => "[STOP] 위치 안전가드 발동으로 런 중단 (사용자 취소 아님 — 위의 [SEMI][SAFETY] 로그 참조)",
                    "shutdown" => "[STOP] 앱 종료로 런 중단",
                    _ => "[STOP] Teaching cancelled by user"
                });
                SetStatus("STOPPED", ClrErr);
                await Phase3_CleanupAsync(cameraId, true);  // 취소 시에는 항상 해제

                int okCnt = currentResults.Count(r => r.Success);
                lbl_result.Content = $"Stopped ({okCnt}/{targets.Count})";
                lbl_result.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x8F, 0x8F));

                // 중간 결과라도 저장 (xlsx 우선, 실패 시 CSV 폴백)
                if (currentResults.Any(r => r.Success))
                    SaveResults(currentResults);
            }
            catch (Exception ex)
            {
                AddLog($"[ERR] {ex.Message}");
                SetStatus("ERROR", ClrErr);
                cIniAccess.SaveExLog(gClass.srmNum, "AutoTeaching: " + ex.Message);
                // 예외 경로에서도 RTSP 정리 (취소 경로와 동일) — 안 하면 rtspConnected=true 잔존 → 다음 런 재연결 skip
                try { await Phase3_CleanupAsync(cameraId, true); } catch (Exception ce) { AddLog($"[CLEANUP][WARN] {ce.Message}"); }
            }
            finally
            {
                // ★ 크레인 무장 해제 (Start OFF + 요청플래그 클리어 + 모드 복원) — 정상/취소/예외 모든 종료 경로 공통
                DisarmCraneAfterRun(origGcpTxMode);
                skipRequested = false;
                isRunning = false;
                gClass.str.SrmPacket[gClass.srmNum].isAutoTeaching = false;
                btn_Start.IsEnabled = true;
                btn_Stop.IsEnabled = false;
                btn_Skip.IsEnabled = false;
                btn_Save.IsEnabled = currentResults.Any(r => r.Success);
                btn_Excel.IsEnabled = true;
                cIniAccess.SaveJobLog(gClass.srmNum, "Auto Teaching End");
                // M2.2: 런 종료 → '결과 확인·반영하기' 활성 (자동전환은 안 함, 운영자 선택)
                Dispatcher.Invoke(() => btnRun_Review.IsEnabled = true);
            }
        }

        private void Btn_Stop_Click(object sender, RoutedEventArgs e)
        {
            if (!isRunning) return;
            // 모달을 띄우기 전에 현재 런의 토큰을 로컬로 캡처 — 모달 펌프 도중 런이 끝나고 새 런이 cts를
            // 재할당해도 옛 런만 취소(막 시작한 새 런을 잘못 취소하는 spurious cancel 방지).
            var local = cts;
            var result = MessageBox.Show("Stop Auto Teaching?", "Auto Teaching", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (result == MessageBoxResult.OK)
            {
                stopReason = "user";
                local?.Cancel();
            }
        }

        private void Btn_Skip_Click(object sender, RoutedEventArgs e)
        {
            // 현재 셀의 이동/대기를 중단하고 다음 셀로 진행 (semi-auto 이동 루프가 skipRequested를 폴링).
            if (!isRunning) return;
            skipRequested = true;
            AddLog("[SKIP] 현재 셀 건너뛰기 요청 — 이동/대기 중단 후 다음 셀로");
        }

        // [2026-07-02] 라이다 스캔 공통 실행 — POST /api/gc/cmd/lidar/scan_start + 로깅. 반환 = success.
        //   body 없이 전송(서버 기본 20프레임). 크레인 이동 없음(비전서버 단독 동작).
        //   ★측정값은 서버 내부저장 120초 유효 → 승강(Z) 추론 직전마다 재스캔 필수(스펙 2026-07-02 확정).
        //   호출 경로 4곳: 수동 버튼 / START 헬스체크 / CALIB 과정 / 셀마다 Z추론 직전.
        //   verbose=false → 성공 시 1줄 요약(셀 반복용, 로그 폭주 방지). 실패는 항상 상세 + :9000 status 진단.
        //   호출 전 visionApi.SetBaseUrl이 세팅되어 있어야 한다. 사용자 취소(OCE)는 그대로 전파.
        private async Task<bool> RunLidarScanAsync(string context, CancellationToken ct, bool verbose = true, int? frames = null)
        {
            if (verbose) AddLog($"[LIDAR] ({context}) scan_start 요청 → {visionApi.BaseUrl}/api/gc/cmd/lidar/scan_start ({(frames.HasValue ? $"frames={frames.Value}" : "빈 JSON 본문 = 서버 기본 20프레임")}) @ trav={CurTrav} lift={CurLift}");
            var swLidar = System.Diagnostics.Stopwatch.StartNew();
            LidarScanResponse res;
            try { res = await visionApi.LidarScanStartAsync(frames, ct); } // 클라이언트 타임아웃 180s. frames=null이면 서버 기본 20
            catch (OperationCanceledException) { throw; } // 사용자 STOP → 상위 취소 경로로
            catch (Exception ex)
            {
                AddLog($"[LIDAR][ERR] ({context}) 예외: {ex.Message}");
                await LogLidarDiagAsync(ct);
                return false;
            }
            swLidar.Stop();

            if (res.Success && !verbose)
            {
                // 셀 반복 경로 — 1줄 요약만
                AddLog($"[LIDAR] ({context}) OK HTTP={res.HttpStatusCode} ({swLidar.ElapsedMilliseconds}ms) server_elapsed={res.ElapsedMs}ms");
                return true;
            }

            AddLog($"[LIDAR] ({context}) HTTP={res.HttpStatusCode} ({swLidar.ElapsedMilliseconds}ms) success={res.Success}" +
                   $" frames={(res.Frames.HasValue ? res.Frames.Value.ToString() : "N/A")} elapsed_ms={res.ElapsedMs}");
            if (!string.IsNullOrEmpty(res.Message)) AddLog($"[LIDAR] message: {res.Message}");
            if (!string.IsNullOrEmpty(res.Error)) AddLog($"[LIDAR][ERR] error: {res.Error} (failed_step={res.FailedStep ?? "N/A"})");

            // 응답 원문 — 서버가 주는 그대로 남긴다(비전팀 확인용)
            string raw = visionApi.LastResponseJson ?? "(없음)";
            if (raw.Length > 1000) raw = raw.Substring(0, 1000) + $" ...(+{raw.Length - 1000}자 생략)";
            AddLog($"[LIDAR] 응답 원문: {raw}");

            if (!res.Success) await LogLidarDiagAsync(ct);
            return res.Success;
        }

        // 라이다 실패 진단 — 라이다 서비스(:9000) status를 조회해 원인 후보(불통/미연결/워밍업/baseline 미설정)를 로그로 남긴다.
        //   scan_start 실패 + Z추론 lidar_missing 실패 양쪽에서 호출. 진단 실패는 무해(로그만).
        private async Task LogLidarDiagAsync(CancellationToken ct)
        {
            LidarStatusResponse st = null;
            try { st = await visionApi.LidarStatusAsync(ct); }
            catch (OperationCanceledException) { throw; }
            catch { /* 진단 보조 — 무시 */ }

            if (st == null)
            {
                AddLog($"[LIDAR][DIAG] 라이다 서비스({visionApi.LidarSvcUrl}) status 조회 실패 — 서비스 미기동/네트워크 불통 의심");
                return;
            }
            AddLog($"[LIDAR][DIAG] status: available={st.Available} connected={st.Connected} warming_up={st.WarmingUp}" +
                   $" baseline_y_center={(st.BaselineYCenter.HasValue ? st.BaselineYCenter.Value.ToString("F1") : "미설정")}" +
                   $" hoist_offset_mm={(st.HoistOffsetMm.HasValue ? st.HoistOffsetMm.Value.ToString("F1") : "N/A")}");
            if (!st.Connected) AddLog("[LIDAR][DIAG] 라이다 미연결 — 센서/케이블/서비스 확인");
            else if (st.WarmingUp) AddLog("[LIDAR][DIAG] 워밍업 중 — 잠시 후 재시도");
            if (!st.BaselineYCenter.HasValue)
                AddLog("[LIDAR][DIAG] ★baseline 미설정 — 최초 설치/기구 재배치 후 1회 필수. 비전 웹 UI [현재 프레임을 기준으로] 또는 POST :9000/api/lidar/baseline");
        }

        // 수동 [라이다 스캔] 버튼 — 티칭/캘리브 실행 중에는 비전서버 리소스 경합 방지를 위해 차단.
        private bool lidarBusy = false;
        private async void Btn_Lidar_Click(object sender, RoutedEventArgs e)
        {
            if (isRunning) { AddLog("[LIDAR] 오토티칭/캘리브레이션 실행 중에는 스캔 불가"); return; }
            if (lidarBusy) return;
            lidarBusy = true;
            btn_Lidar.IsEnabled = false;
            try
            {
                string visIni = AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini";
                string ip = cIniAccess.Read(visIni, "SRMINFO_" + gClass.srmNum, "VISIONIP", "127.0.0.1").Trim();
                int port = int.TryParse(cIniAccess.Read(visIni, "SRMINFO_" + gClass.srmNum, "VISIONPORT", "3080").Trim(), out int p) ? p : 3080;
                visionApi.SetBaseUrl(ip, port);

                int? frames = int.TryParse(edit_LidarFrames.Text?.Trim(), out int fv) ? fv : (int?)null;
                await RunLidarScanAsync("수동 버튼", CancellationToken.None, frames: frames);
            }
            catch (Exception ex)
            {
                AddLog($"[LIDAR][ERR] 예외: {ex.Message}");
            }
            finally
            {
                lidarBusy = false;
                btn_Lidar.IsEnabled = true;
            }
        }

        // 수동 [기준설정] 버튼 — 라이다 baseline 설정(POST :9000/api/lidar/baseline). 티칭/캘리브 실행 중 차단.
        private async void Btn_LidarBaseline_Click(object sender, RoutedEventArgs e)
        {
            if (isRunning) { AddLog("[BASELINE] 오토티칭/캘리브레이션 실행 중에는 설정 불가"); return; }
            if (lidarBusy) return;

            var confirm = MessageBox.Show(
                "크레인이 정지한 상태에서 현재 라이다 프레임을 기준(baseline)으로 설정합니다.\n설치/기구 재배치 후 1회만 수행하세요. 계속할까요?",
                "LiDAR Baseline", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            lidarBusy = true;
            btn_LidarBaseline.IsEnabled = false;
            try
            {
                string visIni = AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini";
                string ip = cIniAccess.Read(visIni, "SRMINFO_" + gClass.srmNum, "VISIONIP", "127.0.0.1").Trim();
                int port = int.TryParse(cIniAccess.Read(visIni, "SRMINFO_" + gClass.srmNum, "VISIONPORT", "3080").Trim(), out int p) ? p : 3080;
                visionApi.SetBaseUrl(ip, port);

                await RunLidarBaselineAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                AddLog($"[BASELINE][ERR] 예외: {ex.Message}");
            }
            finally
            {
                lidarBusy = false;
                btn_LidarBaseline.IsEnabled = true;
            }
        }

        // 라이다 baseline 실행 — POST :9000/api/lidar/baseline + 로깅. 반환 = success. RunLidarScanAsync와 동일 구조.
        private async Task<bool> RunLidarBaselineAsync(CancellationToken ct)
        {
            AddLog($"[BASELINE] set baseline 요청 → {visionApi.LidarSvcUrl}/api/lidar/baseline (빈 JSON 본문 = 서버 고정 50프레임)");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            LidarBaselineResponse res;
            try { res = await visionApi.LidarSetBaselineAsync(ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                AddLog($"[BASELINE][ERR] 예외: {ex.Message}");
                await LogLidarDiagAsync(ct);
                return false;
            }
            sw.Stop();

            AddLog($"[BASELINE] HTTP={res.HttpStatusCode} ({sw.ElapsedMilliseconds}ms) success={res.Success} elapsed_ms={res.ElapsedMs}");
            if (!string.IsNullOrEmpty(res.Message)) AddLog($"[BASELINE] message: {res.Message}");
            if (res.BaselineYCenter.HasValue) AddLog($"[BASELINE] baseline_y_center={res.BaselineYCenter.Value:F1}");
            if (!string.IsNullOrEmpty(res.Error)) AddLog($"[BASELINE][ERR] error: {res.Error} (failed_step={res.FailedStep ?? "N/A"})");

            string raw = visionApi.LastResponseJson ?? "(없음)";
            if (raw.Length > 1000) raw = raw.Substring(0, 1000) + $" ...(+{raw.Length - 1000}자 생략)";
            AddLog($"[BASELINE] 응답 원문: {raw}");

            if (!res.Success) await LogLidarDiagAsync(ct);
            return res.Success;
        }

        private async void Btn_Calib_Click(object sender, RoutedEventArgs e)
        {
            if (isRunning)
            {
                AddLog("[CALIB] 오토티칭/캘리브레이션 실행 중에는 시작 불가");
                return;
            }

            // ★ isRunning을 공유 락으로 사용 → CALIB 중 START 차단, 반대도 동일
            isRunning = true;
            btn_Calib.IsEnabled = false;
            btn_Start.IsEnabled = false;
            btn_Stop.IsEnabled = true; // CALIB 중에도 STOP 가능하도록
            cts?.Dispose(); cts = new CancellationTokenSource();

            try
            {
                gClass.str.SrmPacket[gClass.srmNum].isAutoTeaching = true;
                string cameraId = GetCameraId();

                // Vision API BaseUrl 설정 (Phase1_Init과 동일)
                string visIni = AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini";
                string ip = cIniAccess.Read(visIni, "SRMINFO_" + gClass.srmNum, "VISIONIP", "127.0.0.1").Trim();
                int port = int.TryParse(cIniAccess.Read(visIni, "SRMINFO_" + gClass.srmNum, "VISIONPORT", "3080").Trim(), out int p) ? p : 3080;
                visionApi.SetBaseUrl(ip, port);
                AddLog($"[CALIB] Vision API BaseUrl={visionApi.BaseUrl}");

                // 캘리브레이션 기준 셀 — 티칭 타겟 리스트의 첫 셀 사용
                var targets = BuildTargetList();
                if (targets.Count == 0)
                {
                    AddLog("[CALIB][ERR] 캘리브레이션 기준 셀이 비어있음 — Row/Bay/Lev 입력 후 다시 시도");
                    MessageBox.Show("기준 셀이 비어있습니다.\nRow/Bay/Lev를 입력한 뒤 CALIB를 실행하세요.",
                                    "Calibration", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // [2026-07-02 재캘리 사전 체크 — MCU 펌웨어 버그 워크어라운드]
                //   스윕은 현재 위치 기준 ±15mm 0x59 이동인데, MCU가 승강 보수위치를 '주행' ManualOp 범위로
                //   검증하는 복붙 버그(dev_SRM.c, result=8) 때문에 승강이 (주행범위 시작+15mm) 미만이면
                //   14포인트 전부 거부되어 캘리브가 통째로 실패한다(2026-06-30 lift=970mm 사례).
                //   여기서 미리 경고하고 기본은 중단 — 승강 올린 뒤 재시도 유도.
                int guardLo = 1000, guardHi = 25300; // 통상값 — 0xA3 캐시가 있으면 실측값으로 대체
                if (cachedDriveParam != null && cachedDriveParam.Length >= 140)
                {
                    guardLo = BitConverter.ToInt32(cachedDriveParam, 132);
                    guardHi = BitConverter.ToInt32(cachedDriveParam, 136);
                }
                int calibLift = CurLift;
                if (calibLift - 15 < guardLo || calibLift + 15 > guardHi)
                {
                    AddLog($"[CALIB][WARN] 현재 승강 {calibLift}mm — 스윕(±15mm)이 주행 ManualOp 범위 [{guardLo},{guardHi}] 밖 " +
                           $"(펌웨어 승강범위 검증 버그, 0x59 result=8 거부 예상)");
                    var ans = MessageBox.Show(
                        $"현재 승강 위치가 {calibLift}mm 입니다.\n\n" +
                        $"MCU 펌웨어 버그로 승강 {guardLo + 15}mm 미만에서는 캘리브레이션 스윕(±15mm)이 전부 거부됩니다(result=8).\n" +
                        $"승강을 {guardLo + 15}mm 이상 위치로 옮긴 뒤 CALIB를 다시 실행하세요.\n\n" +
                        "그래도 지금 위치에서 진행하시겠습니까?",
                        "Calibration — 승강 위치 경고", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                    if (ans != MessageBoxResult.Yes)
                    {
                        AddLog("[CALIB] 사용자 취소 — 승강 위치 이동 후 재시도");
                        return;
                    }
                    AddLog("[CALIB] 사용자 확인 — 현재 위치에서 강행 (스윕 포인트 거부될 수 있음)");
                }

                AddLog($"[CALIB] 캘리브레이션 시작 (camera={cameraId}, 기준 셀=Row{targets[0].row}/Bay{targets[0].bay}/Lev{targets[0].lev})");
                SetStatus("CALIBRATION", ClrCalib);

                // 캘리브레이션 실행 — MCU가 z ±15mm(소프트리밋 밖)를 자체 처리하므로
                // cellLev/lift 범위확장·임시홈·백업복구 해킹 제거. Phase1_5 내부에서 0x59로 ±15mm 직접 이동.
                bool ok = await Phase1_5_CalibrationAsync(cameraId, targets, cts.Token, forceRecalibrate: true);

                if (ok)
                {
                    AddLog("[CALIB] 캘리브레이션 완료");
                    SetStatus("CAL OK", Color.FromRgb(0x2E, 0x7D, 0x32));
                }
                else
                {
                    AddLog("[CALIB][ERR] 캘리브레이션 실패");
                    SetStatus("CAL FAIL", ClrErr);
                }
            }
            catch (OperationCanceledException)
            {
                AddLog("[CALIB] 캘리브레이션 취소됨");
                SetStatus("CANCELLED", ClrErr);
            }
            catch (Exception ex)
            {
                AddLog($"[CALIB][ERR] {ex.Message}");
                SetStatus("CAL ERR", ClrErr);
            }
            finally
            {
                // RTSP disconnect (ffmpeg 메모리 해제)
                try
                {
                    string camId = GetCameraId();
                    AddLog($"[CALIB] RTSP disconnect 호출 (ffmpeg 종료) camera={camId}");
                    var dc = await visionApi.DisconnectRtspAsync(camId);
                    AddLog($"[CALIB] RTSP disconnect 응답 success={dc?.Success}");
                }
                catch (Exception dex) { AddLog($"[CALIB][WARN] RTSP disconnect 예외: {dex.Message}"); }

                gClass.str.SrmPacket[gClass.srmNum].isAutoTeaching = false;
                isRunning = false; // ★ 공유 락 해제 (START 다시 가능)
                btn_Calib.IsEnabled = true;
                btn_Start.IsEnabled = true;
                btn_Stop.IsEnabled = false;
            }
        }

        private void Btn_Save_Click(object sender, RoutedEventArgs e)
        {
            if (currentResults.Count == 0 || !currentResults.Any(r => r.Success))
            {
                MessageBox.Show("No results to save.", "Save", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            // [크래시 방지] SaveToExcel은 ClosedXML.dll 로드 실패(FileNotFoundException) 시 미처리 예외로
            //   DispatcherUnhandledException → 앱 크래시를 일으켰음(2026-06-29 Crash 로그). 자동저장 경로처럼
            //   try/catch로 강등 — 저장 실패해도 앱은 살아있고 결과는 메모리에 남는다.
            try
            {
                SaveToExcel(currentResults);
                MessageBox.Show($"Saved to Excel.\n{lastExcelPath}", "Save", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddLog($"[ERR] 엑셀 저장 실패: {ex.Message} → CSV로 대체 저장");
                SaveToCsv(currentResults);   // ClosedXML DLL 누락 등 xlsx 실패 시 CSV로라도 보존
                MessageBox.Show($"엑셀(xlsx) 저장 실패로 CSV로 저장했습니다.\n{lastExcelPath}\n\n(원인: {ex.Message}\n — 실행 폴더에 ClosedXML/DocumentFormat.OpenXml DLL 확인)",
                    "Save", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ================================================================
        // 3단계: 검증 (재추론) — 2단계 결과(추정 위치)로 순차 이동 후 다시 추론하여 잔차 확인.
        //   · 정밀 이동(보정된 mm)은 0x59(MoveViaMaintAsync) 사용 — 1단계 캘리와 동일 경로(보존).
        //   · 잔차(재추론 보정량) ≈ 0 이면 정상, 크면 '재확인' 플래그.
        //   · ⚠ "비전 충돌범위"는 비전 API 미지원(엔드포인트/필드 없음) → 추후 비전팀 엔드포인트 생기면 추가.
        //   · 포크 뻗기 물리검증도 추후(별도).
        // ================================================================
        private const int VerifyResidualTolMm = 3;   // 재추론 잔차 허용(mm)

        private async Task RunVerifyReinferAsync(string cameraId, CancellationToken ct)
        {
            var list = currentResults.Where(r => r.Success).ToList();
            if (list.Count == 0) { AddLog("[VERIFY] 검증할 2단계 성공 결과가 없습니다."); return; }

            AddLog($"────────── 3단계 검증(재추론) 시작 — {list.Count}셀 (잔차 허용 ±{VerifyResidualTolMm}mm) ──────────");

            // 0x59 정밀 이동 위해 셋업모드 진입
            if (gClass.str.SrmState[gClass.srmNum].setupMode == 0)
            {
                AddLog("[VERIFY] 0x59 이동 위해 셋업모드 진입 시도");
                if (!await SetCraneModeAsync(1, 5000)) { AddLog("[VERIFY][ERR] 셋업모드 진입 실패 — 검증 중단"); return; }
                await Task.Delay(300, ct);
            }

            int okCnt = 0, ngCnt = 0;
            for (int i = 0; i < list.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var r = list[i];
                string cell = CellKey(r.Row, r.Bay, r.Level);
                AddLog($"[VERIFY {i + 1}/{list.Count}] {cell} → 추정위치 trav={r.BayPos} lift={r.LevelPos} 이동(0x59)");

                // 1) 추정(보정) 위치로 0x59 정밀 이동
                if (!await MoveViaMaintAsync(r.BayPos, r.LevelPos, ct)) { AddLog($"[VERIFY][ERR] {cell} 이동 실패"); ngCnt++; continue; }

                // 2) 레벨별 안정화 후 캡처
                int settleMs = GetCaptureSettleMs(r.Level);
                if (settleMs > 0) await Task.Delay(settleMs, ct);
                int curT = CurTrav;
                int curL = CurLift;
                var req = new CaptureRequest { Row = r.Row, Bay = r.Bay, BayPos = curT, Level = r.Level, LevelPos = curL, HasCargo = r.HasCargo };

                try
                {
                    var cap = await visionApi.RequestCaptureAsync(cameraId, req, ct);
                    if (!cap.Success) { AddLog($"[VERIFY][ERR] {cell} 캡처 실패: {cap.Error}"); ngCnt++; continue; }

                    // 3) 재 X/Z 추론 → 잔차(보정량). 추정위치가 맞으면 보정량 ≈ 0.
                    var xr = await visionApi.RequestTravelInferenceAsync(cameraId, req, ct);
                    var zr = await visionApi.RequestHoistInferenceAsync(cameraId, req, ct);
                    if (!xr.Success || !zr.Success) { AddLog($"[VERIFY][ERR] {cell} 재추론 실패 x={xr.Success}({xr.FailedStep}) z={zr.Success}({zr.FailedStep})"); ngCnt++; continue; }

                    int resX = (int)Math.Round(xr.TravelMoveMm);
                    int resZ = (int)Math.Round(zr.HoistMoveMm);
                    bool good = Math.Abs(resX) <= VerifyResidualTolMm && Math.Abs(resZ) <= VerifyResidualTolMm;
                    if (good) okCnt++; else ngCnt++;
                    AddLog($"[VERIFY][{(good ? "OK" : "재확인")}] {cell} 잔차 주행={resX}mm 승강={resZ}mm");
                }
                catch (Exception ex) { AddLog($"[VERIFY][ERR] {cell} 예외: {ex.Message}"); ngCnt++; }
            }

            AddLog($"────────── 3단계 검증 완료 — OK {okCnt} / 재확인 {ngCnt} (총 {list.Count}) ──────────");
            AddLog("[VERIFY] ※ 현재는 '재추론 잔차' 기준. 비전 충돌범위/포크뻗기 물리검증은 추후 추가.");
        }

        private async void Btn_Verify_Click(object sender, RoutedEventArgs e)
        {
            if (isRunning) { MessageBox.Show("티칭 진행 중에는 검증할 수 없습니다.", "검증", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (!currentResults.Any(r => r.Success)) { MessageBox.Show("검증할 2단계 성공 결과가 없습니다.\n먼저 오토티칭을 수행하세요.", "검증", MessageBoxButton.OK, MessageBoxImage.Information); return; }

            string cameraId = GetCameraId();
            int n = currentResults.Count(r => r.Success);
            var ans = MessageBox.Show($"3단계 검증(재추론)을 시작합니다.\n2단계 성공 {n}셀로 순차 이동(0x59/셋업모드)하며 다시 추론해 잔차를 확인합니다.\n진행할까요?",
                                      "3단계 검증", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (ans != MessageBoxResult.OK) return;

            isRunning = true;
            gClass.str.SrmPacket[gClass.srmNum].isAutoTeaching = true;
            cts?.Dispose(); cts = new CancellationTokenSource();
            btn_Verify.IsEnabled = false;
            btn_Start.IsEnabled = false;
            btn_Stop.IsEnabled = true;
            try
            {
                SetStatus("VERIFY", ClrInfo);
                await RunVerifyReinferAsync(cameraId, cts.Token);
                SetStatus("VERIFY DONE", ClrDone);
            }
            catch (OperationCanceledException) { AddLog("[VERIFY] 사용자 취소"); SetStatus("STOPPED", ClrErr); }
            catch (Exception ex) { AddLog($"[VERIFY][ERR] {ex.Message}"); SetStatus("ERROR", ClrErr); }
            finally
            {
                isRunning = false;
                gClass.str.SrmPacket[gClass.srmNum].isAutoTeaching = false;
                btn_Verify.IsEnabled = true;
                btn_Start.IsEnabled = true;
                btn_Stop.IsEnabled = false;
            }
        }

        private void Btn_Excel_Click(object sender, RoutedEventArgs e)
        {
            // 가장 최근 엑셀 파일 열기
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SRM" + gClass.srmNum, "Teaching");
            string filePath = lastExcelPath;
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                if (Directory.Exists(dir))
                    filePath = Directory.GetFiles(dir, "AutoTeaching_*.xlsx").OrderByDescending(f => f).FirstOrDefault();
            }

            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    AddLog($"[ERR] Open Excel: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("No teaching data file yet.\nRun teaching first.", "Excel", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void Btn_Close_Click(object sender, RoutedEventArgs e)
        {
            if (isRunning)
            {
                MessageBox.Show("Stop teaching before closing.", "Auto Teaching", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 페이지 나갈 때 카메라 1, 3 둘 다 RTSP disconnect (ffmpeg 메모리 해제)
            foreach (string camId in new[] { "camera1", "camera3" })
            {
                try
                {
                    var dc = await visionApi.DisconnectRtspAsync(camId);
                    AddLog($"[CLOSE] {camId} RTSP disconnect success={dc?.Success}");
                }
                catch (Exception ex)
                {
                    AddLog($"[CLOSE] {camId} disconnect 예외 (무시): {ex.Message}");
                }
            }
            rtspConnected = false;
            pMain.Page_Change(cConstDefine.PAGE_SRMSET);
        }

        // ================================================================
        // TAB 2: 셀 테이블 1mm 쓰기 + SEMI_MOVE 포지셔닝
        // ================================================================
        //
        // 1. cellBay/cellLev 원본 백업
        // 2. 임시 배열 생성 (1mm 간격, 타겟 중심)
        //    tempBay[i] = targetMm - maxBay/2 + i  →  target = tempBay[maxBay/2]
        // 3. 0x95로 임시 배열 쓰기
        // 4. SEMI_MOVE로 타겟 인덱스 이동
        // 5. 도착 후 원본 배열 복원

        bool mmMoveInProgress = false;
        CancellationTokenSource mmCts;
        int[] backupBay, backupLev;
        bool backupExists = false;

        string MmBackupIniPath =>
            Path.Combine(Environment.CurrentDirectory, "SRM" + gClass.srmNum, "MmMoveBackup.ini");

        // 셀 배열(Bay/Lev) 원본 데이터 파일.
        // cIniAccess.Read는 버퍼 255자 제한이라 배열을 INI에 통째로 못 넣음 → 별도 파일에 저장.
        // 이 파일이 있어야 앱 재시작/크래시 후에도 원본 복구가 가능하다.
        string MmCellBackupDataPath =>
            Path.Combine(Environment.CurrentDirectory, "SRM" + gClass.srmNum, "MmCellBackup.dat");

        // ================================================================
        // 결과 확인·반영(RESULT REVIEW) — 셀 티칭 레코드 영속화 + 적용 마커
        // ================================================================

        // 셀별 측정값·이미지 경로 저장 파일 (런 완료 시 영속화 → 반자동 스트립 등에서 재조회)
        private string TeachingStatePath =>
            AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + gClass.srmNum + "\\Teaching\\TeachingState.ini";

        private void SaveTeachingState()
        {
            try
            {
                string ini = TeachingStatePath;
                string dir = Path.GetDirectoryName(ini);
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                foreach (var r in currentResults)
                {
                    if (!r.Success) continue;
                    string sec = CellKey(r.Row, r.Bay, r.Level);
                    cIniAccess.Write(ini, sec, "BayPos", r.BayPos.ToString());
                    cIniAccess.Write(ini, sec, "LevPos", r.LevelPos.ToString());
                    cIniAccess.Write(ini, sec, "RawPath", r.RawPath ?? "");
                    cIniAccess.Write(ini, sec, "CalibratedPath", r.CalibratedPath ?? "");
                    cIniAccess.Write(ini, sec, "Timestamp", ts);
                }
                AddLog($"[STATE] 셀 티칭 레코드 저장 ({currentResults.Count(x => x.Success)}셀) → TeachingState.ini");
            }
            catch (Exception ex) { AddLog($"[STATE][WARN] 티칭 레코드 저장 실패 — {ex.Message}"); }
        }

        // 적용 마커는 0-based 인덱스 키(cellBay[bay-1]와 정합). 섹션 RACK_APPLIED_BAY/RACK_APPLIED_LEV.
        private string RackIniPath =>
            AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + gClass.srmNum + "\\RACK\\Rack.ini";

        private void MarkApplied(int bay, int lev, string ts)
        {
            cIniAccess.Write(RackIniPath, "RACK_APPLIED_BAY", "BAY" + (bay - 1), ts);
            cIniAccess.Write(RackIniPath, "RACK_APPLIED_LEV", "LEV" + (lev - 1), ts);
        }

        // "" 이면 미적용 (defaultValue "nowrite" → 키 없을 때 빈 문자열 반환, ini에 쓰지 않음)
        private string AppliedTsBay(int bay) => cIniAccess.Read(RackIniPath, "RACK_APPLIED_BAY", "BAY" + (bay - 1), "nowrite");
        private string AppliedTsLev(int lev) => cIniAccess.Read(RackIniPath, "RACK_APPLIED_LEV", "LEV" + (lev - 1), "nowrite");

        // ---- RESULT REVIEW: 행 모델 + 채우기 + 이미지 표시 + 반영/반려 ----

        public class ReviewRow
        {
            public string Cell { get; set; }
            public int Existing { get; set; }       // 주행 기존
            public int Measured { get; set; }       // 주행 측정
            public int Deviation { get; set; }      // 주행 편차
            public int LiftExisting { get; set; }   // 승강 기존
            public int LiftMeasured { get; set; }   // 승강 측정
            public int LiftDeviation { get; set; }  // 승강 편차
            public bool Apply { get; set; }
            public int Bay { get; set; }
            public int Lev { get; set; }
            public string RawPath { get; set; }
            public string CalibratedPath { get; set; }
            public bool Success { get; set; }        // false면 추론 실패 셀 — 사진 확인용으로만 표시, 반영 불가
            public string Status => Success ? "" : "실패";
        }

        private readonly System.Collections.ObjectModel.ObservableCollection<ReviewRow> reviewRows = new();

        public void PopulateReview()
        {
            reviewRows.Clear();
            var info = gClass.str.SrmInfo[gClass.srmNum];
            foreach (var r in currentResults)
            {
                // 캡처 자체가 안 됐으면(사진 없음) 리뷰에 넣을 게 없다. 추론 실패(X/Z)는 사진 확인용으로 포함.
                if (!r.CaptureOk) continue;
                int existBay = (info.cellBay != null && r.Bay >= 1 && r.Bay <= info.cellBay.Length) ? info.cellBay[r.Bay - 1] : 0;
                int existLev = (info.cellLev != null && r.Level >= 1 && r.Level <= info.cellLev.Length) ? info.cellLev[r.Level - 1] : 0;
                // X추론까지 성공했으면 실측 주행값 표시, 아니면(X추론 자체 실패) 기존값 그대로(편차 0 — 반영 대상 아님)
                int measured = r.XInferenceOk ? r.BayPos : existBay;
                // Z추론까지 성공했으면 실측 승강값, 아니면(라이다 폴백·Z실패) 기존값(편차 0)
                int measuredLev = r.ZInferenceOk ? r.LevelPos : existLev;
                reviewRows.Add(new ReviewRow
                {
                    Cell = CellKey(r.Row, r.Bay, r.Level),
                    Existing = existBay,
                    Measured = measured,
                    Deviation = measured - existBay,
                    LiftExisting = existLev,
                    LiftMeasured = measuredLev,
                    LiftDeviation = measuredLev - existLev,
                    Apply = false,
                    Bay = r.Bay, Lev = r.Level,
                    RawPath = r.RawPath, CalibratedPath = r.CalibratedPath,
                    Success = r.Success
                });
            }
            // 실패 셀 먼저(사진 확인 편의), 그 다음 편차 큰 순
            var sorted = reviewRows.OrderByDescending(x => !x.Success).ThenByDescending(x => Math.Abs(x.Deviation)).ToList();
            reviewRows.Clear();
            foreach (var x in sorted) reviewRows.Add(x);
            grid_Review.ItemsSource = reviewRows;
            int needCheck = reviewRows.Count(x => Math.Abs(x.Deviation) > 15);
            Dispatcher.Invoke(() => lblRev_Badge.Content = needCheck > 0 ? $"확인 필요 {needCheck}" : "");
            UpdateSelInfo();
        }

        // ---- 'N개 셀 선택됨' 표시: Apply 체크된 행 수를 lblRev_SelInfo에 반영 ----
        private void UpdateSelInfo() => lblRev_SelInfo.Content = $"{reviewRows.Count(r => r.Apply)}개 셀 선택됨";

        private void Grid_Review_CellEditEnding(object sender, System.Windows.Controls.DataGridCellEditEndingEventArgs e)
            => Dispatcher.BeginInvoke(new Action(UpdateSelInfo));   // BeginInvoke: 체크박스 바인딩 커밋 이후 실행

        private static System.Windows.Media.Imaging.BitmapImage LoadImageNoLock(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            try
            {
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
                bmp.UriSource = new Uri(path);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }   // 비전이 덮어쓰는 중인 잘린/손상 이미지 → 빈 칸으로 강등
        }

        private void Grid_Review_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (grid_Review.SelectedItem is ReviewRow row)
            {
                // 표는 BAY 편차만 보여주지만 반영은 BAY+LEV 둘 다 쓰므로, 선택 셀 상세에 승강(LEV)도 노출.
                var info = gClass.str.SrmInfo[gClass.srmNum];
                var levSrc = currentResults.FirstOrDefault(x => x.Success && x.Bay == row.Bay && x.Level == row.Lev);
                img_Raw.Source = LoadImageNoLock(row.RawPath);
                img_Cal.Source = LoadImageNoLock(row.CalibratedPath);
                lblRev_TravBA.Content = $"주행 위치   {row.Existing} → {row.Measured}";
                int levExistBA = (info.cellLev != null && row.Lev >= 1 && row.Lev <= info.cellLev.Length) ? info.cellLev[row.Lev - 1] : 0;
                lblRev_LiftBA.Content = levSrc.Success ? $"승강 위치   {levExistBA} → {levSrc.LevelPos}" : "승강 위치   —";
            }
        }

        private void Btn_SelectNormal_Click(object sender, RoutedEventArgs e)
        {
            foreach (var r in reviewRows) r.Apply = r.Success;   // 실패 셀(사진 확인용)은 절대 선택하지 않음
            grid_Review.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
            grid_Review.Items.Refresh();
            UpdateSelInfo();
        }

        private void Btn_RejectCell_Click(object sender, RoutedEventArgs e)
        {
            if (grid_Review.SelectedItem is ReviewRow row)
            {
                row.Apply = false;
                grid_Review.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
                grid_Review.Items.Refresh();
                UpdateSelInfo();
            }
        }

        private async void Btn_ApplySelected_Click(object sender, RoutedEventArgs e)
        {
            // 재진입 차단: 두 반영 버튼(btn_ApplySelected/btnRev_ApplyOne)이 서로를 재진입시켜
            // 2번째 BackupCellArrays()가 이미 변경된 cellBay/cellLev를 "원본"으로 클론하면 롤백 기준선이 파괴됨.
            // ~10s WriteCellRangeAsync await 동안 두 버튼 모두 비활성화.
            btn_ApplySelected.IsEnabled = false;
            btnRev_ApplyOne.IsEnabled   = false;
            try { await ApplySelectedCellsAsync(); PopulateReview(); }
            finally { btn_ApplySelected.IsEnabled = true; btnRev_ApplyOne.IsEnabled = true; }
        }

        // ---- '이 셀 반영': 선택된 단일 셀만 Apply로 표시해 기존 안전 경로(ApplySelectedCellsAsync) 호출 ----
        private async void Btn_ApplyOne_Click(object sender, RoutedEventArgs e)
        {
            if (grid_Review.SelectedItem is not ReviewRow sel) return;
            foreach (var r in reviewRows) r.Apply = (r == sel);
            UpdateSelInfo();
            grid_Review.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
            grid_Review.Items.Refresh();
            btn_ApplySelected.IsEnabled = false;
            btnRev_ApplyOne.IsEnabled   = false;
            try { await ApplySelectedCellsAsync(); PopulateReview(); }
            finally { btn_ApplySelected.IsEnabled = true; btnRev_ApplyOne.IsEnabled = true; }
        }

        // ---- 반영(0x95 쓰기): 백업 → in-memory 갱신 → 전체범위 쓰기 → 적용마커 ⚠️ 실기 게이트 ----
        private async Task ApplySelectedCellsAsync()
        {
            // Success=false(사진 확인용 실패 셀)는 체크박스가 수동으로 켜져 있어도 절대 반영하지 않는다(2중 안전장치).
            var picked = reviewRows.Where(r => r.Apply && r.Success).ToList();
            if (picked.Count == 0)
            {
                bool onlyFailedChecked = reviewRows.Any(r => r.Apply && !r.Success);
                MessageBox.Show(onlyFailedChecked ? "실패한 셀은 반영할 수 없습니다(측정값 없음)." : "반영할 셀을 선택하세요.",
                    "결과 반영", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            // 셀 배열 미로드(SRM 미연결) 가드 — BackupCellArrays() 이전에 차단해야
            // 백업만 남기고 NullReferenceException으로 중단되는 상황을 막는다.
            var info0 = gClass.str.SrmInfo[gClass.srmNum];
            if (info0.cellBay == null || info0.cellLev == null)
            {
                MessageBox.Show("셀 배열이 로드되지 않았습니다 (SRM 미연결?).", "결과 반영", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var confirm = MessageBox.Show($"{picked.Count}개 셀을 SRM에 반영합니다.\n반영 전 기존값은 자동 백업됩니다.\n\n진행할까요?",
                "결과 반영", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            // 1) 자동 백업 (기존 메커니즘, Pending=1)
            BackupCellArrays();

            // 2) in-memory 갱신 (선택 셀의 bay/lev 인덱스)
            var info = gClass.str.SrmInfo[gClass.srmNum];
            foreach (var r in picked)
            {
                if (info.cellBay != null && r.Bay >= 1 && r.Bay <= info.cellBay.Length) info.cellBay[r.Bay - 1] = r.Measured;
                // Lev 측정값: reviewRows는 Bay 기준 측정만 담았으므로, currentResults에서 LevelPos 조회
                var src = currentResults.FirstOrDefault(x => x.Success && x.Bay == r.Bay && x.Level == r.Lev);
                // src.Success 가드: 매칭 실패 시 default struct(LevelPos 0)가 크레인에 0mm를 쓰는 사고 방지
                if (src.Success && info.cellLev != null && r.Lev >= 1 && r.Lev <= info.cellLev.Length) info.cellLev[r.Lev - 1] = src.LevelPos;
            }
            gClass.str.SrmInfo[gClass.srmNum] = info;

            // 3) 전체범위 0x95 쓰기 (RestoreCellArraysAsync와 동일한 호출 형태)
            bool bayOk = await WriteCellRangeAsync(1, 0, info.cellBay.Length - 1, info.cellBay);
            bool levOk = await WriteCellRangeAsync(2, 0, info.cellLev.Length - 1, info.cellLev);

            if (bayOk && levOk)
            {
                // 4) 적용 마커 + 백업 커밋 (Pending=0)
                string ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                foreach (var r in picked) MarkApplied(r.Bay, r.Lev, ts);
                cIniAccess.Write(MmBackupIniPath, "MM_BACKUP", "Pending", "0");
                AddLog($"[APPLY] {picked.Count}셀 반영 완료 (0x95 ACK)");
                MessageBox.Show($"{picked.Count}개 셀 반영 완료.", "결과 반영", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // ACK 실패 → 적용 마커 미기록. 백업(Pending=1)은 그대로 남아 다음 진입 시 자동 롤백.
                AddLog($"[APPLY][FAIL] 0x95 ACK 실패 (bay={bayOk} lev={levOk}) — 미반영, 백업 보존(자동 롤백 대상)");
                MessageBox.Show("0x95 반영 실패. 다음 페이지 진입 시 자동 복구됩니다.", "결과 반영", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // 페이지 진입 시 자동 복구: MM_BACKUP Pending=1 이면 셀 배열을 원본으로 되돌린다.
        // SRM 연결/셀 로드/셋업모드가 모두 보장될 때만 실제 write. 안 되면 Pending 유지 → [CAL 복구] 버튼.
        private async Task CheckAndRestorePendingMmBackup()
        {
            try
            {
                string ini = MmBackupIniPath;
                if (!File.Exists(ini)) return;
                if (cIniAccess.Read(ini, "MM_BACKUP", "Pending", "nowrite") != "1") return;

                AddMmLog("[RESTORE] 이전 세션의 셀 배열 백업 감지 — 자동 복구 시도");

                // SRM에서 셀 배열이 로드될 때까지 대기 (최대 ~20초). 미연결이면 보류 → 버튼 복구.
                int waitCnt = 0;
                while (waitCnt < 100 &&
                       (gClass.str.SrmInfo[gClass.srmNum].cellBay == null || gClass.str.SrmInfo[gClass.srmNum].cellBay.Length == 0))
                {
                    await Task.Delay(200);
                    waitCnt++;
                }
                if (gClass.str.SrmInfo[gClass.srmNum].cellBay == null || gClass.str.SrmInfo[gClass.srmNum].cellBay.Length == 0)
                {
                    AddMmLog("[RESTORE][WARN] 셀 배열 로드 타임아웃(SRM 미연결?) — [CAL 복구] 버튼으로 수동 복구하세요");
                    return;
                }

                await RestoreCellArraysAsync();
            }
            catch (Exception ex)
            {
                AddMmLog($"[RESTORE][ERR] 자동 복구 예외: {ex.Message}");
            }
        }

        // ---- 0x95 배열 쓰기 ----

        private async Task<bool> WriteCellRangeAsync(int dataType, int start, int end, int[] values)
        {
            string typeStr = dataType == 1 ? "Bay" : "Lev";
            int count = end - start + 1;

            // 배열 크기를 실제 전송 개수에 맞춤 (배열이 더 크면 잘라서 보냄)
            int[] sendData;
            if (values.Length == count)
            {
                sendData = values;
            }
            else
            {
                sendData = new int[count];
                Array.Copy(values, start, sendData, 0, count);
            }

            var packet = gClass.str.SrmPacket[gClass.srmNum];
            packet.cellPosWriteType = dataType;
            packet.cellPosWriteStart = start;
            packet.cellPosWriteEnd = end;
            packet.cellPosWriteData = sendData;
            packet.cellPosWriteDone = false;
            packet.cellPosWriteNack = false;
            packet.cellPosWriteReq = true;
            gClass.str.SrmPacket[gClass.srmNum] = packet;

            AddMmLog($"[0x95] Write {typeStr}[{start}..{end}] count={count} first={sendData[0]}mm last={sendData[count - 1]}mm");

            // 디버그: 전체 값 로그
            string valStr = "";
            for (int i = 0; i < Math.Min(count, 20); i++)
                valStr += $"{sendData[i]} ";
            if (count > 20) valStr += $"... (총{count}개)";
            AddMmLog($"[0x95] {typeStr} data=[{valStr.TrimEnd()}]");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
            {
                await Task.Delay(100);

                var curPacket = gClass.str.SrmPacket[gClass.srmNum];
                if (curPacket.cellPosWriteDone)
                {
                    if (curPacket.cellPosWriteNack)
                    {
                        AddMmLog($"[0x95] {typeStr} NACK reason=0x{curPacket.cellPosWriteNackReason:X2} ({sw.ElapsedMilliseconds}ms)");
                        return false;
                    }
                    AddMmLog($"[0x95] {typeStr} ACK ({sw.ElapsedMilliseconds}ms)");
                    return true;
                }

                // cellPosWriteReq가 false로 바뀌었는지 확인 (송신 완료 여부)
                if (i == 10)
                {
                    AddMmLog($"[0x95] {typeStr} 1초 경과 — cellPosWriteReq={curPacket.cellPosWriteReq} cellPosWriteDone={curPacket.cellPosWriteDone}");
                }
            }

            // 타임아웃 시 상태 덤프
            var finalPacket = gClass.str.SrmPacket[gClass.srmNum];
            AddMmLog($"[0x95] {typeStr} TIMEOUT ({sw.ElapsedMilliseconds}ms) req={finalPacket.cellPosWriteReq} done={finalPacket.cellPosWriteDone} nack={finalPacket.cellPosWriteNack}");
            return false;
        }

        // ---- CAL 복구 버튼 핸들러 (셀 배열 MM_BACKUP 원본 복구) ----
        private async void Btn_CalRestore_Click(object sender, RoutedEventArgs e)
        {
            string ini = MmBackupIniPath;
            if (!File.Exists(ini))
            {
                MessageBox.Show("백업 INI 파일이 없습니다.", "CAL 복구",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            bool mmPending = cIniAccess.Read(ini, "MM_BACKUP", "Pending") == "1";
            if (!mmPending)
            {
                MessageBox.Show("복구할 백업이 없습니다 (Pending=0).", "CAL 복구",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string ts = cIniAccess.Read(ini, "MM_BACKUP", "Timestamp");
            string mb = cIniAccess.Read(ini, "MM_BACKUP", "MaxBay");
            string ml = cIniAccess.Read(ini, "MM_BACKUP", "MaxLev");
            var ans = MessageBox.Show(
                $"셀 배열 원본으로 복구합니다 (셋업모드 진입 후 0x95 SRM 쓰기)\n\n[CELL ARRAY] {ts}\n  Bay {mb} + Lev {ml}\n\n진행하시겠습니까?",
                "CAL 복구", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (ans != MessageBoxResult.OK) return;

            btn_calRestore.IsEnabled = false;
            try
            {
                AddLog("[CAL-RESTORE-BTN] 셀 배열(MM_BACKUP) 복구 시도");
                bool ok = await RestoreCellArraysAsync();
                MessageBox.Show(ok ? "복구 완료." : "복구 실패. 로그 확인 + SRM 알람/키 위치 점검 후 재시도.",
                                "CAL 복구", MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            finally
            {
                btn_calRestore.IsEnabled = true;
            }
        }

        // ---- 백업/복원 ----

        private void BackupCellArrays()
        {
            try
            {
                var info = gClass.str.SrmInfo[gClass.srmNum];
                int maxBay = info.bay;
                int maxLev = info.lev;
                backupBay = (int[])info.cellBay.Clone();
                backupLev = (int[])info.cellLev.Clone();

                string ini = MmBackupIniPath;
                string dir = Path.GetDirectoryName(ini);
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                // ★ 실제 배열은 별도 파일에 저장 (재시작/크래시 후 복구용).
                //   순서 주의: 데이터 파일을 먼저 완성 → 메타 → 맨 마지막에 Pending=1.
                //   (중간 크래시 시 Pending=0 으로 남아 반쪽 백업이 복구되는 사고 방지)
                string bayCsv = string.Join(",", backupBay.Take(maxBay));
                string levCsv = string.Join(",", backupLev.Take(maxLev));
                File.WriteAllLines(MmCellBackupDataPath, new[] { "BAY," + bayCsv, "LEV," + levCsv });

                cIniAccess.Write(ini, "MM_BACKUP", "Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cIniAccess.Write(ini, "MM_BACKUP", "MaxBay", maxBay.ToString());
                cIniAccess.Write(ini, "MM_BACKUP", "MaxLev", maxLev.ToString());
                cIniAccess.Write(ini, "MM_BACKUP", "Pending", "1");   // ← 맨 마지막 (이 줄이 써져야 "복구 대상 있음")

                backupExists = true;
                AddMmLog($"[BACKUP] 원본 셀 배열 백업 완료 (Bay {maxBay} + Lev {maxLev}) → {Path.GetFileName(MmCellBackupDataPath)}");
            }
            catch (Exception ex)
            {
                // 백업 실패(파일/INI IO) 시 런을 죽이지 않고 경고만 — 단, rollback 불가임을 명확히 표면화.
                backupExists = false;
                AddLog($"[BACKUP][WARN] 셀 원본 백업 실패 (rollback 불가) — {ex.Message}");
            }
        }

        // 셀 배열(Bay/Lev) 원본 복구 — 메모리가 아니라 파일에서 읽으므로 재시작/크래시 후에도 동작.
        // 0x95 write는 셋업모드 전제 → 진입 실패 시 Pending 유지하고 false (다음 진입/버튼에서 재시도).
        private async Task<bool> RestoreCellArraysAsync()
        {
            string ini = MmBackupIniPath;
            if (cIniAccess.Read(ini, "MM_BACKUP", "Pending", "nowrite") != "1")
            {
                AddMmLog("[RESTORE] MM_BACKUP Pending 아님 — 복구 불필요");
                return true;
            }

            // ★ 원본 배열을 파일에서 로드 (메모리 의존 X)
            if (!TryLoadCellBackupFile(out int[] bayArr, out int[] levArr))
            {
                AddMmLog("[RESTORE][ERR] 셀 백업 데이터 파일 없음/손상 — 복구 불가 (Pending 유지). " + MmCellBackupDataPath);
                return false;
            }

            int srmNum = gClass.srmNum;

            // ★ 셋업모드 진입 (0x95 write 필수). 실패하면 복구 보류.
            if (gClass.str.SrmState[srmNum].setupMode == 0)
            {
                AddMmLog("[RESTORE] 셋업모드 진입 시도 (0x95 write 필수)");
                if (!await SetCraneModeAsync(1, 5000))
                {
                    AddMmLog("[RESTORE][ERR] 셋업모드 진입 실패 — 복구 보류 (Pending 유지). 알람/키 확인 후 [CAL 복구] 버튼");
                    return false;
                }
                await Task.Delay(300);
            }

            AddMmLog($"[RESTORE] 원본 셀 배열 복원 시작 (Bay {bayArr.Length} + Lev {levArr.Length})...");
            bool bayOk = await WriteCellRangeAsync(1, 0, bayArr.Length - 1, bayArr);
            bool levOk = await WriteCellRangeAsync(2, 0, levArr.Length - 1, levArr);

            if (bayOk && levOk)
            {
                // 로컬 캐시도 원본으로 맞춰둠
                var info = gClass.str.SrmInfo[srmNum];
                if (info.cellBay != null) Array.Copy(bayArr, info.cellBay, Math.Min(bayArr.Length, info.cellBay.Length));
                if (info.cellLev != null) Array.Copy(levArr, info.cellLev, Math.Min(levArr.Length, info.cellLev.Length));
                backupExists = false;
                cIniAccess.Write(ini, "MM_BACKUP", "Pending", "0");
                AddMmLog("[RESTORE] ✓ 셀 배열 복원 완료");
                return true;
            }

            // 부분 복구(한쪽만 성공) = 셀 그리드 혼합 상태 → 매우 명확히 경고하고 Pending 유지(다음 진입/버튼에서 재시도해 양축 일치).
            //   진정한 원자성은 0x95가 Bay/Lev 2회 분리 전송이라 불가 → 재시도로 수렴시키는 게 현실적.
            if (bayOk != levOk)
                AddMmLog($"[RESTORE][★경고] 부분 복구 — Bay={(bayOk ? "성공" : "실패")} Lev={(levOk ? "성공" : "실패")}. 셀 그리드가 혼합 상태일 수 있음! Pending 유지 → [CAL 복구]로 재시도해 양축을 일치시킬 것.");
            else
                AddMmLog($"[RESTORE][ERR] 0x95 복원 실패 Bay={bayOk} Lev={levOk} — Pending 유지(재시도)");
            return false;
        }

        /// <summary>MmCellBackup.dat 에서 Bay/Lev 원본 배열 로드. 성공 시 true.</summary>
        private bool TryLoadCellBackupFile(out int[] bayArr, out int[] levArr)
        {
            bayArr = null; levArr = null;
            try
            {
                string path = MmCellBackupDataPath;
                if (!File.Exists(path)) return false;
                foreach (var line in File.ReadAllLines(path))
                {
                    if (line.StartsWith("BAY,")) bayArr = ParseCsvInts(line.Substring(4));
                    else if (line.StartsWith("LEV,")) levArr = ParseCsvInts(line.Substring(4));
                }
                return bayArr != null && bayArr.Length > 0 && levArr != null && levArr.Length > 0;
            }
            catch (Exception ex)
            {
                AddMmLog($"[RESTORE][ERR] 백업 파일 파싱 예외: {ex.Message}");
                return false;
            }
        }

        private static int[] ParseCsvInts(string csv)
        {
            var parts = csv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var arr = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++) arr[i] = int.Parse(parts[i]);
            return arr;
        }

        // ---- 모드 전환 ----

        /// <summary>
        /// SRM 모드 전환 (0x58)
        /// mode: 0=수동, 1=셋업, 2=자동
        /// </summary>
        private async Task<bool> WaitFlagAsync(Func<bool> flag, int timeoutMs, CancellationToken ct)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs && !ct.IsCancellationRequested)
            {
                if (flag()) return true;
                await Task.Delay(50, ct);   // 완료 감지 폴링(100→50ms) — 동작 동일, 감지만 빠르게
            }
            return false;
        }

        // ── 0xA3/A5 읽기 · 0xA4/A6 쓰기 공통 헬퍼(중복 제거) ──
        //   요청 플래그 세팅 + 완료 대기까지만. 결과코드(writeResult)·에러로그·길이검사는 호출부가 그대로 수행.
        /// <summary>0xA3 Drive 파라미터 읽기 요청 + 완료 대기(data!=null && Length>=minLen). 성공 시 driveParamData에 데이터.</summary>
        private async Task<bool> ReadDriveParamAsync(int minLen, int timeoutMs, CancellationToken ct)
        {
            int n = gClass.srmNum;
            gClass.str.SrmPacket[n].driveParamData = null;
            gClass.str.SrmPacket[n].driveParamReadDone = false;
            gClass.str.SrmPacket[n].driveParamReadReq = true;
            return await WaitFlagAsync(() => gClass.str.SrmPacket[n].driveParamData != null
                && gClass.str.SrmPacket[n].driveParamData.Length >= minLen, timeoutMs, ct);
        }

        /// <summary>0xA5 Lift 파라미터 읽기 요청 + 완료 대기. 성공 시 liftParamData에 데이터.</summary>
        private async Task<bool> ReadLiftParamAsync(int minLen, int timeoutMs, CancellationToken ct)
        {
            int n = gClass.srmNum;
            gClass.str.SrmPacket[n].liftParamData = null;
            gClass.str.SrmPacket[n].liftParamReadDone = false;
            gClass.str.SrmPacket[n].liftParamReadReq = true;
            return await WaitFlagAsync(() => gClass.str.SrmPacket[n].liftParamData != null
                && gClass.str.SrmPacket[n].liftParamData.Length >= minLen, timeoutMs, ct);
        }

        /// <summary>0xA4 Drive 파라미터 쓰기 요청 + 완료 대기. 결과코드는 driveParamWriteResult로 호출부가 확인.</summary>
        private async Task<bool> WriteDriveParamAsync(byte[] ctrl, int timeoutMs, CancellationToken ct)
        {
            int n = gClass.srmNum;
            gClass.str.SrmPacket[n].driveParamWriteData = ctrl;
            gClass.str.SrmPacket[n].driveParamWriteDone = false;
            gClass.str.SrmPacket[n].driveParamWriteReq = true;
            return await WaitFlagAsync(() => gClass.str.SrmPacket[n].driveParamWriteDone, timeoutMs, ct);
        }

        /// <summary>0xA6 Lift 파라미터 쓰기 요청 + 완료 대기. 결과코드는 liftParamWriteResult로 호출부가 확인.</summary>
        private async Task<bool> WriteLiftParamAsync(byte[] ctrl, int timeoutMs, CancellationToken ct)
        {
            int n = gClass.srmNum;
            gClass.str.SrmPacket[n].liftParamWriteData = ctrl;
            gClass.str.SrmPacket[n].liftParamWriteDone = false;
            gClass.str.SrmPacket[n].liftParamWriteReq = true;
            return await WaitFlagAsync(() => gClass.str.SrmPacket[n].liftParamWriteDone, timeoutMs, ct);
        }

        /// <summary>
        /// 0x52 이상리셋 명령 송신 후 1초 대기. SRM 알람으로 모드 전환 거부 시 자동 복구용.
        /// </summary>
        private async Task ResetAlarmAsync()
        {
            AddMmLog("[RESET] 0x52 이상리셋 송신");
            var pkt = gClass.str.SrmPacket[gClass.srmNum];
            pkt.resetCmd = 1;
            pkt.pulseClicked = true;
            gClass.str.SrmPacket[gClass.srmNum] = pkt;
            await Task.Delay(1000); // 송신/처리 대기
        }

        /// <summary>
        /// 0x53 작업삭제 (Fork1/Fork2 전체) 송신 후 1초 대기.
        /// 완료/실패/정지 상태의 작업 잔류 제거용.
        /// </summary>
        private async Task DeleteAllJobsAsync()
        {
            AddMmLog("[RESET] 0x53 작업삭제 송신 (Fork1+Fork2)");
            var pkt = gClass.str.SrmPacket[gClass.srmNum];
            pkt.wcsCmdDeleteAll = true;
            gClass.str.SrmPacket[gClass.srmNum] = pkt;
            await Task.Delay(1000); // 송신/처리 대기
        }

        private async Task<bool> SetCraneModeAsync(byte mode, int timeoutMs = 5000, bool forceMode = false)
        {
            // 1차: 호출자 지정 옵션 그대로
            bool ok = await SetCraneModeOnceAsync(mode, timeoutMs, forceMode);
            if (ok) return true;

            // 2차: 이상리셋 후 재시도 (호출자 지정 옵션)
            AddMmLog("[MODE] 모드 전환 실패 → 0x52 이상리셋 + 재시도");
            await ResetAlarmAsync();
            await Task.Delay(500);
            ok = await SetCraneModeOnceAsync(mode, timeoutMs, forceMode);
            if (ok) { AddMmLog("[MODE] 이상리셋 후 재시도 ✓ 성공"); return true; }

            // 3차: 셋업(cmd=1) 거부 시 → 수동+강제(cmd=0 opt=1) 우회 진입 후 셋업(cmd=1 opt=0) 재시도
            // 사용자 환경에서 셋업+강제(cmd=1+opt=1) 동시 송신은 SRM이 거부함 → 단계 분리
            if (mode == 1 && !forceMode)
            {
                AddMmLog("[MODE] 셋업 거부 → 수동+강제(cmd=0 opt=1) 우회 진입 시도");
                await ResetAlarmAsync();
                await Task.Delay(500);
                bool manForceOk = await SetCraneModeOnceAsync(0, timeoutMs, forceMode: true);
                if (manForceOk)
                {
                    AddMmLog("[MODE] 수동+강제 OK → 셋업(cmd=1 opt=0) 재시도");
                    await Task.Delay(300);
                    ok = await SetCraneModeOnceAsync(1, timeoutMs, forceMode: false);
                    if (ok) { AddMmLog("[MODE] 셋업 우회 진입 ✓ 성공"); return true; }
                }
            }
            // 수동 진입 거부 시 → 수동+강제(cmd=0 opt=1)만 시도
            else if (mode == 0 && !forceMode)
            {
                AddMmLog("[MODE] 수동 거부 → 수동+강제(cmd=0 opt=1) 시도");
                await ResetAlarmAsync();
                await Task.Delay(500);
                ok = await SetCraneModeOnceAsync(0, timeoutMs, forceMode: true);
                if (ok) { AddMmLog("[MODE] 수동+강제 ✓ 성공"); return true; }
            }

            // 4차: 셋업 진입 거부 시 → 0x52 이상리셋 + 0x53 작업삭제 → 셋업 재시도
            // (잔류 완료/실패 작업으로 셋업 거부되는 케이스 — Vexi 안내문 참고)
            if (mode == 1 && !forceMode)
            {
                AddMmLog("[MODE] 셋업 여전히 거부 → 0x52 이상리셋 + 0x53 작업삭제 후 셋업 재시도");
                await ResetAlarmAsync();
                await DeleteAllJobsAsync();
                await Task.Delay(500);
                ok = await SetCraneModeOnceAsync(1, timeoutMs, forceMode: false);
                if (ok) { AddMmLog("[MODE] 이상리셋+작업삭제 후 셋업 ✓ 성공"); return true; }
            }

            AddMmLog($"[MODE][FATAL] 모드 전환 최종 실패 — SRM 측 점검 필요 (알람/키 스위치/인버터 fault/작업잔류)");
            AddMmLog($"[MODE][FATAL] 권장: Vexi에서 디바이스 리셋(이상리셋) 직접 실행");
            return false;
        }

        private async Task<bool> SetCraneModeOnceAsync(byte mode, int timeoutMs, bool forceMode)
        {
            string[] modeNames = { "수동", "셋업", "자동" };
            string modeName = mode < modeNames.Length ? modeNames[mode] : $"0x{mode:X2}";
            string forceSuffix = forceMode ? "+강제" : "";

            var packet = gClass.str.SrmPacket[gClass.srmNum];
            packet.modeSetCmd = mode;
            packet.modeSetOpt = (byte)(forceMode ? 1 : 0);  // Bit0 = 강제모드
            packet.modeSetReq = true;
            gClass.str.SrmPacket[gClass.srmNum] = packet;

            AddMmLog($"[MODE] 0x58 → {modeName}{forceSuffix}모드 (cmd={mode}, opt={packet.modeSetOpt})");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                await Task.Delay(80); // 모드 확인 폴링 (300→80ms, 전환 즉시 감지)
                var state = gClass.str.SrmState[gClass.srmNum];
                bool ok = mode switch
                {
                    0 => state.manualMode > 0,
                    1 => state.setupMode > 0,
                    2 => state.autoMode > 0,
                    _ => false
                };
                if (ok)
                {
                    AddMmLog($"[MODE] {modeName}{forceSuffix}모드 확인 OK ({sw.ElapsedMilliseconds}ms)");
                    return true;
                }
            }

            var cur = gClass.str.SrmState[gClass.srmNum];
            AddMmLog($"[ERR] {modeName}{forceSuffix}모드 전환 타임아웃 auto={cur.autoMode} manual={cur.manualMode} setup={cur.setupMode}");
            return false;
        }

        // ---- UI ----

        private void Btn_MmReadPos_Click(object sender, RoutedEventArgs e)
        {
            int t = CurTrav;
            int l = CurLift;
            lbl_mmBayId.Content = $"{t}mm";
            lbl_mmLevId.Content = $"{l}mm";
            AddMmLog($"[POS] TRAV={t}mm  LIFT={l}mm");
        }

        // ================================================================
        // 로그 폴더 열기 / 화면 로그 클리어
        //   파일 로그: SRM{N}/Teaching/Log (Auto Teaching) , SRM{N}/Teaching/MmLog (mm-move)
        //   시간별 분리 저장 (예: AutoTeachingLog_20260430_14.txt)
        // ================================================================

        private void Btn_OpenLog_Click(object sender, RoutedEventArgs e)
        {
            string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "SRM" + gClass.srmNum, "Teaching", "Log");
            OpenFolderSafe(logDir, "Auto Teaching Log");
        }

        private void Btn_OpenMmLog_Click(object sender, RoutedEventArgs e)
        {
            string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "SRM" + gClass.srmNum, "Teaching", "MmLog");
            OpenFolderSafe(logDir, "MM Move Log");
        }

        private void OpenFolderSafe(string dir, string label)
        {
            try
            {
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{label} 폴더 열기 실패:\n경로: {dir}\n에러: {ex.Message}",
                    "Open Folder", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Btn_ClearLog_Click(object sender, RoutedEventArgs e)
        {
            listBox_Log.Items.Clear();
            AddLog("[CLEAR] 화면 로그 클리어 (파일 로그는 유지됨)");
        }

        private void Btn_ClearMmLog_Click(object sender, RoutedEventArgs e)
        {
            listBox_MmLog.Items.Clear();
            AddMmLog("[CLEAR] 화면 로그 클리어 (파일 로그는 유지됨)");
        }

        // ================================================================
        // SoftLimit 영구 편집 (Vexi 화면 대체) — 0xA3/A5 read, 0xA4/A6 write
        //   Vexi Form_SRM*Param_Speed의 SoftLimit_HomePos/EndPos 필드와 동일 동작
        //   파라미터 구조체 offset: SoftLimit_DetectSet[178] / HomePos[179..182] / EndPos[183..186]
        // ================================================================

        private async void Btn_SlRead_Click(object sender, RoutedEventArgs e)
        {
            if (isRunning || mmMoveInProgress) { AddMmLog("[SL-READ] 티칭/이동 진행 중 — 파라미터 공유 충돌 방지로 차단"); return; }
            btn_slRead.IsEnabled = false;
            btn_slSave.IsEnabled = false;
            using var slCts = new CancellationTokenSource(15000);   // using — 핸들러 종료 시 내부 타이머 확정 해제
            var ct = slCts.Token;

            try
            {
                AddMmLog("[SL-READ] Drive 파라미터 읽기 (0xA3)...");
                if (!await ReadDriveParamAsync(187, 10000, ct))
                {
                    AddMmLog("[ERR] Drive 읽기 실패");
                    return;
                }
                byte[] dd = gClass.str.SrmPacket[gClass.srmNum].driveParamData;
                byte dDet = dd[178];
                uint dHome = BitConverter.ToUInt32(dd, 179);
                uint dEnd = BitConverter.ToUInt32(dd, 183);
                // ManualOp 범위도 함께 표시 (01-08 진단용)
                uint dMoStart = BitConverter.ToUInt32(dd, 132);
                uint dMoEnd = BitConverter.ToUInt32(dd, 136);
                Dispatcher.Invoke(() =>
                {
                    edit_slDriveHome.Text = dHome.ToString();
                    edit_slDriveEnd.Text = dEnd.ToString();
                });
                AddMmLog($"[SL-READ] Drive SoftLimit Det={dDet} Home={dHome}mm End={dEnd}mm");
                AddMmLog($"[SL-READ] Drive ManualOp  Start={dMoStart}mm End={dMoEnd}mm  ← 수동 jog 허용 범위");

                AddMmLog("[SL-READ] Lift 파라미터 읽기 (0xA5)...");
                if (!await ReadLiftParamAsync(187, 10000, ct))
                {
                    AddMmLog("[ERR] Lift 읽기 실패");
                    return;
                }
                byte[] ld = gClass.str.SrmPacket[gClass.srmNum].liftParamData;
                byte lDet = ld[178];
                int lHome = BitConverter.ToInt32(ld, 179);
                int lEnd = BitConverter.ToInt32(ld, 183);
                // Lift ManualOp는 Int32 (signed)
                int lMoStart = BitConverter.ToInt32(ld, 132);
                int lMoEnd = BitConverter.ToInt32(ld, 136);
                Dispatcher.Invoke(() =>
                {
                    edit_slLiftHome.Text = lHome.ToString();
                    edit_slLiftEnd.Text = lEnd.ToString();
                });
                AddMmLog($"[SL-READ] Lift  SoftLimit Det={lDet} Home={lHome}mm End={lEnd}mm");
                AddMmLog($"[SL-READ] Lift  ManualOp  Start={lMoStart}mm End={lMoEnd}mm  ← 수동 jog 허용 범위");

                // === Lift 파라미터 전체 덤프 (1000=0x3E8 또는 970=0x3CA 패턴 위치 찾기) ===
                AddMmLog($"[SL-READ] Lift 전체 dump len={ld.Length}B — 1000 또는 970 패턴 위치 찾기");
                // 4-byte Int32 슬라이딩으로 1000 또는 970 ± 30 패턴 찾기
                for (int off = 0; off <= ld.Length - 4; off += 1)
                {
                    int v = BitConverter.ToInt32(ld, off);
                    if ((v >= 950 && v <= 1100) || (v >= -1100 && v <= -950))
                    {
                        AddMmLog($"  offset[{off}] Int32={v}mm  hex=[{ld[off]:X2} {ld[off + 1]:X2} {ld[off + 2]:X2} {ld[off + 3]:X2}]");
                    }
                }
                // hex 덤프 (16바이트씩 줄바꿈)
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < ld.Length; i++)
                {
                    if (i % 16 == 0) sb.Append($"\n  [{i:D3}] ");
                    sb.Append($"{ld[i]:X2} ");
                }
                AddMmLog($"[SL-READ] Lift hex dump:{sb}");
            }
            catch (Exception ex)
            {
                AddMmLog($"[ERR] SL Read: {ex.Message}");
            }
            finally
            {
                btn_slRead.IsEnabled = true;
                btn_slSave.IsEnabled = true;
            }
        }

        private async void Btn_SlSave_Click(object sender, RoutedEventArgs e)
        {
            if (isRunning || mmMoveInProgress) { AddMmLog("[SL-SAVE] 티칭/이동 진행 중 — 파라미터 공유 충돌 방지로 차단"); return; }

            // 입력 검증
            if (!uint.TryParse(edit_slDriveHome.Text.Trim(), out uint newDriveHome) ||
                !uint.TryParse(edit_slDriveEnd.Text.Trim(), out uint newDriveEnd))
            {
                MessageBox.Show("Drive 값은 양수(UInt32)여야 합니다.", "SL Save",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(edit_slLiftHome.Text.Trim(), out int newLiftHome) ||
                !int.TryParse(edit_slLiftEnd.Text.Trim(), out int newLiftEnd))
            {
                MessageBox.Show("Lift 값은 정수(Int32, 음수 가능)여야 합니다.", "SL Save",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (newDriveEnd <= newDriveHome || newLiftEnd <= newLiftHome)
            {
                MessageBox.Show("End가 Home보다 커야 합니다.", "SL Save",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var preState = gClass.str.SrmState[gClass.srmNum];
            var ans = MessageBox.Show(
                $"⚠️ 위치 제한을 SRM에 영구 저장합니다.\n\n" +
                $"Drive Home: {newDriveHome}mm   End: {newDriveEnd}mm  (SoftLimit만 변경)\n" +
                $"Lift  Home: {newLiftHome}mm   End: {newLiftEnd}mm   ← 3중 동기화\n" +
                $"   • SoftLimit_Home/End\n" +
                $"   • ManualOp_Start (= Home, 수동조깅 영역)\n" +
                $"   • RefSetDog_HomePos (= Home, 위치 명령 dog)\n\n" +
                $"현재 모드: setup={preState.setupMode} manual={preState.manualMode} auto={preState.autoMode}\n" +
                $"(셋업모드 권장)\n\n" +
                $"안전 지침:\n" +
                $"• 셋업 mm 이동(0x59)이 SoftLimit/ManualOp/RefSetDog 3곳 모두 통과해야 함\n" +
                $"• 하드 리밋(센서)은 최종 보호선 — 그 안에서만 변경\n" +
                $"• 너무 극단적인 값은 충돌 위험 (실제 운영 영역 + 마진)\n" +
                $"• 변경 후 mm-move로 검증\n\n" +
                $"진행하시겠습니까?",
                "위치 제한 영구 저장", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (ans != MessageBoxResult.OK) return;

            btn_slRead.IsEnabled = false;
            btn_slSave.IsEnabled = false;
            using var slCts = new CancellationTokenSource(45000);   // using — 핸들러 종료 시 내부 타이머 확정 해제
            var ct = slCts.Token;

            try
            {
                // 셋업모드 전환 (필요시)
                if (preState.setupMode == 0)
                {
                    AddMmLog("[SL-SAVE] 셋업모드 전환...");
                    if (!await SetCraneModeAsync(1, 5000))
                    {
                        AddMmLog("[ERR] 셋업모드 전환 실패");
                        return;
                    }
                    await Task.Delay(500, ct);
                }

                // ───────── Drive: read → modify SoftLimit → write (0xA4) ─────────
                AddMmLog("[SL-SAVE] Drive 현재 파라미터 읽기...");
                if (!await ReadDriveParamAsync(187, 10000, ct))
                {
                    AddMmLog("[ERR] Drive 읽기 실패");
                    return;
                }
                byte[] driveData = gClass.str.SrmPacket[gClass.srmNum].driveParamData;
                uint origDriveHome = BitConverter.ToUInt32(driveData, 179);
                uint origDriveEnd = BitConverter.ToUInt32(driveData, 183);
                AddMmLog($"[SL-SAVE] Drive 원본 Home={origDriveHome} End={origDriveEnd} → 신규 Home={newDriveHome} End={newDriveEnd}");

                int driveCTRLLen = 35 + driveData.Length;
                byte[] driveCTRL = new byte[driveCTRLLen];
                // CtrlFlag — 프로토콜 Rev.92 0xA4 byte3 bit7 = "소프트웨어 리미트 이상" 그룹만 ON
                //   (Save All 대신 SoftLimit-only로 다른 그룹 보호)
                driveCTRL[0] = 0x00; driveCTRL[1] = 0x00; driveCTRL[2] = 0x00; driveCTRL[3] = 0x80; driveCTRL[4] = 0x00;
                Array.Copy(driveData, 0, driveCTRL, 35, driveData.Length);
                // SoftLimit_HomePos[179] / EndPos[183] 만 수정 (CTRL offset = 35 + 구조체 offset)
                Buffer.BlockCopy(BitConverter.GetBytes(newDriveHome), 0, driveCTRL, 35 + 179, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(newDriveEnd), 0, driveCTRL, 35 + 183, 4);

                AddMmLog($"[0xA4] Drive SoftLimit 쓰기 (CtrlFlag=00 00 00 80 00, SoftLimit-only)...");
                if (!await WriteDriveParamAsync(driveCTRL, 10000, ct))
                {
                    AddMmLog("[ERR] Drive 쓰기 타임아웃");
                    return;
                }
                byte dRes = gClass.str.SrmPacket[gClass.srmNum].driveParamWriteResult;
                AddMmLog($"[0xA4] Drive 응답 result={dRes} ({(dRes == 0 ? "OK" : "FAIL")})");
                if (dRes != 0)
                {
                    AddMmLog("[ERR] Drive 쓰기 NACK — 진행 중단");
                    MessageBox.Show($"Drive SoftLimit 저장 실패 (result={dRes}).", "SL Save",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // ───────── Lift: read → modify SoftLimit → write (0xA6) ─────────
                AddMmLog("[SL-SAVE] Lift 현재 파라미터 읽기...");
                if (!await ReadLiftParamAsync(187, 10000, ct))
                {
                    AddMmLog("[ERR] Lift 읽기 실패");
                    return;
                }
                byte[] liftData = gClass.str.SrmPacket[gClass.srmNum].liftParamData;
                int origLiftHome = BitConverter.ToInt32(liftData, 179);
                int origLiftEnd = BitConverter.ToInt32(liftData, 183);
                int origLiftManualStart = BitConverter.ToInt32(liftData, 132);
                int origLiftManualEnd = BitConverter.ToInt32(liftData, 136);
                int origLiftRefDogHome = BitConverter.ToInt32(liftData, 209);
                AddMmLog($"[SL-SAVE] Lift 원본: SoftLimit Home={origLiftHome} End={origLiftEnd}");
                AddMmLog($"[SL-SAVE] Lift 원본: ManualOp Start={origLiftManualStart} End={origLiftManualEnd}");
                AddMmLog($"[SL-SAVE] Lift 원본: RefSetDog Home={origLiftRefDogHome}");
                AddMmLog($"[SL-SAVE] Lift 신규: Home={newLiftHome} End={newLiftEnd} (ManualOp_Start, RefSetDog_Home 동기화)");

                int liftCTRLLen = 35 + liftData.Length;
                byte[] liftCTRL = new byte[liftCTRLLen];
                // CtrlFlag — VER1 원본 일괄 활성 (모든 그룹: ManualOp/SoftLimit/RefSetDog 동시 적용)
                //   byte1=0xFF: 속도그룹 8개 모두
                //   byte2=0x1F: Lift 추가 그룹 (Drive는 0x07)
                //   byte3=0xFF: Maintance + SoftLimit + RefSetDog + 기타
                //   byte4=0x0F: DecelDog 등
                liftCTRL[0] = 0x00; liftCTRL[1] = 0xFF; liftCTRL[2] = 0x1F; liftCTRL[3] = 0xFF; liftCTRL[4] = 0x0F;
                Array.Copy(liftData, 0, liftCTRL, 35, liftData.Length);
                // SoftLimit_HomePos[179] / EndPos[183]
                Buffer.BlockCopy(BitConverter.GetBytes(newLiftHome), 0, liftCTRL, 35 + 179, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(newLiftEnd), 0, liftCTRL, 35 + 183, 4);
                // ManualOp_Start[132] — Lift Home과 동기화 (수동조깅 범위)
                Buffer.BlockCopy(BitConverter.GetBytes(newLiftHome), 0, liftCTRL, 35 + 132, 4);
                // RefSetDog_HomePos[209] — Lift Home과 동기화 (위치 명령 시작 dog)
                Buffer.BlockCopy(BitConverter.GetBytes(newLiftHome), 0, liftCTRL, 35 + 209, 4);

                AddMmLog($"[0xA6] Lift 쓰기 (CtrlFlag=00 FF 1F FF 0F, SoftLimit+ManualOp+RefSetDog 일괄)...");
                if (!await WriteLiftParamAsync(liftCTRL, 10000, ct))
                {
                    AddMmLog("[ERR] Lift 쓰기 타임아웃");
                    return;
                }
                byte lRes = gClass.str.SrmPacket[gClass.srmNum].liftParamWriteResult;
                AddMmLog($"[0xA6] Lift 응답 result={lRes} ({(lRes == 0 ? "OK" : "FAIL")})");
                if (lRes != 0)
                {
                    AddMmLog("[ERR] Lift 쓰기 NACK");
                    MessageBox.Show($"Lift SoftLimit 저장 실패 (result={lRes}).\n" +
                                    $"Drive는 이미 변경됨 — 필요 시 [READ SL]로 확인 후 재시도.",
                                    "SL Save", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // ★ 0x59 경로가 캐싱한 Drive/Lift 파라미터 무효화 — 방금 SoftLimit/ManualOp(offset 132/136 등)를 바꿨으므로
                //   다음 MoveViaMaintInternalAsync가 stale 범위로 잘못 판정(거부/허용)하지 않도록 재읽기를 강제한다.
                cachedDriveParam = null;
                cachedLiftParam = null;
                AddMmLog("[SL-SAVE] 0x59 파라미터 캐시 무효화 (변경된 SoftLimit/ManualOp 반영)");

                AddMmLog($"[SL-SAVE] ✓ SoftLimit 영구 저장 완료. mm-move 재시도 가능.");
                MessageBox.Show("SoftLimit이 SRM에 영구 저장되었습니다.\n이제 mm-move를 다시 시도하세요.",
                    "SL Save 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddMmLog($"[ERR] SL Save: {ex.Message}");
                MessageBox.Show($"SoftLimit 저장 실패: {ex.Message}", "SL Save",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btn_slRead.IsEnabled = true;
                btn_slSave.IsEnabled = true;
            }
        }

        // Btn_MmRestore_Click 제거됨 — 순수 조깅 방식은 셀 변경 없음

        private async void Btn_MmMove_Click(object sender, RoutedEventArgs e)
        {
            if (mmMoveInProgress) return;
            if (isRunning) { AddMmLog("[MM] 오토티칭 진행 중 — mm 이동 불가 (파라미터 공유 충돌 방지)"); return; }

            if (!int.TryParse(edit_mmTrav.Text.Trim(), out int travMm) ||
                !int.TryParse(edit_mmLift.Text.Trim(), out int liftMm))
            {
                MessageBox.Show("유효한 mm 값을 입력하세요.", "MM Move", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // ⚠️ 0x59 보수위치 이동 방식은 Vexi/SEW MOVI-C SoftLimit_HomePos/EndPos 안에서만 동작
            //    (실측: Lift HomePos≈1000~1500mm, EndPos≈5150mm. 그 밖은 거부 또는 5150mm 클램프)
            //    →  CMD2_80 수동 조깅 + 펄스 보정 방식으로 전환 (D 옵션). SoftLimit 영향 적음.

            // stopDist (조기정지 거리) — UI의 STOP(mm) 입력값 사용 (기본 70mm)
            int stopDist = 70;
            if (int.TryParse(edit_mmRow.Text.Trim(), out int parsedStop) && parsedStop >= 10 && parsedStop <= 1000)
                stopDist = parsedStop;

            int curTrav = CurTrav;
            int curLift = CurLift;

            AddMmLog($"──── mm 이동 계획 (0x59 보수위치 이동) ────");
            AddMmLog($"  TRAV: {curTrav}mm → {travMm}mm (diff={travMm - curTrav}mm)");
            AddMmLog($"  LIFT: {curLift}mm → {liftMm}mm (diff={liftMm - curLift}mm)");
            AddMmLog($"  방식: 셋업모드 + 0xA4/A6 Maintance_Pos 수정 + 0x59 이동");
            AddMmLog($"──────────────────────────────────────");

            var result = MessageBox.Show(
                $"0x59 보수위치 이동 방식\n\n" +
                $"TRAV: {curTrav}mm → {travMm}mm\n" +
                $"LIFT: {curLift}mm → {liftMm}mm\n\n" +
                $"방식 설명:\n" +
                $"  1. 셋업모드 진입 (필요시 자동 전환)\n" +
                $"  2. 0xA3/A5 Drive·Lift 파라미터 읽기\n" +
                $"  3. 0xA4/A6 Maintance_Position만 변경 (CtrlFlag 00 00 00 20 00)\n" +
                $"  4. 0x59 보수위치 이동 명령\n" +
                $"  5. curPos 모니터링 → 도착 대기\n\n" +
                $"장점: 인버터 자동 속도 (빠름), 셋업 단일 모드 (안전)\n" +
                $"제약: SoftLimit_HomePos~EndPos 범위 안 (현재 Det=0 무제한)\n\n" +
                $"진행하시겠습니까?",
                "MM Move (0x59)", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (result != MessageBoxResult.OK) return;

            mmMoveInProgress = true;
            btn_mmMove.IsEnabled = false;
            btn_mmStop2.IsEnabled = true;

            mmCts?.Dispose(); mmCts = new CancellationTokenSource();

            var preState = gClass.str.SrmState[gClass.srmNum];
            byte origGcpTxMode = preState.gcpState.gcpTxMode;
            AddMmLog($"[CHECK] autoMode={preState.autoMode} manualMode={preState.manualMode} setupMode={preState.setupMode} startSt={preState.dSt1StartSt} gcpTxMode={origGcpTxMode}");

            Task warningMonitorTask = null;

            try
            {
                // ═══ 방식: CMD2_80 수동 조깅 + 펄스 보정 (단축 순차) ═══
                // udpClientClass.cs:2842-2858 송신 조건: manualMode>0 AND gcpTxMode==1
                // → SRM을 수동모드로 전환 후 JogAxisAsync 호출

                // Step 0: SoftLimit 사전 검증 (jog도 SRM SoftLimit 적용됨 — 01-08/09 경고 사전 차단)
                //         Vexi 구조체: SoftLimit_DetectSet[178], _HomePos[179..182], _EndPos[183..186]
                SetStatus("CHECK SL", ClrWarn);
                AddMmLog("[0xA3] Drive 파라미터 읽기 (SoftLimit 검증)...");
                if (!await ReadDriveParamAsync(187, 10000, mmCts.Token))
                {
                    AddMmLog("[ERR] Drive 파라미터 읽기 실패 — SoftLimit 검증 불가");
                    return;
                }
                byte[] driveData = gClass.str.SrmPacket[gClass.srmNum].driveParamData;
                // SoftLimit (offset 178/179/183)
                byte driveSL_Det = driveData[178];
                uint driveSL_Home = BitConverter.ToUInt32(driveData, 179);
                uint driveSL_End = BitConverter.ToUInt32(driveData, 183);
                // ManualOp 범위 (offset 132/136, Drive=UInt32)
                uint driveMO_Start = BitConverter.ToUInt32(driveData, 132);
                uint driveMO_End = BitConverter.ToUInt32(driveData, 136);
                // ★ RefSetDog / DecelDog — 01-08의 진짜 원인 후보
                byte refDogDet = driveData[206];
                uint refDogHome = BitConverter.ToUInt32(driveData, 209);
                uint refDogEnd = BitConverter.ToUInt32(driveData, 217);
                byte decelDogDet = driveData[225];
                uint decelDogF1_1 = BitConverter.ToUInt32(driveData, 228);
                uint decelDogF1_2 = BitConverter.ToUInt32(driveData, 232);
                uint decelDogF2 = BitConverter.ToUInt32(driveData, 236);
                uint decelDogR1_1 = BitConverter.ToUInt32(driveData, 244);
                uint decelDogR1_2 = BitConverter.ToUInt32(driveData, 248);
                uint decelDogR2 = BitConverter.ToUInt32(driveData, 252);

                AddMmLog($"[SoftLimit] Drive Det={driveSL_Det} Home={driveSL_Home}mm End={driveSL_End}mm");
                AddMmLog($"[ManualOp] Drive Start={driveMO_Start}mm End={driveMO_End}mm");
                AddMmLog($"[RefDog]   Drive Det={refDogDet} Home={refDogHome}mm End={refDogEnd}mm  ★ 시작/끝 dog");
                AddMmLog($"[DecelDog] Drive Det={decelDogDet} Front1=[{decelDogF1_1},{decelDogF1_2}]mm Front2={decelDogF2}mm");
                AddMmLog($"[DecelDog] Drive            Rear1=[{decelDogR1_1},{decelDogR1_2}]mm Rear2={decelDogR2}mm");

                // cellBay (랙 첫/끝 BAY 위치) + Station 위치 표시
                try
                {
                    var info = gClass.str.SrmInfo[gClass.srmNum];
                    if (info.cellBay != null && info.bay > 0)
                    {
                        int diagBayCnt = Math.Min(info.bay, info.cellBay.Length);
                        AddMmLog($"[Rack] cellBay[0]={info.cellBay[0]}mm  cellBay[{diagBayCnt - 1}]={info.cellBay[diagBayCnt - 1]}mm");
                    }
                    if (info.SrmStation != null && info.SrmStation.Length > 0)
                    {
                        var stations = info.SrmStation.Where(s => s.travPos > 0).Take(3).ToList();
                        if (stations.Count > 0)
                            AddMmLog($"[Station] travPos: {string.Join(", ", stations.Select(s => s.travPos + "mm"))}");
                    }
                }
                catch { /* SrmInfo 미초기화 가능, 무시 */ }

                // 현재 위치와 가까운 dog 위치 진단 (현재 mm가 어떤 dog와 가까운지)
                int curTravNow_diag = CurTrav;
                var nearbyDogs = new List<(string name, long pos, long dist)>
                {
                    ("RefDog_Home", refDogHome, Math.Abs(refDogHome - (long)curTravNow_diag)),
                    ("RefDog_End", refDogEnd, Math.Abs(refDogEnd - (long)curTravNow_diag)),
                    ("DecelDog_F1_1", decelDogF1_1, Math.Abs(decelDogF1_1 - (long)curTravNow_diag)),
                    ("DecelDog_F1_2", decelDogF1_2, Math.Abs(decelDogF1_2 - (long)curTravNow_diag)),
                    ("DecelDog_F2", decelDogF2, Math.Abs(decelDogF2 - (long)curTravNow_diag)),
                    ("DecelDog_R1_1", decelDogR1_1, Math.Abs(decelDogR1_1 - (long)curTravNow_diag)),
                    ("DecelDog_R1_2", decelDogR1_2, Math.Abs(decelDogR1_2 - (long)curTravNow_diag)),
                    ("DecelDog_R2", decelDogR2, Math.Abs(decelDogR2 - (long)curTravNow_diag)),
                };
                nearbyDogs = nearbyDogs.Where(d => d.pos > 0).OrderBy(d => d.dist).ToList();
                if (nearbyDogs.Count > 0)
                {
                    var top3 = nearbyDogs.Take(3);
                    AddMmLog($"[DIAG] 현재 TRAV {curTravNow_diag}mm 근처 dog 3개:");
                    foreach (var d in top3)
                        AddMmLog($"[DIAG]   {d.name} @ {d.pos}mm  (거리: {d.dist}mm)");
                }

                AddMmLog("[0xA5] Lift 파라미터 읽기 (SoftLimit + ManualOp 검증)...");
                if (!await ReadLiftParamAsync(187, 10000, mmCts.Token))
                {
                    AddMmLog("[ERR] Lift 파라미터 읽기 실패 — SoftLimit 검증 불가");
                    return;
                }
                byte[] liftData = gClass.str.SrmPacket[gClass.srmNum].liftParamData;
                byte liftSL_Det = liftData[178];
                int liftSL_Home = BitConverter.ToInt32(liftData, 179);
                int liftSL_End = BitConverter.ToInt32(liftData, 183);
                // Lift ManualOp는 Int32 (signed)
                int liftMO_Start = BitConverter.ToInt32(liftData, 132);
                int liftMO_End = BitConverter.ToInt32(liftData, 136);
                // Lift RefDog/DecelDog도 Int32
                byte liftRefDogDet = liftData[206];
                int liftRefDogHome = BitConverter.ToInt32(liftData, 209);
                int liftRefDogEnd = BitConverter.ToInt32(liftData, 217);
                byte liftDecelDogDet = liftData[225];
                int liftDecelDogF1_1 = BitConverter.ToInt32(liftData, 228);
                int liftDecelDogF1_2 = BitConverter.ToInt32(liftData, 232);
                int liftDecelDogR1_1 = BitConverter.ToInt32(liftData, 244);
                int liftDecelDogR1_2 = BitConverter.ToInt32(liftData, 248);

                AddMmLog($"[SoftLimit] Lift  Det={liftSL_Det} Home={liftSL_Home}mm End={liftSL_End}mm");
                AddMmLog($"[ManualOp] Lift  Start={liftMO_Start}mm End={liftMO_End}mm");
                AddMmLog($"[RefDog]   Lift  Det={liftRefDogDet} Home={liftRefDogHome}mm End={liftRefDogEnd}mm");
                AddMmLog($"[DecelDog] Lift  Det={liftDecelDogDet} F1=[{liftDecelDogF1_1},{liftDecelDogF1_2}]mm R1=[{liftDecelDogR1_1},{liftDecelDogR1_2}]mm");

                // cellLev 표시
                try
                {
                    var info = gClass.str.SrmInfo[gClass.srmNum];
                    if (info.cellLev != null && info.lev > 0)
                    {
                        int diagLevCnt = Math.Min(info.lev, info.cellLev.Length);
                        AddMmLog($"[Rack] cellLev[0]={info.cellLev[0]}mm  cellLev[{diagLevCnt - 1}]={info.cellLev[diagLevCnt - 1]}mm");
                    }
                }
                catch { }

                // Lift 현재 위치 근처 dog 진단
                int curLiftNow_diag = CurLift;
                var nearbyLiftDogs = new List<(string name, long pos, long dist)>
                {
                    ("RefDog_Home", liftRefDogHome, Math.Abs(liftRefDogHome - (long)curLiftNow_diag)),
                    ("RefDog_End", liftRefDogEnd, Math.Abs(liftRefDogEnd - (long)curLiftNow_diag)),
                    ("DecelDog_F1_1", liftDecelDogF1_1, Math.Abs(liftDecelDogF1_1 - (long)curLiftNow_diag)),
                    ("DecelDog_F1_2", liftDecelDogF1_2, Math.Abs(liftDecelDogF1_2 - (long)curLiftNow_diag)),
                    ("DecelDog_R1_1", liftDecelDogR1_1, Math.Abs(liftDecelDogR1_1 - (long)curLiftNow_diag)),
                    ("DecelDog_R1_2", liftDecelDogR1_2, Math.Abs(liftDecelDogR1_2 - (long)curLiftNow_diag)),
                };
                nearbyLiftDogs = nearbyLiftDogs.Where(d => d.pos > 0).OrderBy(d => d.dist).ToList();
                if (nearbyLiftDogs.Count > 0)
                {
                    var top3 = nearbyLiftDogs.Take(3);
                    AddMmLog($"[DIAG] 현재 LIFT {curLiftNow_diag}mm 근처 dog 3개:");
                    foreach (var d in top3)
                        AddMmLog($"[DIAG]   {d.name} @ {d.pos}mm  (거리: {d.dist}mm)");
                }

                // ───── 검증 1: SoftLimit (DetectSet>0 일 때만) ─────
                bool driveSlOOL = false, liftSlOOL = false;
                if (driveSL_Det > 0 && driveSL_End > driveSL_Home)
                    driveSlOOL = travMm < (int)driveSL_Home || travMm > (int)driveSL_End;
                if (liftSL_Det > 0 && liftSL_End > liftSL_Home)
                    liftSlOOL = liftMm < liftSL_Home || liftMm > liftSL_End;

                // ───── 검증 2: ManualOp 범위 (수동모드 jog 가능 영역) ─────
                //   01-08 "시작 위치 도달" 의 진짜 원인. SoftLimit Det=0 이어도 이게 막음.
                //   현재위치 또는 목표가 [ManualOp_Start, ManualOp_End] 밖이면 차단.
                int curTravNow = CurTrav;
                int curLiftNow = CurLift;
                bool driveMoOOL = false, liftMoOOL = false;
                if (driveMO_End > driveMO_Start)
                {
                    if (travMm < (int)driveMO_Start || travMm > (int)driveMO_End) driveMoOOL = true;
                    // 현재 위치가 이미 ManualOp 밖이면 어느 방향이든 거부될 가능성 큼
                    if (curTravNow < (int)driveMO_Start) driveMoOOL = true;
                    if (curTravNow > (int)driveMO_End) driveMoOOL = true;
                }
                if (liftMO_End > liftMO_Start)
                {
                    if (liftMm < liftMO_Start || liftMm > liftMO_End) liftMoOOL = true;
                    if (curLiftNow < liftMO_Start) liftMoOOL = true;
                    if (curLiftNow > liftMO_End) liftMoOOL = true;
                }

                if (driveSlOOL || liftSlOOL || driveMoOOL || liftMoOOL)
                {
                    string msg = "❌ 목표 또는 현재 mm가 jog 허용 범위 밖입니다.\n" +
                                 "시도 시 01-08(시작 위치 도달) 또는 01-09(끝 위치 도달) 경고 발생.\n\n";
                    if (driveSlOOL)
                        msg += $"[SoftLimit] Drive: 목표 {travMm}mm  허용 [{driveSL_Home} ~ {driveSL_End}]mm\n";
                    if (liftSlOOL)
                        msg += $"[SoftLimit] Lift:  목표 {liftMm}mm  허용 [{liftSL_Home} ~ {liftSL_End}]mm\n";
                    if (driveMoOOL)
                        msg += $"[ManualOp] Drive: 현재 {curTravNow}mm / 목표 {travMm}mm  허용 [{driveMO_Start} ~ {driveMO_End}]mm\n";
                    if (liftMoOOL)
                        msg += $"[ManualOp] Lift:  현재 {curLiftNow}mm / 목표 {liftMm}mm  허용 [{liftMO_Start} ~ {liftMO_End}]mm\n";

                    msg += "\n해결책:\n" +
                           "• ManualOp 영역: Vexi → SRMDriveParam_Speed → 수동운전 Start/End mm 확장 후 [Set]\n" +
                           "• SoftLimit: SoftLimit Home/End 확장 (지상반 [SL READ/SAVE] 또는 Vexi)\n" +
                           "• 허용 범위 안의 mm로 다시 시도";

                    MessageBox.Show(msg, "jog 범위 초과 — 차단됨", MessageBoxButton.OK, MessageBoxImage.Warning);
                    AddMmLog($"[BLOCK] 범위 초과 — Drive_SL={driveSlOOL} Lift_SL={liftSlOOL} Drive_MO={driveMoOOL} Lift_MO={liftMoOOL}");
                    SetStatus("BLOCKED", ClrErr);
                    return;
                }
                AddMmLog("[OK] 목표·현재 mm 모두 SoftLimit + ManualOp 안 — 진행");

                // ═══ 방식: MoveViaMaintAsync 호출 (X→Z 분리, AutoTeaching/CALIB와 동일 흐름) ═══
                //   - 두 축 모두 변경: 주행만 먼저 이동 → 도착 후 승강 이동
                //   - 한 축만 변경: 그대로 단일 0x59 이동
                //   - 내부에서 셋업모드/0xA3·A5/0xA4·A6/0x59/curPos 모니터링 처리
                AddMmLog($"[MAINT] MoveViaMaintAsync 호출 TRAV={travMm}mm LIFT={liftMm}mm");
                AddMmLog("[MAINT] 상세 진행 로그는 메인 로그 창(상단)에서 확인");
                SetStatus("MAINT MOVE", ClrDone);

                gClass.str.SrmPacket[gClass.srmNum].isAutoTeaching = true;
                bool maintOk = false;
                try
                {
                    maintOk = await MoveViaMaintAsync(travMm, liftMm, mmCts.Token);
                }
                finally
                {
                    gClass.str.SrmPacket[gClass.srmNum].isAutoTeaching = false;
                }

                int resT = CurTrav;
                int resL = CurLift;
                Dispatcher.Invoke(() =>
                {
                    lbl_mmBayId.Content = $"{resT}mm";
                    lbl_mmLevId.Content = $"{resL}mm";
                });
                AddMmLog($"[RESULT] TRAV={resT}mm(오차={resT - travMm}) LIFT={resL}mm(오차={resL - liftMm}) ok={maintOk}");
                SetStatus(maintOk ? "DONE" : "TIMEOUT",
                          maintOk ? ClrDone : ClrWarn);

            }
            catch (OperationCanceledException)
            {
                AddMmLog("[STOP] 취소됨");
                SetStatus("STOPPED", ClrErr);
            }
            catch (Exception ex)
            {
                AddMmLog($"[ERR] {ex.Message}");
                SetStatus("ERROR", ClrErr);
            }
            finally
            {
                // semiJobClicked 잔류 클리어 (이미 SRM이 처리했으면 무해)
                try
                {
                    var pkt = gClass.str.SrmPacket[gClass.srmNum];
                    pkt.semiJobClicked = false;
                    gClass.str.SrmPacket[gClass.srmNum] = pkt;
                }
                catch { }

                gClass.str.SrmState[gClass.srmNum].gcpState.gcpTxMode = origGcpTxMode;
                mmMoveInProgress = false;
                btn_mmMove.IsEnabled = true;
                btn_mmStop2.IsEnabled = false;
            }
        }

        private void Btn_MmStop_Click(object sender, RoutedEventArgs e)
        {
            AddMmLog("[STOP] 정지 요청");
            mmCts?.Cancel();
            SetStatus("STOPPING", ClrErr);
        }

        // ================================================================
        // 0x59 프로토콜 탐색 (Probe)
        // ================================================================

        private async Task<(bool ok, byte[] resp, int len)> SendProbe59Async(byte[] data, int timeoutMs = 3000)
        {
            var pkt = gClass.str.SrmPacket[gClass.srmNum];
            pkt.probeData = data;
            pkt.probeDone = false;
            pkt.probeResp = null;
            pkt.probeRespLen = 0;
            pkt.probeReq = true;
            gClass.str.SrmPacket[gClass.srmNum] = pkt;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                await Task.Delay(100);
                if (gClass.str.SrmPacket[gClass.srmNum].probeDone)
                {
                    var resp = gClass.str.SrmPacket[gClass.srmNum].probeResp ?? new byte[0];
                    int len = gClass.str.SrmPacket[gClass.srmNum].probeRespLen;
                    return (true, resp, len);
                }
            }
            return (false, null, 0);
        }

        private string BytesToHex(byte[] data)
        {
            if (data == null || data.Length == 0) return "(empty)";
            return BitConverter.ToString(data).Replace("-", " ");
        }

        private async void Btn_Probe59_Click(object sender, RoutedEventArgs e)
        {
            if (isRunning || mmMoveInProgress) { AddMmLog("[PROBE] 티칭/이동 진행 중 — 파라미터 공유 충돌 방지로 차단"); return; }

            var result = MessageBox.Show(
                "0x59 프로토콜 탐색을 시작합니다.\n\n" +
                "현재 모드: 셋업모드 필요\n" +
                "탐색 항목:\n" +
                "1) 빈 패킷\n" +
                "2) 길이 1~12 바이트 (0x00, 0x01 등)\n" +
                "3) 현재 TRAV/LIFT mm값 패턴\n" +
                "4) Bay/Level 인덱스 패턴\n\n" +
                "약 100회 전송, 3~5분 소요",
                "Probe 0x59", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (result != MessageBoxResult.OK) return;

            btn_probe59.IsEnabled = false;
            mmCts?.Dispose(); mmCts = new CancellationTokenSource();

            int curTrav = CurTrav;
            int curLift = CurLift;
            var state = gClass.str.SrmState[gClass.srmNum];
            AddMmLog($"══════ 0x59 PROBE 시작 ══════");
            AddMmLog($"[PROBE] setupMode={state.setupMode} forcedMode={state.forcedMode} manualMode={state.manualMode}");
            AddMmLog($"[PROBE] curTrav={curTrav}mm curLift={curLift}mm");

            int probeCount = 0;
            int ackCount = 0;

            // ── 헬퍼: 1개 패턴 전송+로그 ──
            async Task ProbeOne(string label, byte[] data)
            {
                if (mmCts.IsCancellationRequested) return;
                probeCount++;
                string hex = BytesToHex(data);
                var (ok, resp, len) = await SendProbe59Async(data, 3000);
                if (ok)
                {
                    string respHex = BytesToHex(resp);
                    string respDetail = "";
                    if (resp != null && resp.Length >= 1)
                        respDetail = $" result={resp[0]}";
                    if (resp != null && resp.Length >= 2)
                        respDetail += $" reason={resp[1]}";

                    bool isAck = resp != null && resp.Length >= 1 && resp[0] == 0;
                    if (isAck) ackCount++;

                    string tag = isAck ? "★ACK★" : "NACK";
                    AddMmLog($"[{tag}] #{probeCount} {label} TX=[{hex}]({data?.Length ?? 0}B) → RX=[{respHex}]({len}B){respDetail}");
                }
                else
                {
                    AddMmLog($"[TIMEOUT] #{probeCount} {label} TX=[{hex}]({data?.Length ?? 0}B)");
                }
                await Task.Delay(300);  // SRM 부하 방지
            }

            try
            {
                // ── Phase 1: 빈 패킷 ──
                AddMmLog("── Phase 1: 빈 패킷 ──");
                await ProbeOne("empty", new byte[0]);

                // ── Phase 2: 길이 탐색 (1~12 바이트, 값=0x00) ──
                AddMmLog("── Phase 2: 길이 탐색 (0x00 패딩) ──");
                for (int len = 1; len <= 12; len++)
                {
                    await ProbeOne($"len={len} val=0x00", new byte[len]);
                }

                // ── Phase 3: 1바이트 값 탐색 ──
                AddMmLog("── Phase 3: 1바이트 값 ──");
                foreach (byte v in new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x10, 0x20, 0xFF })
                {
                    await ProbeOne($"[0x{v:X2}]", new byte[] { v });
                }

                // ── Phase 4: 2바이트 축+동작 코드 패턴 ──
                AddMmLog("── Phase 4: 2바이트 (축+코드) ──");
                foreach (byte axis in new byte[] { 0x00, 0x01, 0x02, 0x03 })
                {
                    foreach (byte code in new byte[] { 0x00, 0x01, 0x02 })
                    {
                        await ProbeOne($"axis={axis} code={code}", new byte[] { axis, code });
                    }
                }

                // ── Phase 5: 현재 위치값 패턴 (Little Endian & Big Endian) ──
                AddMmLog("── Phase 5: 현재 위치값 패턴 ──");
                byte[] travLE = BitConverter.GetBytes(curTrav);
                byte[] travBE = new byte[] { travLE[3], travLE[2], travLE[1], travLE[0] };
                byte[] liftLE = BitConverter.GetBytes(curLift);
                byte[] liftBE = new byte[] { liftLE[3], liftLE[2], liftLE[1], liftLE[0] };

                // 4바이트: TRAV만
                await ProbeOne($"TRAV_LE={curTrav}", travLE);
                await ProbeOne($"TRAV_BE={curTrav}", travBE);
                // 4바이트: LIFT만
                await ProbeOne($"LIFT_LE={curLift}", liftLE);
                await ProbeOne($"LIFT_BE={curLift}", liftBE);
                // 8바이트: TRAV+LIFT
                var travLift = new byte[8];
                Array.Copy(travLE, 0, travLift, 0, 4);
                Array.Copy(liftLE, 0, travLift, 4, 4);
                await ProbeOne($"TRAV+LIFT_LE", travLift);
                var travLiftBE = new byte[8];
                Array.Copy(travBE, 0, travLiftBE, 0, 4);
                Array.Copy(liftBE, 0, travLiftBE, 4, 4);
                await ProbeOne($"TRAV+LIFT_BE", travLiftBE);

                // 2바이트: TRAV mm (16bit LE/BE)
                byte[] trav16LE = BitConverter.GetBytes((short)curTrav);
                byte[] trav16BE = new byte[] { trav16LE[1], trav16LE[0] };
                await ProbeOne($"TRAV_16LE={curTrav}", trav16LE);
                await ProbeOne($"TRAV_16BE={curTrav}", trav16BE);

                // ── Phase 6: 축(1B) + mm(4B) 패턴 ──
                AddMmLog("── Phase 6: 축(1B)+mm(4B) ──");
                foreach (byte axis in new byte[] { 0x01, 0x02 })
                {
                    byte[] mm = axis == 0x01 ? travLE : liftLE;
                    var pkt5 = new byte[5];
                    pkt5[0] = axis;
                    Array.Copy(mm, 0, pkt5, 1, 4);
                    await ProbeOne($"axis={axis}+mm_LE", pkt5);
                }

                // ── Phase 7: 축(1B) + mm(2B) 패턴 ──
                AddMmLog("── Phase 7: 축(1B)+mm(2B) ──");
                foreach (byte axis in new byte[] { 0x01, 0x02 })
                {
                    short mmVal = (short)(axis == 0x01 ? curTrav : curLift);
                    byte[] mm16 = BitConverter.GetBytes(mmVal);
                    await ProbeOne($"axis={axis}+mm16_LE={mmVal}", new byte[] { axis, mm16[0], mm16[1] });
                }

                // ── Phase 8: 목표값 패턴 (현재+1000mm) ──
                AddMmLog("── Phase 8: 목표값 (cur+1000mm) ──");
                int targetTrav = curTrav + 1000;
                int targetLift = curLift + 500;
                byte[] tgtTravLE = BitConverter.GetBytes(targetTrav);
                byte[] tgtLiftLE = BitConverter.GetBytes(targetLift);
                await ProbeOne($"tgtTRAV_LE={targetTrav}", tgtTravLE);
                await ProbeOne($"tgtLIFT_LE={targetLift}", tgtLiftLE);
                // 축+목표
                var tgt5T = new byte[5];
                tgt5T[0] = 0x01;
                Array.Copy(tgtTravLE, 0, tgt5T, 1, 4);
                await ProbeOne($"axis=1+tgtTRAV={targetTrav}", tgt5T);
                var tgt5L = new byte[5];
                tgt5L[0] = 0x02;
                Array.Copy(tgtLiftLE, 0, tgt5L, 1, 4);
                await ProbeOne($"axis=2+tgtLIFT={targetLift}", tgt5L);

                // ── Phase 9: Bay/Level 인덱스 패턴 ──
                AddMmLog("── Phase 9: Bay/Level 인덱스 ──");
                await ProbeOne("bay=1 lev=1", new byte[] { 0x01, 0x01 });
                await ProbeOne("bay=5 lev=3", new byte[] { 0x05, 0x03 });
                await ProbeOne("row=1 bay=1 lev=1", new byte[] { 0x01, 0x01, 0x01 });
                // Bay 2바이트 + Lev 1바이트
                await ProbeOne("bay16=1 lev=1", new byte[] { 0x00, 0x01, 0x01 });
                await ProbeOne("bay16=5 lev=3", new byte[] { 0x00, 0x05, 0x03 });

                AddMmLog($"══════ PROBE 완료: {probeCount}회 전송, ACK={ackCount}회 ══════");
            }
            catch (Exception ex)
            {
                AddMmLog($"[ERR] Probe 중단: {ex.Message}");
            }
            finally
            {
                btn_probe59.IsEnabled = true;
            }
        }

        private string _mmLogDir = "";
        private string _mmLogFile = "";
        private readonly object _mmLogLock = new object();

        private void AddMmLog(string msg)
        {
            string logMsg = $"[{DateTime.Now:HH:mm:ss.fff}] {msg}";
            Dispatcher.Invoke(() =>
            {
                listBox_MmLog.Items.Add(logMsg);
                if (listBox_MmLog.Items.Count > 500)
                    listBox_MmLog.Items.RemoveAt(0);
                listBox_MmLog.ScrollIntoView(listBox_MmLog.Items[listBox_MmLog.Items.Count - 1]);
            });
            WriteMmLogFile(logMsg);
        }

        private void WriteMmLogFile(string logMsg)
        {
            try
            {
                if (string.IsNullOrEmpty(_mmLogDir))
                {
                    _mmLogDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SRM" + gClass.srmNum, "Teaching", "MmLog");
                    Directory.CreateDirectory(_mmLogDir);
                }

                string today = DateTime.Now.ToString("yyyyMMdd_HH");
                string newFile = Path.Combine(_mmLogDir, $"MmMoveLog_{today}.txt");
                if (newFile != _mmLogFile)
                {
                    _mmLogFile = newFile;
                    // 15일 보관 — MmLog 시간별 파일 무한 누적 방지(다른 로그 경로와 동일 정책). 새 시(hour)에만 1회.
                    try { cIniAccess.DeleteOldFiles(gClass.srmNum, _mmLogDir, 15); } catch { }
                }

                lock (_mmLogLock)
                {
                    File.AppendAllText(_mmLogFile, logMsg + Environment.NewLine);
                }
            }
            catch { }
        }

        // ================================================================
        // UI Helpers
        // ================================================================

        private void SetStatus(string text, Color color)
        {
            Dispatcher.Invoke(() =>
            {
                lbl_status.Content = text;
                ellStatus.Fill = new SolidColorBrush(color);
            });
        }

        private void UpdateProgress(int current, int total, string cellName)
        {
            Dispatcher.Invoke(() =>
            {
                lbl_curTarget.Content = cellName;
                lbl_progress.Content = $"{current} / {total}";
                progressBar.Value = total > 0 ? (double)current / total * 100 : 0;
            });
        }

        private void AddLog(string msg)
        {
            string logMsg = $"[{DateTime.Now:HH:mm:ss.fff}] {msg}";
            Dispatcher.Invoke(() =>
            {
                listBox_Log.Items.Add(logMsg);
                if (listBox_Log.Items.Count > 1000)
                    listBox_Log.Items.RemoveAt(0);
                listBox_Log.ScrollIntoView(listBox_Log.Items[listBox_Log.Items.Count - 1]);
            });
            WriteLogFile(logMsg);
        }

        // ================================================================
        // 파일 로그 (1시간 단위 파일 분리)
        // ================================================================

        private string _lastLogHour = "";
        private string _logFilePath = "";
        private readonly object _logFileLock = new object();

        private void WriteLogFile(string logMsg)
        {
            try
            {
                string now = DateTime.Now.ToString("yyyyMMdd_HH");
                if (now != _lastLogHour)
                {
                    _lastLogHour = now;
                    string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SRM" + gClass.srmNum, "Teaching", "Log");
                    Directory.CreateDirectory(dir);
                    _logFilePath = Path.Combine(dir, $"AutoTeachingLog_{now}.txt");
                    // 15일 보관 — 시간별 파일이 무한 누적되지 않도록(다른 모든 로그 경로와 동일 정책). 새 시(hour)에만 1회.
                    try { cIniAccess.DeleteOldFiles(gClass.srmNum, dir, 15); } catch { }
                }

                lock (_logFileLock)
                {
                    File.AppendAllText(_logFilePath, logMsg + Environment.NewLine);
                }
            }
            catch { /* 파일 로그 실패 시 무시 */ }
        }
    }
}
