using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Timers;
using System.Windows.Threading;
using gcp_Wpf.MenuWindow;

namespace gcp_Wpf
{
    /// <summary>
    /// PageCraneSet.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PageCraneSet : Page
    {
        singletonClass gClass;
        MainWindow pMain;

        //Test Timer
        Timer pageTimer = new Timer();
        // 현재시간(utcTime) 주기 갱신용
        DispatcherTimer utcTimeUpdateTimer;

        // 콤보박스 아이템 리스트 (인덱스 기반) - 모든 항목이 번역 키로 사용됨
        private readonly string[] forkCntItems = new string[] { "수신안됨", "싱글", "트윈" };
        private readonly string[] forkTypeItems = new string[] { "NONE", "싱글", "더블 2POS", "더블 3POS", "더블 2POS 베리언트", "더블 3POS 베리언트" };
        private readonly string[] stationItems = new string[] { "NO", "YES" };
        public PageCraneSet(MainWindow parent)
        {
            // SingleTone Test
            gClass = singletonClass.Instance;
            InitializeComponent();
            pMain = parent;

            SetPageMode(false);

            // 언어 변경 이벤트 구독
            TranslationSource.Instance.PropertyChanged += TranslationSource_PropertyChanged;

            pageTimer.Interval = 1000; // 1 second
            pageTimer.AutoReset = true; // Repeat the timer
            pageTimer.Elapsed += PageTimer_Elapsed;

            // Initialize Config Data  - 불러온 설정 값 UI에 표시
            // #1
            edit_srmID1.Text = gClass.str.SrmInfo[gClass.srmNum].srmID.ToString();
            edit_srmType1.Text = gClass.str.SrmInfo[gClass.srmNum].srmID.ToString();
            UpdateForkCntLabel(gClass.str.SrmInfo[gClass.srmNum].forkCnt);
            UpdateForkTypeLabel(gClass.str.SrmInfo[gClass.srmNum].forkType);

            edit_srmRow1.Text = gClass.str.SrmInfo[gClass.srmNum].row.ToString();
            edit_srmBay1.Text = gClass.str.SrmInfo[gClass.srmNum].bay.ToString();
            edit_srmLev1.Text = gClass.str.SrmInfo[gClass.srmNum].lev.ToString();
            edit_srmStn1.Text = gClass.str.SrmInfo[gClass.srmNum].stn.ToString();

            // 주행 방향 반전 설정 로드 (기능 제거됨 - Disable만 유지)
            //chk_travDirectionReverse.IsChecked = gClass.str.SrmInfo[gClass.srmNum].travDirectionReverse > 0;

            lbl_projNo.Content = gClass.str.SrmState[gClass.srmNum].projNo;
            lbl_groupNo.Content = gClass.str.SrmState[gClass.srmNum].groupNo;
            lbl_srmNo.Content = gClass.str.SrmState[gClass.srmNum].srmNo;
            lbl_firmVerNo.Content = gClass.str.SrmState[gClass.srmNum].firmwareVer;
            lbl_protoVerNo.Content = gClass.str.SrmState[gClass.srmNum].protocolVer;
            UpdateUtcTimeDisplay();

            // 현재시간 주기 갱신 타이머 (1초 간격)
            utcTimeUpdateTimer = new DispatcherTimer(DispatcherPriority.Normal, this.Dispatcher)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            utcTimeUpdateTimer.Tick += UtcTimeUpdateTimer_Tick;
            Loaded += PageCraneSet_Loaded;
            Unloaded += PageCraneSet_Unloaded;

            /* hello world!!*/

            // #2
            //edit_srmID2.Text = gClass.str.SrmInfo[1].srmID.ToString();
            //edit_srmType2.Text = gClass.str.SrmInfo[1].srmID.ToString();
            //combo_forkType2.SelectedIndex = gClass.str.SrmInfo[1].forkType;
            //edit_srmRow2.Text = gClass.str.SrmInfo[1].row.ToString();
            //edit_srmBay2.Text = gClass.str.SrmInfo[1].bay.ToString();
            //edit_srmLev2.Text = gClass.str.SrmInfo[1].lev.ToString();
            //edit_srmStn2.Text = gClass.str.SrmInfo[1].stn.ToString();
            //// #3
            //edit_srmID3.Text = gClass.str.SrmInfo[2].srmID.ToString();
            //edit_srmType3.Text = gClass.str.SrmInfo[2].srmID.ToString();
            //combo_forkType3.SelectedIndex = gClass.str.SrmInfo[2].forkType;
            //edit_srmRow3.Text = gClass.str.SrmInfo[2].row.ToString();
            //edit_srmBay3.Text = gClass.str.SrmInfo[2].bay.ToString();
            //edit_srmLev3.Text = gClass.str.SrmInfo[2].lev.ToString();
            //edit_srmStn3.Text = gClass.str.SrmInfo[2].stn.ToString();
        }

        private void PageCraneSet_Loaded(object sender, RoutedEventArgs e)
        {
            utcTimeUpdateTimer?.Start();
        }

        private void PageCraneSet_Unloaded(object sender, RoutedEventArgs e)
        {
            utcTimeUpdateTimer?.Stop();
        }

        private void UtcTimeUpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateUtcTimeDisplay();
        }

        /// <summary>
        /// gClass.str.SrmState[srmNum].utcTime (Unix UTC 초)을 현재시간 문자열로 변환하여 lbl_utcTime에 표시
        /// </summary>
        private void UpdateUtcTimeDisplay()
        {
            try
            {
                if (gClass == null || gClass.str.SrmState == null || gClass.srmNum < 0 || gClass.srmNum >= gClass.str.SrmState.Length)
                    return;
                uint utcSec = gClass.str.SrmState[gClass.srmNum].utcTime;
                if (utcSec == 0)
                {
                    lbl_utcTime.Content = "-";
                    return;
                }
                var dt = DateTimeOffset.FromUnixTimeSeconds(utcSec);
                lbl_utcTime.Content = dt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch
            {
                lbl_utcTime.Content = "-";
            }
        }

        private void PageTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (!gClass.str.SrmPacket[gClass.srmNum].craneSetRequest && !gClass.str.SrmPacket[gClass.srmNum].rackRequest)
                {
                    Console.WriteLine("Page CraneSet Timer Stop");
                    Dispatcher.Invoke(() => {
                        SetPageInit();
                    });
                    pageTimer.Stop();
                }
                else
                {
                    //pageTimer.Start();
                }
            }
            catch (Exception ex)
            {
                cIniAccess.SaveExLog(0, "EXCEPTION - PageCraneSetTimer : " + ex.Message);
            }
        }

            // 페이지 전환 시 해당 크레인 데이터로 초기화
        public void SetPageInit()
        {
            edit_srmID1.Text = cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "SRMINFO_" + gClass.srmNum, "SRMID");
            edit_srmType1.Text = cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "SRMINFO_" + gClass.srmNum, "SRMTYPE");

            int forkCnt = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + gClass.srmNum + "\\ForkInfo.ini", "FORKINFO", "FORKCNT"));
            int forkType = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + gClass.srmNum + "\\ForkInfo.ini", "FORKINFO", "FORKTYPE"));
            UpdateForkCntLabel(forkCnt);
            UpdateForkTypeLabel(forkType);


            edit_srmRow1.Text = cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + gClass.srmNum + "\\RACK" + "\\Rack.ini", "RACKINFO", "MaxRow");
            edit_srmBay1.Text = cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + gClass.srmNum + "\\RACK" + "\\Rack.ini", "RACKINFO", "Maxbay");
            edit_srmLev1.Text = cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + gClass.srmNum + "\\RACK" + "\\Rack.ini", "RACKINFO", "MaxLev");

            edit_srmStn1.Text = cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + gClass.srmNum + "\\Station.ini", "STATION", "COUNT");

            string visIni = AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini";
            edit_VisionIP1.Text = cIniAccess.Read(visIni, "SRMINFO_" + gClass.srmNum, "VISIONIP", "127.0.0.1");
            edit_VisionPort1.Text = cIniAccess.Read(visIni, "SRMINFO_" + gClass.srmNum, "VISIONPORT", "3080");

            // 주행 방향 반전 설정 로드 (INI 파일에서) - 기능 제거됨
            //string travDirReverse = cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "SRMINFO_" + gClass.srmNum, "TRAVDIRREVERSE");
            //if (string.IsNullOrEmpty(travDirReverse))
            //{
            //    travDirReverse = "0";  // 기본값: 정방향
            //}
            //int travDirReverseValue = int.Parse(travDirReverse);
            //chk_travDirectionReverse.IsChecked = travDirReverseValue > 0;
            //gClass.str.SrmInfo[gClass.srmNum].travDirectionReverse = travDirReverseValue;

            lbl_projNo.Content = gClass.str.SrmState[gClass.srmNum].projNo;
            lbl_groupNo.Content = gClass.str.SrmState[gClass.srmNum].groupNo;
            lbl_srmNo.Content = gClass.str.SrmState[gClass.srmNum].srmNo;
            lbl_firmVerNo.Content = gClass.str.SrmState[gClass.srmNum].firmwareVer;
            lbl_protoVerNo.Content = gClass.str.SrmState[gClass.srmNum].protocolVer;
            UpdateUtcTimeDisplay();

            SetPageMode(gClass.str.GcpInfo.isAdminMode);
        }

        public void SetPageMode(bool isAdmin)
        {
            // 페이지 전체를 비활성화/활성화
            this.IsEnabled = isAdmin;
        }

        private void TranslationSource_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 언어 변경 시 Label 텍스트 업데이트
            Dispatcher.Invoke(() =>
            {
                // 현재 값을 다시 설정하여 번역된 텍스트로 업데이트
                if (gClass != null)
                {
                    UpdateForkCntLabel(gClass.str.SrmInfo[gClass.srmNum].forkCnt);
                    UpdateForkTypeLabel(gClass.str.SrmInfo[gClass.srmNum].forkType);
                    // Station1은 아직 데이터 소스를 확인해야 함
                }
            });
        }

        private void Btn_DeviceTimeSync_Click(object sender, RoutedEventArgs e)
        {
            VarMessageBoxResult result = VarMessageBox.Show(cConstDefine.tr("장비시간 동기화"), cConstDefine.tr("장비시간 동기화 문구"), VarMessageBoxButton.YesNo);
            if (result == VarMessageBoxResult.Yes)
            {
                gClass.str.SrmPacket[gClass.srmNum].stdInfoControl = true;
                cIniAccess.SaveJobLog(gClass.srmNum, "GCP == 장비시간 동기화 버튼 Click");
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //TranslationSource.Instance
            VarMessageBoxResult result = VarMessageBox.Show(cConstDefine.tr("저장"), cConstDefine.tr("저장문구"), VarMessageBoxButton.OKCancel);

            if (result == VarMessageBoxResult.OK)
            {
                // OK button clicked
                // INI 파일 설정 정보 공유 구조체로 초기화  (순서 중요)

                // 설정 값 내부 변수 저장 — 빈값/비숫자(붙여넣기 등) 입력 시 int.Parse가 FormatException → TryParse로 방어
                if (!int.TryParse(edit_srmID1.Text, out int srmIdVal))
                {
                    VarMessageBox.Show(cConstDefine.tr("저장"), cConstDefine.tr("SRM ID는 숫자만 입력 가능합니다."), VarMessageBoxButton.OK);
                    return;
                }
                gClass.str.SrmInfo[gClass.srmNum].srmID = srmIdVal;
                // 설정 값 INI 파일 저장
                string configIni = AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini";
                cIniAccess.Write(configIni, "SRMINFO_" + gClass.srmNum, "SRMID", gClass.str.SrmInfo[gClass.srmNum].srmID.ToString());

                string visIp = edit_VisionIP1.Text.Trim();
                if (System.Net.IPAddress.TryParse(visIp, out _) == false)
                {
                    VarMessageBox.Show(cConstDefine.tr("저장"), cConstDefine.tr("Vision IP 형식이 올바르지 않습니다."), VarMessageBoxButton.OK);
                    return;
                }
                if (!int.TryParse(edit_VisionPort1.Text.Trim(), out int visPort) || visPort < 1 || visPort > 65535)
                {
                    VarMessageBox.Show(cConstDefine.tr("저장"), cConstDefine.tr("Vision Port는 1~65535 숫자만 가능합니다."), VarMessageBoxButton.OK);
                    return;
                }
                // Vision IP/Port: ini가 source of truth (in-memory struct 필드 없음 — Phase1_Init이 매번 ini에서 읽음)
                cIniAccess.Write(configIni, "SRMINFO_" + gClass.srmNum, "VISIONIP", visIp);
                cIniAccess.Write(configIni, "SRMINFO_" + gClass.srmNum, "VISIONPORT", visPort.ToString());

                // 주행 방향 반전 설정 저장 (기능 제거됨)
                //gClass.str.SrmInfo[gClass.srmNum].travDirectionReverse = chk_travDirectionReverse.IsChecked.GetValueOrDefault(false) ? 1 : 0;
                //cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "SRMINFO_" + gClass.srmNum, "TRAVDIRREVERSE", gClass.str.SrmInfo[gClass.srmNum].travDirectionReverse.ToString());
            }
            else if (result == VarMessageBoxResult.Cancel)
            {
                // Cancel button clicked or dialog closed using the X button
            }
        }

        private void Request_Click(object sender, RoutedEventArgs e)
        {
            // 저장 된 자료 불러오기 240207
            //gClass.str.SrmPacket[gClass.srmNum].stdInfoRequest = true;
            gClass.str.SrmPacket[gClass.srmNum].craneSetRequest = true;
            gClass.str.SrmPacket[gClass.srmNum].rackRequest = true;
            gClass.str.SrmPacket[gClass.srmNum].rackReqType = 1;                // 좌측기준 베이부터 요청
            gClass.str.SrmPacket[gClass.srmNum].rackReqCount = 255;             // 요청갯수 구분
            gClass.str.SrmPacket[gClass.srmNum].stationRequest = true; 
            gClass.str.SrmPacket[gClass.srmNum].forkRequest = true;
            gClass.str.SrmPacket[gClass.srmNum].prohRackRequest = true;

            pageTimer.Start();
            //pageTimer.Enabled = true;
            cIniAccess.SaveJobLog(gClass.srmNum, "GCP == 크레인 정보요청 버튼 Click");
        }

        private void NumberTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Check if the input is a non-numeric character
            if (!IsNumericInput(e.Text))
            {
                e.Handled = true; // Cancel the input event
            }
        }

        private bool IsNumericInput(string input)
        {
            return input.All(char.IsDigit);
        }

        private void Click_OpenNumpad(object sender, MouseButtonEventArgs e)
        {
            // 관리자 모드가 아니면 넘버패드를 열지 않음
            if (!gClass.str.GcpInfo.isAdminMode)
            {
                e.Handled = true;
                return;
            }

            TextBox edit = sender as TextBox;
            if (edit == null || pMain == null)
            {
                Console.WriteLine("Click_OpenNumpad: edit or pMain is null");
                return;
            }

            // TextBox가 비활성화되어 있으면 넘버패드를 열지 않음
            if (!edit.IsEnabled)
            {
                Console.WriteLine("Click_OpenNumpad: edit is disabled");
                return;
            }

            try
            {
                Window parentWindow = Window.GetWindow(this);
                if (parentWindow != null && pMain.tmpNumPad != null)
                {
                    pMain.tmpNumPad.AttachTo(edit, parentWindow, pMain.PointToScreen(new Point(0, 0)), pMain.ActualWidth, pMain.ActualHeight);
                    Console.WriteLine("Click_OpenNumpad Success: " + edit.Name);
                }
                else
                {
                    Console.WriteLine("Click_OpenNumpad: parentWindow or tmpNumPad is null");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Click_OpenNumpad Error: " + ex.Message);
            }
        }
		
		

        // forkCnt 값을 인덱스로 변환하여 Label 텍스트 업데이트
        // 데이터 값: 1=싱글, 2=트윈 (주석: 포크 수 1: 싱글, 2: 트윈)
        // 콤보박스 아이템 인덱스: 0=수신안됨, 1=싱글, 2=트윈
        // 따라서 forkCnt 값이 그대로 인덱스가 됨 (forkCnt=1 → 인덱스 1=싱글, forkCnt=2 → 인덱스 2=트윈)
        private void UpdateForkCntLabel(int forkCnt)
        {
            int index;
            if (forkCnt == 1)
            {
                index = 1; // 싱글
            }
            else if (forkCnt == 2)
            {
                index = 2; // 트윈
            }
            else
            {
                index = 0; // 수신안됨 (forkCnt=0 또는 유효하지 않음)
            }

            if (index < 0 || index >= forkCntItems.Length)
            {
                index = 0; // 안전장치: 기본값 수신안됨
            }

            string text = forkCntItems[index];
            lbl_forkCnt.Content = cConstDefine.tr(text);
        }

        // forkType 값을 인덱스로 변환하여 Label 텍스트 업데이트
        // 데이터 값: 1=싱글딥, 2=더블딥 2POS, 3=더블딥 3POS, 4=더블딥 2POS 베리언트, 5=더블딥 3POS 베리언트
        // 콤보박스 아이템 인덱스: 0=NONE, 1=싱글, 2=더블 2POS, 3=더블 3POS, 4=더블 2POS 베리언트, 5=더블 3POS 베리언트
        // 따라서 forkType 값이 그대로 인덱스가 됨 (forkType=1 → 인덱스 1=싱글, forkType=2 → 인덱스 2=더블 2POS, ...)
        private void UpdateForkTypeLabel(int forkType)
        {
            int index;
            if (forkType >= 1 && forkType <= 5)
            {
                index = forkType; // forkType 값이 그대로 인덱스
            }
            else
            {
                index = 0; // NONE (forkType=0 또는 유효하지 않음)
            }

            if (index < 0 || index >= forkTypeItems.Length)
            {
                index = 0; // 안전장치: 기본값 NONE
            }

            string text = forkTypeItems[index];
            lbl_forkType.Content = cConstDefine.tr(text);
        }
    }
}
