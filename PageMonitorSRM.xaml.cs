using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using gcp_Wpf.MenuWindow;

namespace gcp_Wpf
{
    /// <summary>
    /// PageMonitorSRM.xaml에 대한 상호 작용 논리
    /// </summary>
    /// 
    public partial class PageMonitorSRM : Page
    {
        ImageBrush moveR = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/slowR.png")));
        ImageBrush moveL = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/slowL.png")));

        List<MonitorData> monitorList = new List<MonitorData>();

        //Singletone
        singletonClass gClass;

        //Test Timer
        Timer dataCheckTimer = new Timer();
        int timerCnt = 0;

        string[][] dataStrList = new string[20][];
        int mapListCnt = 0;
        int idxCnt = 0;

        int dispPageNo = 0;

        public PageMonitorSRM()
        {
            InitializeComponent();

            // Event Init
            Btn_LfTr.Click += Click_DispChange;
            Btn_Fork1.Click += Click_DispChange;
            Btn_Fork2.Click += Click_DispChange;
            Btn_SP1.Click += Click_DispChange;
            Btn_SP2.Click += Click_DispChange;
            Btn_SP3.Click += Click_DispChange;
            Btn_Left.Click += Click_LeftRight;
            Btn_Right.Click += Click_LeftRight;

            gClass = singletonClass.Instance;
            
            // 언어 변경 이벤트 구독
            TranslationSource.Instance.PropertyChanged += TranslationSource_PropertyChanged;
            
            // SRM State 라벨 텍스트 초기화
            InitializeDataStrList();


            // 센터 / 라이트 테이블 가변형

            // Center Table Data Init
            for (int i = 0; i < dataCenter.RowDefinitions.Count; i++)
            {
                if (dataCenter.ColumnDefinitions.Count < 2)
                {
                    VarMessageBox.Show(cConstDefine.tr("변경확인"), cConstDefine.tr("SRM Center 모니터 컬럼 카운트 오류"), VarMessageBoxButton.OK);
                    break;
                }
                MonitorData monData = new MonitorData("", "");
                Grid.SetRow(monData.lbl_type, i);
                Grid.SetColumn(monData.lbl_type, 0);
                Grid.SetRow(monData.lbl_value, i);
                Grid.SetColumn(monData.lbl_value, 1);
                dataCenter.Children.Add(monData.lbl_type);
                dataCenter.Children.Add(monData.lbl_value);
                monitorList.Add(monData);
            }

            idxCnt += dataCenter.RowDefinitions.Count;

            // Right Table Data Init
            for (int i = 0; i < dataRight.RowDefinitions.Count; i++)
            {
                if (dataRight.ColumnDefinitions.Count < 2)
                {
                    VarMessageBox.Show(cConstDefine.tr("변경확인"), cConstDefine.tr("SRM Center 모니터 컬럼 카운트 오류"), VarMessageBoxButton.OK);
                    break;
                }
                MonitorData monData = new MonitorData("", "");
                Grid.SetRow(monData.lbl_type, i);
                Grid.SetColumn(monData.lbl_type, 0);
                Grid.SetRow(monData.lbl_value, i);
                Grid.SetColumn(monData.lbl_value, 1);
                dataRight.Children.Add(monData.lbl_type);
                dataRight.Children.Add(monData.lbl_value);
                monitorList.Add(monData);
            }

            // 초기 페이지 init
            display_Change(0);

            // to do 타이머 켜는 시점 정리 필요
            dataCheckTimer.Interval = 1000; // 1 second
            dataCheckTimer.AutoReset = true; // Repeat the timer
            dataCheckTimer.Elapsed += dataTimer_Elapsed;
            dataCheckTimer.Start();

            //dataGrid.ColumnDefinitions[0].Width = new GridLength(0, GridUnitType.Star); // 0*
            //dataCenter.Visibility = Visibility.Collapsed;
        }
        unsafe private void dataTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            int curListCnt = 0;
            //
            Dispatcher.Invoke(() =>
            {
                try
                {

                    ref Srm_State refState = ref gClass.str.SrmState[gClass.srmNum];
                    string tmpValue, curValue;

                    // Data Left (Static List) Display
                    tmpValue = refState.setupMode.ToString();
                    monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 셋업모드
                    tmpValue = refState.forcedMode.ToString();
                    monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 강제모드
                    tmpValue = refState.manualMode.ToString();
                    monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 수동모드
                    tmpValue = refState.autoMode.ToString();
                    monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 자동모드

                    tmpValue = refState.dSt1ReqCmd.ToString();
                    monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 작업요구

                    tmpValue = refState.dSt1InvConn.ToString();
                    monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 인버터 접속상태
                    tmpValue = refState.dSt1Abnormal.ToString();
                    monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 이상 상태
                    tmpValue = refState.dSt1Warning.ToString();
                    monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 경고 상태
                    tmpValue = refState.dSt1EmStop.ToString();
                    monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 비상 정지
                    tmpValue = refState.dSt1StartSt.ToString();
                    monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 시작 상태

                    tmpValue = gClass.str.SrmPacket[gClass.srmNum].startEnable.ToString();
                    monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 시작 준비

                    tmpValue = refState.dSt2EmSwitch.ToString();
                    monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 비상스위치 상태
                    tmpValue = refState.dSt2ManAutoSw.ToString();
                    monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 자동/수동 스위치 상태
                    tmpValue = refState.dSt2maintPos.ToString();
                    monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 보수 위치
                    tmpValue = refState.dSt2homePos.ToString();
                    monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 홈 위치

                    tmpValue = refState.operCode.ToString();
                    monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 장비 동작코드

                    tmpValue = refState.errcodeH.ToString();
                    monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 메인 코드
                    tmpValue = refState.errcodeM.ToString();
                    monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 경고 코드
                    tmpValue = refState.errcodeL.ToString();
                    monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 서브 코드         16

                    tmpValue = Enum.GetName(typeof(JOBSTATE), (object)this.gClass.str.SrmPacket[this.gClass.srmNum].jobState);
                    monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 동작 스텝         

                    if (dispPageNo == 0)
                    {
                        tmpValue = refState.trav.homeMove.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 주행 홈복귀
                        tmpValue = refState.trav.trSt1OriginPos.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 주행 정위치
                        tmpValue = refState.trav.trSt1MoveDirec.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 주행 이동방향
                        tmpValue = refState.trav.trSt1DecState.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 주행 감속중
                        tmpValue = refState.trav.trSt1AccState.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 주행 가속중
                        tmpValue = refState.trav.trSt1OperState.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 주행 동작중

                        tmpValue = refState.trav.trSt2LoadTunn.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 주행 부하 튜닝중
                        tmpValue = refState.trav.trSt2NoLoadTunn.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 주행 무부하 튜닝중
                        tmpValue = refState.trav.trSt2HomeCheck.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 주행 원점확인
                        tmpValue = refState.trav.trSt2InvAlarmSt.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 주행 인버터 알람
                        tmpValue = refState.invErrorTravMain.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 주행 인버터 메인코드
                        tmpValue = refState.invErrorTravSub.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 주행 인버터 서브코드
                        tmpValue = refState.trav.trSt2InvConnSt.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 주행 인버터 접속

                        tmpValue = refState.trav.fwDecNo.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 주행 전진감속번호
                        tmpValue = refState.trav.bwDecNo.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 주행 후진감속번호
                        tmpValue = refState.trav.curPos.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 주행 현재위치
                        tmpValue = refState.trav.curSpd.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 주행 현재속도
                        tmpValue = refState.trav.targetPos.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 주행 목표위치
                        tmpValue = refState.trav.targetSpd.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 주행 목표속도

                        tmpValue = refState.lift.homeMove.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 승강 홈복귀
                        tmpValue = refState.lift.liSt1OriginPos.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 승강 정위치
                        tmpValue = refState.lift.liSt1MoveDirec.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 승강 이동방향
                        tmpValue = refState.lift.liSt1DecState.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 승강 감속중
                        tmpValue = refState.lift.liSt1AccState.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 승강 가속중
                        tmpValue = refState.lift.liSt1OperState.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 승강 동작중

                        tmpValue = refState.lift.liSt2LoadTunn.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 승강 부하 튜닝중
                        tmpValue = refState.lift.liSt2NoLoadTunn.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 승강 무부하 튜닝중
                        tmpValue = refState.lift.liSt2HomeCheck.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 승강 원점확인
                        tmpValue = refState.lift.liSt2InvAlarmSt.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 승강 인버터 알람
                        tmpValue = refState.invErrorLiftMain.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 승강 인버터 메인코드
                        tmpValue = refState.invErrorLiftSub.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 승강 인버터 서브코드
                        tmpValue = refState.lift.liSt2InvConnSt.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 승강 인버터 접속

                        tmpValue = refState.lift.upDecNo.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 승강 상승감속번호
                        tmpValue = refState.lift.dnDecNo.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 승강 하강감속번호
                        tmpValue = refState.lift.curPos.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 승강 현재위치
                        tmpValue = refState.lift.curSpd.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 승강 현재속도
                        tmpValue = refState.lift.targetPos.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 승강 목표위치
                        tmpValue = refState.lift.targetSpd.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 승강 목표속도
                    }
                    else if (dispPageNo == 1)
                    {
                        tmpValue = refState.fork1.curStation.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 현재 스테이션
                        tmpValue = refState.fork1.curBay.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 현재 베이
                        tmpValue = refState.fork1.curLev.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 현재 레벨
                        tmpValue = refState.fork1.curPosNum.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 현재 위치번호
                        tmpValue = refState.fork1.posRightBottom.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 우 승강하위치
                        tmpValue = refState.fork1.posRightUp.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 우 승강상위치
                        tmpValue = refState.fork1.posRightTravExac.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 우 주행정위치
                        tmpValue = refState.fork1.posLeftBottom.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 좌 승강하위치
                        tmpValue = refState.fork1.posLeftUp.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 좌 승강상위치
                        tmpValue = refState.fork1.posLeftTravExac.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 우 주행정위치

                        tmpValue = refState.fork1.posRightExac3.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 우 정위치3
                        tmpValue = refState.fork1.posRightExac2.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 우 정위치2
                        tmpValue = refState.fork1.posRightExac1.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 우 정위치1
                        tmpValue = refState.fork1.posLeftExac3.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 좌 정위치3
                        tmpValue = refState.fork1.posLeftExac2.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 좌 정위치2
                        tmpValue = refState.fork1.posLeftExac1.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 좌 정위치1
                        tmpValue = refState.fork1.posCenterExac.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 중심정위치

                        tmpValue = refState.fork1.targetStation.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 목적 스테이션
                        tmpValue = refState.fork1.targetRow.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 목적 ROW
                        tmpValue = refState.fork1.targetBay.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 목적 BAY
                        tmpValue = refState.fork1.targetLev.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 목적 LEV

                        tmpValue = refState.fork1.forkRightEnable.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 우측진출가능
                        tmpValue = refState.fork1.forkLeftEnable.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 좌측진출가능
                        tmpValue = refState.fork1.loadState.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 화물적재
                        tmpValue = refState.fork1.originPos.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 정위치
                        tmpValue = refState.fork1.moveDirec.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 이동방향
                        tmpValue = refState.fork1.decState.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 감속중
                        tmpValue = refState.fork1.accState.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 가속중
                        tmpValue = refState.fork1.operState.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 동작중

                        tmpValue = refState.fork1.loadTunn.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 부하 튜닝중
                        tmpValue = refState.fork1.noLoadTunn.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 무부하 튜닝중
                        tmpValue = refState.fork1.homeCheck.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 원점확인
                        tmpValue = refState.fork1.invAlarmSt.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 인버터 알람
                        tmpValue = refState.invErrorFork1Main.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 인버터 메인코드
                        tmpValue = refState.invErrorFork1Sub.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 인버터 서브코드
                        tmpValue = refState.fork1.invConnSt.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 인버터 접속

                        //tmpValue = refState.fork1.loadType.ToString();
                        //monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 화물타입
                        tmpValue = refState.fork1.curPos.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 현재위치
                        tmpValue = refState.fork1.curSpd.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 현재속도
                        tmpValue = refState.fork1.targetPos.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 목표위치
                        tmpValue = refState.fork1.targetSpd.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크1 목표속도
                    }
                    else if (dispPageNo == 2)
                    {
                        tmpValue = refState.fork2.curStation.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 현재 스테이션
                        tmpValue = refState.fork2.curBay.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 현재 베이
                        tmpValue = refState.fork2.curLev.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 현재 레벨
                        tmpValue = refState.fork2.curPosNum.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 현재 위치번호
                        tmpValue = refState.fork2.posRightBottom.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 우 승강하위치
                        tmpValue = refState.fork2.posRightUp.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 우 승강상위치
                        tmpValue = refState.fork2.posRightTravExac.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 우 주행정위치
                        tmpValue = refState.fork2.posLeftBottom.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 좌 승강하위치
                        tmpValue = refState.fork2.posLeftUp.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 좌 승강상위치
                        tmpValue = refState.fork2.posLeftTravExac.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 우 주행정위치

                        tmpValue = refState.fork2.posRightExac3.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 우 정위치3
                        tmpValue = refState.fork2.posRightExac2.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 우 정위치2
                        tmpValue = refState.fork2.posRightExac1.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 우 정위치1
                        tmpValue = refState.fork2.posLeftExac3.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 좌 정위치3
                        tmpValue = refState.fork2.posLeftExac2.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 좌 정위치2
                        tmpValue = refState.fork2.posLeftExac1.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 좌 정위치1
                        tmpValue = refState.fork2.posCenterExac.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 중심정위치

                        tmpValue = refState.fork2.targetStation.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 목적 스테이션
                        tmpValue = refState.fork2.targetRow.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 목적 ROW
                        tmpValue = refState.fork2.targetBay.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 목적 BAY
                        tmpValue = refState.fork2.targetLev.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 목적 LEV

                        tmpValue = refState.fork2.forkRightEnable.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 우측진출가능
                        tmpValue = refState.fork2.forkLeftEnable.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 좌측진출가능
                        tmpValue = refState.fork2.loadState.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 화물적재
                        tmpValue = refState.fork2.originPos.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 정위치
                        tmpValue = refState.fork2.moveDirec.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 이동방향
                        tmpValue = refState.fork2.decState.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 감속중
                        tmpValue = refState.fork2.accState.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 가속중
                        tmpValue = refState.fork2.operState.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 동작중

                        tmpValue = refState.fork2.loadTunn.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 부하 튜닝중
                        tmpValue = refState.fork2.noLoadTunn.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 무부하 튜닝중
                        tmpValue = refState.fork2.homeCheck.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 원점확인
                        tmpValue = refState.fork2.invAlarmSt.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 인버터 알람
                        tmpValue = refState.invErrorFork2Main.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 인버터 메인코드
                        tmpValue = refState.invErrorFork2Sub.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 인버터 서브코드
                        tmpValue = refState.fork2.invConnSt.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 인버터 접속

                        //tmpValue = refState.fork2.loadType.ToString();
                        //monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 화물타입
                        tmpValue = refState.fork2.curPos.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 현재위치
                        tmpValue = refState.fork2.curSpd.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 현재속도
                        tmpValue = refState.fork2.targetPos.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 목표위치
                        tmpValue = refState.fork2.targetSpd.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 포크2 목표속도
                    }
                    else if (dispPageNo == 3)
                    {
                        tmpValue = refState.fork1.jobNo.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 반송 작업번호
                        tmpValue = refState.fork1.taskIdx.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 반송 태스크번호
                        tmpValue = refState.fork1.fromStation.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 반송 출발 스테이션
                        tmpValue = refState.fork1.fromRow.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 반송 출발 ROW
                        tmpValue = refState.fork1.fromBay.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 반송 출발 BAY
                        tmpValue = refState.fork1.fromLev.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 반송 출발 LEVEL
                        tmpValue = refState.fork1.toStation.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 반송 목적 스테이션
                        tmpValue = refState.fork1.toRow.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 반송 목적 ROW
                        tmpValue = refState.fork1.toBay.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 반송 목적 BAY
                        tmpValue = refState.fork1.toLev.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 반송 목적 LEVEL

                        tmpValue = refState.fork1.cmdCode.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 반송 지령코드
                        tmpValue = refState.fork1.procState.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 반송 처리상태
                        tmpValue = refState.fork1.procStep.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 반송 처리단계
                        tmpValue = refState.fork1.mvJobNo.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 이동 작업번호
                        tmpValue = refState.fork1.mvToStation.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 이동 목적 스테이션
                        tmpValue = refState.fork1.mvToRow.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 이동 목적 ROW
                        tmpValue = refState.fork1.mvToBay.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 이동 목적 BAY
                        tmpValue = refState.fork1.mvToLev.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 이동 목적 LEVEL
                        tmpValue = refState.fork1.mvProcState.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 이동 처리상태
                        tmpValue = refState.fork1.mvProcStep.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 이동 처리단계


                        // 포크 2 반송작업
                        tmpValue = refState.fork2.jobNo.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 반송 작업번호
                        tmpValue = refState.fork2.taskIdx.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 반송 태스크번호
                        tmpValue = refState.fork2.fromStation.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 반송 출발 스테이션
                        tmpValue = refState.fork2.fromRow.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 반송 출발 ROW
                        tmpValue = refState.fork2.fromBay.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 반송 출발 BAY
                        tmpValue = refState.fork2.fromLev.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 반송 출발 LEVEL
                        tmpValue = refState.fork2.toStation.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 반송 목적 스테이션
                        tmpValue = refState.fork2.toRow.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 반송 목적 ROW
                        tmpValue = refState.fork2.toBay.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 반송 목적 BAY
                        tmpValue = refState.fork2.toLev.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 반송 목적 LEVEL

                        tmpValue = refState.fork2.cmdCode.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 반송 지령코드
                        tmpValue = refState.fork2.procState.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 반송 처리상태
                        tmpValue = refState.fork2.procStep.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 반송 처리단계
                        tmpValue = refState.fork2.mvJobNo.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 이동 작업번호
                        tmpValue = refState.fork2.mvToStation.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 이동 목적 스테이션
                        tmpValue = refState.fork2.mvToRow.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 이동 목적 ROW
                        tmpValue = refState.fork2.mvToBay.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 이동 목적 BAY
                        tmpValue = refState.fork2.mvToLev.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 이동 목적 LEVEL
                        tmpValue = refState.fork2.mvProcState.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 이동 처리상태
                        tmpValue = refState.fork2.mvProcStep.ToString();
                        monitorList[curListCnt++].lbl_value.Content = tmpValue;        // 이동 처리단계
                    }
                    else if (dispPageNo == 4)
                    {

                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.travSetSpd.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.liftSetSpd.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.fork1SetSpd.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.fork2SetSpd.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.travSetAcc.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.liftSetAcc.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.fork1SetAcc.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.fork2SetAcc.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.travSetDec.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.liftSetDec.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.fork1SetDec.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.fork2SetDec.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.travSetJerk.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.liftSetJerk.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.fork1SetJerk.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.fork2SetJerk.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.preLoadMoveDelay.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.postLoadMoveDelay.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.preLoadForkExtendDelay.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.postLoadForkExtendDelay.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.preLoadForkLiftDelay.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.postLoadForkLiftDelay.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.preLoadForkRetractDelay.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.postLoadForkRetractDelay.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.preUnloadMoveDelay.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.postUnloadMoveDelay.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.preUnloadForkExtendDelay.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.postUnloadForkExtendDelay.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.preUnloadForkLiftDelay.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.postUnloadForkLiftDelay.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.preUnloadForkRetractDelay.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.postUnloadForkRetractDelay.ToString();
                    }
                    else if (this.dispPageNo == 5)
                    {
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.travMotorTorque.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.liftMotorTorque.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.fork1MotorTorque.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.fork2MotorTorque.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.totalOperationTime.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.travOperationTime.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.liftOperationTime.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.fork1OperationTime.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.fork2OperationTime.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.travBrakeOpenCount.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.liftBrakeOpenCount.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.fork1BrakeOpenCount.ToString();
                        this.monitorList[curListCnt++].lbl_value.Content = refState.extState.fork2BrakeOpenCount.ToString();
                    }

                    while (curListCnt < monitorList.Count)
                    {
                        monitorList[curListCnt++].lbl_value.Content = "";
                    }



                    // Compare Data Change
                    for (int i = 0; i < curListCnt; i++)
                    {
                        tmpValue = monitorList[i].lbl_value.Content.ToString();
                        curValue = monitorList[i].oldValue;
                        if (curValue != tmpValue)
                        {
                            monitorList[i].oldValue = tmpValue;
                            monitorList[i].lbl_value.Foreground = Brushes.DarkTurquoise;
                            monitorList[i].lbl_value.FontSize = 15;
                        }
                        else
                        {
                            monitorList[i].lbl_value.Foreground = Brushes.White;
                            monitorList[i].lbl_value.FontSize = 12;
                        }
                    }
                }
                catch (Exception ex)
                {
                    cIniAccess.SaveExLog(0, "EXCEPTION - PageMonitorSRM : " + ex.Message);
                }

            });
        }

        private void Click_DispChange(object sender, RoutedEventArgs e)
        {
            // Handle the button click event here
            ToggleButton toggleButton = sender as ToggleButton;


            if (toggleButton.IsChecked == true)
            {
                if (toggleButton == Btn_LfTr)
                {
                    dispPageNo = 0;
                }
                else if (toggleButton == Btn_Fork1)
                {
                    dispPageNo = 1;
                }
                else if (toggleButton == Btn_Fork2)
                {
                    dispPageNo = 2;
                }
                else if (toggleButton == Btn_SP1)
                {
                    dispPageNo = 3;
                }
                else if (toggleButton == Btn_SP2)
                {
                    dispPageNo = 4;
                }
                else if (toggleButton == Btn_SP3)
                {
                    dispPageNo = 5;
                }
                else
                {
                    dispPageNo = 0;
                }
                display_Change(dispPageNo);
            }
            else
            {
                toggleButton.IsChecked = true;
            }

            Console.WriteLine("SRM Display Button Click Event");
        }

        private void InitializeDataStrList()
        {
            mapListCnt = 0;
            // SRM to GCP Mapping Data
            // UI 리스트 인덱스 ----------- 실제 데이터 맵핑 주소, 타입, 값
            dataStrList[mapListCnt++] = new string[2] { cConstDefine.tr("셋업 모드"), "-" };        // 0
            dataStrList[mapListCnt++] = new string[2] { cConstDefine.tr("강제 모드"), "-" };
            dataStrList[mapListCnt++] = new string[2] { cConstDefine.tr("수동 모드"), "-" };
            dataStrList[mapListCnt++] = new string[2] { cConstDefine.tr("자동 모드"), "-" };

            dataStrList[mapListCnt++] = new string[2] { cConstDefine.tr("작업 요구"), "-" };
            dataStrList[mapListCnt++] = new string[2] { cConstDefine.tr("인버터 접속 상태"), "-" };
            dataStrList[mapListCnt++] = new string[2] { cConstDefine.tr("이상 상태"), "-" };
            dataStrList[mapListCnt++] = new string[2] { cConstDefine.tr("경고 상태"), "-" };
            dataStrList[mapListCnt++] = new string[2] { cConstDefine.tr("비상 정지"), "-" };
            dataStrList[mapListCnt++] = new string[2] { cConstDefine.tr("시작 상태"), "-" };
            dataStrList[mapListCnt++] = new string[2] { cConstDefine.tr("시작 가능"), "-" };

            dataStrList[mapListCnt++] = new string[2] { cConstDefine.tr("비상스위치 상태"), "-" };
            dataStrList[mapListCnt++] = new string[2] { cConstDefine.tr("자동/수동 스위치"), "-" };
            dataStrList[mapListCnt++] = new string[2] { cConstDefine.tr("보수 위치"), "-" };
            dataStrList[mapListCnt++] = new string[2] { cConstDefine.tr("홈 위치"), "-" };

            dataStrList[mapListCnt++] = new string[2] { cConstDefine.tr("장비 동작코드"), "-" };
            dataStrList[mapListCnt++] = new string[2] { cConstDefine.tr("에러 코드"), "-" };
            dataStrList[mapListCnt++] = new string[2] { cConstDefine.tr("경고 코드"), "-" };
            dataStrList[mapListCnt++] = new string[2] { cConstDefine.tr("서브 코드"), "-" };
            dataStrList[mapListCnt++] = new string[2] { cConstDefine.tr("작업 스텝"), "-" };
            
            // Left Table Data Init
            for (int i = 0; i < dataLeft.RowDefinitions.Count && i < mapListCnt; i++)
            {
                if (dataLeft.ColumnDefinitions.Count < 2)
                {
                    VarMessageBox.Show(cConstDefine.tr("변경확인"), cConstDefine.tr("SRM Left 모니터 컬럼 카운트 오류"), VarMessageBoxButton.OK);
                    break;
                }
                if (monitorList.Count > i)
                {
                    // 기존 라벨의 텍스트만 업데이트
                    monitorList[i].lbl_type.Content = dataStrList[i][0];
                }
                else
                {
                    // 새로 생성
                    MonitorData monData = new MonitorData(dataStrList[i][0], dataStrList[i][1]);
                    Grid.SetRow(monData.lbl_type, i);
                    Grid.SetColumn(monData.lbl_type, 0);
                    Grid.SetRow(monData.lbl_value, i);
                    Grid.SetColumn(monData.lbl_value, 1);
                    dataLeft.Children.Add(monData.lbl_type);
                    dataLeft.Children.Add(monData.lbl_value);
                    monitorList.Add(monData);
                }
            }

            idxCnt = dataLeft.RowDefinitions.Count;
        }

        private void TranslationSource_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 언어 변경 시 SRM State 라벨 텍스트 업데이트
            Dispatcher.Invoke(() =>
            {
                InitializeDataStrList();
                // 현재 표시 중인 페이지의 텍스트도 업데이트
                display_Change(dispPageNo);
            });
        }

        private void Click_LeftRight(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;

            if (btn == Btn_Left)
            {
                if (dispPageNo > 0)
                {
                    dispPageNo--;
                }
                else
                {
                    // LastPage No
                    dispPageNo = 5;
                }
            }
            else if (btn == Btn_Right)
            {
                if (dispPageNo < 5) // LastPage No
                {
                    dispPageNo++;
                }
                else
                {
                    dispPageNo = 0;
                }
            }

            display_Change(dispPageNo);
            if (lbl_PageInfo != null)
                lbl_PageInfo.Content = $"{dispPageNo + 1} / 6";
            Console.WriteLine("SRM Left Right Button Click Event");
        }

        private void display_Change(int pageNo)
        {
            Btn_LfTr.IsChecked = false;
            Btn_Fork1.IsChecked = false;
            Btn_Fork2.IsChecked = false;
            Btn_SP1.IsChecked = false;
            Btn_SP2.IsChecked = false;
            Btn_SP3.IsChecked = false;
            int curListCnt = mapListCnt;

            switch (pageNo)
            {
                case 0:
                    Btn_LfTr.IsChecked = true;
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("주행 홈복귀");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("주행 정위치");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("주행 이동방향");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("주행 감속중");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("주행 가속중");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("주행 동작중");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("주행 부하 튜닝중");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("주행 무부하 튜닝중");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("주행 원점확인");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("주행 인버터 알람");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("주행 메인코드");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("주행 서브코드");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("주행 인버터 접속");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("주행 전진감속번호");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("주행 후진감속번호");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("주행 현재위치");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("주행 현재속도");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("주행 목표위치");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("주행 목표속도");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("승강 홈복귀");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("승강 정위치");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("승강 이동방향");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("승강 감속중");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("승강 가속중");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("승강 동작중");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("승강 부하 튜닝중");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("승강 무부하 튜닝중");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("승강 원점확인");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("승강 인버터 알람");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("승강 메인코드");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("승강 서브코드");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("승강 인버터 접속");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("승강 상승감속번호");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("승강 하강감속번호");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("승강 현재위치");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("승강 현재속도");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("승강 목표위치");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("승강 목표속도");

                    while (curListCnt < monitorList.Count)
                    {
                        monitorList[curListCnt++].lbl_type.Content = "";
                    }
                    break;
                case 1:
                    Btn_Fork1.IsChecked = true;
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 현재 STATION");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 현재 BAY");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 현재 LEVEL");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 현재 위치번호");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 우 승강하위치");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 우 승강상위치");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 우 주행정위치");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 좌 승강하위치");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 좌 승강상위치");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 좌 주행정위치");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 우 정위치3");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 우 정위치2");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 우 정위치1");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 좌 정위치3");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 좌 정위치2");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 좌 정위치1");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 중심정위치");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 목적 스테이션");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 목적 ROW");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 목적 BAY");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 목적 LEVEL");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 우측진출가능");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 좌측진출가능");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 화물적재");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 정위치");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 이동방향");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 감속중");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 가속중");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 동작중");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 부하 튜닝중");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 무부하 튜닝중");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 원점확인");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 인버터 알람");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 메인코드");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 서브코드");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 인버터 접속");

                    //monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 화물타입");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 현재위치");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 현재속도");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 목표위치");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 목표속도");
                    while (curListCnt < monitorList.Count)
                    {
                        monitorList[curListCnt++].lbl_type.Content = "";
                    }
                    break;
                case 2:
                    Btn_Fork2.IsChecked = true;
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 현재 STATION");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 현재 BAY");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 현재 LEVEL");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 현재 위치번호");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 우 승강하위치");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 우 승강상위치");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 우 주행정위치");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 좌 승강하위치");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 좌 승강상위치");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 좌 주행정위치");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 우 정위치3");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 우 정위치2");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 우 정위치1");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 좌 정위치3");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 좌 정위치2");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 좌 정위치1");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 중심정위치");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 목적 스테이션");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 목적 ROW");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 목적 BAY");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 목적 LEVEL");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 우측진출가능");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 좌측진출가능");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 화물적재");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 정위치");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 이동방향");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 감속중");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 가속중");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 동작중");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 부하 튜닝중");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 무부하 튜닝중");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 원점확인");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 인버터 알람");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 메인코드");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 서브코드");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 인버터 접속");

                    //monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 화물타입");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 현재위치");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 현재속도");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 목표위치");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 목표속도");
                    while (curListCnt < monitorList.Count)
                    {
                        monitorList[curListCnt++].lbl_type.Content = "";
                    }
                    break;
                case 3:
                    Btn_SP1.IsChecked = true;
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("반송 작업번호");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("반송 태스크번호");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("반송 출발 스테이션");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("반송 출발 ROW");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("반송 출발 BAY");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("반송 출발 LEVEL");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("반송 목적 스테이션");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("반송 목적 ROW");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("반송 목적 BAY");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("반송 목적 LEVEL");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("반송 지령코드");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("반송 처리상태");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("반송 처리단계");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("이동 작업번호");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("이동 목적 스테이션");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("이동 목적 ROW");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("이동 목적 BAY");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("이동 목적 LEVEL");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("이동 처리상태");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("이동 처리단계");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("반송 작업번호");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("반송 태스크번호");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("반송 출발 스테이션");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("반송 출발 ROW");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("반송 출발 BAY");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("반송 출발 LEVEL");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("반송 목적 스테이션");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("반송 목적 ROW");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("반송 목적 BAY");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("반송 목적 LEVEL");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("반송 지령코드");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("반송 처리상태");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("반송 처리단계");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("이동 작업번호");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("이동 목적 스테이션");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("이동 목적 ROW");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("이동 목적 BAY");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("이동 목적 LEVEL");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("이동 처리상태");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("이동 처리단계");
                    while (curListCnt < monitorList.Count)
                    {
                        monitorList[curListCnt++].lbl_type.Content = "";
                    }
                    break;
                case 4:
                    Btn_SP2.IsChecked = true;
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("주행 설정 속도");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("승강 설정 속도");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 설정 속도");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 설정 속도");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("주행 설정 가속도");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("승강 설정 가속도");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 설정 가속도");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 설정 가속도");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("주행 설정 감속도");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("승강 설정 감속도");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 설정 감속도");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 설정 감속도");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("주행 설정 저크");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("승강 설정 저크");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 설정 저크");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 설정 저크");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("로딩 전 이동 지연시간");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("로딩 후 이동 지연시간");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("로딩 전 포크 전개 지연시간");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("로딩 후 포크 전개 지연시간");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("로딩 전 포크 승강 지연시간");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("로딩 후 포크 승강 지연시간");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("로딩 전 포크 후진 지연시간");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("로딩 후 포크 후진 지연시간");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("언로딩 전 이동 지연시간");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("언로딩 후 이동 지연시간");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("언로딩 전 포크 전개 지연시간");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("언로딩 후 포크 전개 지연시간");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("언로딩 전 포크 승강 지연시간");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("언로딩 후 포크 승강 지연시간");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("언로딩 전 포크 후진 지연시간");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("언로딩 후 포크 후진 지연시간");
                    while (curListCnt < monitorList.Count)
                    {
                        monitorList[curListCnt++].lbl_type.Content = "";
                    }
                    break;
                case 5:
                    Btn_SP3.IsChecked = true;
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("주행 모터 토크");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("승강 모터 토크");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 모터 토크");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 모터 토크");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("총 동작 시간");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("주행 동작 시간");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("승강 동작 시간");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 동작 시간");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 동작 시간");

                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("주행 브레이크 해제 횟수");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("승강 브레이크 해제 횟수");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크1 브레이크 해제 횟수");
                    monitorList[curListCnt++].lbl_type.Content = cConstDefine.tr("포크2 브레이크 해제 횟수");
                    while (curListCnt < monitorList.Count)
                    {
                        monitorList[curListCnt++].lbl_type.Content = "";
                    }
                    break;
            }

        }
    }
}
