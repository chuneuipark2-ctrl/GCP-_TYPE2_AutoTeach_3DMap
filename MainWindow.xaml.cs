using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using gcp_Wpf;
using gcp_Wpf.commClass;
using gcp_Wpf.MenuWindow;
using static System.Runtime.CompilerServices.RuntimeHelpers;
using static gcp_Wpf.cConstDefine;
using Path = System.IO.Path;


namespace gcp_Wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    public partial class MainWindow : Window
    {
        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        public static int watchDogCount = 0;

        //General Var
        public int testVar;
        double dpiX, dpiY;
        int craneCnt;

        //Common Image
        ImageBrush img_connect = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/connected.png")));
        ImageBrush img_disconnect = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/disconnected.png")));

        //Gcp UdpClient
        private udpClientClass[] srmComm;
        //Wcs TcpServer
        private tcpServerClass[] hostComm;
        // Modbus Rtu DIO
        private modbusRtuClass[] rtuComm;
        // Fastech Udp DIO
        private gcpDioClass[] dioComm;
        //Serve Server
        private tcpServerClass servServer;

        //Test Timer
        Timer myTimer = new Timer();

        // Admin Mode Timer
        Timer adminModeTimer = new Timer();
        DateTime adminModeStartTime;

        // Menu
        Menu_Setting menu_Setting;


        // Host Log Window 생성 (동적 할당)
        WindowSrmLog win_HostLog1;
        WindowSrmLog win_HostLog2;
        WindowSrmLog win_HostLog3;

        // SRM Log Window 생성 (동적 할당)
        WindowSrmLog win_SrmLog1;
        WindowSrmLog win_SrmLog2;
        WindowSrmLog win_SrmLog3;

        // DIO Log Window 생성 (동적 할당)
        WindowSrmLog win_DioLog1;
        WindowSrmLog win_DioLog2;
        WindowSrmLog win_DioLog3;

        // 조작로그 (통합표시 - 파일만 별도 저장)
        //WindowSrmLog win_OpLog;

        // 작업로그 (통합표시 - 파일만 별도 저장)
        WindowSrmLog win_JobLog;

        // Dev State Page
        public PageDevState[] pageDevList;

        // Public Numpad
        public NumberPadPopup tmpNumPad = new();                        // 외부 클릭 시 종료되도록 하기위해 Global 용도로 사용

        // Page Define
        PageMain pageMain;
        PageManual pageManual;
        PageAuto pageAuto;
        PageProhibitRack pageProhRack;
        PageCommSet pageCommSet;
        PageCraneSet pageCraneSet;
        PageStationSet pageStationSet;
        PageSemiAuto pageSemiAuto;
        PageAlarmLog pageAlarmLog;
        PageDIO pageDio;
        PageMonitorJOB pageToWcs;
        PageMonitorFromWCS pageFromWcs;
        PageMonitorSRM pageMonitorSrm;
        PageCraneOperate pageCraneOperate;
        PageAutoTeaching pageAutoTeaching;

        //Frm_Manual.Source = new Uri("PageManual.xaml", UriKind.Relative);
        //Frm_ProhRack.Source = new Uri("PageProhibitRack.xaml", UriKind.Relative);
        //Frm_CommSet.Source = new Uri("PageCommSet.xaml", UriKind.Relative);
        //Frm_CraneSet.Source = new Uri("PageCraneSet.xaml", UriKind.Relative);
        //Frm_StationSet.Source = new Uri("PageStationSet.xaml", UriKind.Relative);
        //Frm_SemiAuto.Source = new Uri("PageSemiAuto.xaml", UriKind.Relative);
        //Frm_AlarmList.Source = new Uri("PageAlarmLog.xaml", UriKind.Relative);

        Point m_position;

        // 기상반/지상반 모드 저장 플래그
        int controlMode = 0;
        // 지상반 모드 변경(키 스위치) 저장 플래그 
        int gcpMode = 0;
        // 현재 페이지 인덱스
        int curPageIdx = 0;
        // 2초 (2카운트 * 타이머주기)  타이머 주기 변경 시 동시변경
        int timer2s = 2;
        // 통신스레드 생성 지연 카운트
        int delayedTimer = 8;
        // DIO 워치독 비교 카운트
        long[] dioAliveCnt;

        // Set the Source property to the URI of the Page to display

        // GCP DIO 리스트 구조체 생성
        private List<inoutData> inputData;
        private List<inoutData> outputData;

        //Singletone
        singletonClass gClass;

        static System.Threading.Mutex mutex = new System.Threading.Mutex();

        // 종료 여부 확인 플래그 (X 버튼/종료 버튼 공용)
        private bool isExitConfirmed = false;

        //public static bool watchDogFlag = false;
        //Mutex mutexChk = new Mutex(true);

        //private static void WatchdogThread(int timeout)
        //{
        //    var startTime = DateTime.Now;

        //    // Keep running until the main operation is completed or timeout is reached
        //    while (true)
        //    {
        //        if (watchDogFlag)
        //        {

        //        }
        //        if ((DateTime.Now - startTime).TotalMilliseconds > timeout)
        //        {
        //            Console.WriteLine("Warning: Watchdog timeout exceeded. Potential problem detected.");
        //            return;
        //        }

        //        Thread.Sleep(1000); // Sleep for a short duration to avoid excessive CPU usage
        //    }

        //    Console.WriteLine("Watchdog: Main operation completed within the timeout.");
        //}

        public MainWindow()
        {
            // Check Update Folder-------------------------------------------------------------------------------------
            bool restartFlag = false;
            string buildDateStr = cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "GCPINFO", "UpdateDate");
            Console.WriteLine($"Build DateTime : " + buildDateStr);
            try
            {
                // Get the current date
                DateTime currentDate = DateTime.Now;

                // Iterate through files in the folder
                string[] subdirectories = Directory.GetDirectories(AppDomain.CurrentDomain.BaseDirectory);
                foreach (string dirPath in subdirectories)
                {
                    // Get the last modification time of the file
                    if (dirPath.Contains("gcp_update"))
                    {
                        Console.WriteLine("Find Dir : " + dirPath);
                        DateTime modifiedTime = Directory.GetLastWriteTime(dirPath);

                        if (String.Compare(buildDateStr, modifiedTime.ToString("yyyyMMddHHmmss")) < 0)       // 현재 폴더가 더 최신폴더 존재여부만 파악 후 Updater.exe 실행
                        {
                            Console.WriteLine("modify Time Find  : " + double.Parse(buildDateStr) + " " + double.Parse(modifiedTime.ToString("yyyyMMddHHmmss")));
                            restartFlag = true;
                            //break;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Another Dir : " + dirPath);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Check Update Folder Error : {e.Message}");
            }

            if (restartFlag)
            {
                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.FileName = "GcpStarter.exe";
                    startInfo.Arguments = "";
                    startInfo.RedirectStandardOutput = false;
                    //startInfo.UseShellExecute = true;
                    startInfo.CreateNoWindow = false;
                    // Start the process
                    Process.Start(startInfo);
                    Environment.Exit(0);
                    //Application.Current.Shutdown();

                }
                catch (Exception e)
                {
                    Console.WriteLine($"Gcp Starter Exec Error : {e.Message}");
                }
            }

            //------------------------------------------------------------------------------------------------------------
            // SingleTone Test
            gClass = singletonClass.Instance;




            string curDir = System.IO.Path.Combine(Environment.CurrentDirectory, cConstDefine.PATH_CONFIG);
            if (!Directory.Exists(curDir))
            {
                Directory.CreateDirectory(curDir);
                Console.WriteLine("Folder created at: " + curDir);
            }

            InitializeComponent();

            //byte[] testBuf = new byte[4];
            //testBuf[0] = 0x01;
            //testBuf[1] = 0x02;
            //testBuf[2] = 0x03;
            //testBuf[3] = 0x04;
            //uint test = BitConverter.ToUInt32(testBuf, 0);     // System DateTime


            // 테스트버튼 숨기기
            btn_test1.Visibility = Visibility.Collapsed;
            // 오토티칭 칸(col4)은 XAML의 Width="*"를 그대로 적용 — 코드에서 폭을 건드리지 않는다(2026-06-30).
            // (과거 이 칸이 테스트버튼 자리라 여기서 0폭으로 죽였었고, v1에서 오토티칭을 col4로 옮기며 그 줄이 버그가 돼 탭이 안 보였음.)

            //
            grid_Main.ColumnDefinitions[5].Width = new GridLength(0);
            grid_Main.ColumnDefinitions[6].Width = new GridLength(0);
            grid_Main.ColumnDefinitions[7].Width = new GridLength(0);
            //Combo_srmNum.ToolTip = "TEST ToolTip";

            //byte tmpValue = 255;
            //ushort testVal = (ushort)(tmpValue << 2);
            //Console.WriteLine("Test Shift Val  = " + tmpValue + "  " + testVal);



            // 페이지 접근 권한 프레임 초기화
            lbl_blockMode.Content = "";                                                         // 그 외 페이지 제어 가능
            Canvas.SetZIndex(lbl_blockMode, 0);


            myTimer.Interval = 1000; // 1 second
            myTimer.AutoReset = true; // Repeat the timer
            myTimer.Elapsed += TestTimer_Elapsed;

            // Admin Mode Timer 초기화
            adminModeTimer.Interval = 1000; // 1 second
            adminModeTimer.AutoReset = true;
            adminModeTimer.Elapsed += AdminModeTimer_Elapsed;

            menu_Setting = new Menu_Setting(this);
            menu_Setting.Hide();


            dpiX = VisualTreeHelper.GetDpi(this).PixelsPerInchX;
            dpiY = VisualTreeHelper.GetDpi(this).PixelsPerInchY;

            Btn_Main.Click += Mode_Click;
            Btn_Auto.Click += Mode_Click;
            Btn_Manual.Click += Mode_Click;
            Btn_SemiAuto.Click += Mode_Click;
            btn_test1.Click += Mode_Click;
            Btn_AutoTeaching.Click += Mode_Click;

            pageDevList = new PageDevState[3];          // 상태정보 패널 리스트 초기화

            dioAliveCnt = new long[3];


            // UI만 표시  --- Test


            // INI 파일 설정 정보 공유 구조체로 초기화  (순서 중요)  language
            // INI 파일은 자동 생성 됨
            gClass.str.GcpInfo.isAdminMode = false;
            gClass.str.GcpInfo.srmCount = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "GCPINFO", "CraneCount", "1"));
            gClass.str.GcpInfo.language = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "GCPINFO", "LanguageIdx", "0"));
            gClass.str.GcpInfo.buildDate = cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "GCPINFO", "BuildDate", "0");
            craneCnt = gClass.str.GcpInfo.srmCount;

            // 언어 설정 초기화
            switch (gClass.str.GcpInfo.language)
            {
                case 0:
                    TranslationSource.Instance.CurrentCulture = null;
                    break;
                case 1:
                    TranslationSource.Instance.CurrentCulture = new CultureInfo("en");
                    break;
                case 2:
                    TranslationSource.Instance.CurrentCulture = new CultureInfo("zh");
                    break;
            }

            // SRM <-> GCP   Udp Connection Init
            srmComm = new udpClientClass[craneCnt];
            // HOST <-> GCP   Tcp Connection Init
            hostComm = new tcpServerClass[craneCnt];
            // DIO <-> GCP    Modbus RTU Connection Init
            rtuComm = new modbusRtuClass[craneCnt];
            // Fastech DIO Init
            dioComm = new gcpDioClass[craneCnt];



            string tmpPath;
            for (int i = 0; i < craneCnt; i++)
            {
                // Config.ini
                gClass.str.SrmInfo[i].srmID = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "SRMINFO_" + i, "SRMID"));
                gClass.str.SrmInfo[i].srmType = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "SRMINFO_" + i, "SRMTYPE"));

                // ForkInfo.ini------------------------------------------------------------------
                gClass.str.SrmInfo[i].forkCnt = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + i + "\\ForkInfo.ini", "FORKINFO", "FORKCNT"));
                gClass.str.SrmInfo[i].forkType = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + i + "\\ForkInfo.ini", "FORKINFO", "FORKTYPE"));

                gClass.str.SrmInfo[i].forkLeftLimit = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + i + "\\ForkInfo.ini", "FORKLIMIT", "LEFT"));        // sw limit
                gClass.str.SrmInfo[i].forkRightLimit = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + i + "\\ForkInfo.ini", "FORKLIMIT", "RIGHT"));

                // DioInfo.ini------------------------------------------------------------------

                // DINPUT Dictionary 초기화
                for (int j = 0; j < Enum.GetValues(typeof(DISTATE)).Length; j++)
                {
                    string name = Enum.GetName(typeof(DISTATE), j);
                    string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"SRM{i}", "DioInfo.ini");
                    uint pinValue = uint.Parse(cIniAccess.Read(iniPath, name, "PIN"));
                    gClass.str.DioPacket[i].DISET[j].pin = pinValue;

                    string maskStr = cIniAccess.Read(iniPath, name, "MASK");
                    bool maskBool;
                    if (bool.TryParse(maskStr, out maskBool))
                    {
                        gClass.str.DioPacket[i].DISET[j].mask = maskBool;
                        Console.WriteLine("MASK DI TEST " + gClass.str.DioPacket[i].DISET[j].mask + " " + name + " " + gClass.str.DioPacket[i].DISET[j].pin);
                    }
                    else
                    {
                        gClass.str.DioPacket[i].DISET[j].mask = false;
                        Console.WriteLine("MASK DI FAIL " + gClass.str.DioPacket[i].DISET[j].mask + " " + name + " " + gClass.str.DioPacket[i].DISET[j].pin);
                    }
                }
                // DOUTPUT Dictionary 초기화
                for (int j = 0; j < Enum.GetValues(typeof(DOSTATE)).Length; j++)
                {
                    string name = Enum.GetName(typeof(DOSTATE), j);
                    string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"SRM{i}", "DioInfo.ini");
                    uint pinValue = uint.Parse(cIniAccess.Read(iniPath, name, "PIN"));


                    gClass.str.DioPacket[i].DOSET[j].pin = pinValue;

                    string maskStr = cIniAccess.Read(iniPath, name, "MASK");
                    bool maskBool;
                    if (bool.TryParse(maskStr, out maskBool))
                    {
                        gClass.str.DioPacket[i].DOSET[j].mask = maskBool;
                        Console.WriteLine("MASK DO TEST " + gClass.str.DioPacket[i].DOSET[j].mask);
                    }
                    else
                    {
                        gClass.str.DioPacket[i].DOSET[j].mask = false;
                        Console.WriteLine("MASK DO FAIL " + gClass.str.DioPacket[i].DOSET[j].mask + name);
                    }
                }

                // RackInfo.ini------------------------------------------------------------------
                gClass.str.SrmInfo[i].row = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + i + "\\RACK" + "\\Rack.ini", "RACKINFO", "MaxRow"));
                gClass.str.SrmInfo[i].bay = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + i + "\\RACK" + "\\Rack.ini", "RACKINFO", "Maxbay"));
                gClass.str.SrmInfo[i].lev = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + i + "\\RACK" + "\\Rack.ini", "RACKINFO", "MaxLev"));

                gClass.str.SrmInfo[i].stn = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + i + "\\Station.ini", "STATION", "COUNT"));


                gClass.str.SrmInfo[i].srmIP = cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "SRMCOMM_" + i, "SRMIP");
                gClass.str.SrmInfo[i].srmPORT = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "SRMCOMM_" + i, "SRMPORT"));
                gClass.str.SrmInfo[i].modemErrorCheck = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "SRMCOMM_" + i, "MODEMERRORCHECK", "1"));

                gClass.str.SrmInfo[i].hostPORT = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "HOSTCOMM_" + i, "HOSTPORT"));
                gClass.str.SrmInfo[i].hostTimeout = uint.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "HOSTCOMM_" + i, "HOSTTIMEOUT"));
                gClass.str.SrmInfo[i].heartBeatCheck = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "HOSTCOMM_" + i, "HEARTBEATCHECK", "0"));
                gClass.str.SrmInfo[i].heartBeatTimeout = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "HOSTCOMM_" + i, "HEARTBEATTIMEOUT", "5"));

                gClass.str.SrmInfo[i].dioUse = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "DIOCOMM_" + i, "DIOUSE", "1"));     // DIO Rtu Use Default =  ON           Bool Parse Exception 방지
                gClass.str.SrmInfo[i].sfUse = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "DIOCOMM_" + i, "SFUSE", "1"));     // Safety Plug Use
                gClass.str.SrmInfo[i].dioType = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "DIOCOMM_" + i, "DIOTYPE", "0"));     // Dio Type

                gClass.str.SrmInfo[i].dioIP = cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "DIOCOMM_" + i, "DIOIP", "192.168.0.2");     // DIO IP Default =  192.168.0.2
                gClass.str.SrmInfo[i].dioID = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "DIOCOMM_" + i, "DIOID", "0"));     // DIO ID Default = 0

                gClass.str.SrmInfo[i].comPORT = cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "DIOCOMM_" + i, "COMPORT");
                gClass.str.SrmInfo[i].baudRate = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "DIOCOMM_" + i, "BAUDRATE", "57600"));           // 보레이트 부품변경 없는 한 고정
                gClass.str.SrmInfo[i].parity = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "DIOCOMM_" + i, "PARITY", "0"));
                gClass.str.SrmInfo[i].dataBit = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "DIOCOMM_" + i, "DATABIT", "8"));
                gClass.str.SrmInfo[i].stopBit = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "DIOCOMM_" + i, "STOPBIT", "1"));


                // Station.ini

                for (int stn = 0; stn < gClass.str.SrmInfo[i].stn; stn++)
                {
                    gClass.str.SrmInfo[i].SrmStation[stn].stnType = byte.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + i + "\\Station.ini", "STATION" + stn, "TYPE", gClass.str.SrmInfo[i].SrmStation[stn].stnType.ToString()));
                    gClass.str.SrmInfo[i].SrmStation[stn].goodType = byte.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + i + "\\Station.ini", "STATION" + stn, "GOODTYPE", gClass.str.SrmInfo[i].SrmStation[stn].goodType.ToString()));
                    gClass.str.SrmInfo[i].SrmStation[stn].travPos = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + i + "\\Station.ini", "STATION" + stn, "TRAVPOS", gClass.str.SrmInfo[i].SrmStation[stn].travPos.ToString()));
                    gClass.str.SrmInfo[i].SrmStation[stn].liftPos = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + i + "\\Station.ini", "STATION" + stn, "LIFTPOS", gClass.str.SrmInfo[i].SrmStation[stn].liftPos.ToString()));
                    gClass.str.SrmInfo[i].SrmStation[stn].forkPos = short.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + i + "\\Station.ini", "STATION" + stn, "FORKPOS", gClass.str.SrmInfo[i].SrmStation[stn].forkPos.ToString()));
                    gClass.str.SrmInfo[i].SrmStation[stn].upOffset = byte.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + i + "\\Station.ini", "STATION" + stn, "UPOFFSET", gClass.str.SrmInfo[i].SrmStation[stn].upOffset.ToString()));
                    gClass.str.SrmInfo[i].SrmStation[stn].downOffset = byte.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + i + "\\Station.ini", "STATION" + stn, "DOWNOFFSET", gClass.str.SrmInfo[i].SrmStation[stn].downOffset.ToString()));
                    gClass.str.SrmInfo[i].SrmStation[stn].intNum = byte.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + i + "\\Station.ini", "STATION" + stn, "INTNUM", gClass.str.SrmInfo[i].SrmStation[stn].intNum.ToString()));
                }

                // Rack
                tmpPath = System.IO.Path.Combine(Environment.CurrentDirectory, "SRM" + i, cConstDefine.PATH_RACK);
                if (!Directory.Exists(tmpPath))
                {
                    Directory.CreateDirectory(tmpPath);
                    Console.WriteLine("Folder created at: " + tmpPath);
                }

                for (int bay = 0; bay < 256; bay++)               // BAY Array Initialize
                {
                    gClass.str.SrmInfo[i].cellBay[bay] = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + i + "\\RACK" + "\\Rack.ini", "RACK_BAY", "BAY" + bay, "0"));
                }
                for (int lev = 0; lev < 128; lev++)               // LEV Array Initialize
                {
                    gClass.str.SrmInfo[i].cellLev[lev] = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + i + "\\RACK" + "\\Rack.ini", "RACK_LEV", "LEV" + lev, "0"));
                }


                // ProhRack
                tmpPath = System.IO.Path.Combine(Environment.CurrentDirectory, "SRM" + i, cConstDefine.PATH_PRHRACK);
                if (!Directory.Exists(tmpPath))
                {
                    Directory.CreateDirectory(tmpPath);
                    Console.WriteLine("Folder created at: " + tmpPath);
                }
                else
                {
                    gClass.str.SrmInfo[i].prohCnt = int.Parse(cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + i + "\\PRHRACK" + "\\ProhRack.ini", "PROH_INFO", "COUNT"));
                    // ini 정보 불러오기
                    string[] prohStr;
                    gClass.str.SrmInfo[i].prohParseCnt = 0;
                    for (int j = 0; j < gClass.str.SrmInfo[i].prohCnt; j++)
                    {
                        gClass.str.SrmInfo[i].prohRack[j] = cIniAccess.Read(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + i + "\\PRHRACK" + "\\ProhRack.ini", "PROH_LIST", j.ToString());
                        prohStr = gClass.str.SrmInfo[i].prohRack[j].Split("-");
                        if (prohStr.Count() > 2)
                        {
                            if (int.Parse(prohStr[0]) == 0)          // 전체 ROW
                            {
                                for (int k = 1; k <= gClass.str.SrmInfo[i].row; k++)
                                {
                                    ProhRackParse(i, k, int.Parse(prohStr[1]), int.Parse(prohStr[2]));
                                }
                            }
                            else
                            {
                                ProhRackParse(i, int.Parse(prohStr[0]), int.Parse(prohStr[1]), int.Parse(prohStr[2]));
                            }
                        }
                    }

                    for (int cnt = 0; cnt < gClass.str.SrmInfo[i].prohParseCnt; cnt++)           // 금지 ROW 파싱데이터 카운트
                    {
                        Console.WriteLine("Find Proh Rack List : " + i + "/" + gClass.str.SrmInfo[i].prohDataList[cnt][0] + "/" + gClass.str.SrmInfo[i].prohDataList[cnt][1] + "/" + gClass.str.SrmInfo[i].prohDataList[cnt][2]);
                    }
                }

                // 초기 구동 시 에러방지
                gClass.str.DioPacket[i].DISET[(int)DISTATE.EM_SW].value = true;
                gClass.str.DioPacket[i].DISET[(int)DISTATE.SF_PLUG].value = true;
                gClass.str.DioPacket[i].DISET[(int)DISTATE.MODEM_EN].value = true;
                gClass.str.DioPacket[i].DISET[(int)DISTATE.REQ_STOP].value = false;

                // 광모뎀 카운트 초기화
                gClass.str.SrmPacket[i].gcpModemFltCnt = 2;

                // Alarm
                tmpPath = System.IO.Path.Combine(Environment.CurrentDirectory, "SRM" + i, cConstDefine.PATH_LOG, cConstDefine.PATH_ALARMLOG);
                if (!Directory.Exists(tmpPath))
                {
                    Directory.CreateDirectory(tmpPath);
                    Console.WriteLine("Folder created at: " + tmpPath);
                }

                // 상태정보 페이지 생성 및 초기화
                pageDevList[i] = new PageDevState(i);         // SRM #1  배열 인덱스에 맞춤

                //NotProcessed_JobCheck(i);


                // 통신 스레드 초기화
                srmComm[i] = new udpClientClass(i);
                hostComm[i] = new tcpServerClass(this, i, gClass.str.SrmInfo[i].hostPORT);
                //rtuComm[i] = new modbusRtuClass(i, gClass.str.SrmInfo[i].comPORT, gClass.str.SrmInfo[i].baudRate, gClass.str.SrmInfo[i].parity, gClass.str.SrmInfo[i].dataBit, gClass.str.SrmInfo[i].stopBit);
                dioComm[i] = new gcpDioClass(i, gClass.str.SrmInfo[i].dioIP, gClass.str.SrmInfo[i].dioID);
            }

            gClass.str.GcpInfo.isAdminMode = false;

            //Set Program Ver Text-------------------------------------------------------------------------------------
            string progType;
#if DONGWON
            progType = "DONGWON";
#else
            progType = "V1.0";
#endif

            string filePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            // 파일 정보 가져오기
            FileInfo fileInfo = new FileInfo(filePath);

            // 생성일자 가져오기
            DateTime creationDate = fileInfo.CreationTime;

            // 수정일자 가져오기
            DateTime lastModifiedDate = fileInfo.LastWriteTime;

            // 결과 출력
            Console.WriteLine("파일 생성일: " + creationDate);
            Console.WriteLine("파일 수정일: " + lastModifiedDate);

            if (lastModifiedDate.ToString() != gClass.str.GcpInfo.buildDate)
            {
                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "GCPINFO", "BuildDate", lastModifiedDate.ToString());
            }

            // 프로그램 타이틀에 빌드 날짜 추가
            this.Title = "GCP - BuildDate: " + lastModifiedDate.ToString("yyyy-MM-dd HH:mm");

            // 현재 시간 표시 초기화
            UpdateCurrentTime();
            //------------------------------------------------------------------------------------------------------------


            // Page 객체 생성
            pageMain = new PageMain();
            pageManual = new PageManual();
            pageAuto = new PageAuto();
            pageProhRack = new PageProhibitRack();
            pageCommSet = new PageCommSet(this);
            pageCraneSet = new PageCraneSet(this);
            pageStationSet = new PageStationSet();
            pageSemiAuto = new PageSemiAuto(this);
            pageAlarmLog = new PageAlarmLog();
            pageDio = new PageDIO();
            pageToWcs = new PageMonitorJOB(this);
            pageFromWcs = new PageMonitorFromWCS();
            pageMonitorSrm = new PageMonitorSRM();
            //pageCraneOperate = new PageCraneOperate();
            pageAutoTeaching = new PageAutoTeaching(this);


            Bg_Page.Content = pageAuto;

            if (craneCnt > 1)
            {
                Page_Change(cConstDefine.PAGE_MAIN);
                Btn_Main.IsEnabled = true;
            }
            else
            {
                Page_Change(cConstDefine.PAGE_MANUAL);
                //Page_Change(cConstDefine.PAGE_CRANE_OPERATE);
                Btn_Main.IsEnabled = false;
            }

            for (int i = 0; i < craneCnt; i++)
            {

                Combo_srmNum.Items.Add("SRM #" + gClass.str.SrmInfo[i].srmID.ToString());
            }

            if (Combo_srmNum.Items.Count > 0)
            {
                Combo_srmNum.SelectedIndex = 0;

            }
            gClass.srmNum = 0;
            // tcpServer Class 접근을 위한 클래스 레퍼런스 체인지
            servServer = hostComm[gClass.srmNum];

            // 언어 선택 ContextMenu 초기화 (현재 언어 표시)
            UpdateLangContextMenu();

            // 관리자 모드 보더 초기화
            Brd_AdminMode.Visibility = Visibility.Collapsed;
            Grid_LogoNormal.Visibility = Visibility.Visible;

            // 언어 변경 이벤트 구독
            TranslationSource.Instance.PropertyChanged += TranslationSource_PropertyChanged;

            //rtuComm[0] = new modbusRtuClass(0, gClass.str.SrmInfo[0].comPort, gClass.str.SrmInfo[0].baudRate, gClass.str.SrmInfo[0].parity, gClass.str.SrmInfo[0].dataBit, gClass.str.SrmInfo[0].stopBit);


            //foreach (var window in Application.Current.Windows)
            //{
            //    //Console.WriteLine("ClassName : " + window.GetType().Name);

            //    if (window.GetType().Name == "WindowSrmLog1")
            //    {
            //        //(window.GetType().
            //        //Console.WriteLine("찾았다!!!");
            //        // Found the window!
            //    }
            //}

            // WindowSrmLog 동적 할당
            win_HostLog1 = new WindowSrmLog(1, 1);
            win_HostLog2 = new WindowSrmLog(1, 2);
            win_HostLog3 = new WindowSrmLog(1, 3);

            win_SrmLog1 = new WindowSrmLog(2, 1);
            win_SrmLog2 = new WindowSrmLog(2, 2);
            win_SrmLog3 = new WindowSrmLog(2, 3);

            win_DioLog1 = new WindowSrmLog(3, 1);
            win_DioLog2 = new WindowSrmLog(3, 2);
            win_DioLog3 = new WindowSrmLog(3, 3);

            win_JobLog = new WindowSrmLog(4, 1);

            Dispatcher.Invoke(() =>
            {
                // Get Alarm Log------------------------------------------------------------
                AlarmList_Init(gClass.srmNum);
            });


            myTimer.Start();


            //gClass.str.SrmPacket[gClass.srmNum].jobError = true;
            //gClass.str.SrmPacket[gClass.srmNum].gcpErrorCode = 66;  // DATA ReportOK 대기 타임아웃
            //gClass.str.SrmPacket[gClass.srmNum].gcpSubCode = 08;

            //------------------------------------------------------------------------------------------------------------
            // WatchDog Thread Started
            //var watchdog = new Thread(() => WatchdogThread(5000));
            //watchdog.Start();
        }

        private void AlarmList_Init(int srmNo)
        {
            try
            {
                string alarmPath = System.IO.Path.Combine(Environment.CurrentDirectory, "SRM" + srmNo, cConstDefine.PATH_LOG, cConstDefine.PATH_ALARMLOG);

                DateTime alDate;
                string filePath;

                string[] alText = new string[3];
                string[] alList;

                // 기존 에러 리스트 초기화
                pageAlarmLog.errorListClear();

                // 최근 3일 내역 불러오기 (오늘 포함)
                for (int i = 2; i >= 0; i--)
                {
                    alDate = DateTime.Now.AddDays(-i);
                    filePath = System.IO.Path.Combine(alarmPath, "ALARMLOG_" + alDate.ToString("yyyyMMdd") + ".log");

                    if (File.Exists(filePath))
                    {
                        string[] lines = File.ReadAllLines(filePath);
                        // Display each line
                        foreach (string line in lines)
                        {
                            if (line.Contains("File created on"))                // 파일 시작 로드 X
                            {
                                continue;
                            }
                            alList = line.Split('/');
                            for (int j = 0; j < 3; j++)
                            {
                                if (j < alList.Length)
                                {
                                    alText[j] = alList[j];
                                }
                                else
                                {
                                    alText[j] = "";         // 리스트 Fault 방지
                                }
                            }
                            pageAlarmLog.errorOccurred(srmNo, alText[0], alText[1], alText[2], false);         // file save Flag = false  초기 로딩에서는 저장 플래그 X
                            //Console.WriteLine(filePath + " " + line);
                        }
                    }
                    else
                    {
                        continue;
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("AlarmList Init Exception " + ex.Message);
            }
        }

        ~MainWindow()
        {
            Console.WriteLine("Destructor MainWindow Delete");
        }


        // "2023-03-31 15:02:26"  
        private void TestTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                // WatchDog Cnt Inc
                cIniAccess.watchDogCnt += 1;

                // 현재 시간 업데이트
                UpdateCurrentTime();


                // Console.WriteLine("Timer Test : ");

                if (delayedTimer > 0)
                {
                    delayedTimer -= 1;
                    if (delayedTimer == 0)
                    {
                        for (int i = 0; i < craneCnt; i++)
                        {
                            try
                            {
                                srmComm[i].connect(gClass.str.SrmInfo[i].srmIP, gClass.str.SrmInfo[i].srmPORT);
                                hostComm[i].StartServer(gClass.str.SrmInfo[i].hostPORT);
                                if (gClass.str.SrmInfo[i].dioType == 0)          // Fastech 16
                                {
                                    dioComm[i].StartDioClient();
                                }
                                else if (rtuComm[i] != null)   // rtuComm 요소는 현재 미초기화(생성 주석처리) → null 가드로 NRE 방지
                                {
                                    rtuComm[i].StartModbusClient();
                                    //rtuComm[i].RestartModbusThread();
                                }
                                gClass.str.SrmInfo[i].dioAliveCnt = 1;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"통신스레드 시작 실패(SRM {i + 1}): {ex.Message}");
                                cIniAccess.SaveExLog(i, $"EXCEPTION - InitCommThread : {ex.Message}");
                            }
                        }
                    }
                    return;
                }

                if (timer2s-- < 0)       // 2초 주기 체크
                {
                    for (int i = 0; i < craneCnt; i++)
                    {
                        if (gClass.str.SrmInfo[i].dioType == 0 && !dioComm[i]._isConnected)          // Fastech 16
                        {
                            Console.WriteLine("Restart DIO TCP Thread ");
                            //rtuComm[i].Close();                     // 중복 스레드 제거용
                            dioComm[i].StartDioClient();
                        }
                        else if (gClass.str.SrmInfo[i].dioType == 1 && rtuComm[i] != null && !rtuComm[i]._isRunning)
                        {
                            Console.WriteLine("Restart DIO RTU Thread ");
                            //dioComm[i].Close();                     // 중복 스레드 제거용
                            rtuComm[i].RestartModbusThread();
                        }
                    }

                    timer2s = 2;
                }


                //Console.WriteLine("Current Job STEP : " + gClass.str.SrmPacket[gClass.srmNum].jobState);

                // BeginInvoke 사용: Invoke는 동기 호출이라 타이머 스레드가 UI 완료까지 대기 → UI 지연 시 다음 틱이 겹치며 블로킹/멈춤 발생 가능. BeginInvoke로 비동기 큐잉만 하면 타이머는 계속 동작.
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 포지션 값에 따른 현재위치 계산

                    // TEST CODE
                    //cIniAccess.SaveJobLog(0, "테스트 로그");

                    try
                    {
                        //Console.WriteLine("Error Text Test : ");
                        //Console.WriteLine("Error Text Test : " + pageAlarmLog.getErrorText("66", "00"));
                        //if(Bg_Page.Content != pageAuto)
                        //{
                        //    Console.WriteLine("Page Background Not Auto");
                        //}

                        // 키 모드 체크 및 페이지 전환
                        if (controlMode != modeCheck())       // 모드체크 상태변경 시
                        {
                            pageCheck(modeCheck());
                        }

                        // 메인 스레드 종료여부 확인 워치독
                        if (watchDogCount > 255)
                        {
                            watchDogCount = 0;
                        }
                        else
                        {
                            watchDogCount += 1;
                        }
                        // Check Alarm Occured
                        errorCheck();

                        // Comm TX/RX Check
                        // SRM Comm State Check-------------------------------------------------
                        if (gClass.str.SrmPacket[gClass.srmNum].srmCommDiscCnt > 0)
                        {
                            lbl_SRMComm.Background = img_connect;
                        }
                        else
                        {
                            lbl_SRMComm.Background = img_disconnect;
                        }

                        if (gClass.str.SrmPacket[gClass.srmNum].txSrmComm)
                        {
                            if (SRM_TX.Foreground == Brushes.LightGreen)
                            {
                                SRM_TX.Foreground = Brushes.DarkGreen;
                                SRM_TX.FontWeight = FontWeights.Regular;
                                SRM_TX.FontSize = 12;
                            }
                            else
                            {
                                SRM_TX.Foreground = Brushes.LightGreen;
                                SRM_TX.FontWeight = FontWeights.Bold;
                                SRM_TX.FontSize = 13;
                            }
                        }
                        else
                        {
                            SRM_TX.Foreground = Brushes.DarkGreen;
                            SRM_TX.FontWeight = FontWeights.Regular;
                            SRM_TX.FontSize = 12;
                        }


                        if (gClass.str.SrmPacket[gClass.srmNum].rxSrmComm)
                        {
                            if (SRM_RX.Foreground == Brushes.Red)
                            {
                                SRM_RX.Foreground = Brushes.DarkRed;
                                SRM_RX.FontWeight = FontWeights.Regular;
                                SRM_RX.FontSize = 12;
                            }
                            else
                            {
                                SRM_RX.Foreground = Brushes.Red;
                                SRM_RX.FontWeight = FontWeights.Bold;
                                SRM_RX.FontSize = 13;
                            }
                        }
                        else
                        {
                            SRM_RX.Foreground = Brushes.DarkRed;
                            SRM_RX.FontWeight = FontWeights.Regular;
                            SRM_RX.FontSize = 12;
                        }

                        // DIO Comm State Check---------------------------------------------------
                        if (gClass.str.SrmPacket[gClass.srmNum].dioCommDiscCnt > 0)     // Connect / Disconnect를 반복하기 때문에
                        {
                            lbl_DIOComm.Background = img_connect;
                        }
                        else
                        {
                            lbl_DIOComm.Background = img_disconnect;
                        }

                        if (gClass.str.SrmPacket[gClass.srmNum].txDioComm)
                        {
                            if (DIO_TX.Foreground == Brushes.LightGreen)
                            {
                                DIO_TX.Foreground = Brushes.DarkGreen;
                                DIO_TX.FontWeight = FontWeights.Regular;
                                DIO_TX.FontSize = 12;
                            }
                            else
                            {
                                DIO_TX.Foreground = Brushes.LightGreen;
                                DIO_TX.FontWeight = FontWeights.Bold;
                                DIO_TX.FontSize = 13;
                            }
                        }
                        else
                        {
                            DIO_TX.Foreground = Brushes.DarkGreen;
                            DIO_TX.FontWeight = FontWeights.Regular;
                            DIO_TX.FontSize = 12;
                        }

                        if (gClass.str.SrmPacket[gClass.srmNum].rxDioComm)
                        {
                            if (DIO_RX.Foreground == Brushes.Red)
                            {
                                DIO_RX.Foreground = Brushes.DarkRed;
                                DIO_RX.FontWeight = FontWeights.Regular;
                                DIO_RX.FontSize = 12;
                            }
                            else
                            {
                                DIO_RX.Foreground = Brushes.Red;
                                DIO_RX.FontWeight = FontWeights.Bold;
                                DIO_RX.FontSize = 13;
                            }
                        }
                        else
                        {
                            DIO_RX.Foreground = Brushes.DarkRed;
                            DIO_RX.FontWeight = FontWeights.Regular;
                            DIO_RX.FontSize = 12;
                        }

                        // WCS Comm State Check---------------------------------------------------
                        if (gClass.str.SrmPacket[gClass.srmNum].stWcsComm)
                        {
                            lbl_WCSComm.Background = img_connect;
                        }
                        else
                        {
                            lbl_WCSComm.Background = img_disconnect;
                        }

                        if (gClass.str.SrmPacket[gClass.srmNum].txWcsComm)
                        {
                            if (WCS_TX.Foreground == Brushes.LightGreen)
                            {
                                WCS_TX.Foreground = Brushes.DarkGreen;
                                WCS_TX.FontWeight = FontWeights.Regular;
                                WCS_TX.FontSize = 12;
                            }
                            else
                            {
                                WCS_TX.Foreground = Brushes.LightGreen;
                                WCS_TX.FontWeight = FontWeights.Bold;
                                WCS_TX.FontSize = 13;
                            }

                            gClass.str.SrmPacket[gClass.srmNum].txWcsComm = false;
                        }
                        else
                        {
                            WCS_TX.Foreground = Brushes.DarkGreen;
                            WCS_TX.FontWeight = FontWeights.Regular;
                            WCS_TX.FontSize = 12;
                        }

                        if (gClass.str.SrmPacket[gClass.srmNum].rxWcsComm)
                        {
                            if (WCS_RX.Foreground == Brushes.Red)
                            {
                                WCS_RX.Foreground = Brushes.DarkRed;
                                WCS_RX.FontWeight = FontWeights.Regular;
                                WCS_RX.FontSize = 12;
                            }
                            else
                            {
                                WCS_RX.Foreground = Brushes.Red;
                                WCS_RX.FontWeight = FontWeights.Bold;
                                WCS_RX.FontSize = 13;
                            }

                            gClass.str.SrmPacket[gClass.srmNum].rxWcsComm = false;
                        }
                        else
                        {
                            WCS_RX.Foreground = Brushes.DarkRed;
                            WCS_RX.FontWeight = FontWeights.Regular;
                            WCS_RX.FontSize = 12;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception - MainWindowTimer");
                        cIniAccess.SaveExLog(0, "EXCEPTION - MainWindowTimer : " + ex.Message);
                    }
                }));
            }
            catch (Exception ex)
            {
                cIniAccess.SaveExLog(0, "EXCEPTION - TestTimer_Elapsed (timer thread): " + ex.Message);
            }
        }

        private void ProhRackParse(int srmNum, int row, int bay, int lev)
        {
            if (bay == 0)
            {            // 모든 Bay
                for (int j = 0; j < gClass.str.SrmInfo[srmNum].bay; j++)
                {
                    if (lev == 0)     // 모든 Lev
                    {
                        for (int k = 0; k < gClass.str.SrmInfo[srmNum].lev; k++)
                        {
                            gClass.str.SrmInfo[srmNum].prohDataList[gClass.str.SrmInfo[srmNum].prohParseCnt][0] = row;
                            gClass.str.SrmInfo[srmNum].prohDataList[gClass.str.SrmInfo[srmNum].prohParseCnt][1] = j + 1;
                            gClass.str.SrmInfo[srmNum].prohDataList[gClass.str.SrmInfo[srmNum].prohParseCnt][2] = k + 1;
                            gClass.str.SrmInfo[srmNum].prohParseCnt++;
                        }
                    }
                    else
                    {
                        gClass.str.SrmInfo[srmNum].prohDataList[gClass.str.SrmInfo[srmNum].prohParseCnt][0] = row;
                        gClass.str.SrmInfo[srmNum].prohDataList[gClass.str.SrmInfo[srmNum].prohParseCnt][1] = j + 1;
                        gClass.str.SrmInfo[srmNum].prohDataList[gClass.str.SrmInfo[srmNum].prohParseCnt][2] = lev;
                        gClass.str.SrmInfo[srmNum].prohParseCnt++;
                    }
                }
            }
            else
            {
                if (lev == 0)     // 모든 Lev
                {
                    for (int k = 0; k < gClass.str.SrmInfo[srmNum].lev; k++)
                    {
                        gClass.str.SrmInfo[srmNum].prohDataList[gClass.str.SrmInfo[srmNum].prohParseCnt][0] = row;
                        gClass.str.SrmInfo[srmNum].prohDataList[gClass.str.SrmInfo[srmNum].prohParseCnt][1] = bay;
                        gClass.str.SrmInfo[srmNum].prohDataList[gClass.str.SrmInfo[srmNum].prohParseCnt][2] = k + 1;
                        gClass.str.SrmInfo[srmNum].prohParseCnt++;
                    }
                }
                else
                {
                    gClass.str.SrmInfo[srmNum].prohDataList[gClass.str.SrmInfo[srmNum].prohParseCnt][0] = row;
                    gClass.str.SrmInfo[srmNum].prohDataList[gClass.str.SrmInfo[srmNum].prohParseCnt][1] = bay;
                    gClass.str.SrmInfo[srmNum].prohDataList[gClass.str.SrmInfo[srmNum].prohParseCnt][2] = lev;
                    gClass.str.SrmInfo[srmNum].prohParseCnt++;
                }
            }
        }

        // 현재 포지션 체크
        private void posCheck()
        {
            //for (int i = 0; i < craneCnt; i++)
            //{
            //    for (int j = 0; j < gClass.str.SrmInfo[i].bay; j++)
            //    {
            //        //gClass.str.SrmPacket[i].curBay = 1;
            //    }
            //    for (int j = 0; j < gClass.str.SrmInfo[i].lev; j++)
            //    {

            //    }
            //}
        }

        // 에러체크 및 알람발생
        private void errorCheck()
        {
            for (int i = 0; i < craneCnt; i++)
            {
                bool curErrorSt = true;         // 에러 해제용 플래그
                bool curWarningSt = true;       // 경고 해제용 플래그

                // 크레인 발생 에러/경고
                // dSt1Abnormal 또는 dSt1Warning이 1이고 errcodeH가 0보다 클 때만 에러/경고로 처리
                // errcodeH가 0이면 실제 에러가 아니므로 무시 (통신 끊김 시 이전 값이 남아있을 수 있음)
                if (((gClass.str.SrmState[i].dSt1Abnormal > 0) || (gClass.str.SrmState[i].dSt1Warning > 0)) && gClass.str.SrmState[i].errcodeH > 0)
                {
                    // dSt1Abnormal이면 에러로 처리
                    if (gClass.str.SrmState[i].dSt1Abnormal > 0)
                    {
                        gClass.str.SrmPacket[i].gcpError = true;
                        gClass.str.SrmPacket[i].gcpErrorCode = gClass.str.SrmState[i].errcodeH;
                        gClass.str.SrmPacket[i].gcpSubCode = gClass.str.SrmState[i].errcodeM;
                    }
                    // dSt1Warning이면 경고로 처리
                    if (gClass.str.SrmState[i].dSt1Warning > 0)
                    {
                        gClass.str.SrmPacket[i].gcpWarning = true;
                        gClass.str.SrmPacket[i].gcpWarningCode = gClass.str.SrmState[i].errcodeH;
                        gClass.str.SrmPacket[i].gcpWarningSubCode = gClass.str.SrmState[i].errcodeM;
                    }
                }
                else
                {
                    //SRM 에러/ 경고가 없을 때 해제
                    //if (gClass.str.SrmState[i].dSt1Abnormal == 0)
                    //{
                    //    curErrorSt = false;
                    //}
                    if (gClass.str.SrmState[i].dSt1Warning == 0)
                    {
                        curWarningSt = false;
                    }
                }

                // 지상반 발생 에러 체크 (SRM 에러가 없을 때만)
                if (gClass.str.SrmState[i].dSt1Abnormal == 0)
                {
                    // 지상반 발생 에러
                    // 광모뎀 에러
                    // to do
                    if (gClass.str.SrmInfo[i].modemErrorCheck == 1 && (cIniAccess.watchDogCnt > 10) && !gClass.str.SrmPacket[i].isAutoTeaching)      // 지상반 광모뎀 에러 체크는 구동 후 10초지연 후 체크 (오토티칭 중 비활성화)
                    {
                        // MODEM_EN 핀 설정이 되어 있는 경우: DIO 핀 값으로 체크
                        if (gClass.str.DioPacket[i].DISET[(int)DISTATE.MODEM_EN].pin > 0)
                        {
                            if (gClass.str.DioPacket[i].DISET[(int)DISTATE.MODEM_EN].value == false)
                            {
                                // 250421 지상반 광모뎀 에러 제거
                                gClass.str.SrmPacket[i].gcpModemFltCnt -= 1;
                                if (gClass.str.SrmPacket[i].gcpModemFltCnt <= 0)
                                {
                                    gClass.str.SrmPacket[i].gcpModemFlt = true;
                                    gClass.str.SrmPacket[i].gcpError = true;
                                    gClass.str.SrmPacket[i].gcpErrorCode = 96;            // 지상반 에러코드 확인
                                    gClass.str.SrmPacket[i].gcpSubCode = 01;              // 지상반 서브코드 확인
                                    // FLT 96-01 : 광모뎀 통신 장애 (지상반에서 광모뎀 통신 장애 감지 시 발생, SRM에서는 MODEM_EN 핀으로 체크)
                                }
                            }
                        }
                        // MODEM_EN 핀 설정이 안되어 있는 경우: UDP 통신 타임아웃으로 체크 (카운트 없이 바로 에러 발생)
                        else
                        {

                        }
                    }
                    if (gClass.str.SrmInfo[i].modemErrorCheck == 1 && !gClass.str.SrmPacket[i].isAutoTeaching && gClass.str.DioPacket[i].DISET[(int)DISTATE.MODEM_EN].pin > 0 && gClass.str.DioPacket[i].DISET[(int)DISTATE.MODEM_EN].value == false)
                    {
                        gClass.str.SrmPacket[i].gcpModemFltCnt -= 1;
                        if (gClass.str.SrmPacket[i].gcpModemFltCnt <= 0)
                        {
                            gClass.str.SrmPacket[i].gcpModemFlt = true;
                            gClass.str.SrmPacket[i].gcpError = true;
                            gClass.str.SrmPacket[i].gcpErrorCode = 96;            // 지상반 에러코드 확인
                            gClass.str.SrmPacket[i].gcpSubCode = 01;              // 지상반 서브코드 확인
                            // FLT 96-01 : 광모뎀 통신 장애 (지상반에서 광모뎀 통신 장애 감지 시 발생, SRM에서는 MODEM_EN 핀으로 체크)
                        }
                    }
                    else if (gClass.str.SrmInfo[i].modemErrorCheck == 1 && !gClass.str.SrmPacket[i].isAutoTeaching && gClass.str.SrmPacket[i].lastUdpReceiveTime != DateTime.MinValue && (DateTime.Now - gClass.str.SrmPacket[i].lastUdpReceiveTime).TotalSeconds > 1.0)
                    {
                        // 마지막 UDP 수신 시간이 한 번이라도 들어온 상태에서 1초 이상 지난 경우에만 에러
                        // 타임아웃 에러는 카운트 없이 바로 에러 발생
                        gClass.str.SrmPacket[i].gcpModemFlt = true;
                        gClass.str.SrmPacket[i].gcpError = true;
                        gClass.str.SrmPacket[i].gcpErrorCode = 96;            // 지상반 에러코드 확인
                        gClass.str.SrmPacket[i].gcpSubCode = 01;              // 지상반 서브코드 확인
                        // FLT 96-01 : 광모뎀 통신 장애 (지상반에서 광모뎀 통신 장애 감지 시 발생, SRM에서는 UDP 통신 타임아웃으로 체크)
                    }
                    else if (gClass.str.SrmInfo[i].heartBeatCheck == 1 && gClass.str.SrmPacket[i].stWcsComm && (DateTime.Now - gClass.str.SrmPacket[i].lastHeartBeatTime).TotalSeconds > gClass.str.SrmInfo[i].heartBeatTimeout && gClass.str.SrmPacket[i].lastHeartBeatTime != DateTime.MinValue)
                    {
                        gClass.str.SrmPacket[i].heartBeatError = true;
                        gClass.str.SrmPacket[i].gcpError = true;
                        gClass.str.SrmPacket[i].gcpErrorCode = 96;            // 지상반 에러코드 확인
                        gClass.str.SrmPacket[i].gcpSubCode = 02;              // 지상반 서브코드 확인
                        // FLT 96-02 : WCS Heartbeat Timeout (WCS와의 통신은 되고 있지만, WCS에서 일정 시간 동안 Heartbeat 신호를 받지 못한 경우 발생)
                    }
                    else if (gClass.str.DioPacket[i].DISET[(int)DISTATE.EM_SW].value == false)
                    {
                        gClass.str.SrmPacket[i].gcpError = true;
                        gClass.str.SrmPacket[i].gcpErrorCode = 03;            // 지상반 에러코드 확인
                        gClass.str.SrmPacket[i].gcpSubCode = 01;              // 지상반 서브코드 확인
                        // FLT 03-01 : 비상정지 스위치 작동 (비상정지 스위치가 눌린 경우 발생)
                    }
                    else if (gClass.str.SrmPacket[i].jobError)      // 작업 SRM 송신 응답 에러 발생 시
                    {
                        gClass.str.SrmPacket[i].gcpError = true;
                    }
                    else
                    {
                            curErrorSt = false;
                            gClass.str.SrmPacket[i].gcpModemFltCnt = 2;
                    }
                }

                if (gClass.str.SrmPacket[i].gcpError)      // 에러 발생 시
                {
                    cIniAccess.ChangeJobState(i, JOBSTATE.STOP);
                    if (gClass.str.SrmState[i].dSt1StartSt > 0)
                    {
                        if (!gClass.str.SrmPacket[i].offlineErrorLogged)
                        {
                            cIniAccess.SaveJobLog(i, "GCP -> SRM == 에러발생 OFFLINE 전환요청");
                            gClass.str.SrmPacket[i].offlineErrorLogged = true;
                        }
                        gClass.str.SrmPacket[i].pulseClicked = true;
                        gClass.str.SrmPacket[i].startCmd = 1;
                        gClass.str.SrmPacket[i].startOnOff = 0;
                    }
                }

                if (!curErrorSt)                // 에러코드 해제 조건
                {
                    if (gClass.str.SrmPacket[i].gcpModemFlt)                // 광모뎀 에러는 자동 해제 하지 않는다.
                    {

                    }
                    else
                    {
                        gClass.str.SrmPacket[i].gcpError = false;
                        gClass.str.SrmPacket[i].gcpErrorCode = 00;            // 지상반 에러코드 확인
                        gClass.str.SrmPacket[i].gcpSubCode = 00;              // 지상반 서브코드 확인
                        gClass.str.SrmPacket[i].offlineErrorLogged = false;
                    }
                }
                else
                {
                    gClass.str.SrmPacket[i].offlineErrorLogged = false;
                }

                if (!curWarningSt)              // 경고코드 해제 조건
                {
                    gClass.str.SrmPacket[i].gcpWarning = false;
                    gClass.str.SrmPacket[i].gcpWarningCode = 00;            // 지상반 경고코드 확인
                    gClass.str.SrmPacket[i].gcpWarningSubCode = 00;         // 지상반 경고서브코드 확인
                }

                // For Test
                //gClass.str.SrmPacket[i].gcpError = true;
                //gClass.str.SrmPacket[i].gcpErrorCode = 03;            // 지상반 에러코드 확인
                //gClass.str.SrmPacket[i].gcpSubCode = 01;              // 지상반 서브코드 확인


                // 에러 또는 경고 코드 변경 체크 (에러 우선순위)
                bool errorChanged = (gClass.str.SrmPacket[i].gcpErrorCode != gClass.str.SrmPacket[i].oldErrCode) || (gClass.str.SrmPacket[i].gcpSubCode != gClass.str.SrmPacket[i].oldSubCode);
                bool warningChanged = (gClass.str.SrmPacket[i].gcpWarningCode != gClass.str.SrmPacket[i].oldWarningCode) || (gClass.str.SrmPacket[i].gcpWarningSubCode != gClass.str.SrmPacket[i].oldWarningSubCode);



                if (errorChanged || warningChanged)
                {
                    // 에러가 있으면 에러를 우선 표시 (빨간색)
                    if (gClass.str.SrmPacket[i].gcpErrorCode > 0)
                    {
                        string mainCode = gClass.str.SrmPacket[i].gcpErrorCode.ToString();
                        if (mainCode.Length < 2) mainCode = "0" + mainCode;
                        string subCode = gClass.str.SrmPacket[i].gcpSubCode.ToString();
                        if (subCode.Length < 2) subCode = "0" + subCode;

                        if (gClass.srmNum == i)             // 현재 호기와 에러 발생 호기가 동일할 경우
                        {
                            lbl_errCode.Foreground = Brushes.Red;
                            lbl_errCode.Content = mainCode + "-" + subCode;
                            ErrorText.Text = pageAlarmLog.getErrorText(mainCode, subCode);
                        }

                        if (gClass.str.SrmPacket[i].jobError)
                        {
                            cIniAccess.SaveJobLog(i, "GCP -> WCS == JOB 조건에러 발생");
                        }

                        cIniAccess.SaveJobLog(i, "GCP -> WCS == 에러발생 - " + mainCode + "-" + subCode);
                        pageAlarmLog.errorOccurred(i, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), mainCode, subCode, true);         // file save Flag = true
                    }
                    // 에러가 없고 경고가 있으면 경고 표시 (주황색)
                    else if (gClass.str.SrmPacket[i].gcpWarningCode > 0)
                    {
                        string mainCode = gClass.str.SrmPacket[i].gcpWarningCode.ToString();
                        if (mainCode.Length < 2) mainCode = "0" + mainCode;
                        string subCode = gClass.str.SrmPacket[i].gcpWarningSubCode.ToString();
                        if (subCode.Length < 2) subCode = "0" + subCode;

                        if (gClass.srmNum == i)             // 현재 호기와 경고 발생 호기가 동일할 경우
                        {
                            lbl_errCode.Foreground = Brushes.Orange;
                            lbl_errCode.Content = mainCode + "-" + subCode;
                            ErrorText.Text = pageAlarmLog.getWarningText(mainCode, subCode);
                        }

                        cIniAccess.SaveJobLog(i, "GCP -> WCS == 경고발생 - " + mainCode + "-" + subCode);
                        // 경고는 저장하지 않음
                        //pageAlarmLog.errorOccurred(i, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), mainCode, subCode, true);         // file save Flag = true
                    }
                    // 둘 다 없으면 정상 표시 (녹색)
                    else
                    {
                        if (gClass.srmNum == i)             // 현재 호기와 에러 발생 호기가 동일할 경우
                        {
                            lbl_errCode.Foreground = Brushes.Green;
                            lbl_errCode.Content = "00-00";
                            ErrorText.Text = String.Empty;
                        }
                    }

                    if (ErrorText.Text != String.Empty)
                    {
                        StartMarquee();     // 애니메이션 재시작
                    }
                }

                gClass.str.SrmPacket[i].oldErrCode = gClass.str.SrmPacket[i].gcpErrorCode;
                gClass.str.SrmPacket[i].oldSubCode = gClass.str.SrmPacket[i].gcpSubCode;
                gClass.str.SrmPacket[i].oldWarningCode = gClass.str.SrmPacket[i].gcpWarningCode;
                gClass.str.SrmPacket[i].oldWarningSubCode = gClass.str.SrmPacket[i].gcpWarningSubCode;

                // 작업요구비트 취소: tcpServerClass.UpdateJobRequestBitForTcp() 에서 일괄 처리 (TCP 송신 직전 호출)

                // 경광등 및 부저처리
                if (gClass.str.DioPacket[i].DO_TESTMODE == false)
                {
                    if (gClass.str.SrmPacket[i].gcpError == true)
                    {
                        if (!gClass.str.SrmPacket[i].buzzerStop)
                        {
                            gClass.str.DioPacket[i].DOSET[(int)DOSTATE.BUZZER].value = true;        // BUZZER
                        }
                        gClass.str.DioPacket[i].DOSET[(int)DOSTATE.RED].value = true;       // RED
                        gClass.str.DioPacket[i].DOSET[(int)DOSTATE.YELLOW].value = false;   // YELLOW
                        gClass.str.DioPacket[i].DOSET[(int)DOSTATE.GREEN].value = false;    // GREEN
                        gClass.str.DioPacket[i].DOSET[(int)DOSTATE.BLUE].value = false;     // BLUE
                        gClass.str.DioPacket[i].DOSET[(int)DOSTATE.WHITE].value = false;    // WHITE
                    }
                    else if (gClass.str.SrmPacket[i].gcpWarning == true)
                    {
                        if (!gClass.str.SrmPacket[i].buzzerStop)
                        {
                            gClass.str.DioPacket[i].DOSET[(int)DOSTATE.BUZZER].value = true;        // BUZZER
                        }
                        gClass.str.DioPacket[i].DOSET[(int)DOSTATE.RED].value = false;      // RED
                        gClass.str.DioPacket[i].DOSET[(int)DOSTATE.YELLOW].value = true;   // YELLOW
                        gClass.str.DioPacket[i].DOSET[(int)DOSTATE.GREEN].value = false;    // GREEN
                        gClass.str.DioPacket[i].DOSET[(int)DOSTATE.BLUE].value = false;      // BLUE
                        gClass.str.DioPacket[i].DOSET[(int)DOSTATE.WHITE].value = false;    // WHITE
                    }
                    else
                    {
                        if (gClass.str.DioPacket[i].DISET[(int)DISTATE.MANUAL].value == true)
                        {
                            gClass.str.DioPacket[i].DOSET[(int)DOSTATE.RED].value = false;      // RED
                            gClass.str.DioPacket[i].DOSET[(int)DOSTATE.YELLOW].value = false;   // YELLOW
                            gClass.str.DioPacket[i].DOSET[(int)DOSTATE.GREEN].value = false;    // GREEN
                            gClass.str.DioPacket[i].DOSET[(int)DOSTATE.WHITE].value = false;    // WHITE
                            if (gClass.str.SrmPacket[i].operState)      // 동작 중 점멸
                            {
                                if (gClass.str.DioPacket[i].DOSET[(int)DOSTATE.BLUE].value == true)
                                {
                                    gClass.str.DioPacket[i].DOSET[(int)DOSTATE.BLUE].value = false;      // BLUE
                                }
                                else
                                {
                                    gClass.str.DioPacket[i].DOSET[(int)DOSTATE.BLUE].value = true;      // BLUE
                                }
                            }
                            else
                            {
                                gClass.str.DioPacket[i].DOSET[(int)DOSTATE.BLUE].value = true;      // BLUE
                            }
                        }
                        else if (gClass.str.DioPacket[i].DISET[(int)DISTATE.SEMI_AUTO].value == true || gClass.str.DioPacket[i].DISET[(int)DISTATE.AUTO].value == true)
                        {
                            if (gClass.str.SrmPacket[i].startEnable)
                            {
                                gClass.str.DioPacket[i].DOSET[(int)DOSTATE.RED].value = false;      // RED
                                gClass.str.DioPacket[i].DOSET[(int)DOSTATE.YELLOW].value = false;   // YELLOW
                                if (gClass.str.DioPacket[i].DOSET[(int)DOSTATE.GREEN].value == true)            // 시작가능 시 초록점멸
                                {
                                    gClass.str.DioPacket[i].DOSET[(int)DOSTATE.GREEN].value = false;    // GREEN
                                }
                                else
                                {
                                    gClass.str.DioPacket[i].DOSET[(int)DOSTATE.GREEN].value = true;    // GREEN
                                }
                                gClass.str.DioPacket[i].DOSET[(int)DOSTATE.BLUE].value = true;      // BLUE
                                gClass.str.DioPacket[i].DOSET[(int)DOSTATE.WHITE].value = false;    // WHITE
                            }
                            else if (gClass.str.SrmState[i].dSt1StartSt > 0)
                            {
                                gClass.str.DioPacket[i].DOSET[(int)DOSTATE.RED].value = false;      // RED
                                gClass.str.DioPacket[i].DOSET[(int)DOSTATE.YELLOW].value = false;   // YELLOW
                                gClass.str.DioPacket[i].DOSET[(int)DOSTATE.BLUE].value = false;      // BLUE
                                gClass.str.DioPacket[i].DOSET[(int)DOSTATE.WHITE].value = false;    // WHITE
                                if (gClass.str.SrmPacket[i].operState)      // 동작 중 점멸
                                {
                                    if (gClass.str.DioPacket[i].DOSET[(int)DOSTATE.GREEN].value == true)
                                    {
                                        gClass.str.DioPacket[i].DOSET[(int)DOSTATE.GREEN].value = false;      // GREEN
                                    }
                                    else
                                    {
                                        gClass.str.DioPacket[i].DOSET[(int)DOSTATE.GREEN].value = true;      // GREEN
                                    }
                                }
                                else
                                {
                                    gClass.str.DioPacket[i].DOSET[(int)DOSTATE.GREEN].value = true;      // GREEN
                                }
                            }
                            else
                            {
                                gClass.str.DioPacket[i].DOSET[(int)DOSTATE.RED].value = false;      // RED
                                gClass.str.DioPacket[i].DOSET[(int)DOSTATE.YELLOW].value = false;   // YELLOW
                                gClass.str.DioPacket[i].DOSET[(int)DOSTATE.BLUE].value = true;      // BLUE
                                gClass.str.DioPacket[i].DOSET[(int)DOSTATE.GREEN].value = true;    // GREEN
                                gClass.str.DioPacket[i].DOSET[(int)DOSTATE.WHITE].value = false;    // WHITE
                            }
                            gClass.str.DioPacket[i].DOSET[(int)DOSTATE.RED].value = false;             // RED
                        }
                        else
                        {
                            gClass.str.DioPacket[i].DOSET[(int)DOSTATE.RED].value = false;      // RED
                            gClass.str.DioPacket[i].DOSET[(int)DOSTATE.YELLOW].value = true;   // YELLOW
                            gClass.str.DioPacket[i].DOSET[(int)DOSTATE.BLUE].value = false;      // BLUE
                            gClass.str.DioPacket[i].DOSET[(int)DOSTATE.GREEN].value = false;    // GREEN
                            gClass.str.DioPacket[i].DOSET[(int)DOSTATE.WHITE].value = false;    // WHITE
                        }

                        gClass.str.DioPacket[i].DOSET[(int)DOSTATE.BUZZER].value = false;             // BUZZER
                        gClass.str.SrmPacket[i].buzzerStop = false;        // 부저정지 플래그 복구
                    }
                }
            }
        }


        // 기상반 /  지상반 모드 확인 및 페이지 전환 용도
        private int modeCheck()
        {

            int result = 0;
            if (gClass.str.SrmState[gClass.srmNum].dSt2ManAutoSw > 0)        // 크레인 키 스위치 수동
            {
                // 크레인 키 수동 시 지상반 제어 불가처리
                result = 99;
            }
            else
            {
                if (gClass.str.DioPacket[gClass.srmNum].DISET[(int)DISTATE.SEMI_AUTO].value == true)              // 지상반 키 반자동   반자동 키 없으면 MANUAL에서 진입하지만. 이 조건을 먼저보기 때문에 상관없음
                {
                    if ((gClass.str.SrmState[gClass.srmNum].manualMode) > 0)                  // 기상반 모드 수동
                    {
                        gClass.str.SrmPacket[gClass.srmNum].modeSetCmd = 2;
                        gClass.str.SrmPacket[gClass.srmNum].modeSetOpt = 0;
                        gClass.str.SrmPacket[gClass.srmNum].modeSetReq = true;              // 키모드 수동 & 기상반 자동이면 -> 기상반 모드 자동전환 요청
                        result = 3;
                    }
                    else if ((gClass.str.SrmState[gClass.srmNum].autoMode) > 0)             // 기상반 모드 자동
                    {
                        result = 4;
                    }
                    else
                    {
                        gClass.str.SrmPacket[gClass.srmNum].modeSetCmd = 2;
                        gClass.str.SrmPacket[gClass.srmNum].modeSetOpt = 0;
                        gClass.str.SrmPacket[gClass.srmNum].modeSetReq = true;              // 키모드 수동 & 기상반 자동이면 -> 기상반 모드 자동전환 요청
                        // 강제모드 / 셋업모드 / Else
                        result = 97;
                    }
                }
                else if (gClass.str.DioPacket[gClass.srmNum].DISET[(int)DISTATE.MANUAL].value == true)                 // 지상반 키 수동
                {
                    if ((gClass.str.SrmState[gClass.srmNum].manualMode) > 0)                  // 기상반 모드 수동
                    {
                        result = 1;
                    }
                    else if ((gClass.str.SrmState[gClass.srmNum].autoMode) > 0)             // 기상반 모드 자동
                    {
                        if ((gClass.str.SrmState[gClass.srmNum].dSt1StartSt) > 0)                            // 장비 상태 1 - Bit0   OFF상태
                        {
                            gClass.str.SrmPacket[gClass.srmNum].startCmd = 1;          // START CMD 시작
                            gClass.str.SrmPacket[gClass.srmNum].startOnOff = 0;        // TX SubItem = START OFF
                        }

                        gClass.str.SrmPacket[gClass.srmNum].modeSetCmd = 0;
                        gClass.str.SrmPacket[gClass.srmNum].modeSetOpt = 0;
                        gClass.str.SrmPacket[gClass.srmNum].modeSetReq = true;              // 키모드 수동 & 기상반 자동이면 -> 기상반 모드 수동전환 요청
                        result = 2;
                    }
                    else
                    {
                        // 강제모드 / 셋업모드 / Else
                        result = 97;
                    }
                }
                else if (gClass.str.DioPacket[gClass.srmNum].DISET[(int)DISTATE.AUTO].value == true)              // 지상반 키 자동
                {
                    if ((gClass.str.SrmState[gClass.srmNum].manualMode) > 0)                  // 기상반 모드 수동
                    {
                        gClass.str.SrmPacket[gClass.srmNum].modeSetCmd = 2;
                        gClass.str.SrmPacket[gClass.srmNum].modeSetOpt = 0;
                        gClass.str.SrmPacket[gClass.srmNum].modeSetReq = true;              // 키모드 수동 & 기상반 자동이면 -> 기상반 모드 자동전환 요청
                        result = 5;
                    }
                    else if ((gClass.str.SrmState[gClass.srmNum].autoMode) > 0)             // 기상반 모드 자동
                    {
                        result = 6;
                    }
                    else
                    {
                        gClass.str.SrmPacket[gClass.srmNum].modeSetCmd = 2;
                        gClass.str.SrmPacket[gClass.srmNum].modeSetOpt = 0;
                        gClass.str.SrmPacket[gClass.srmNum].modeSetReq = true;              // 키모드 수동 & 기상반 자동이면 -> 기상반 모드 자동전환 요청
                        // 강제모드 / 셋업모드 / Else
                        result = 97;
                    }
                }
                else if (gClass.str.DioPacket[gClass.srmNum].DISET[(int)DISTATE.MAINT].value == true)              // 지상반 키 MAINT
                {
                    if ((gClass.str.SrmState[gClass.srmNum].manualMode) > 0)                  // 기상반 모드 수동
                    {
                        //gClass.str.SrmPacket[gClass.srmNum].modeSetCmd = 1;
                        //gClass.str.SrmPacket[gClass.srmNum].modeSetOpt = 0;
                        //gClass.str.SrmPacket[gClass.srmNum].modeSetReq = true;              // 키모드 MAINT & 기상반 수동이면 -> 기상반 모드 셋업모드 요청
                        result = 95;
                    }
                    else if ((gClass.str.SrmState[gClass.srmNum].autoMode) > 0)             // 기상반 모드 자동
                    {
                        gClass.str.SrmPacket[gClass.srmNum].modeSetCmd = 0;
                        gClass.str.SrmPacket[gClass.srmNum].modeSetOpt = 0;
                        gClass.str.SrmPacket[gClass.srmNum].modeSetReq = true;              // 키모드 MAINT & 기상반 자동이면 -> 기상반 모드 수동모드 요청
                        result = 95;
                    }
                    else
                    {
                        // 강제모드 / 셋업모드 / Else
                        result = 95;
                    }
                }
                else
                {
                    // to do 지상반 셀렉트 스위치 이상 점검 필요
                    //result = 96;
                    result = 98;
                }
            }
            //Console.WriteLine("Current PAGEMODE : " + result);
            return result;
        }


        private void pageCheck(int mode)
        {
            // 지상반 모드 변경 체크   // 수동 1,  반자동 2,  자동 3
            int tmpMode = (gClass.str.DioPacket[gClass.srmNum].DISET[(int)DISTATE.MANUAL].value ? 1 : 0) + (gClass.str.DioPacket[gClass.srmNum].DISET[(int)DISTATE.SEMI_AUTO].value ? 2 : 0) + (gClass.str.DioPacket[gClass.srmNum].DISET[(int)DISTATE.AUTO].value ? 3 : 0);

            if (gClass.str.DioPacket[gClass.srmNum].DISET[(int)DISTATE.MANUAL].value && gClass.str.DioPacket[gClass.srmNum].DISET[(int)DISTATE.SEMI_AUTO].value)
            {
                // 둘다 true인 경우라면, SEMI MODE 켜진거임
                tmpMode = 2;  // Semi라고 알려야함
            }

            string blockContent = "";

            switch (mode)
            {
                case 1:     // 지상반 수동 && 기상반 수동
                    if (gcpMode != tmpMode)                  // 지상반 키 변경으로 모드 변경 시 페이지 자동변경
                    {
                        controlMode = mode;         // 변경 모드 저장
                        gcpMode = tmpMode;          // 지상반 키 모드 변경 상태에 따라 페이지 처리 필요
                        Page_Change(1);             // 수동 페이지 전환
                    }
                    if (curPageIdx == cConstDefine.PAGE_AUTO)              // 제어페이지 AND 기상반 수동 = 권한불가 표시
                    {
                        blockContent = tr("지상반 키 수동 상태입니다");
                        //blockContent = "지상반 키 수동 상태입니다";
                        Canvas.SetZIndex(lbl_blockMode, 2);     // 화면 블럭 막으려면 주석 처리
                    }
                    else
                    {
                        blockContent = "";                                                         // 그 외 페이지 제어 가능
                        Canvas.SetZIndex(lbl_blockMode, 0);
                    }
                    break;
                case 2:     // 지상반 수동 && 기상반 자동
                    if (gcpMode != tmpMode)                                 // 지상반 키 변경으로 모드 변경 시 페이지 변경
                    {
                        controlMode = mode;         // 변경 모드 저장
                        gcpMode = tmpMode;          // 지상반 키 모드 변경 상태에 따라 페이지 처리 필요
                        Page_Change(1);
                    }
                    if (curPageIdx == cConstDefine.PAGE_MANUAL)                                                     // 수동 페이지 제어 가능
                    {
                        blockContent = tr("기상반 자동모드 입니다");
                        Canvas.SetZIndex(lbl_blockMode, 2);     // 화면 블럭 막으려면 주석 처리
                    }
                    else if (curPageIdx == cConstDefine.PAGE_AUTO || curPageIdx == cConstDefine.PAGE_SEMI)              // 제어페이지 AND 기상반 수동 = 권한불가 표시
                    {
                        blockContent = tr("지상반 키 수동 상태입니다");
                        Canvas.SetZIndex(lbl_blockMode, 2);
                    }
                    else
                    {
                        blockContent = "";                                                         // 그 외 페이지 제어 가능
                        Canvas.SetZIndex(lbl_blockMode, 0);
                    }
                    break;
                case 3:     // 지상반 반자동 && 기상반 수동
                    if (gcpMode != tmpMode)                  // 지상반 키 변경으로 모드 변경 진입 시 페이지 자동변경
                    {
                        controlMode = mode;         // 변경 모드 저장
                        gcpMode = tmpMode;          // 지상반 키 모드 변경 상태에 따라 페이지 처리 필요
                        Page_Change(2);
                    }
                    if (curPageIdx == cConstDefine.PAGE_MANUAL || curPageIdx == cConstDefine.PAGE_AUTO)      // 수동 페이지 제어 가능
                    {
                        blockContent = tr("지상반 키 반자동 상태입니다");
                        Canvas.SetZIndex(lbl_blockMode, 2);     // 화면 블럭 막으려면 주석 처리
                    }
                    else if (curPageIdx == cConstDefine.PAGE_SEMI)              // 제어페이지 AND 기상반 수동 = 권한불가 표시
                    {
                        blockContent = tr("기상반 수동모드 입니다");
                        Canvas.SetZIndex(lbl_blockMode, 2);     // 화면 블럭 막으려면 주석 처리
                    }
                    else
                    {
                        blockContent = "";                                                         // 그 외 페이지 제어 가능
                        Canvas.SetZIndex(lbl_blockMode, 0);
                    }
                    break;
                case 4:     // 지상반 반자동 && 기상반 자동
                    if (gcpMode != tmpMode)                                 // 지상반 키 변경으로 모드 변경 시 페이지 변경
                    {
                        controlMode = mode;         // 변경 모드 저장
                        gcpMode = tmpMode;          // 지상반 키 모드 변경 상태에 따라 페이지 처리 필요
                        Page_Change(2);
                    }
                    if (curPageIdx == cConstDefine.PAGE_MANUAL || curPageIdx == cConstDefine.PAGE_AUTO)                                                     // 수동 페이지 제어 가능
                    {
                        blockContent = tr("지상반 키 반자동 상태입니다");
                        Canvas.SetZIndex(lbl_blockMode, 2);     // 화면 블럭 막으려면 주석 처리
                    }
                    else
                    {
                        blockContent = "";                                                         // 그 외 페이지 제어 가능
                        Canvas.SetZIndex(lbl_blockMode, 0);
                    }
                    break;
                case 5:     // 지상반 자동 && 기상반 수동
                    if (gcpMode != tmpMode)                  // 지상반 키 변경으로 모드 변경 시 페이지 자동변경
                    {
                        controlMode = mode;         // 변경 모드 저장
                        gcpMode = tmpMode;          // 지상반 키 모드 변경 상태에 따라 페이지 처리 필요
                        Page_Change(0);
                    }
                    if (curPageIdx == cConstDefine.PAGE_MANUAL || curPageIdx == cConstDefine.PAGE_SEMI)      // 수동 페이지 제어 가능
                    {
                        blockContent = tr("지상반 키 자동 상태입니다");
                        Canvas.SetZIndex(lbl_blockMode, 2);     // 화면 블럭 막으려면 주석 처리
                    }
                    else if (curPageIdx == cConstDefine.PAGE_AUTO)              // 제어페이지 AND 기상반 수동 = 권한불가 표시
                    {
                        blockContent = tr("기상반 수동모드 입니다");
                        Canvas.SetZIndex(lbl_blockMode, 2);     // 화면 블럭 막으려면 주석 처리
                    }
                    else
                    {
                        blockContent = "";                                                         // 그 외 페이지 제어 가능
                        Canvas.SetZIndex(lbl_blockMode, 0);
                    }
                    break;
                case 6:     // 지상반 자동 && 기상반 자동
                    if (gcpMode != tmpMode)                                 // 지상반 키 변경으로 모드 변경 시 페이지 변경
                    {
                        controlMode = mode;         // 변경 모드 저장
                        gcpMode = tmpMode;          // 지상반 키 모드 변경 상태에 따라 페이지 처리 필요
                        Page_Change(0);
                    }
                    if (curPageIdx == cConstDefine.PAGE_MANUAL || curPageIdx == cConstDefine.PAGE_SEMI)      // 수동 페이지 제어 가능
                    {
                        blockContent = tr("지상반 키 자동 상태입니다");
                        Canvas.SetZIndex(lbl_blockMode, 2);     // 화면 블럭 막으려면 주석 처리
                    }
                    else
                    {
                        blockContent = "";                                                         // 그 외 페이지 제어 가능
                        Canvas.SetZIndex(lbl_blockMode, 0); ;
                    }
                    break;
                case 95:    // 지상반 키모드 MAINT 상태
                    if (curPageIdx == cConstDefine.PAGE_MANUAL || curPageIdx == cConstDefine.PAGE_AUTO || curPageIdx == cConstDefine.PAGE_SEMI)      // 그 외 페이지 제어 가능
                    {
                        blockContent = tr("지상반 보수모드 상태입니다");
                        Canvas.SetZIndex(lbl_blockMode, 2);     // 화면 블럭 막으려면 주석 처리
                    }
                    else
                    {
                        blockContent = "";                                                         // 그 외 페이지 제어 가능
                        Canvas.SetZIndex(lbl_blockMode, 0); ;
                    }
                    break;
                case 96:    // 테스트모드
                    blockContent = "TEST MODE";
                    Canvas.SetZIndex(lbl_blockMode, -1);     // 화면 블럭 막으려면 주석 처리
                    //Canvas.SetZIndex(lbl_blockMode, 2);     // 화면 블럭 막으려면 주석 처리
                    break;
                case 97:    // 강제모드 or 셋업모드
                    if (curPageIdx == cConstDefine.PAGE_MANUAL || curPageIdx == cConstDefine.PAGE_AUTO || curPageIdx == cConstDefine.PAGE_SEMI)      // 그 외 페이지 제어 가능
                    {
                        //blockContent = tr("기상반 셋업/강제모드 입니다");
                        //Canvas.SetZIndex(lbl_blockMode, 2);     // 화면 블럭 막으려면 주석 처리
                    }
                    else
                    {
                        blockContent = "";                                                         // 그 외 페이지 제어 가능
                        Canvas.SetZIndex(lbl_blockMode, 0); ;
                    }
                    break;
                case 98:    // 지상반 키 값 이상
                    if (curPageIdx == cConstDefine.PAGE_MANUAL || curPageIdx == cConstDefine.PAGE_AUTO || curPageIdx == cConstDefine.PAGE_SEMI)      // 그 외 페이지 제어 가능
                    {
                        blockContent = tr("지상반 키 모드확인");
                        Canvas.SetZIndex(lbl_blockMode, 2);     // 화면 블럭 막으려면 주석 처리
                    }
                    else
                    {
                        blockContent = "";                                                         // 그 외 페이지 제어 가능
                        Canvas.SetZIndex(lbl_blockMode, 0); ;
                    }
                    break;
                case 99:    // 기상반 키모드 수동상태
                    if (curPageIdx == cConstDefine.PAGE_MANUAL || curPageIdx == cConstDefine.PAGE_AUTO || curPageIdx == cConstDefine.PAGE_SEMI)      // 그 외 페이지 제어 가능
                    {
                        blockContent = tr("기상반 키 수동 상태입니다");
                        Canvas.SetZIndex(lbl_blockMode, 2);     // 화면 블럭 막으려면 주석 처리
                    }
                    else
                    {
                        blockContent = "";                                                         // 그 외 페이지 제어 가능
                        Canvas.SetZIndex(lbl_blockMode, 0); ;
                    }
                    break;
                    // to do 원점확인 블록페이지 로직 추가
            }

            if (gClass.str.SrmPacket[gClass.srmNum].srmCommDiscCnt <= 0 && (curPageIdx == cConstDefine.PAGE_MANUAL || curPageIdx == cConstDefine.PAGE_AUTO || curPageIdx == cConstDefine.PAGE_SEMI))       // 크레인 통신 접속상태
            {
                //blockContent = tr("SRM 통신상태 확인");//  tr("SRM 통신상태 확인");

               //Canvas.SetZIndex(lbl_blockMode, 2);     // 화면 블럭 막으려면 주석 처리
                //Canvas.SetZIndex(lbl_blockMode, 0);        // For Test
            }
            else
            {
                if (blockContent == "")
                {
                    Canvas.SetZIndex(lbl_blockMode, 0);
                }
            }

            lbl_blockMode.Content = blockContent;

            // --------------------------------------

            controlMode = mode;         // 변경 모드 저장
            gcpMode = tmpMode;          // 지상반 키 모드 변경 상태에 따라 페이지 처리 필요

            // to do  통합지상반에서는 Key Input을 각각 받아서 모드 저장 해주어야 함 (하드웨어 별도 구성 필요)
            gClass.str.SrmState[gClass.srmNum].gcpState.gcpTxMode = (byte)gcpMode;    //gcpTxMode Manual = 1, Semi = 2, Online = 3  231126 Vexi Protocol Rev1
        }


        public void Page_Change(int pageIdx)
        {
            Dispatcher.Invoke(() =>
            {
                Console.WriteLine("Called Page Change " + pageIdx);

                // 오토티칭 런 중 다른 페이지로 전환 시 런 취소 + 크레인 무장 해제
                if (curPageIdx == cConstDefine.PAGE_AUTOTEACHING && pageIdx != cConstDefine.PAGE_AUTOTEACHING)
                {
                    try { pageAutoTeaching?.AbortAndDisarmForShutdown(); }
                    catch (Exception ex) { Console.WriteLine($"오토티칭 런 취소 오류: {ex.Message}"); }
                }

                if (Btn_Main.IsChecked == true)
                {
                    Btn_Main.IsChecked = false;
                }
                if (Btn_Auto.IsChecked == true)
                {
                    Btn_Auto.IsChecked = false;
                }
                if (Btn_Manual.IsChecked == true)
                {
                    Btn_Manual.IsChecked = false;
                }
                if (Btn_SemiAuto.IsChecked == true)
                {
                    Btn_SemiAuto.IsChecked = false;
                }
                if (btn_test1.IsChecked == true)
                {
                    btn_test1.IsChecked = false;
                }
                if (Btn_AutoTeaching.IsChecked == true)
                {
                    Btn_AutoTeaching.IsChecked = false;
                }
                //if(Bg_Page.Content != null)
                //{
                //    Application.Current.Dispatcher.Invoke(() =>
                //    {
                //        Bg_Page.Navigate(null);                             // 이동 애니메이션 배경 사용 시 Auto Page 표시 유무
                //    });
                //}
                Bg_Page.Content = null;
                //pageDevList[gClass.srmNum].StartBtn_Enable(false);
                switch (pageIdx)
                {
                    case cConstDefine.PAGE_AUTO:
                        //Frm_Page.Source = new Uri("PageAuto.xaml", UriKind.Relative);
                        pageAuto.PageBlockDisplay(false);
                        //pageDevList[gClass.srmNum].StartBtn_Enable(true);
                        Frm_Page.Content = pageAuto;
                        //Bg_Page.Content = null;
                        Btn_Auto.IsChecked = true;
                        break;
                    case cConstDefine.PAGE_MANUAL:
                        pageAuto.PageBlockDisplay(true);
                        Frm_Page.Content = pageManual;
                        Bg_Page.Content = pageAuto;
                        Btn_Manual.IsChecked = true;
                        break;
                    case cConstDefine.PAGE_SEMI:
                        pageAuto.PageBlockDisplay(true);
                        Frm_Page.Content = pageSemiAuto;
                        Bg_Page.Content = pageAuto;
                        Btn_SemiAuto.IsChecked = true;
                        break;
                    case cConstDefine.PAGE_PROHRACK:
                        Frm_Page.Content = pageProhRack;
                        break;
                    case cConstDefine.PAGE_COMMSET:
                        Frm_Page.Content = pageCommSet;
                        break;
                    case cConstDefine.PAGE_SRMSET:
                        pageCraneSet.SetPageInit();         // 페이지 데이터 초기화 함수
                        Frm_Page.Content = pageCraneSet;
                        break;
                    case cConstDefine.PAGE_STATION:
                        pageStationSet.resetStationList();
                        Frm_Page.Content = pageStationSet;
                        break;
                    case cConstDefine.PAGE_ALARM:
                        Frm_Page.Content = pageAlarmLog;
                        break;
                    case cConstDefine.PAGE_MAIN:
                        pageAuto.PageBlockDisplay(true);
                        Frm_Page.Content = pageMain;
                        Bg_Page.Content = pageAuto;
                        Btn_Main.IsChecked = true;
                        break;
                    case cConstDefine.PAGE_DIO:
                        pageDio.Dio_Change();
                        Frm_Page.Content = pageDio;
                        break;
                    case cConstDefine.PAGE_TOWCS:
                        Frm_Page.Content = pageToWcs;
                        break;
                    case cConstDefine.PAGE_FROMWCS:
                        Frm_Page.Content = pageFromWcs;
                        break;
                    case cConstDefine.PAGE_SRMIO:
                        Frm_Page.Content = pageMonitorSrm;
                        break;
                    case cConstDefine.PAGE_CRANE_OPERATE:
                        Frm_Page.Content = pageCraneOperate;
                        break;
                    case cConstDefine.PAGE_AUTOTEACHING:
                        pageAutoTeaching.PageInit();
                        Frm_Page.Content = pageAutoTeaching;
                        Btn_AutoTeaching.IsChecked = true;
                        break;
                }

                curPageIdx = pageIdx;

                pageCheck(controlMode);             // 페이지 변경 후 모드에 따른 처리 ---- 페이지가 바로 바뀌지 않아 pageIdx를 사용

            });
        }

        private bool ConfirmExit()
        {
            cIniAccess.SaveJobLog(0, "GCP == 프로그램 종료버튼 클릭");

            VarMessageBoxResult result = VarMessageBox.Show(
                cConstDefine.tr("종료"),
                cConstDefine.tr("프로그램을 종료하시겠습니까?"),
                VarMessageBoxButton.YesNo);

            return result == VarMessageBoxResult.Yes;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            if (ConfirmExit())
            {
                isExitConfirmed = true;
                this.Close();  // 또는 Environment.Exit(0);
            }
        }

        private void Mode_Click(object sender, RoutedEventArgs e)
        {
            // Handle the button click event here
            ToggleButton toggleButton = sender as ToggleButton;

            if (toggleButton.IsChecked == true)
            {
                if (toggleButton == Btn_Auto)
                {
                    //                    // Button Mode is Auto
                    //                    if (gClass.str.DioPacket[gClass.srmNum].DIBIT[3] == false)       // 지상반 키 Online 아닐 경우
                    //                    {
                    //                        VarMessageBox.Show(tr("지상반 키 모드확인", TranslationSource.Instance.CurrentCulture), tr("지상반 키 Online 상태가 아닙니다", TranslationSource.Instance.CurrentCulture), 2);
                    //                        //MessageBox.Show(tr("지상반 키 Online 상태가 아닙니다", TranslationSource.Instance.CurrentCulture), tr("지상반 키 모드확인", TranslationSource.Instance.CurrentCulture),
                    //                        //MessageBoxButton.OK, MessageBoxImage.Information);

                    //#if NORMALMODE
                    //                        toggleButton.IsChecked = false;           
                    //                        return;
                    //#endif
                    //                    }
                    Page_Change(0);
                }
                else if (toggleButton == Btn_Manual)
                {
                    // Button Mode is Manual
                    Page_Change(1);
                }
                else if (toggleButton == Btn_SemiAuto)
                {
                    // Button Mode is SemiAuto
                    pageSemiAuto.Page_Init();
                    Page_Change(2);
                }
                else if (toggleButton == Btn_AutoTeaching)
                {
                    Page_Change(cConstDefine.PAGE_AUTOTEACHING);
                }
                else if (toggleButton == btn_test1)
                {
                    // Button Mode is Test1
                    Page_Change(cConstDefine.PAGE_CRANE_OPERATE);
                }
                else
                {
                    // Button Mode is Main
                    Page_Change(8);
                }
            }
            else
            {
                toggleButton.IsChecked = true;
            }


            Console.WriteLine("Get Mode Button Click Event");
        }


        public void RevealAutoTeachingTab()
        {
            Dispatcher.Invoke(() => { Btn_AutoTeaching.Visibility = Visibility.Visible; });
        }

        public void HideAutoTeachingTab()
        {
            // 정책 변경(2026-06-30): 오토티칭 탭을 로그인과 무관하게 항상 노출.
            // 더 이상 admin 해제/타임아웃 시 탭을 숨기거나 페이지를 이탈시키지 않는다.
            // (메서드는 호출부 호환을 위해 남겨두되 동작 없음.)
        }

        private void SRM_COM_Click(object sender, RoutedEventArgs e)
        {
            SrmLogWindowChanged();
        }

        private void SrmLogWindowChanged()
        {
            win_SrmLog1?.Hide();
            win_SrmLog2?.Hide();
            win_SrmLog3?.Hide();
            switch (gClass.srmNum)
            {
                case 0:
                    win_SrmLog1?.Show();
                    win_SrmLog1!.WindowState = WindowState.Normal;
                    break;
                case 1:
                    win_SrmLog2?.Show();
                    win_SrmLog2!.WindowState = WindowState.Normal;
                    break;
                case 2:
                    win_SrmLog3?.Show();
                    win_SrmLog3!.WindowState = WindowState.Normal;
                    break;
            }
        }

        private void DIO_COM_Click(object sender, RoutedEventArgs e)
        {
            DioLogWindowChanged();
        }

        private void DioLogWindowChanged()
        {
            win_DioLog1?.Hide();
            win_DioLog2?.Hide();
            win_DioLog3?.Hide();
            switch (gClass.srmNum)
            {
                case 0:
                    win_DioLog1?.Show();
                    win_DioLog1!.WindowState = WindowState.Normal;
                    break;
                case 1:
                    win_DioLog2?.Show();
                    win_DioLog2!.WindowState = WindowState.Normal;
                    break;
                case 2:
                    win_DioLog3?.Show();
                    win_DioLog3!.WindowState = WindowState.Normal;
                    break;
            }
        }

        private void WCS_COM_Click(object sender, RoutedEventArgs e)
        {
            HostLogWindowChanged();
        }

        private void HostLogWindowChanged()
        {
            win_HostLog1?.Hide();
            win_HostLog2?.Hide();
            win_HostLog3?.Hide();
            switch (gClass.srmNum)
            {
                case 0:
                    win_HostLog1?.Show();
                    win_HostLog1!.WindowState = WindowState.Normal;
                    break;
                case 1:
                    win_HostLog2?.Show();
                    win_HostLog2!.WindowState = WindowState.Normal;
                    break;
                case 2:
                    win_HostLog3?.Show();
                    win_HostLog3!.WindowState = WindowState.Normal;
                    break;
            }
        }
        private void OP_LOG_Click(object sender, RoutedEventArgs e)
        {
            OperationLogWindowChanged();
        }

        private void OperationLogWindowChanged()
        {
            win_JobLog?.Hide();
            // JOB 로그표시로 변경 
            win_JobLog?.Show();
            win_JobLog!.WindowState = WindowState.Normal;
            //win_OpLog.Show();
        }


        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // X 버튼으로 닫을 때도 종료 버튼과 동일하게 확인창을 띄우기 위한 처리
            if (!isExitConfirmed)
            {
                if (!ConfirmExit())
                {
                    e.Cancel = true;
                    return;
                }

                isExitConfirmed = true;
            }

            cIniAccess.SaveJobLog(0, "GCP == 프로그램 종료시작");

            myTimer.Stop();
            adminModeTimer.Stop();   // 종료 중 1초 주기로 Dispatcher.Invoke 시도 방지 (정리 누락 보완)

            // WindowSrmLog 안전한 정리
            try
            {
                win_HostLog1?.Finished();
                win_HostLog2?.Finished();
                win_HostLog3?.Finished();

                win_SrmLog1?.Finished();
                win_SrmLog2?.Finished();
                win_SrmLog3?.Finished();

                win_DioLog1?.Finished();
                win_DioLog2?.Finished();
                win_DioLog3?.Finished();

                win_JobLog?.Finished();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WindowSrmLog 정리 중 오류: {ex.Message}");
            }

            // ★ 종료 전: 진행 중 오토티칭 무장 해제 — 통신 Close 전에 Start OFF(0x50) 플래그를 세팅하고
            //   UDP 스레드가 실제 전송할 시간을 준 뒤 Close. (안 그러면 크레인이 자동+Start ON으로 잔류해 위험)
            try
            {
                pageAutoTeaching?.AbortAndDisarmForShutdown();
                System.Threading.Thread.Sleep(500);   // sendTimer/수신루프가 0x50 OFF를 실제 송신할 시간(통신 Close 전)
            }
            catch (Exception ex) { Console.WriteLine($"종료 무장해제 오류: {ex.Message}"); }

            for (int i = 0; i < craneCnt; i++)
            {
                if (srmComm[i] != null)
                {
                    srmComm[i].Close();
                }

                if (hostComm[i] != null)
                {
                    hostComm[i].Close();
                }


                // 중간에 설정을 바꿀경우 재시작 조건으로 스레드 생성될 수 있으므로, 둘다 종료되도록 처리
                if (dioComm[i] != null)
                {
                    dioComm[i].Close();
                }

                if (rtuComm[i] != null)
                {
                    rtuComm[i].Close();
                }

            }
            Console.WriteLine("MainWindow_Closing");

            Application.Current.Shutdown();

            cIniAccess.watchDogCnt = 0;     // 워치독 초기화

            //IntPtr hwnd = GetConsoleWindow();
            //if (hwnd != IntPtr.Zero)
            //{
            //    // Hide the console window
            //    ShowWindow(hwnd, 0);
            //}
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            //FreeConsole();
        }

        private void btn_log_Click(object sender, RoutedEventArgs e)
        {
            Page_Change(7);
        }

        private void Logo_Click(object sender, MouseButtonEventArgs e)
        {
            // 관리자 모드일 때는 바로 해제
            if (gClass.str.GcpInfo.isAdminMode)
            {
                gClass.str.GcpInfo.isAdminMode = false;
                adminModeTimer.Stop();

                // 관리자 모드 보더 숨김 및 애니메이션 중지
                Dispatcher.Invoke(() =>
                {
                    Brd_AdminMode.Visibility = Visibility.Collapsed;
                    Grid_LogoNormal.Visibility = Visibility.Visible;
                    StopAdminModeBlinkAnimation();
                });

                // Update Pages based on Admin Mode
                pageCommSet.SetPageMode(false);
                pageCraneSet.SetPageMode(false);
                pageDio.SetPageMode(false);
                HideAutoTeachingTab();
                return;
            }

            // 일반 모드일 때는 로그인 창 표시
            bool? result = new WindowLogin().ShowDialog();
            if (result.GetValueOrDefault())
            {
                gClass.str.GcpInfo.isAdminMode = true;
                RevealAutoTeachingTab();
                adminModeStartTime = DateTime.Now;
                adminModeTimer.Start();

                // 관리자 모드 보더 표시 및 깜빡임 애니메이션 시작
                Dispatcher.Invoke(() =>
                {
                    Grid_LogoNormal.Visibility = Visibility.Collapsed;
                    Brd_AdminMode.Visibility = Visibility.Visible;
                    StartAdminModeBlinkAnimation();
                });

                // Update Pages based on Admin Mode
                pageCommSet.SetPageMode(gClass.str.GcpInfo.isAdminMode);
                pageCraneSet.SetPageMode(gClass.str.GcpInfo.isAdminMode);
                pageDio.SetPageMode(gClass.str.GcpInfo.isAdminMode);
            }
        }

        private void StartAdminModeBlinkAnimation()
        {
            // 깜빡임 애니메이션 생성
            DoubleAnimation blinkAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.3,
                Duration = new Duration(TimeSpan.FromSeconds(0.5)),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            Brd_AdminMode.BeginAnimation(UIElement.OpacityProperty, blinkAnimation);
        }

        private void StopAdminModeBlinkAnimation()
        {
            Brd_AdminMode.BeginAnimation(UIElement.OpacityProperty, null);
            Brd_AdminMode.Opacity = 1.0;
        }

        private void AdminModeTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // 5분(300초) 경과 확인
            if ((DateTime.Now - adminModeStartTime).TotalSeconds >= 300)
            {
                Dispatcher.Invoke(() =>
                {
                    gClass.str.GcpInfo.isAdminMode = false;
                    adminModeTimer.Stop();

                    // 관리자 모드 보더 숨김 및 애니메이션 중지
                    Brd_AdminMode.Visibility = Visibility.Collapsed;
                    Grid_LogoNormal.Visibility = Visibility.Visible;
                    StopAdminModeBlinkAnimation();

                    // Update Pages based on Admin Mode
                    pageCommSet.SetPageMode(false);
                    pageCraneSet.SetPageMode(false);
                    pageDio.SetPageMode(false);
                    // 정책 변경(2026-06-30): 오토티칭 탭은 항상 노출 — admin 타임아웃에도 숨기거나 이탈시키지 않음.
                });
            }
        }

        private void Combo_srmNum_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                Console.WriteLine("Combo_srmNum_SelectionChanged " + Combo_srmNum.SelectedIndex);
                win_HostLog1?.Hide();
                win_HostLog2?.Hide();
                win_HostLog3?.Hide();
                win_SrmLog1?.Hide();
                win_SrmLog2?.Hide();
                win_SrmLog3?.Hide();
                win_DioLog1?.Hide();
                win_DioLog2?.Hide();
                win_DioLog3?.Hide();


                gClass.srmNum = Combo_srmNum.SelectedIndex;

                // tcpServer Class 접근을 위한 클래스 레퍼런스 체인지
                servServer = hostComm[gClass.srmNum];

                switch (Combo_srmNum.SelectedIndex)
                {
                    case 0:
                        Frm_State.Content = pageDevList[0];
                        pageMain.craneDisplay(ref pageDevList[1], ref pageDevList[2]);
                        break;
                    case 1:
                        Frm_State.Content = pageDevList[1];
                        pageMain.craneDisplay(ref pageDevList[0], ref pageDevList[2]);
                        break;
                    case 2:
                        Frm_State.Content = pageDevList[2];
                        pageMain.craneDisplay(ref pageDevList[0], ref pageDevList[1]);
                        break;
                }


                AlarmList_Init(gClass.srmNum);

                pageCommSet.changeData();

                pageDio.Dio_Change();

                //HostLogWindowChanged();
                //DioLogWindowChanged();
                //SrmLogWindowChanged();

            });
        }

        private void Btn_Lang_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void LangMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            if (menuItem != null && menuItem.Tag != null)
            {
                string tagStr = menuItem.Tag.ToString();
                if (!string.IsNullOrEmpty(tagStr))
                {
                    int selectedIdx = int.Parse(tagStr);

                    switch (selectedIdx)
                    {
                        case 0:
                            if (TranslationSource.Instance.CurrentCulture != null)
                                TranslationSource.Instance.CurrentCulture = null;
                            break;
                        case 1:
                            TranslationSource.Instance.CurrentCulture = new CultureInfo("en");
                            break;
                        case 2:
                            TranslationSource.Instance.CurrentCulture = new CultureInfo("zh");
                            break;
                    }

                    gClass.str.GcpInfo.language = selectedIdx;
                    cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "GCPINFO", "LanguageIdx", selectedIdx.ToString());

                    // ContextMenu 업데이트
                    UpdateLangContextMenu();
                }
            }
        }

        private void UpdateLangContextMenu()
        {
            if (Btn_Lang?.ContextMenu != null)
            {
                foreach (MenuItem item in Btn_Lang.ContextMenu.Items)
                {
                    if (item.Tag != null && item.Tag.ToString() == gClass.str.GcpInfo.language.ToString())
                    {
                        item.IsChecked = true;
                    }
                    else
                    {
                        item.IsChecked = false;
                    }
                }
            }
        }

        private void TranslationSource_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 언어 변경 시 페이지 블록 텍스트 업데이트
            Dispatcher.Invoke(() =>
            {
                // 현재 모드에 따라 pageCheck를 다시 호출하여 블록 텍스트 업데이트
                if (controlMode > 0)
                {
                    pageCheck(controlMode);
                }

                UpdateCurrentErrorText();
            });
        }

        private void UpdateCurrentErrorText()
        {
            if (pageAlarmLog == null || ErrorText == null)
            {
                return;
            }

            int srmNum = gClass.srmNum;
            if (srmNum < 0 || srmNum >= gClass.str.SrmPacket.Length)
            {
                return;
            }

            ref Srm_Packet packet = ref gClass.str.SrmPacket[srmNum];

            if (packet.gcpErrorCode > 0)
            {
                string mainCode = packet.gcpErrorCode.ToString("00");
                string subCode = packet.gcpSubCode.ToString("00");

                lbl_errCode.Foreground = Brushes.Red;
                lbl_errCode.Content = mainCode + "-" + subCode;
                ErrorText.Text = pageAlarmLog.getErrorText(mainCode, subCode);
            }
            else if (packet.gcpWarningCode > 0)
            {
                string mainCode = packet.gcpWarningCode.ToString("00");
                string subCode = packet.gcpWarningSubCode.ToString("00");

                lbl_errCode.Foreground = Brushes.Orange;
                lbl_errCode.Content = mainCode + "-" + subCode;
                ErrorText.Text = pageAlarmLog.getWarningText(mainCode, subCode);
            }
            else
            {
                lbl_errCode.Foreground = Brushes.Green;
                lbl_errCode.Content = "00-00";
                ErrorText.Text = String.Empty;
            }

            if (ErrorText.Text != String.Empty)
            {
                StartMarquee();
            }
        }

        private void UpdateCurrentTime()
        {
            Dispatcher.Invoke(() =>
            {
                if (gClass.str.GcpInfo.isAdminMode && Brd_AdminMode.Visibility == Visibility.Visible)
                {
                    lbl_buildDate.Content = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                }
                else
                {
                    lbl_buildDate_normal.Content = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                }
            });
        }

        private void Btn_IO_Click(object sender, RoutedEventArgs e)
        {
            //menu_Setting
            Point position = Btn_IO.PointToScreen(new Point(0, 0));
            Point m_position = this.PointToScreen(new Point(0, 0));

            menu_Setting.Open_RelativeMenu(position, m_position, 2);            // Monitor Menu
            //Page_Change(9);
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            //Console.WriteLine("Activated MainWindow");
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            //Console.WriteLine("Window_PreviewMouseDown MainWindow");
            tmpNumPad.Hide();
        }

        private void Btn_Setting_Click(object sender, RoutedEventArgs e)
        {
            //menu_Setting
            Point position = Btn_Setting.PointToScreen(new Point(0, 0));
            Point m_position = this.PointToScreen(new Point(0, 0));

            //Console.WriteLine("Get Screen Position " + SystemParameters.PrimaryScreenWidth + " " + SystemParameters.PrimaryScreenHeight + " " + m_position.X + " " + m_position.Y + " " + this.Width);

            //menu_Setting.Left = position.X / dpiX * 96.0 - 120;
            //menu_Setting.Top = position.Y / dpiY * 96.0 - 80;
            //menu_Setting.Show();
            //Console.WriteLine("Position {0} {1} = {2} {3} {4} {5}", position, m_position , menu_Setting.Left , menu_Setting.Top, dpiX, dpiY);

            menu_Setting.Open_RelativeMenu(position, m_position, 1);            // Setting Menu

        }


        #region 작업 유효성체크 Srm_JobEnableParse
        //------------------------------------------Check Job Enable--------------------------------------------------------
        public int Srm_JobEnableParse(ref string tmpStr, int srmNum, bool wcsFlag)
        {
            bool bEnable = false;
            int retFlag = 99; 


            // WCS / SEMI 작업 구분     작업파싱은 Semi 버퍼 데이터로 하기 때문에 WCS작업 시 WCS 데이터로 초기화 해주어야 함
            if (wcsFlag)
            {
                gClass.str.SrmPacket[srmNum].semiJobCodeFk1 = gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1JobCmd;
                gClass.str.SrmPacket[srmNum].semiJobCodeFk2 = gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2JobCmd;

                // Fork1
                gClass.str.SrmPacket[srmNum].semiJobNoFk1 = gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1JobNo;

                gClass.str.SrmPacket[srmNum].semiFromStFk1 = (byte)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1FromSt;
                gClass.str.SrmPacket[srmNum].semiFromRowFk1 = (byte)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1FromRow;
                gClass.str.SrmPacket[srmNum].semiFromBayFk1 = gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1FromBay;
                gClass.str.SrmPacket[srmNum].semiFromLevFk1 = (byte)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1FromLev;

                gClass.str.SrmPacket[srmNum].semiToStFk1 = (byte)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1ToSt;
                gClass.str.SrmPacket[srmNum].semiToRowFk1 = (byte)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1ToRow;
                gClass.str.SrmPacket[srmNum].semiToBayFk1 = gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1ToBay;
                gClass.str.SrmPacket[srmNum].semiToLevFk1 = (byte)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1ToLev;

                gClass.str.SrmPacket[srmNum].semiGoodsTypeFk1 = 0;

                // Fork2
                gClass.str.SrmPacket[srmNum].semiJobNoFk2 = gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2JobNo;

                gClass.str.SrmPacket[srmNum].semiFromStFk2 = (byte)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2FromSt;
                gClass.str.SrmPacket[srmNum].semiFromRowFk2 = (byte)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2FromRow;
                gClass.str.SrmPacket[srmNum].semiFromBayFk2 = gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2FromBay;
                gClass.str.SrmPacket[srmNum].semiFromLevFk2 = (byte)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2FromLev;

                gClass.str.SrmPacket[srmNum].semiToStFk2 = (byte)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2ToSt;
                gClass.str.SrmPacket[srmNum].semiToRowFk2 = (byte)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2ToRow;
                gClass.str.SrmPacket[srmNum].semiToBayFk2 = gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2ToBay;
                gClass.str.SrmPacket[srmNum].semiToLevFk2 = (byte)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2ToLev;

                gClass.str.SrmPacket[srmNum].semiGoodsTypeFk2 = 0;


                // 청라 테스트베드 수정 전까지는 이거써야됨 260423
                gClass.str.SrmPacket[srmNum].semiDestSt = gClass.str.SrmPacket[srmNum].semiToStFk1;
                gClass.str.SrmPacket[srmNum].semiDestRow = gClass.str.SrmPacket[srmNum].semiToRowFk1;
                gClass.str.SrmPacket[srmNum].semiDestBay = gClass.str.SrmPacket[srmNum].semiToBayFk1;
                gClass.str.SrmPacket[srmNum].semiDestLev = gClass.str.SrmPacket[srmNum].semiToLevFk1;

                // 이동 작업 시 화물감지 여부에 따라 목적위치 버퍼 비교 - 명령 전달은 Dest 버퍼에서만 처리되도록
                //if (gClass.str.SRMIO[gClass.srmNum].GOX1)
                //{
                //    gClass.str.SrmPacket[srmNum].semiDestSt = gClass.str.SrmPacket[srmNum].semiToStFk1;
                //    gClass.str.SrmPacket[srmNum].semiDestRow = gClass.str.SrmPacket[srmNum].semiToRowFk1;
                //    gClass.str.SrmPacket[srmNum].semiDestBay = gClass.str.SrmPacket[srmNum].semiToBayFk1;
                //    gClass.str.SrmPacket[srmNum].semiDestLev = gClass.str.SrmPacket[srmNum].semiToLevFk1;
                //}
                //else
                //{
                //    gClass.str.SrmPacket[srmNum].semiDestSt = gClass.str.SrmPacket[srmNum].semiFromStFk1;
                //    gClass.str.SrmPacket[srmNum].semiDestRow = gClass.str.SrmPacket[srmNum].semiFromRowFk1;
                //    gClass.str.SrmPacket[srmNum].semiDestBay = gClass.str.SrmPacket[srmNum].semiFromBayFk1;
                //    gClass.str.SrmPacket[srmNum].semiDestLev = gClass.str.SrmPacket[srmNum].semiFromLevFk1;
                //}
            }
            else
            {
                // 반자동 시 이동작업 구분 (이동작업은 Fork1 기준 위치로만 이동 함)
                gClass.str.SrmPacket[srmNum].semiDestSt = gClass.str.SrmPacket[srmNum].semiToStFk1;
                gClass.str.SrmPacket[srmNum].semiDestRow = gClass.str.SrmPacket[srmNum].semiToRowFk1;
                gClass.str.SrmPacket[srmNum].semiDestBay = gClass.str.SrmPacket[srmNum].semiToBayFk1;
                gClass.str.SrmPacket[srmNum].semiDestLev = gClass.str.SrmPacket[srmNum].semiToLevFk1;
            }


            // Fork 1 작업체크----------------------------------------------------------------------------------------------------------
            if (gClass.str.SrmPacket[srmNum].semiJobNoFk1 > 0)
            {
                switch (gClass.str.SrmPacket[srmNum].semiJobCodeFk1)
                {
                    case 0:
                        tmpStr = "명령코드없음";
                        retFlag = 99;           // 조건 에러 시 서브코드 리턴
                        break;
                    case 1:          // 이동명령
                        if ((gClass.str.SrmPacket[srmNum].semiDestSt > 0) ||
                            (gClass.str.SrmPacket[srmNum].semiDestRow > 0 && gClass.str.SrmPacket[srmNum].semiDestBay > 0 && gClass.str.SrmPacket[srmNum].semiDestLev > 0))
                        {
                            if (gClass.str.SrmPacket[srmNum].semiDestSt > 0)      // 스테이션 이동
                            {
                                if (gClass.str.SrmPacket[srmNum].semiDestRow == 0 && gClass.str.SrmPacket[srmNum].semiDestBay == 0 && gClass.str.SrmPacket[srmNum].semiDestLev == 0) // 스테이션 외 입력 값 예외처리
                                {
                                    if (gClass.str.SrmInfo[srmNum].stn < gClass.str.SrmPacket[srmNum].semiDestSt)
                                    {
                                        tmpStr = "Station 값 초과";
                                        retFlag = 5;           // 조건 에러 시 서브코드 리턴
                                    }
                                    else
                                    {
                                        gClass.str.SrmPacket[srmNum].semiSendCodeFk1 = 0x01;     // 이동
                                        bEnable = true;
                                        retFlag = 0;
                                    }
                                }
                                else
                                {
                                    tmpStr = "중복 값 입력";
                                    retFlag = 6;           // 조건 에러 시 서브코드 리턴
                                }
                            }
                            else
                            {
                                if (gClass.str.SrmInfo[srmNum].row < gClass.str.SrmPacket[srmNum].semiDestRow)
                                {
                                    tmpStr = "Row 에러";
                                    retFlag = 8;
                                }
                                else if (gClass.str.SrmInfo[srmNum].bay < gClass.str.SrmPacket[srmNum].semiDestBay)
                                {
                                    tmpStr = "Bay 값 초과";
                                    retFlag = 4;
                                }
                                else if (gClass.str.SrmInfo[srmNum].lev < gClass.str.SrmPacket[srmNum].semiDestLev)
                                {
                                    tmpStr = "Lev 값 초과";
                                    retFlag = 2;
                                }
                                else
                                {
                                    gClass.str.SrmPacket[srmNum].semiSendCodeFk1 = 0x01;     // 이동
                                    bEnable = true;
                                    retFlag = 0;
                                }
                            }
                        }
                        else
                        {
                            if (gClass.str.SrmPacket[srmNum].semiDestRow > 0 || gClass.str.SrmPacket[srmNum].semiDestBay > 0 || gClass.str.SrmPacket[srmNum].semiDestLev > 0)
                            {
                                if (gClass.str.SrmPacket[srmNum].semiDestRow == 0)
                                {
                                    tmpStr = "Row 0 에러";
                                    retFlag = 8;
                                }
                                else if (gClass.str.SrmPacket[srmNum].semiDestBay == 0)
                                {
                                    tmpStr = "Bay 0 에러";
                                    retFlag = 3;
                                }
                                else if (gClass.str.SrmPacket[srmNum].semiDestLev == 0)
                                {
                                    tmpStr = "Lev 0 에러";
                                    retFlag = 2;
                                }
                                else
                                {
                                    tmpStr = "Except";
                                    retFlag = 99;
                                }
                            }
                            else
                            {
                                if (gClass.str.SrmPacket[srmNum].semiDestSt == 0)      // 스테이션 이동
                                {
                                    tmpStr = "Station 0 에러";
                                    retFlag = 5;        // 스테이션 에러
                                }
                                else
                                {
                                    tmpStr = "Except";
                                    retFlag = 99;
                                }
                            }
                        }
                        break;
                    case 2:          // 입고명령
                        if ((gClass.str.SrmPacket[srmNum].semiFromStFk1 > 0) && (gClass.str.SrmPacket[srmNum].semiToRowFk1 > 0) &&
                            (gClass.str.SrmPacket[srmNum].semiToBayFk1 > 0) && (gClass.str.SrmPacket[srmNum].semiToLevFk1 > 0))
                        {
                            if (CheckStation(ref tmpStr, srmNum, 1, gClass.str.SrmPacket[srmNum].semiFromStFk1) > 0)
                            {
                                retFlag = CheckStation(ref tmpStr, srmNum, 1, gClass.str.SrmPacket[srmNum].semiFromStFk1);           // 조건 에러 시 서브코드 리턴
                            }
                            else if (CheckRow(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToRowFk1) > 0)
                            {
                                retFlag = CheckRow(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToRowFk1);
                            }
                            else if (CheckBay(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToBayFk1) > 0)
                            {
                                retFlag = CheckBay(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToBayFk1);
                            }
                            else if (CheckLev(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToLevFk1) > 0)
                            {
                                retFlag = CheckLev(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToLevFk1);
                            }
                            else
                            {
                                // 금지렉 체크
                                if (CheckProhibitRack(srmNum, gClass.str.SrmPacket[srmNum].semiToRowFk1, gClass.str.SrmPacket[srmNum].semiToBayFk1, gClass.str.SrmPacket[srmNum].semiToLevFk1))
                                {
                                    gClass.str.SrmPacket[srmNum].semiSendCodeFk1 = 0x12;     // 입고
                                    bEnable = true;
                                    retFlag = 0;
                                }
                                else
                                {
                                    tmpStr = "금지렉 에러";
                                    retFlag = 1;
                                }
                            }
                        }
                        else
                        {
                            if (gClass.str.SrmPacket[srmNum].semiFromStFk1 == 0)      // 스테이션 이동
                            {
                                tmpStr = "Station 에러";
                                retFlag = 5;        // 스테이션 에러
                            }
                            else
                            {
                                if (gClass.str.SrmPacket[srmNum].semiToBayFk1 == 0)
                                {
                                    tmpStr = "Bay 에러";
                                    retFlag = 3;
                                }
                                else if (gClass.str.SrmPacket[srmNum].semiToRowFk1 == 0)
                                {
                                    tmpStr = "Row 에러";
                                    retFlag = 8;
                                }
                                else
                                {
                                    tmpStr = "Lev 에러";
                                    retFlag = 2;
                                }
                            }
                        }
                        break;
                    case 4:          // 출고명령
                        if ((gClass.str.SrmPacket[srmNum].semiToStFk1 > 0) && (gClass.str.SrmPacket[srmNum].semiFromRowFk1 > 0) &&
                            (gClass.str.SrmPacket[srmNum].semiFromBayFk1 > 0) && (gClass.str.SrmPacket[srmNum].semiFromLevFk1 > 0))
                        {
                            if (CheckStation(ref tmpStr, srmNum, 2, gClass.str.SrmPacket[srmNum].semiToStFk1) > 0)
                            {
                                retFlag = CheckStation(ref tmpStr, srmNum, 2, gClass.str.SrmPacket[srmNum].semiToStFk1);           // 조건 에러 시 서브코드 리턴
                            }
                            else if (CheckRow(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromRowFk1) > 0)
                            {
                                retFlag = CheckRow(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromRowFk1);
                            }
                            else if (CheckBay(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromBayFk1) > 0)
                            {
                                retFlag = CheckBay(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromBayFk1);
                            }
                            else if (CheckLev(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromLevFk1) > 0)
                            {
                                retFlag = CheckLev(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromLevFk1);
                            }
                            else
                            {
                                if (CheckProhibitRack(srmNum, gClass.str.SrmPacket[srmNum].semiFromRowFk1, gClass.str.SrmPacket[srmNum].semiFromBayFk1, gClass.str.SrmPacket[srmNum].semiFromLevFk1))
                                {
                                    gClass.str.SrmPacket[srmNum].semiSendCodeFk1 = 0x13;     // 출고
                                    bEnable = true;
                                    retFlag = 0;
                                }
                                else
                                {
                                    tmpStr = "금지렉 에러";
                                    retFlag = 1;
                                }
                            }
                        }
                        else
                        {
                            if (gClass.str.SrmPacket[srmNum].semiToStFk1 == 0)      // 스테이션 이동
                            {
                                tmpStr = "Station 에러";
                                retFlag = 5;        // 스테이션 에러
                            }
                            else
                            {
                                if (gClass.str.SrmPacket[srmNum].semiFromBayFk1 == 0)
                                {
                                    tmpStr = "Bay 에러";
                                    retFlag = 3;
                                }
                                else
                                {
                                    tmpStr = "Lev 에러";
                                    retFlag = 2;
                                }
                            }
                        }
                        break;
                    case 8:          // Rack to Rack
                        if ((gClass.str.SrmPacket[srmNum].semiFromRowFk1 > 0) && (gClass.str.SrmPacket[srmNum].semiFromBayFk1 > 0) && (gClass.str.SrmPacket[srmNum].semiFromLevFk1 > 0) &&
                            (gClass.str.SrmPacket[srmNum].semiToRowFk1 > 0) && (gClass.str.SrmPacket[srmNum].semiToBayFk1 > 0) && (gClass.str.SrmPacket[srmNum].semiToLevFk1 > 0))
                        {
                            if (CheckRow(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromRowFk1) > 0)
                            {
                                retFlag = CheckRow(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromRowFk1);
                            }
                            else if (CheckBay(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromBayFk1) > 0)
                            {
                                retFlag = CheckBay(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromBayFk1);
                            }
                            else if (CheckLev(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromLevFk1) > 0)
                            {
                                retFlag = CheckLev(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromLevFk1);
                            }
                            else if (CheckRow(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToRowFk1) > 0)
                            {
                                retFlag = CheckRow(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToRowFk1);
                            }
                            else if (CheckBay(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToBayFk1) > 0)
                            {
                                retFlag = CheckBay(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToBayFk1);
                            }
                            else if (CheckLev(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToLevFk1) > 0)
                            {
                                retFlag = CheckLev(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToLevFk1);
                            }
                            else
                            {
                                if (CheckProhibitRack(srmNum, gClass.str.SrmPacket[srmNum].semiFromRowFk1, gClass.str.SrmPacket[srmNum].semiFromBayFk1, gClass.str.SrmPacket[srmNum].semiFromLevFk1))
                                {
                                    if (CheckProhibitRack(srmNum, gClass.str.SrmPacket[srmNum].semiToRowFk1, gClass.str.SrmPacket[srmNum].semiToBayFk1, gClass.str.SrmPacket[srmNum].semiToLevFk1))
                                    {
                                        gClass.str.SrmPacket[srmNum].semiSendCodeFk1 = 0x14;     // 렉간 반송
                                        bEnable = true;
                                        retFlag = 0;
                                    }
                                    else
                                    {
                                        tmpStr = "금지렉 에러";
                                        retFlag = 1;
                                    }
                                }
                                else
                                {
                                    tmpStr = "금지렉 에러";
                                    retFlag = 1;
                                }
                            }
                        }
                        else
                        {
                            if (gClass.str.SrmPacket[srmNum].semiToBayFk1 == 0)
                            {
                                tmpStr = "Bay 에러";
                                retFlag = 3;
                            }
                            else
                            {
                                tmpStr = "Lev 에러";
                                retFlag = 2;
                            }
                        }
                        break;
                    case 16:         // Station to Station
                        if ((gClass.str.SrmPacket[srmNum].semiFromStFk1 > 0) && (gClass.str.SrmPacket[srmNum].semiToStFk1 > 0))
                        {
                            if (CheckStation(ref tmpStr, srmNum, 1, gClass.str.SrmPacket[srmNum].semiFromStFk1) > 0)
                            {
                                retFlag = CheckStation(ref tmpStr, srmNum, 1, gClass.str.SrmPacket[srmNum].semiFromStFk1);           // 조건 에러 시 서브코드 리턴
                            }
                            else if (CheckStation(ref tmpStr, srmNum, 2, gClass.str.SrmPacket[srmNum].semiToStFk1) > 0)
                            {
                                retFlag = CheckStation(ref tmpStr, srmNum, 2, gClass.str.SrmPacket[srmNum].semiToStFk1);           // 조건 에러 시 서브코드 리턴
                            }
                            else
                            {
                                gClass.str.SrmPacket[srmNum].semiSendCodeFk1 = 0x15;     // 스테이션간 반송
                                bEnable = true;
                                retFlag = 0;
                            }
                        }
                        else
                        {
                            tmpStr = "Station 에러";
                            retFlag = 5;
                        }
                        break;
                    case 32:         // 목적지 변경 (Rack)
                        if ((gClass.str.SrmPacket[srmNum].semiToRowFk1 > 0) && (gClass.str.SrmPacket[srmNum].semiToBayFk1 > 0) && (gClass.str.SrmPacket[srmNum].semiToLevFk1 > 0))
                        {
                            if (CheckRow(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToRowFk1) > 0)
                            {
                                retFlag = CheckRow(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToRowFk1);
                            }
                            else if (CheckBay(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToBayFk1) > 0)
                            {
                                retFlag = CheckBay(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToBayFk1);
                            }
                            else if (CheckLev(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToLevFk1) > 0)
                            {
                                retFlag = CheckLev(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToLevFk1);
                            }
                            else
                            {
                                // 금지렉 체크
                                if (CheckProhibitRack(srmNum, gClass.str.SrmPacket[srmNum].semiToRowFk1, gClass.str.SrmPacket[srmNum].semiToBayFk1, gClass.str.SrmPacket[srmNum].semiToLevFk1))
                                {
                                    gClass.str.SrmPacket[srmNum].semiSendCodeFk1 = 0x16;     // 렉 목적지 변경
                                    bEnable = true;
                                    retFlag = 0;
                                }
                                else
                                {
                                    tmpStr = "금지렉 에러";
                                    retFlag = 1;
                                }
                            }
                        }
                        else
                        {
                            if (gClass.str.SrmPacket[srmNum].semiToBayFk1 == 0)
                            {
                                tmpStr = "Bay 에러";
                                retFlag = 3;
                            }
                            else
                            {
                                tmpStr = "Lev 에러";
                                retFlag = 2;
                            }
                        }
                        break;
                    case 64:         // 목적지 변경 (Station)
                        if ((gClass.str.SrmPacket[srmNum].semiToStFk1 > 0))
                        {
                            if (CheckStation(ref tmpStr, srmNum, 2, gClass.str.SrmPacket[srmNum].semiToStFk1) > 0)
                            {
                                retFlag = CheckStation(ref tmpStr, srmNum, 2, gClass.str.SrmPacket[srmNum].semiToStFk1);           // 조건 에러 시 서브코드 리턴
                            }
                            else
                            {
                                gClass.str.SrmPacket[srmNum].semiSendCodeFk1 = 0x17;     // 스테이션 목적지 변경
                                bEnable = true;
                                retFlag = 0;
                            }
                        }
                        else
                        {
                            tmpStr = "Station 에러";
                            retFlag = 5;
                        }
                        break;
                    case 128:         // Sticky   From에만 데이터 지정으로 변경 251120
                        if ((gClass.str.SrmPacket[srmNum].semiFromRowFk1 > 0) && (gClass.str.SrmPacket[srmNum].semiFromBayFk1 > 0) && (gClass.str.SrmPacket[srmNum].semiFromLevFk1 > 0))
                        {
                            if (CheckRow(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromRowFk1) > 0)
                            {
                                retFlag = CheckRow(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromRowFk1);
                            }
                            else if (CheckBay(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromBayFk1) > 0)
                            {
                                retFlag = CheckBay(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromBayFk1);
                            }
                            else if (CheckLev(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromLevFk1) > 0)
                            {
                                retFlag = CheckLev(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromLevFk1);
                            }
                            else
                            {
                                if (CheckProhibitRack(srmNum, gClass.str.SrmPacket[srmNum].semiFromRowFk1, gClass.str.SrmPacket[srmNum].semiFromBayFk1, gClass.str.SrmPacket[srmNum].semiFromLevFk1))
                                {
                                    gClass.str.SrmPacket[srmNum].semiSendCodeFk1 = 0x1A;     // Sticky  0x18 -> 0x1A
                                    bEnable = true;
                                    retFlag = 0;
                                }
                                else
                                {
                                    tmpStr = "금지렉 에러";
                                    retFlag = 1;
                                }
                            }
                        }
                        else
                        {
                            if (gClass.str.SrmPacket[srmNum].semiFromRowFk1 == 0)
                            {
                                tmpStr = "Row 에러";
                                retFlag = 8;
                            }
                            else if (gClass.str.SrmPacket[srmNum].semiFromBayFk1 == 0)
                            {
                                tmpStr = "Bay 에러";
                                retFlag = 3;
                            }
                            else
                            {
                                tmpStr = "Lev 에러";
                                retFlag = 2;
                            }
                        }
                        break;
                    default:
                        tmpStr = "잘못된 명령코드";
                        retFlag = 99;           // 조건 에러 시 서브코드 리턴
                        break;
                }

                if (bEnable)
                {
                    //gClass.str.SrmPacket[srmNum].reqJobNoFk1 = gClass.str.SrmPacket[srmNum].semiJobNoFk1;
                }
            }

            if (retFlag > 0)
            {
                return retFlag;     // Fork 1에서 이미 조건에러 시 리턴
            }

            bEnable = false;
            // Fork 2 작업체크----------------------------------------------------------------------------------------------------------
            if (gClass.str.SrmPacket[srmNum].semiJobNoFk2 > 0)
            {
                switch (gClass.str.SrmPacket[srmNum].semiJobCodeFk2)
                {
                    case 0:
                        tmpStr = "명령코드없음";
                        retFlag = 99;           // 조건 에러 시 서브코드 리턴
                        break;
                    case 1:          // 이동명령
                        if ((gClass.str.SrmPacket[srmNum].semiToStFk2 > 0) ||
                            (gClass.str.SrmPacket[srmNum].semiToBayFk2 > 0 && gClass.str.SrmPacket[srmNum].semiToLevFk2 > 0))
                        {
                            if (gClass.str.SrmPacket[srmNum].semiToStFk2 > 0)      // 스테이션 이동
                            {
                                if (gClass.str.SrmInfo[srmNum].stn < gClass.str.SrmPacket[srmNum].semiToStFk2)
                                {
                                    tmpStr = "Station 값 초과";
                                    retFlag = 5;           // 조건 에러 시 서브코드 리턴
                                }
                                else
                                {
                                    gClass.str.SrmPacket[srmNum].semiSendCodeFk2 = 0x01;     // 이동
                                    bEnable = true;
                                    retFlag = 0;
                                }
                            }
                            else
                            {
                                if (gClass.str.SrmInfo[srmNum].bay < gClass.str.SrmPacket[srmNum].semiToBayFk2)
                                {
                                    tmpStr = "Bay 값 초과";
                                    retFlag = 4;
                                }
                                else if (gClass.str.SrmInfo[srmNum].lev < gClass.str.SrmPacket[srmNum].semiToLevFk2)
                                {
                                    tmpStr = "Lev 값 초과";
                                    retFlag = 2;
                                }
                                else
                                {
                                    gClass.str.SrmPacket[srmNum].semiSendCodeFk2 = 0x01;     // 이동
                                    bEnable = true;
                                    retFlag = 0;
                                }
                            }
                        }
                        else
                        {
                            if (gClass.str.SrmPacket[srmNum].semiToStFk2 == 0)      // 스테이션 이동
                            {
                                tmpStr = "Station 에러";
                                retFlag = 5;        // 스테이션 에러
                            }
                            else
                            {
                                if (gClass.str.SrmPacket[srmNum].semiToBayFk2 == 0)
                                {
                                    tmpStr = "Bay 에러";
                                    retFlag = 3;
                                }
                                else
                                {
                                    tmpStr = "Lev 에러";
                                    retFlag = 2;
                                }
                            }
                        }
                        break;
                    case 2:          // 입고명령
                        if ((gClass.str.SrmPacket[srmNum].semiFromStFk2 > 0) && (gClass.str.SrmPacket[srmNum].semiToRowFk2 > 0) &&
                            (gClass.str.SrmPacket[srmNum].semiToBayFk2 > 0) && (gClass.str.SrmPacket[srmNum].semiToLevFk2 > 0))
                        {
                            if (CheckStation(ref tmpStr, srmNum, 1, gClass.str.SrmPacket[srmNum].semiFromStFk2) > 0)
                            {
                                retFlag = CheckStation(ref tmpStr, srmNum, 1, gClass.str.SrmPacket[srmNum].semiFromStFk2);           // 조건 에러 시 서브코드 리턴
                            }
                            else if (CheckRow(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToRowFk2) > 0)
                            {
                                retFlag = CheckRow(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToRowFk2);
                            }
                            else if (CheckBay(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToBayFk2) > 0)
                            {
                                retFlag = CheckBay(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToBayFk2);
                            }
                            else if (CheckLev(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToLevFk2) > 0)
                            {
                                retFlag = CheckLev(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToLevFk2);
                            }
                            else
                            {
                                // 금지렉 체크
                                if (CheckProhibitRack(srmNum, gClass.str.SrmPacket[srmNum].semiToRowFk2, gClass.str.SrmPacket[srmNum].semiToBayFk2, gClass.str.SrmPacket[srmNum].semiToLevFk2))
                                {
                                    gClass.str.SrmPacket[srmNum].semiSendCodeFk2 = 0x12;     // 입고
                                    bEnable = true;
                                    retFlag = 0;
                                }
                                else
                                {
                                    tmpStr = "금지렉 에러";
                                    retFlag = 1;
                                }
                            }
                        }
                        else
                        {
                            if (gClass.str.SrmPacket[srmNum].semiFromStFk2 == 0)      // 스테이션 이동
                            {
                                tmpStr = "Station 에러";
                                retFlag = 5;        // 스테이션 에러
                            }
                            else
                            {
                                if (gClass.str.SrmPacket[srmNum].semiToBayFk2 == 0)
                                {
                                    tmpStr = "Bay 에러";
                                    retFlag = 3;
                                }
                                else if (gClass.str.SrmPacket[srmNum].semiToRowFk2 == 0)
                                {
                                    tmpStr = "Row 에러";
                                    retFlag = 8;
                                }
                                else
                                {
                                    tmpStr = "Lev 에러";
                                    retFlag = 2;
                                }
                            }
                        }
                        break;
                    case 4:          // 출고명령
                        if ((gClass.str.SrmPacket[srmNum].semiToStFk2 > 0) && (gClass.str.SrmPacket[srmNum].semiFromRowFk2 > 0) &&
                            (gClass.str.SrmPacket[srmNum].semiFromBayFk2 > 0) && (gClass.str.SrmPacket[srmNum].semiFromLevFk2 > 0))
                        {
                            if (CheckStation(ref tmpStr, srmNum, 2, gClass.str.SrmPacket[srmNum].semiToStFk2) > 0)
                            {
                                retFlag = CheckStation(ref tmpStr, srmNum, 2, gClass.str.SrmPacket[srmNum].semiToStFk2);           // 조건 에러 시 서브코드 리턴
                            }
                            else if (CheckRow(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromRowFk2) > 0)
                            {
                                retFlag = CheckRow(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromRowFk2);
                            }
                            else if (CheckBay(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromBayFk2) > 0)
                            {
                                retFlag = CheckBay(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromBayFk2);
                            }
                            else if (CheckLev(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromLevFk2) > 0)
                            {
                                retFlag = CheckLev(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromLevFk2);
                            }
                            else
                            {
                                if (CheckProhibitRack(srmNum, gClass.str.SrmPacket[srmNum].semiFromRowFk2, gClass.str.SrmPacket[srmNum].semiFromBayFk2, gClass.str.SrmPacket[srmNum].semiFromLevFk2))
                                {
                                    gClass.str.SrmPacket[srmNum].semiSendCodeFk2 = 0x13;     // 출고
                                    bEnable = true;
                                    retFlag = 0;
                                }
                                else
                                {
                                    tmpStr = "금지렉 에러";
                                    retFlag = 1;
                                }
                            }
                        }
                        else
                        {
                            if (gClass.str.SrmPacket[srmNum].semiToStFk2 == 0)      // 스테이션 이동
                            {
                                tmpStr = "Station 에러";
                                retFlag = 5;        // 스테이션 에러
                            }
                            else
                            {
                                if (gClass.str.SrmPacket[srmNum].semiFromBayFk2 == 0)
                                {
                                    tmpStr = "Bay 에러";
                                    retFlag = 3;
                                }
                                else
                                {
                                    tmpStr = "Lev 에러";
                                    retFlag = 2;
                                }
                            }
                        }
                        break;
                    case 8:          // Rack to Rack
                        if ((gClass.str.SrmPacket[srmNum].semiFromRowFk2 > 0) && (gClass.str.SrmPacket[srmNum].semiFromBayFk2 > 0) && (gClass.str.SrmPacket[srmNum].semiFromLevFk2 > 0) &&
                            (gClass.str.SrmPacket[srmNum].semiToRowFk2 > 0) && (gClass.str.SrmPacket[srmNum].semiToBayFk2 > 0) && (gClass.str.SrmPacket[srmNum].semiToLevFk2 > 0))
                        {
                            if (CheckRow(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromRowFk2) > 0)
                            {
                                retFlag = CheckRow(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromRowFk2);
                            }
                            else if (CheckBay(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromBayFk2) > 0)
                            {
                                retFlag = CheckBay(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromBayFk2);
                            }
                            else if (CheckLev(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromLevFk2) > 0)
                            {
                                retFlag = CheckLev(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiFromLevFk2);
                            }
                            else if (CheckRow(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToRowFk2) > 0)
                            {
                                retFlag = CheckRow(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToRowFk2);
                            }
                            else if (CheckBay(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToBayFk2) > 0)
                            {
                                retFlag = CheckBay(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToBayFk2);
                            }
                            else if (CheckLev(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToLevFk2) > 0)
                            {
                                retFlag = CheckLev(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToLevFk2);
                            }
                            else
                            {
                                if (CheckProhibitRack(srmNum, gClass.str.SrmPacket[srmNum].semiFromRowFk2, gClass.str.SrmPacket[srmNum].semiFromBayFk2, gClass.str.SrmPacket[srmNum].semiFromLevFk2))
                                {
                                    if (CheckProhibitRack(srmNum, gClass.str.SrmPacket[srmNum].semiFromRowFk2, gClass.str.SrmPacket[srmNum].semiFromBayFk2, gClass.str.SrmPacket[srmNum].semiFromLevFk2))
                                    {
                                        gClass.str.SrmPacket[srmNum].semiSendCodeFk2 = 0x14;     // 렉간 반송
                                        bEnable = true;
                                        retFlag = 0;
                                    }
                                    else
                                    {
                                        tmpStr = "금지렉 에러";
                                        retFlag = 1;
                                    }
                                }
                                else
                                {
                                    tmpStr = "금지렉 에러";
                                    retFlag = 1;
                                }
                            }
                        }
                        else
                        {
                            if (gClass.str.SrmPacket[srmNum].semiToBayFk2 == 0)
                            {
                                tmpStr = "Bay 에러";
                                retFlag = 3;
                            }
                            else
                            {
                                tmpStr = "Lev 에러";
                                retFlag = 2;
                            }
                        }
                        break;
                    case 32:         // 목적지 변경 (Rack)
                        if ((gClass.str.SrmPacket[srmNum].semiToRowFk2 > 0) && (gClass.str.SrmPacket[srmNum].semiToBayFk2 > 0) && (gClass.str.SrmPacket[srmNum].semiToLevFk2 > 0))
                        {
                            if (CheckRow(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToRowFk2) > 0)
                            {
                                retFlag = CheckRow(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToRowFk2);
                            }
                            else if (CheckBay(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToBayFk2) > 0)
                            {
                                retFlag = CheckBay(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToBayFk2);
                            }
                            else if (CheckLev(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToLevFk2) > 0)
                            {
                                retFlag = CheckLev(ref tmpStr, srmNum, gClass.str.SrmPacket[srmNum].semiToLevFk2);
                            }
                            else
                            {
                                // 금지렉 체크
                                if (CheckProhibitRack(srmNum, gClass.str.SrmPacket[srmNum].semiToRowFk2, gClass.str.SrmPacket[srmNum].semiToBayFk2, gClass.str.SrmPacket[srmNum].semiToLevFk2))
                                {
                                    gClass.str.SrmPacket[srmNum].semiSendCodeFk2 = 0x16;     // 렉 목적지 변경
                                    bEnable = true;
                                    retFlag = 0;
                                }
                                else
                                {
                                    tmpStr = "금지렉 에러";
                                    retFlag = 1;
                                }
                            }
                        }
                        else
                        {
                            if (gClass.str.SrmPacket[srmNum].semiToBayFk2 == 0)
                            {
                                tmpStr = "Bay 에러";
                                retFlag = 3;
                            }
                            else
                            {
                                tmpStr = "Lev 에러";
                                retFlag = 2;
                            }
                        }
                        break;
                    case 16:         // Station to Station
                        if ((gClass.str.SrmPacket[srmNum].semiFromStFk2 > 0) && (gClass.str.SrmPacket[srmNum].semiToStFk2 > 0))
                        {
                            if (CheckStation(ref tmpStr, srmNum, 1, gClass.str.SrmPacket[srmNum].semiFromStFk2) > 0)
                            {
                                retFlag = CheckStation(ref tmpStr, srmNum, 1, gClass.str.SrmPacket[srmNum].semiFromStFk2);           // 조건 에러 시 서브코드 리턴
                            }
                            else if (CheckStation(ref tmpStr, srmNum, 2, gClass.str.SrmPacket[srmNum].semiToStFk2) > 0)
                            {
                                retFlag = CheckStation(ref tmpStr, srmNum, 2, gClass.str.SrmPacket[srmNum].semiToStFk2);           // 조건 에러 시 서브코드 리턴
                            }
                            else
                            {
                                gClass.str.SrmPacket[srmNum].semiSendCodeFk2 = 0x15;     // 스테이션간 반송
                                bEnable = true;
                                retFlag = 0;
                            }
                        }
                        else
                        {
                            tmpStr = "Station 에러";
                            retFlag = 5;
                        }
                        break;
                    case 64:         // 목적지 변경 (Station)
                        if ((gClass.str.SrmPacket[srmNum].semiToStFk2 > 0))
                        {
                            if (CheckStation(ref tmpStr, srmNum, 2, gClass.str.SrmPacket[srmNum].semiToStFk2) > 0)
                            {
                                retFlag = CheckStation(ref tmpStr, srmNum, 2, gClass.str.SrmPacket[srmNum].semiToStFk2);           // 조건 에러 시 서브코드 리턴
                            }
                            else
                            {
                                gClass.str.SrmPacket[srmNum].semiSendCodeFk2 = 0x17;     // 스테이션 목적지 변경
                                bEnable = true;
                                retFlag = 0;
                            }
                        }
                        else
                        {
                            tmpStr = "Station 에러";
                            retFlag = 5;
                        }
                        break;
                    default:
                        tmpStr = "잘못된 명령코드";
                        retFlag = 99;           // 조건 에러 시 서브코드 리턴
                        break;
                }

                if (bEnable)
                {
                    //gClass.str.SrmPacket[srmNum].reqJobNoFk2 = gClass.str.SrmPacket[srmNum].semiJobNoFk2;
                }
            }

            return retFlag;
        }
        #endregion
        private int CheckStation(ref string tmpStr, int srmNum, int type, int stn)
        {
            if (gClass.str.SrmInfo[srmNum].stn < stn)
            {
                tmpStr = "Station 값 초과";
                return 5;           // 조건 에러 시 서브코드 리턴
            }
            if (type == 1)       // 입고 LOAD
            {
                if (gClass.str.SrmInfo[srmNum].SrmStation[stn - 1].stnType != 1 && gClass.str.SrmInfo[srmNum].SrmStation[stn - 1].stnType != 3)
                {
                    tmpStr = "Station Type에러";
                    return 6;
                }
                else
                {
                    return 0;
                }
            }
            else if (type == 2)       // 출고 UNLOAD
            {
                if (gClass.str.SrmInfo[srmNum].SrmStation[stn - 1].stnType != 2 && gClass.str.SrmInfo[srmNum].SrmStation[stn - 1].stnType != 3)
                {
                    tmpStr = "Station Type에러";
                    return 6;
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                return 0;
            }
        }

        private int CheckRow(ref string tmpStr, int srmNum, int row)
        {
            if (gClass.str.SrmInfo[srmNum].row < row)
            {
                tmpStr = "Row 값 초과";
                return 8;           // 조건 에러 시 서브코드 리턴
            }
            else if (row <= 0)
            {
                tmpStr = "Row 값 에러";
                return 8;           // 조건 에러 시 서브코드 리턴
            }
            else
            {
                return 0;
            }
        }

        private int CheckBay(ref string tmpStr, int srmNum, int bay)
        {
            if (gClass.str.SrmInfo[srmNum].bay < bay)
            {
                tmpStr = "Bay 값 초과";
                return 4;           // 조건 에러 시 서브코드 리턴
            }
            else if (bay <= 0)
            {
                tmpStr = "Bay 값 에러";
                return 3;           // 조건 에러 시 서브코드 리턴
            }
            else
            {
                return 0;
            }
        }

        private int CheckLev(ref string tmpStr, int srmNum, int lev)
        {
            if (gClass.str.SrmInfo[srmNum].lev < lev)
            {
                tmpStr = "Lev 값 초과";
                return 2;           // 조건 에러 시 서브코드 리턴
            }
            else if (lev <= 0)
            {
                tmpStr = "Lev 값 에러";
                return 2;           // 조건 에러 시 서브코드 리턴
            }
            else
            {
                return 0;
            }
        }

        public bool CheckProhibitRack(int srmNum, int row, int bay, int lev)
        {
            bool retFlag = true;
            for (int i = 0; i < gClass.str.SrmInfo[srmNum].prohParseCnt; i++)           // 금지 ROW 파싱데이터 카운트        * 금지렉 리스트에는 Row 0부터 저장함 인덱스 확인 필 (row-1)
            {
                if (gClass.str.SrmInfo[srmNum].prohDataList[i][0] == row && gClass.str.SrmInfo[srmNum].prohDataList[i][1] == bay && gClass.str.SrmInfo[srmNum].prohDataList[i][2] == lev)
                {
                    retFlag = false;
                    Console.WriteLine("Check Proh Rack : " + srmNum + "/" + row + "/" + bay + "/" + lev);
                    break;
                }
            }
            return retFlag;
        }


        private void StartMarquee()
        {
            ErrorText.UpdateLayout();

            double textWidth = ErrorText.ActualWidth;
            double canvasWidth = MarqueeCanvas.ActualWidth;

            if (textWidth <= 0 || canvasWidth <= 0)
                return;

            if (textWidth <= canvasWidth)
            {
                ErrorText.BeginAnimation(Canvas.LeftProperty, null);  // 기존 애니메이션 제거
                Canvas.SetLeft(ErrorText, 0);  // 왼쪽 정렬
                return;
            }
            // 시작 위치: 오른쪽 밖
            Canvas.SetLeft(ErrorText, canvasWidth);

            var animation = new DoubleAnimation
            {
                From = canvasWidth,
                To = -textWidth,
                Duration = TimeSpan.FromSeconds(10),
                RepeatBehavior = RepeatBehavior.Forever
            };

            ErrorText.BeginAnimation(Canvas.LeftProperty, null);
            ErrorText.BeginAnimation(Canvas.LeftProperty, animation);
        }

    }
}
