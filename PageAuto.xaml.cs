using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Media.Animation;

namespace gcp_Wpf
{
    /// <summary>
    /// PageAuto.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PageAuto : Page
    {
        // Define ImageBrush
        ImageBrush mast1 = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/mast.png")));
        ImageBrush mast2 = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/mast2.png")));
        ImageBrush railBg = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/rail.png")));
        ImageBrush box1 = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/rack_sel.png")));

        ImageBrush fCenter = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/fCenter.png")));
        ImageBrush fLeft1 = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/fLeft1.png")));
        ImageBrush fLeft2 = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/fLeft2.png")));
        ImageBrush fLeft3 = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/fLeft3.png")));
        ImageBrush fLeft4 = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/fLeft4.png")));
        ImageBrush fLeft5 = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/fLeft5.png")));
        ImageBrush fLeft6 = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/fLeft6.png")));
        ImageBrush fLeft7 = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/fLeft7.png")));
        ImageBrush fLeft8 = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/fLeft8.png")));
        ImageBrush fLeft9 = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/fLeft9.png")));
        ImageBrush fLeft10 = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/fLeft10.png")));

        ImageBrush fRight1 = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/fRight1.png")));
        ImageBrush fRight2 = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/fRight2.png")));
        ImageBrush fRight3 = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/fRight3.png")));
        ImageBrush fRight4 = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/fRight4.png")));
        ImageBrush fRight5 = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/fRight5.png")));
        ImageBrush fRight6 = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/fRight6.png")));
        ImageBrush fRight7 = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/fRight7.png")));
        ImageBrush fRight8 = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/fRight8.png")));
        ImageBrush fRight9 = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/fRight9.png")));
        ImageBrush fRight10 = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/fRight10.png")));


        //Test Timer
        Timer modeCheckTimer = new Timer();

        public bool b_blockAutoPage = false;            // 오토 페이지외 그래픽 디스플레이 여부 결정

        bool dirMast = false;
        bool operMast = false;
        bool dirCarrige = false;
        bool operCarrige = false;

        int liftDelayStd = 0;
        int liftDelayCnt = 0;

        bool dirFork = false;
        bool operFork = false;
        int forkDirection = 0; // Left Move

        int curBay = 1;
        
        // 애니메이션 실행 상태 추적
        private bool bayAnimationRunning = false;
        private bool levUpAnimationRunning = false;
        private bool levDownAnimationRunning = false;
        private bool forkLeftAnimationRunning = false;
        private bool forkRightAnimationRunning = false;
        
        // 시뮬레이션 플래그
        private bool simulationMode = false; // 시뮬레이션 모드 비활성화
        private int simulationCounter = 0; // 시뮬레이션 카운터
        
        int curLev = 1;
        int rowLimit = 50;
        int columnLimit = 73;

        // 이전 위치 저장 (curPos 변화 감지용)
        int prevTravPos = 0;
        int prevLiftPos = 0;
        int prevForkPos = 0;
        bool isInitialized = false;  // 초기화 완료 플래그



        //Singletone
        singletonClass gClass;
        public PageAuto()
        {
            mast1.Opacity = 0.3;
            mast2.Opacity = 0.3;
            railBg.Opacity = 0.3;
            box1.Opacity = 0.3;
            fCenter.Opacity = 0.3;
            fLeft1.Opacity = 0.3;
            fLeft2.Opacity = 0.3;
            fLeft3.Opacity = 0.3;
            fLeft4.Opacity = 0.3;
            fLeft5.Opacity = 0.3;
            fLeft6.Opacity = 0.3;
            fLeft7.Opacity = 0.3;
            fLeft8.Opacity = 0.3;
            fLeft9.Opacity = 0.3;
            fLeft10.Opacity = 0.3;
            fRight1.Opacity = 0.3;
            fRight2.Opacity = 0.3;
            fRight3.Opacity = 0.3;
            fRight4.Opacity = 0.3;
            fRight5.Opacity = 0.3;
            fRight6.Opacity = 0.3;
            fRight7.Opacity = 0.3;
            fRight8.Opacity = 0.3;
            fRight9.Opacity = 0.3;
            fRight10.Opacity = 0.3;

            InitializeComponent();

            gClass = singletonClass.Instance;

            // to do 타이머 켜는 시점 정리 필요
            modeCheckTimer.Interval = 100; // msecond
            modeCheckTimer.AutoReset = true; // Repeat the timer
            modeCheckTimer.Elapsed += ModeTimer_Elapsed;

            fork.Background = fCenter;

            //fork.Background = fLeft5;

            // to do 모든 이미지 없애기
            //mast.Background = null;
            //fork.Background = null;
            //rail.Background = null;
            //carrige.Background = null;

            // 초기상태
            box.Background = null;
            lbl_load.Background = null;
            lbl_box1.Background = null;
            lbl_box2.Background = null;
            lbl_box3.Background = null;
            lbl_box4.Background = null;


            modeCheckTimer.Start();

            // 이전 이후 베이 숨김
            lbl_preBay.Visibility = Visibility.Hidden;
            lbl_nextBay.Visibility = Visibility.Hidden;
            lbl_downLev.Visibility = Visibility.Hidden;
            lbl_upLev.Visibility = Visibility.Hidden;

            // 화살표 애니메이션은 실제 동작 시에만 시작됨 (operation 변수 체크 후)
        }



        // 오토 페이지 외의 페이지에서 그래픽 디스플레이만 하고 싶을 때 blockPage : true
        public void  PageBlockDisplay(bool blockPage)
        {
            if (blockPage)
            {
                grid_autoInfo.Visibility = Visibility.Collapsed;
                //lbl_curBay.Visibility = Visibility.Hidden;
                //lbl_preBay.Visibility = Visibility.Hidden;
                //lbl_nextBay.Visibility = Visibility.Hidden;
                //lbl_curLev.Visibility = Visibility.Hidden;
                //lbl_downLev.Visibility = Visibility.Hidden;
                //lbl_upLev.Visibility = Visibility.Hidden;
            }
            else
            {
                grid_autoInfo.Visibility = Visibility.Visible;
                //lbl_curBay.Visibility = Visibility.Visible;
                //lbl_preBay.Visibility = Visibility.Visible;
                //lbl_nextBay.Visibility = Visibility.Visible;
                //lbl_curLev.Visibility = Visibility.Visible;
                //lbl_downLev.Visibility = Visibility.Visible;
                //lbl_upLev.Visibility = Visibility.Visible;
            }
        }

        private void ModeTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                try 
                {
                    // For Test - 주행/승강 시뮬레이션 코드 (테스트용, 제거 가능)
                    // 주행 시뮬레이션
                    //if (testTravDirection)
                    //{
                    //    testTravPos += 10;  // 증가
                    //    if (testTravPos >= 5000)
                    //    {
                    //        testTravDirection = false;  // 방향 전환: 감소
                    //        testTravOper = false;  // 잠시 정지
                    //    }
                    //}
                    //else
                    //{
                    //    testTravPos -= 10;  // 감소
                    //    if (testTravPos <= 1000)
                    //    {
                    //        testTravDirection = true;  // 방향 전환: 증가
                    //        testTravOper = false;  // 잠시 정지
                    //    }
                    //}
                    //// 동작 상태 시뮬레이션 (증가/감소 중간에 정지 구간)
                    //if (testTravPos > 2000 && testTravPos < 4000)
                    //{
                    //    testTravOper = true;
                    //}
                    //else
                    //{
                    //    testTravOper = false;
                    //}
                    //gClass.str.SrmState[gClass.srmNum].trav.curPos = testTravPos;
                    //gClass.str.SrmState[gClass.srmNum].trav.trSt1OperState = (byte)(testTravOper ? 1 : 0);

                    //// 승강 시뮬레이션
                    //if (testLiftDirection)
                    //{
                    //    testLiftPos += 5;  // 증가
                    //    if (testLiftPos >= 3000)
                    //    {
                    //        testLiftDirection = false;  // 방향 전환: 감소
                    //        testLiftOper = false;  // 잠시 정지
                    //    }
                    //}
                    //else
                    //{
                    //    testLiftPos -= 5;  // 감소
                    //    if (testLiftPos <= 500)
                    //    {
                    //        testLiftDirection = true;  // 방향 전환: 증가
                    //        testLiftOper = false;  // 잠시 정지
                    //    }
                    //}
                    //// 동작 상태 시뮬레이션 (증가/감소 중간에 정지 구간)
                    //if (testLiftPos > 800 && testLiftPos < 2500)
                    //{
                    //    testLiftOper = true;
                    //}
                    //else
                    //{
                    //    testLiftOper = false;
                    //}
                    //gClass.str.SrmState[gClass.srmNum].lift.curPos = testLiftPos;
                    //gClass.str.SrmState[gClass.srmNum].lift.liSt1OperState = (byte)(testLiftOper ? 1 : 0);
                    // For Test - 시뮬레이션 코드 끝

                    // 동작방향 & 동작체크

                    // Trav Operation 체크
                    bool prevOperMast = operMast;  // 이전 상태 저장
                    if (gClass.str.SrmState[gClass.srmNum].trav.trSt1OperState > 0)          // 주행 동작 중 비트 ON
                    {
                        operMast = true;
                    }
                    else
                    {
                        operMast = false;
                    }

                    // Trav 초기화: 첫 동작 시작 시점에 초기화 (false → true로 변할 때)
                    if (!isInitialized && operMast && !prevOperMast)
                    {
                        prevTravPos = gClass.str.SrmState[gClass.srmNum].trav.curPos;
                        prevLiftPos = gClass.str.SrmState[gClass.srmNum].lift.curPos;
                        isInitialized = true;
                    }

                    // Trav Direction - curPos 변화로만 결정
                    int currentTravPos = gClass.str.SrmState[gClass.srmNum].trav.curPos;
                    if (operMast && isInitialized)  // 초기화 완료되고 동작 중일 때만
                    {
                        if (currentTravPos > prevTravPos)      // curPos 증가 = 우측(전진)
                        {
                            dirMast = true;
                        }
                        else if (currentTravPos < prevTravPos)  // curPos 감소 = 좌측(후진)
                        {
                            dirMast = false;
                        }
                        // 같으면 이전 방향 유지
                    }

                    // Lift Operation 체크
                    bool prevOperCarrige = operCarrige;  // 이전 상태 저장
                    if (gClass.str.SrmState[gClass.srmNum].lift.liSt1OperState > 0)          // 승강 동작 중 비트 ON
                    {
                        operCarrige = true;
                    }
                    else
                    {
                        operCarrige = false;
                    }

                    // Lift 초기화: 첫 동작 시작 시점에 초기화 (false → true로 변할 때, Trav 초기화가 안된 경우)
                    if (!isInitialized && operCarrige && !prevOperCarrige)
                    {
                        prevTravPos = gClass.str.SrmState[gClass.srmNum].trav.curPos;
                        prevLiftPos = gClass.str.SrmState[gClass.srmNum].lift.curPos;
                        isInitialized = true;
                    }

                    // Lift Direction - curPos 변화로만 결정
                    int currentLiftPos = gClass.str.SrmState[gClass.srmNum].lift.curPos;
                    if (operCarrige && isInitialized)  // 초기화 완료되고 동작 중일 때만
                    {
                        if (currentLiftPos > prevLiftPos)      // curPos 증가 = 상승
                        {
                            dirCarrige = true;
                        }
                        else if (currentLiftPos < prevLiftPos)  // curPos 감소 = 하강
                        {
                            dirCarrige = false;
                        }
                        // 같으면 이전 방향 유지
                    }

                    // Fork Direction / Operation
                    if (gClass.str.SrmState[gClass.srmNum].fork1.moveDirec > 0)       // 이동방향 : 0 = 우측, 1 = 좌측
                    {
                        dirFork = true;            // 좌측
                    }
                    else
                    {
                        dirFork = false;             // 우측
                    }

                    if (gClass.str.SrmState[gClass.srmNum].fork1.operState > 0)          // 주행 동작 중 비트 ON
                    {
                        operFork = true;
                    }
                    else
                    {
                        operFork = false;
                    }

                    if (gClass.str.SrmState[gClass.srmNum].fork1.curLev > 4)
                    {
                        if (mast.Background != mast2)
                        {
                            mast.Background = mast2;
                            rail.Background = null;
                            rowLimit = 58;
                            columnLimit = 67;
                        }
                    }
                    else
                    {
                        if (mast.Background != mast1)
                        {
                            mast.Background = mast1;
                            rail.Background = railBg;
                            rowLimit = 50;
                            columnLimit = 70;
                        }
                    }

                    // to do Fork2 구분필요
                    if(gClass.str.SRMIO[gClass.srmNum].GOX1)       // gClass.str.SrmState[gClass.srmNum].fork1.loadState > 0 ||
                    {
                        if(box.Background != box1)
                        {
                            box.Background = box1;
                        }
                    
                        if (lbl_load.Background != box1)
                        {
                            lbl_load.Background = box1;
                        }
                    }
                    else
                    {
                        // 캐리지화물
                        if (box.Background == box1)
                        {
                            box.Background = null;
                        }

                        // 포크화물
                        if (lbl_load.Background == box1)
                        {
                            lbl_load.Background = null;
                        }
                    }

                    //if (gClass.str.SrmState[gClass.srmNum].fork1.posLeftUp > 0 || gClass.str.SrmState[gClass.srmNum].fork1.posRightUp > 0)       // 정위치1 - 승강상위치(좌측)            주행/승강/상/하/좌/우 정위치   ---------- to do 장치상태 - 승강 - 상태1 정위치 비트 동시에 들어오는지. 확인 후 조건처리 필요
                    //{
                    //    if (lbl_top.BorderBrush != Brushes.Green)
                    //    {
                    //        lbl_top.BorderBrush = Brushes.Green;
                    //    }
                    //}
                    //else
                    //{
                    //    if (lbl_top.BorderBrush != Brushes.Gray)
                    //    {
                    //        lbl_top.BorderBrush = Brushes.Gray;
                    //    }
                    //}

                    //if (gClass.str.SrmState[gClass.srmNum].fork1.posLeftBottom > 0 || gClass.str.SrmState[gClass.srmNum].fork1.posRightBottom > 0)       // 정위치1 - 승강하위치(좌측)            주행/승강/상/하/좌/우 정위치   ---------- to do 일단 좌측 상/하 위치로 처리함
                    //{
                    //    if (lbl_bottom.BorderBrush != Brushes.Green)
                    //    {
                    //        lbl_bottom.BorderBrush = Brushes.Green;
                    //    }
                    //}
                    //else
                    //{
                    //    if (lbl_bottom.BorderBrush != Brushes.Gray)
                    //    {
                    //        lbl_bottom.BorderBrush = Brushes.Gray;
                    //    }
                    //}

                    // 랙 화물 표시는 상위치 정지상태에서만 확인---------------------------------------
                    // 승강 동작 X && 상위치 && 센서감지

                    // 우측 상위치 정지
                    if (gClass.str.SrmState[gClass.srmNum].lift.liSt1OperState == 0 && gClass.str.SrmState[gClass.srmNum].trav.trSt1OperState == 0 &&
                    (gClass.str.SrmState[gClass.srmNum].fork1.posLeftUp > 0)  && 
                    (gClass.str.SRMIO[gClass.srmNum].DSTL1 || gClass.str.SRMIO[gClass.srmNum].DSTLe1))
                    {
                        lbl_box2.Background = box1;
                    }
                    else
                    {
                        lbl_box2.Background = null;
                    }

                    // 우측 상위치 정지
                    if (gClass.str.SrmState[gClass.srmNum].lift.liSt1OperState == 0 && gClass.str.SrmState[gClass.srmNum].trav.trSt1OperState == 0 &&
                    (gClass.str.SrmState[gClass.srmNum].fork1.posRightUp > 0) &&
                    (gClass.str.SRMIO[gClass.srmNum].DSTR1 || gClass.str.SRMIO[gClass.srmNum].DSTRe1))
                    {
                        lbl_box3.Background = box1;
                    }
                    else
                    {
                        lbl_box3.Background = null;
                    }

                    //----------------------------------------------------------------------------------

                    // TEST
                    //operMast = true;
                    //operCarrige = true;
                    //dirCarrige = false;
                    //gClass.str.SrmState[gClass.srmNum].fork1.curBay = 5;
                    //gClass.str.SrmState[gClass.srmNum].fork1.curLev = 5;

                    // to do Fork1/2 구분해야함
                    // 현재위치 표시 (기존 참고용)
                    lbl_preBay.Content = gClass.str.SrmState[gClass.srmNum].fork1.curBay - 1;
                    lbl_nextBay.Content = gClass.str.SrmState[gClass.srmNum].fork1.curBay + 1;
                    lbl_downLev.Content = gClass.str.SrmState[gClass.srmNum].fork1.curLev - 1;
                    lbl_upLev.Content = gClass.str.SrmState[gClass.srmNum].fork1.curLev + 1;

                    // 목적위치 계산 (작업 영역과 동작코드를 보고 From/To 판단)
                    int tarBay = 0, tarLev = 0, tarSt = 0;
                    
                    // 작업이 있는지 확인
                    bool hasJob = (gClass.str.SrmState[gClass.srmNum].fork1.jobNo > 0 || gClass.str.SrmState[gClass.srmNum].fork1.mvJobNo > 0);
                    
                    if (hasJob)
                    {
                        int curJobType = (int)gClass.str.SrmState[gClass.srmNum].fork1.cmdCode;
                        int curJobProc = (int)gClass.str.SrmState[gClass.srmNum].fork1.procStep;
                        
                        // mvJobNo가 있으면 이동 작업으로 처리
                        if (gClass.str.SrmState[gClass.srmNum].fork1.mvJobNo > 0)
                        {
                            if (curJobType == 0)
                                curJobType = 1; // 이동
                        }
                        
                        if (curJobType == 0x01)           // 0x01 이동지령
                        {
                            tarSt = gClass.str.SrmState[gClass.srmNum].fork1.mvToStation;
                            tarBay = gClass.str.SrmState[gClass.srmNum].fork1.mvToBay;
                            tarLev = gClass.str.SrmState[gClass.srmNum].fork1.mvToLev;
                        }
                        else if(curJobType == 0x1A)
                        {
                            tarSt = (int)gClass.str.WcsPacket[gClass.srmNum].WCS_PARSE.fork1FromSt;
                            tarBay = (int)gClass.str.WcsPacket[gClass.srmNum].WCS_PARSE.fork1FromBay;
                            tarLev = (int)gClass.str.WcsPacket[gClass.srmNum].WCS_PARSE.fork1FromLev;
                        }
                        else                                                                    // 반송지령
                        {
                            // procStep을 보고 From 수행중인지 To 수행중인지 판단
                            // procStep < 0x07이면 From Step, >= 0x07이면 To Step
                            if (curJobProc < 0x07)        // From Step 수행중
                            {
                                tarSt = gClass.str.SrmState[gClass.srmNum].fork1.fromStation;
                                tarBay = gClass.str.SrmState[gClass.srmNum].fork1.fromBay;
                                tarLev = gClass.str.SrmState[gClass.srmNum].fork1.fromLev;
                            }
                            else                                                                   // To Step 수행중
                            {
                                tarSt = gClass.str.SrmState[gClass.srmNum].fork1.toStation;
                                tarBay = gClass.str.SrmState[gClass.srmNum].fork1.toBay;
                                tarLev = gClass.str.SrmState[gClass.srmNum].fork1.toLev;
                            }
                        }
                    }

                    // 위치 정보 업데이트 (BAY, LEV, FORK)
                    // 현재 위치
                    int curBay = gClass.str.SrmState[gClass.srmNum].fork1.curBay;
                    int curLev = gClass.str.SrmState[gClass.srmNum].fork1.curLev;

                    // 다음 위치 계산 (작업 단계에 따라 from 또는 to 위치)
                    // procStep이 From Step이면 다음 위치는 from 위치, To Step이면 다음 위치는 to 위치
                    
                    // 주행(BAY) 위치 정보 계산 및 표시
                    int travCurPos = gClass.str.SrmState[gClass.srmNum].trav.curPos;
                    int travTarPos = gClass.str.SrmState[gClass.srmNum].trav.targetPos;

                    // 승강(LEV) 위치 정보 계산 및 표시
                    int liftCurPos = gClass.str.SrmState[gClass.srmNum].lift.curPos;
                    int liftTarPos = gClass.str.SrmState[gClass.srmNum].lift.targetPos;
                    
                    int nextLev = 0;

                    if(liftTarPos > 0 && liftTarPos > liftCurPos)
                    {
                        nextLev = curLev + 1;
                    }
                    else if(liftTarPos > 0 && liftTarPos < liftCurPos)
                    {
                        nextLev = curLev - 1;
                    }
                    else
                    {
                        nextLev = 0;
                    }


                    // 포크(FORK) 위치 정보 계산 및 표시
                    sbyte forkCurPosNum = gClass.str.SrmState[gClass.srmNum].fork1.curPosNum;
                    int forkCurPos = gClass.str.SrmState[gClass.srmNum].fork1.curPos;
                    int forkTarPos = gClass.str.SrmState[gClass.srmNum].fork1.targetPos;
                    byte forkOriginPos = gClass.str.SrmState[gClass.srmNum].fork1.originPos;
                    byte forkOperaton = gClass.str.SrmState[gClass.srmNum].fork1.operState;
                    int forkType = gClass.str.SrmInfo[gClass.srmNum].forkType;
                    
                    // 현재 위치와 다음 위치 계산
                    string dispForkPos1 = "-";
                    string dispForkPos2 = "-";

                    // 다음 위치 계산 (목적지 방향으로)
                    if (forkOperaton > 0)
                    {
                        int nextPosNum = forkCurPosNum;
                        if (forkTarPos > forkCurPos)
                        {
                            // 우측으로 이동
                            dispForkPos1 = GetForkPositionString(forkCurPosNum - 1, forkType);
                            dispForkPos1 = GetForkPositionString(forkCurPosNum, forkType);
                        }
                        else if (forkTarPos < forkCurPos)
                        {
                            // 좌측으로 이동
                            dispForkPos1 = GetForkPositionString(forkCurPosNum + 1, forkType);
                            dispForkPos1 = GetForkPositionString(forkCurPosNum, forkType);
                        }
                        else
                        {
                            dispForkPos1 = GetForkPositionString(forkCurPosNum, forkType);
                            dispForkPos2 = "-";
                        }
                    }
                    else
                    {
                        dispForkPos1 = GetForkPositionString(forkCurPosNum, forkType);
                        dispForkPos2 = "-";
                    }
                    
                    // 목적위치: tarStation이나 tarBay 중 0이 아닌 값 사용
                    int targetLev = tarLev > 0 ? tarLev : 0;
                    // 위치 정보 표시 업데이트
                    run_curBay.Content = curBay.ToString();
                    // 목적위치가 스테이션이면 "ST1" 형식으로, Bay면 숫자로 표시
                    run_tarBay.Content = tarSt > 0 ? $"ST{tarSt}" : (tarBay > 0 ? tarBay.ToString() : "-");
                    
                    run_curLev.Content = curLev.ToString();
                    run_tarLev.Content = targetLev > 0 ? targetLev.ToString() : "-";
                    
                    run_curFork.Content = dispForkPos1;
                    run_tarFork.Content = dispForkPos2;
                    
                    // Operation 변수 체크 및 애니메이션 제어
                    bool travOper = simulationMode || (gClass.str.SrmState[gClass.srmNum].trav.trSt1OperState == 1);
                    bool liftOper = simulationMode || (gClass.str.SrmState[gClass.srmNum].lift.liSt1OperState == 1);
                    bool forkOper = simulationMode || (gClass.str.SrmState[gClass.srmNum].fork1.operState == 1);
                    
                    // 시뮬레이션: 승강 상승/하강 전환 (3초마다)
                    if (simulationMode)
                    {
                        simulationCounter++;
                        if (simulationCounter >= 30) // 100ms * 30 = 3초
                        {
                            simulationCounter = 0;
                        }
                    }
                    
                    // 주행(BAY) 화살표 애니메이션 제어
                    if (travOper)
                    {
                        // 애니메이션이 실행 중이 아니면 시작
                        if (!bayAnimationRunning)
                        {
                            StartBayArrowAnimation();
                            bayAnimationRunning = true;
                        }
                    }
                    else
                    {
                        // 애니메이션 중지
                        if (bayAnimationRunning)
                        {
                            lbl_bayArrow1.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_bayArrow2.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_bayArrow3.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_bayArrow4.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_bayArrow5.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_bayArrow6.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_bayArrow7.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_bayArrow8.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_bayArrow9.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_bayArrow10.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_bayArrow1.Opacity = 0;
                            lbl_bayArrow2.Opacity = 0;
                            lbl_bayArrow3.Opacity = 0;
                            lbl_bayArrow4.Opacity = 0;
                            lbl_bayArrow5.Opacity = 0;
                            lbl_bayArrow6.Opacity = 0;
                            lbl_bayArrow7.Opacity = 0;
                            lbl_bayArrow8.Opacity = 0;
                            lbl_bayArrow9.Opacity = 0;
                            lbl_bayArrow10.Opacity = 0;
                            bayAnimationRunning = false;
                        }
                    }
                    
                    // 승강(LEV) 화살표 애니메이션 제어 (상승/하강 분리)
                    if (liftOper)
                    {
                        // 시뮬레이션 모드에서는 카운터로 상승/하강 전환
                        bool isLifting = simulationMode ? (simulationCounter < 15) : (liftCurPos > prevLiftPos);
                        bool isLowering = simulationMode ? (simulationCounter >= 15) : (liftCurPos < prevLiftPos);
                        
                        // 위치값 비교하여 상승/하강 판단
                        if (isLifting)
                        {
                            // 상승 중
                            if (!levUpAnimationRunning)
                            {
                                // 하강 애니메이션 중지
                                if (levDownAnimationRunning)
                                {
                                    lbl_levDownArrow1.BeginAnimation(UIElement.OpacityProperty, null);
                                    lbl_levDownArrow2.BeginAnimation(UIElement.OpacityProperty, null);
                                    lbl_levDownArrow3.BeginAnimation(UIElement.OpacityProperty, null);
                                    lbl_levDownArrow4.BeginAnimation(UIElement.OpacityProperty, null);
                                    lbl_levDownArrow5.BeginAnimation(UIElement.OpacityProperty, null);
                                    lbl_levDownArrow1.Opacity = 0;
                                    lbl_levDownArrow2.Opacity = 0;
                                    lbl_levDownArrow3.Opacity = 0;
                                    lbl_levDownArrow4.Opacity = 0;
                                    lbl_levDownArrow5.Opacity = 0;
                                    levDownAnimationRunning = false;
                                }
                                StartLevUpArrowAnimation();
                                levUpAnimationRunning = true;
                            }
                        }
                        else if (isLowering)
                        {
                            // 하강 중
                            if (!levDownAnimationRunning)
                            {
                                // 상승 애니메이션 중지
                                if (levUpAnimationRunning)
                                {
                                    lbl_levUpArrow1.BeginAnimation(UIElement.OpacityProperty, null);
                                    lbl_levUpArrow2.BeginAnimation(UIElement.OpacityProperty, null);
                                    lbl_levUpArrow3.BeginAnimation(UIElement.OpacityProperty, null);
                                    lbl_levUpArrow4.BeginAnimation(UIElement.OpacityProperty, null);
                                    lbl_levUpArrow5.BeginAnimation(UIElement.OpacityProperty, null);
                                    lbl_levUpArrow1.Opacity = 0;
                                    lbl_levUpArrow2.Opacity = 0;
                                    lbl_levUpArrow3.Opacity = 0;
                                    lbl_levUpArrow4.Opacity = 0;
                                    lbl_levUpArrow5.Opacity = 0;
                                    levUpAnimationRunning = false;
                                }
                                StartLevDownArrowAnimation();
                                levDownAnimationRunning = true;
                            }
                        }
                    }
                    else
                    {
                        // 애니메이션 중지
                        if (levUpAnimationRunning)
                        {
                            lbl_levUpArrow1.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_levUpArrow2.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_levUpArrow3.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_levUpArrow4.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_levUpArrow5.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_levUpArrow1.Opacity = 0;
                            lbl_levUpArrow2.Opacity = 0;
                            lbl_levUpArrow3.Opacity = 0;
                            lbl_levUpArrow4.Opacity = 0;
                            lbl_levUpArrow5.Opacity = 0;
                            levUpAnimationRunning = false;
                        }
                        if (levDownAnimationRunning)
                        {
                            lbl_levDownArrow1.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_levDownArrow2.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_levDownArrow3.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_levDownArrow4.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_levDownArrow5.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_levDownArrow1.Opacity = 0;
                            lbl_levDownArrow2.Opacity = 0;
                            lbl_levDownArrow3.Opacity = 0;
                            lbl_levDownArrow4.Opacity = 0;
                            lbl_levDownArrow5.Opacity = 0;
                            levDownAnimationRunning = false;
                        }
                    }
                    
                    // 포크(FORK) 화살표 애니메이션 제어 (좌측/우측 분리)
                    if (forkOper)
                    {
                        // 시뮬레이션 모드에서는 카운터로 좌측/우측 전환
                        bool isMovingLeft = simulationMode ? (simulationCounter < 15) : (forkCurPos < prevForkPos);
                        bool isMovingRight = simulationMode ? (simulationCounter >= 15) : (forkCurPos > prevForkPos);
                        
                        if (isMovingLeft)
                        {
                            // 좌측으로 이동 중
                            if (!forkLeftAnimationRunning)
                            {
                                // 우측 애니메이션 중지
                                if (forkRightAnimationRunning)
                                {
                                    lbl_forkRightArrow1.BeginAnimation(UIElement.OpacityProperty, null);
                                    lbl_forkRightArrow2.BeginAnimation(UIElement.OpacityProperty, null);
                                    lbl_forkRightArrow3.BeginAnimation(UIElement.OpacityProperty, null);
                                    lbl_forkRightArrow4.BeginAnimation(UIElement.OpacityProperty, null);
                                    lbl_forkRightArrow5.BeginAnimation(UIElement.OpacityProperty, null);
                                    lbl_forkRightArrow1.Opacity = 0;
                                    lbl_forkRightArrow2.Opacity = 0;
                                    lbl_forkRightArrow3.Opacity = 0;
                                    lbl_forkRightArrow4.Opacity = 0;
                                    lbl_forkRightArrow5.Opacity = 0;
                                    forkRightAnimationRunning = false;
                                }
                                StartForkLeftArrowAnimation();
                                forkLeftAnimationRunning = true;
                            }
                        }
                        else if (isMovingRight)
                        {
                            // 우측으로 이동 중
                            if (!forkRightAnimationRunning)
                            {
                                // 좌측 애니메이션 중지
                                if (forkLeftAnimationRunning)
                                {
                                    lbl_forkLeftArrow1.BeginAnimation(UIElement.OpacityProperty, null);
                                    lbl_forkLeftArrow2.BeginAnimation(UIElement.OpacityProperty, null);
                                    lbl_forkLeftArrow3.BeginAnimation(UIElement.OpacityProperty, null);
                                    lbl_forkLeftArrow4.BeginAnimation(UIElement.OpacityProperty, null);
                                    lbl_forkLeftArrow5.BeginAnimation(UIElement.OpacityProperty, null);
                                    lbl_forkLeftArrow1.Opacity = 0;
                                    lbl_forkLeftArrow2.Opacity = 0;
                                    lbl_forkLeftArrow3.Opacity = 0;
                                    lbl_forkLeftArrow4.Opacity = 0;
                                    lbl_forkLeftArrow5.Opacity = 0;
                                    forkLeftAnimationRunning = false;
                                }
                                StartForkRightArrowAnimation();
                                forkRightAnimationRunning = true;
                            }
                        }
                    }
                    else
                    {
                        // 애니메이션 중지
                        if (forkLeftAnimationRunning)
                        {
                            lbl_forkLeftArrow1.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_forkLeftArrow2.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_forkLeftArrow3.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_forkLeftArrow4.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_forkLeftArrow5.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_forkLeftArrow1.Opacity = 0;
                            lbl_forkLeftArrow2.Opacity = 0;
                            lbl_forkLeftArrow3.Opacity = 0;
                            lbl_forkLeftArrow4.Opacity = 0;
                            lbl_forkLeftArrow5.Opacity = 0;
                            forkLeftAnimationRunning = false;
                        }
                        if (forkRightAnimationRunning)
                        {
                            lbl_forkRightArrow1.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_forkRightArrow2.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_forkRightArrow3.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_forkRightArrow4.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_forkRightArrow5.BeginAnimation(UIElement.OpacityProperty, null);
                            lbl_forkRightArrow1.Opacity = 0;
                            lbl_forkRightArrow2.Opacity = 0;
                            lbl_forkRightArrow3.Opacity = 0;
                            lbl_forkRightArrow4.Opacity = 0;
                            lbl_forkRightArrow5.Opacity = 0;
                            forkRightAnimationRunning = false;
                        }
                    }
                    
                    // 이전 위치 업데이트 (curPos 변화 감지용)
                    prevLiftPos = liftCurPos;
                    prevForkPos = forkCurPos;

                    // 현재 작업 정보 표시
                    if (gClass.str.SrmState[gClass.srmNum].fork1.jobNo > 0U || gClass.str.SrmState[gClass.srmNum].fork1.mvJobNo > 0U)
                    {
                        uint curJobNo = gClass.str.SrmState[gClass.srmNum].fork1.jobNo;
                        int curFromStn = (int)gClass.str.SrmState[gClass.srmNum].fork1.fromStation;
                        int curFromRow = (int)gClass.str.SrmState[gClass.srmNum].fork1.fromRow;
                        int curFromBay = (int)gClass.str.SrmState[gClass.srmNum].fork1.fromBay;
                        int curFromLev = (int)gClass.str.SrmState[gClass.srmNum].fork1.fromLev;
                        int curToStn = (int)gClass.str.SrmState[gClass.srmNum].fork1.toStation;
                        int curToRow = (int)gClass.str.SrmState[gClass.srmNum].fork1.toRow;
                        int curToBay = (int)gClass.str.SrmState[gClass.srmNum].fork1.toBay;
                        int curToLev = (int)gClass.str.SrmState[gClass.srmNum].fork1.toLev;
                        int curJobType = (int)gClass.str.SrmState[gClass.srmNum].fork1.cmdCode;
                        int curJobState = (int)gClass.str.SrmState[gClass.srmNum].fork1.procState;
                        int curJobProc = (int)gClass.str.SrmState[gClass.srmNum].fork1.procStep;
                        
                        if (gClass.str.SrmState[gClass.srmNum].fork1.mvJobNo > 0U)
                        {
                            curJobNo = gClass.str.SrmState[gClass.srmNum].fork1.mvJobNo;
                            if (curJobType == 0)
                                curJobType = 1;
                        }
                        
                        string curJob = "";
                        switch (curJobType)
                        {
                            case 0:
                                curJob = cConstDefine.tr("작업없음");
                                break;
                            case 1:
                                curJob = cConstDefine.tr("이동");
                                curToStn = (int)gClass.str.SrmState[gClass.srmNum].fork1.mvToStation;
                                curToRow = (int)gClass.str.SrmState[gClass.srmNum].fork1.mvToRow;
                                curToBay = (int)gClass.str.SrmState[gClass.srmNum].fork1.mvToBay;
                                curToLev = (int)gClass.str.SrmState[gClass.srmNum].fork1.mvToLev;
                                curJobState = (int)gClass.str.SrmState[gClass.srmNum].fork1.mvProcState;
                                curJobProc = (int)gClass.str.SrmState[gClass.srmNum].fork1.mvProcStep;
                                break;
                            case 2:
                                curJob = cConstDefine.tr("로딩");
                                break;
                            case 3:
                                curJob = cConstDefine.tr("언로딩");
                                break;
                            case 18:
                                curJob = cConstDefine.tr("입고");
                                break;
                            case 19:
                                curJob = cConstDefine.tr("출고");
                                break;
                            case 20:
                                curJob = cConstDefine.tr("렉간반송");
                                break;
                            case 21:
                                curJob = cConstDefine.tr("스테이션이동");
                                break;
                            case 22:
                                curJob = cConstDefine.tr("렉변경");
                                break;
                            case 23:
                                curJob = cConstDefine.tr("스테이션변경");
                                break;
                            case 26:
                                curJob = cConstDefine.tr("Sticky 명령");
                                curFromRow = (int)gClass.str.WcsPacket[gClass.srmNum].WCS_PARSE.fork1FromRow;
                                curFromBay = (int)gClass.str.WcsPacket[gClass.srmNum].WCS_PARSE.fork1FromBay;
                                curFromLev = (int)gClass.str.WcsPacket[gClass.srmNum].WCS_PARSE.fork1FromLev;
                                break;
                            default:
                                curJob = "-";
                                break;
                        }
                        
                        string curFrom = curFromStn <= 0 ? $"R:{curFromRow}/{curFromBay}/{curFromLev}" : $"ST:{curFromStn}";
                        string curTo = curToStn <= 0 ? $"R:{curToRow}/{curToBay}/{curToLev}" : $"ST:{curToStn}";
                        lbl_curJob.Text = $"{curJob} #{curJobNo} {curFrom}→{curTo}";
                        
                        string curState = "";
                        switch (curJobState)
                        {
                            case 2:
                                curState = cConstDefine.tr("수행중");
                                break;
                            case 3:
                                curState = "실패";
                                break;
                            case 4:
                                curState = cConstDefine.tr("작업완료");
                                break;
                            case 5:
                                curState = cConstDefine.tr("중지");
                                break;
                            default:
                                curState = "-";
                                break;
                        }
                        
                        string curProc = "";
                        switch (curJobProc)
                        {
                            case 1:
                                curProc = cConstDefine.tr("지령수신");
                                break;
                            case 2:
                                curProc = cConstDefine.tr("From 위치로 이동 중");
                                break;
                            case 3:
                                curProc = cConstDefine.tr("From 위치 도착");
                                break;
                            case 4:
                                curProc = cConstDefine.tr("From 위치에서 포크진입");
                                break;
                            case 5:
                                curProc = cConstDefine.tr("From 위치에서 캐리지상승");
                                break;
                            case 6:
                                curProc = cConstDefine.tr("From 위치에서 포크복귀");
                                break;
                            case 7:
                                curProc = cConstDefine.tr("To 위치로 이동 중");
                                break;
                            case 8:
                                curProc = cConstDefine.tr("To 위치 도착");
                                break;
                            case 9:
                                curProc = cConstDefine.tr("To 위치에서 포크진입");
                                break;
                            case 10:
                                curProc = cConstDefine.tr("To 위치에서 캐리지하강");
                                break;
                            case 11:
                                curProc = cConstDefine.tr("To 위치에서 포크복귀");
                                break;
                            case 12:
                                curProc = cConstDefine.tr("화물 적재 완료");
                                break;
                            case 13:
                                curProc = cConstDefine.tr("화물 이재 완료");
                                break;
                            case 16 /*0x10*/:
                                curProc = cConstDefine.tr("Sticky From 위치로 이동중");
                                break;
                            case 17:
                                curProc = cConstDefine.tr("Sticky From 위치 도착");
                                break;
                            case 18:
                                curProc = cConstDefine.tr("Sticky From 위치에서 포크진입");
                                break;
                            case 19:
                                curProc = cConstDefine.tr("Sticky From 위치에서 캐리지 상승");
                                break;
                            case 20:
                                curProc = cConstDefine.tr("Sticky From 위치에서 캐리지 하강");
                                break;
                            case 21:
                                curProc = cConstDefine.tr("Sticky From 위치에서 포크복귀");
                                break;
                            case 22:
                                curProc = cConstDefine.tr("Sticky 동작 완료");
                                break;
                            case 129:
                                curProc = cConstDefine.tr("지령수신");
                                break;
                            case 130:
                                curProc = cConstDefine.tr("위치로 이동중");
                                break;
                            case 131:
                                curProc = cConstDefine.tr("위치 도착");
                                break;
                            case 132:
                                curProc = cConstDefine.tr("Loading 포크진입");
                                break;
                            case 133:
                                curProc = cConstDefine.tr("Loading 캐리지상승");
                                break;
                            case 134:
                                curProc = cConstDefine.tr("Loading 포크복귀");
                                break;
                            case 135:
                                curProc = cConstDefine.tr("Unloading 포크진입");
                                break;
                            case 136:
                                curProc = cConstDefine.tr("Unloading 캐리지하강");
                                break;
                            case 137:
                                curProc = cConstDefine.tr("Unloading 포크복귀");
                                break;
                            default:
                                curProc = "-";
                                break;
                        }
                        lbl_curJobState.Text = $"{curState} / {curProc}";
                    }
                    else
                    {
                        lbl_curJob.Text = cConstDefine.tr("작업없음");
                        lbl_curJobState.Text = "-";
                    }
                    
                    // WCS 수신 작업 정보 표시
                    // WCS Req 영역: WCS_PARSE 데이터 사용
                    if (gClass.str.WcsPacket[gClass.srmNum].WCS_PARSE.fork1JobNo > 0)
                    {
                        ushort reqJobNo = gClass.str.WcsPacket[gClass.srmNum].WCS_PARSE.fork1JobNo;
                        int reqFromStn = (int)gClass.str.WcsPacket[gClass.srmNum].WCS_PARSE.fork1FromSt;
                        int reqFromRow = (int)gClass.str.WcsPacket[gClass.srmNum].WCS_PARSE.fork1FromRow;
                        int reqFromBay = (int)gClass.str.WcsPacket[gClass.srmNum].WCS_PARSE.fork1FromBay;
                        int reqFromLev = (int)gClass.str.WcsPacket[gClass.srmNum].WCS_PARSE.fork1FromLev;
                        int reqToStn = (int)gClass.str.WcsPacket[gClass.srmNum].WCS_PARSE.fork1ToSt;
                        int reqToRow = (int)gClass.str.WcsPacket[gClass.srmNum].WCS_PARSE.fork1ToRow;
                        int reqToBay = (int)gClass.str.WcsPacket[gClass.srmNum].WCS_PARSE.fork1ToBay;
                        int reqToLev = (int)gClass.str.WcsPacket[gClass.srmNum].WCS_PARSE.fork1ToLev;
                        
                        string reqJob = "";
                        // fork1JobCmd의 비트 플래그 확인
                        if (gClass.str.WcsPacket[gClass.srmNum].WCS_PARSE.fork1Sticky == 1)
                        {
                            reqJob = cConstDefine.tr("Sticky 명령");
                        }
                        else if (gClass.str.WcsPacket[gClass.srmNum].WCS_PARSE.fork1ChangeSt == 1)
                        {
                            reqJob = cConstDefine.tr("스테이션변경");
                        }
                        else if (gClass.str.WcsPacket[gClass.srmNum].WCS_PARSE.fork1ChangeRack == 1)
                        {
                            reqJob = cConstDefine.tr("렉변경");
                        }
                        else if (gClass.str.WcsPacket[gClass.srmNum].WCS_PARSE.fork1StToSt == 1)
                        {
                            reqJob = cConstDefine.tr("스테이션이동");
                        }
                        else if (gClass.str.WcsPacket[gClass.srmNum].WCS_PARSE.fork1RackToRack == 1)
                        {
                            reqJob = cConstDefine.tr("렉간반송");
                        }
                        else if (gClass.str.WcsPacket[gClass.srmNum].WCS_PARSE.fork1Retrieval == 1)
                        {
                            reqJob = cConstDefine.tr("출고");
                        }
                        else if (gClass.str.WcsPacket[gClass.srmNum].WCS_PARSE.fork1Storage == 1)
                        {
                            reqJob = cConstDefine.tr("입고");
                        }
                        else if (gClass.str.WcsPacket[gClass.srmNum].WCS_PARSE.fork1Move == 1)
                        {
                            reqJob = cConstDefine.tr("이동");
                        }
                        else
                        {
                            reqJob = cConstDefine.tr("작업없음");
                        }
                        
                        string reqFrom = reqFromStn <= 0 ? $"R:{reqFromRow}/{reqFromBay}/{reqFromLev}" : $"ST:{reqFromStn}";
                        string reqTo = reqToStn <= 0 ? $"R:{reqToRow}/{reqToBay}/{reqToLev}" : $"ST:{reqToStn}";
                        lbl_wcsReqJob.Text = $"{reqJob} #{reqJobNo} {reqFrom}→{reqTo}";
                    }
                    else
                    {
                        lbl_wcsReqJob.Text = cConstDefine.tr("작업없음");
                    }

                    if (operMast)
                    {
                        if (dirMast)            // 전진
                        {
                            if (Grid.GetColumn(mast) > columnLimit)      // Column 80개 이미지 Span 40
                            {
                                Grid.SetColumn(mast, 0);
                            }
                            else
                            {
                                Grid.SetColumn(mast, Grid.GetColumn(mast) + 1);
                            }
                        }
                        else
                        {
                            if (Grid.GetColumn(mast) < 1)
                            {
                                Grid.SetColumn(mast, columnLimit);
                            }
                            else
                            {
                                Grid.SetColumn(mast, Grid.GetColumn(mast) - 1);
                            }
                        }
                    }
                    else
                    {
                        Grid.SetColumn(mast, 5);
                    }


                    if (gClass.str.SRMIO[gClass.srmNum].FOKL1 || gClass.str.SRMIO[gClass.srmNum].FOKR1) //  FOKL/R 감지로 봐도 될듯.
                    {
                        liftDelayStd = 3;       // 포크 OUT 후 상승은 느리게
                    }
                    else
                    {
                        liftDelayStd = 0;
                    }


                    Grid.SetColumn(lbl_carrige, Grid.GetColumn(mast) + 11);      // 캐리지 위치는 마스트 +11
                    Grid.SetColumn(box, Grid.GetColumn(mast) + 15);      // 화물 위치는 마스트 +15
                    if (operCarrige)
                    {
                        if(liftDelayStd <= liftDelayCnt)
                        {
                            liftDelayCnt = 0;
                            if (dirCarrige)            // 상승
                            {
                                if (Grid.GetRow(lbl_carrige) < 1)      // Row 80개  구동범위   0~60
                                {
                                    Grid.SetRow(lbl_carrige, rowLimit);
                                }
                                else
                                {
                                    Grid.SetRow(lbl_carrige, Grid.GetRow(lbl_carrige) - 1);
                                }
                            }
                            else
                            {
                                if (Grid.GetRow(lbl_carrige) > rowLimit)      // Row 80개  구동범위   0~60
                                {
                                    Grid.SetRow(lbl_carrige, 0);
                                }
                                else
                                {
                                    Grid.SetRow(lbl_carrige, Grid.GetRow(lbl_carrige) + 1);
                                }
                            }
                        }
                        else
                        {
                            liftDelayCnt += 1;
                        }
                    }
                    else
                    {   // 정지 해 있을 때 마스트 높이
                        liftDelayCnt = 0;

                        if (gClass.str.SrmState[gClass.srmNum].lift.curPos < 1100)      // 기준위치 1000임
                        {
                            Grid.SetRow(lbl_carrige, rowLimit);
                        }
                        else
                        {
                            Grid.SetRow(lbl_carrige, 30);
                        }
                    }

                    // 화물 위치는 캐리지 높이를 따라감
                    Grid.SetRow(box, Grid.GetRow(lbl_carrige) + 2);

                    // to do Fork2 관련 작업 추가할 건지 선택으로 볼지....
                    ForkPositionCheck(gClass.str.SrmInfo[gClass.srmNum].row, dirFork, operFork, gClass.str.SrmState[gClass.srmNum].fork1.curPosNum);

                    // 이전 위치 업데이트 (다음 프레임에서 curPos 변화 감지용)
                    prevTravPos = gClass.str.SrmState[gClass.srmNum].trav.curPos;
                    prevLiftPos = gClass.str.SrmState[gClass.srmNum].lift.curPos;

                    // 구동 중 이 아닐 경우  현재위치 베이값 파싱하여 정위치가 아닐경우 중간위치 배치 (베이값 / 80 ) 하여 대략적인 위치 지정
                    // 구동 중 일 경우 구동방향 파악하여 현재위치 타겟 베이로 이동. 부드러운 이동을 위하여 위치 절대이동으로 표시하지 않을 예정 (고민중)


                    //Console.WriteLine("AutoPage Timer After");
                }
                catch (Exception ex)
                {
                    cIniAccess.SaveExLog(0, "EXCEPTION - PageAutoTimer : " + ex.Message);
                }


            });
        }


        // posNum을 위치 표시 문자열로 변환하는 메소드
        private string GetForkPositionString(int posNum, int type)
        {
            switch (type)
            {
                case 1: // Type 1: -1, 0, 1
                    switch (posNum)
                    {
                        case -1: return "LF";
                        case 0: return "C";
                        case 1: return "RF";
                        default: return "-";
                    }
                case 2: // Type 2: -2, -1, 0, 1, 2
                    switch (posNum)
                    {
                        case -2: return "LF";
                        case -1: return "LH";
                        case 0: return "C";
                        case 1: return "RH";
                        case 2: return "RF";
                        default: return "-";
                    }
                case 3: // Type 3: -3, -2, -1, 0, 1, 2, 3
                    switch (posNum)
                    {
                        case -3: return "LF";
                        case -2: return "LM";
                        case -1: return "LH";
                        case 0: return "C";
                        case 1: return "RH";
                        case 2: return "RM";
                        case 3: return "RF";
                        default: return "-";
                    }
                default:
                    return "-";
            }
        }

        private void ForkPositionCheck(int rowCnt, bool direction, bool operation, sbyte posNo)
        {
            int divideCnt = 5;
            int posNum = 0;         // 포크 포지션 설정 위치
            int posCnt = 0;
            switch (rowCnt)
            {
                case 2:
                    divideCnt = 5;
                    break;
                case 4:
                case 6:
                    divideCnt = 10;
                    break;
            }

            if(gClass.str.SrmState[gClass.srmNum].fork1.curPos < 0)
            {
                posCnt = gClass.str.SrmInfo[gClass.srmNum].forkLeftLimit / divideCnt;       //  마이너스 카운트
                if (posNo == 0)      // Fork Position CENTER
                {
                    posNum = 0;
                }
                else
                {
                    for (int i = 1; i <= divideCnt; i++)
                    {
                        if (gClass.str.SrmState[gClass.srmNum].fork1.curPos < (posCnt * i))
                        {
                            posNum = -i;
                            continue;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            else
            {
                posCnt = Math.Abs(gClass.str.SrmInfo[gClass.srmNum].forkRightLimit) / divideCnt;
                if (posNo == 0)      // Fork Position CENTER
                {
                    posNum = 0;
                }
                else
                {
                    posCnt = gClass.str.SrmInfo[gClass.srmNum].forkRightLimit / divideCnt;
                    for (int i = 1; i < divideCnt; i++)
                    {
                        if (gClass.str.SrmState[gClass.srmNum].fork1.curPos > (posCnt * i))
                        {
                            posNum = i;
                            continue;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            if (!operation)     // 정지 정위치 확인
            {
                if(gClass.str.SrmState[gClass.srmNum].fork1.originPos > 0)
                {
                    switch (posNo)      // Fork Position CENTER
                    {
                        case 0:
                            posNum = 0;
                            break;
                        case -1:
                            posNum = -5;
                            break;
                        case -2:
                            posNum = -10;
                            break;
                        case -3:
                            posNum = -10;
                            break;
                        case 1:
                            posNum = 5;
                            break;
                        case 2:
                            posNum = 10;
                            break;
                        case 3:
                            posNum = 10;
                            break;
                    }
                }
            }

            switch (posNum) 
            {
                case 0:
                    fork.Background = fCenter;
                    break;
                case -1:
                    fork.Background = fLeft1;
                    break;
                case -2:
                    fork.Background = fLeft2;
                    break;
                case -3:
                    fork.Background = fLeft3;
                    break;
                case -4:
                    fork.Background = fLeft4;
                    break;
                case -5:
                    fork.Background = fLeft5;
                    break;
                case -6:
                    fork.Background = fLeft6;
                    break;
                case -7:
                    fork.Background = fLeft7;
                    break;
                case -8:
                    fork.Background = fLeft8;
                    break;
                case -9:
                    fork.Background = fLeft9;
                    break;
                case -10:
                    fork.Background = fLeft10;
                    break;
                case 1:
                    fork.Background = fRight1;
                    break;
                case 2:
                    fork.Background = fRight2;
                    break;
                case 3:
                    fork.Background = fRight3;
                    break;
                case 4:
                    fork.Background = fRight4;
                    break;
                case 5:
                    fork.Background = fRight5;
                    break;
                case 6:
                    fork.Background = fRight6;
                    break;
                case 7:
                    fork.Background = fRight7;
                    break;
                case 8:
                    fork.Background = fRight8;
                    break;
                case 9:
                    fork.Background = fRight9;
                    break;
                case 10:
                    fork.Background = fRight10;
                    break;
            }
        }


        // 주행 방향 반전 설정 적용 (기능 제거됨)
        //private void ApplyTravDirectionReverse()
        //{
        //    bool isReversed = gClass.str.SrmInfo[gClass.srmNum].travDirectionReverse > 0;
        //    
        //    // mast 이미지의 ScaleTransform을 가져와서 ScaleX 설정
        //    if (mast.Background is ImageBrush mastBrush)
        //    {
        //        if (mastBrush.RelativeTransform is TransformGroup transformGroup)
        //        {
        //            foreach (Transform transform in transformGroup.Children)
        //            {
        //                if (transform is ScaleTransform scaleTransform)
        //                {
        //                    scaleTransform.ScaleX = isReversed ? -1 : 1;
        //                    break;
        //                }
        //            }
        //        }
        //    }

        //    // rail 이미지도 반전
        //    if (rail.Background is ImageBrush railBrush)
        //    {
        //        if (railBrush.RelativeTransform is TransformGroup transformGroup)
        //        {
        //            foreach (Transform transform in transformGroup.Children)
        //            {
        //                if (transform is ScaleTransform scaleTransform)
        //                {
        //                    scaleTransform.ScaleX = isReversed ? -1 : 1;
        //                    break;
        //                }
        //            }
        //        }
        //    }
        //}

        // BAY 화살표 흐르는 애니메이션 시작
        private void StartBayArrowAnimation()
        {
            // 각 화살표에 대한 애니메이션 생성 (10개)
            Storyboard storyboard = new Storyboard
            {
                RepeatBehavior = RepeatBehavior.Forever,
                Duration = new Duration(TimeSpan.FromSeconds(1.0))
            };

            // 기존 애니메이션 중지 및 제거
            lbl_bayArrow1.BeginAnimation(UIElement.OpacityProperty, null);
            lbl_bayArrow2.BeginAnimation(UIElement.OpacityProperty, null);
            lbl_bayArrow3.BeginAnimation(UIElement.OpacityProperty, null);
            lbl_bayArrow4.BeginAnimation(UIElement.OpacityProperty, null);
            lbl_bayArrow5.BeginAnimation(UIElement.OpacityProperty, null);
            lbl_bayArrow6.BeginAnimation(UIElement.OpacityProperty, null);
            lbl_bayArrow7.BeginAnimation(UIElement.OpacityProperty, null);
            lbl_bayArrow8.BeginAnimation(UIElement.OpacityProperty, null);
            lbl_bayArrow9.BeginAnimation(UIElement.OpacityProperty, null);
            lbl_bayArrow10.BeginAnimation(UIElement.OpacityProperty, null);

            // 10개 화살표 애니메이션 생성 및 추가
            for (int i = 1; i <= 10; i++)
            {
                DoubleAnimation arrowAnimation = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                    BeginTime = TimeSpan.FromSeconds((i - 1) * 0.1),
                    FillBehavior = FillBehavior.HoldEnd
                };
                
                DoubleAnimation arrowFadeOut = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.0,
                    Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                    BeginTime = TimeSpan.FromSeconds((i - 1) * 0.1 + 0.4),
                    FillBehavior = FillBehavior.HoldEnd
                };

                Path arrow = (Path)this.FindName($"lbl_bayArrow{i}");
                if (arrow != null)
                {
                    Storyboard.SetTarget(arrowAnimation, arrow);
                    Storyboard.SetTargetProperty(arrowAnimation, new PropertyPath(UIElement.OpacityProperty));
                    storyboard.Children.Add(arrowAnimation);
                    
                    Storyboard.SetTarget(arrowFadeOut, arrow);
                    Storyboard.SetTargetProperty(arrowFadeOut, new PropertyPath(UIElement.OpacityProperty));
                    storyboard.Children.Add(arrowFadeOut);
                }
            }

            // 애니메이션 시작
            storyboard.Begin();
        }

        // LEV 상승 화살표 흐르는 애니메이션 시작 (아래에서 위로 흐름)
        private void StartLevUpArrowAnimation()
        {
            // 각 화살표에 대한 애니메이션 생성 (5개)
            // 아래(24)에서 위(-24)로 흐르므로 arrow1(아래)부터 시작
            DoubleAnimation arrow1Animation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0),
                FillBehavior = FillBehavior.HoldEnd
            };
            
            DoubleAnimation arrow1FadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.4),
                FillBehavior = FillBehavior.HoldEnd
            };

            DoubleAnimation arrow2Animation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.1),
                FillBehavior = FillBehavior.HoldEnd
            };
            
            DoubleAnimation arrow2FadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.5),
                FillBehavior = FillBehavior.HoldEnd
            };

            DoubleAnimation arrow3Animation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.2),
                FillBehavior = FillBehavior.HoldEnd
            };
            
            DoubleAnimation arrow3FadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.6),
                FillBehavior = FillBehavior.HoldEnd
            };

            DoubleAnimation arrow4Animation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.3),
                FillBehavior = FillBehavior.HoldEnd
            };
            
            DoubleAnimation arrow4FadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.7),
                FillBehavior = FillBehavior.HoldEnd
            };

            DoubleAnimation arrow5Animation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.4),
                FillBehavior = FillBehavior.HoldEnd
            };
            
            DoubleAnimation arrow5FadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.8),
                FillBehavior = FillBehavior.HoldEnd
            };

            Storyboard storyboard = new Storyboard
            {
                RepeatBehavior = RepeatBehavior.Forever,
                Duration = new Duration(TimeSpan.FromSeconds(1.0))
            };

            lbl_levUpArrow1.BeginAnimation(UIElement.OpacityProperty, null);
            lbl_levUpArrow2.BeginAnimation(UIElement.OpacityProperty, null);
            lbl_levUpArrow3.BeginAnimation(UIElement.OpacityProperty, null);
            lbl_levUpArrow4.BeginAnimation(UIElement.OpacityProperty, null);
            lbl_levUpArrow5.BeginAnimation(UIElement.OpacityProperty, null);

            // 상승: 아래(arrow1)에서 위(arrow5)로 흐르도록 순서 반대로
            Storyboard.SetTarget(arrow5Animation, lbl_levUpArrow5); // 위쪽 먼저
            Storyboard.SetTargetProperty(arrow5Animation, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow5Animation);
            
            Storyboard.SetTarget(arrow5FadeOut, lbl_levUpArrow5);
            Storyboard.SetTargetProperty(arrow5FadeOut, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow5FadeOut);

            Storyboard.SetTarget(arrow4Animation, lbl_levUpArrow4);
            Storyboard.SetTargetProperty(arrow4Animation, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow4Animation);
            
            Storyboard.SetTarget(arrow4FadeOut, lbl_levUpArrow4);
            Storyboard.SetTargetProperty(arrow4FadeOut, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow4FadeOut);

            Storyboard.SetTarget(arrow3Animation, lbl_levUpArrow3);
            Storyboard.SetTargetProperty(arrow3Animation, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow3Animation);
            
            Storyboard.SetTarget(arrow3FadeOut, lbl_levUpArrow3);
            Storyboard.SetTargetProperty(arrow3FadeOut, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow3FadeOut);

            Storyboard.SetTarget(arrow2Animation, lbl_levUpArrow2);
            Storyboard.SetTargetProperty(arrow2Animation, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow2Animation);
            
            Storyboard.SetTarget(arrow2FadeOut, lbl_levUpArrow2);
            Storyboard.SetTargetProperty(arrow2FadeOut, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow2FadeOut);

            Storyboard.SetTarget(arrow1Animation, lbl_levUpArrow1); // 아래쪽 나중에
            Storyboard.SetTargetProperty(arrow1Animation, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow1Animation);
            
            Storyboard.SetTarget(arrow1FadeOut, lbl_levUpArrow1);
            Storyboard.SetTargetProperty(arrow1FadeOut, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow1FadeOut);

            storyboard.Begin();
        }

        // LEV 하강 화살표 흐르는 애니메이션 시작
        private void StartLevDownArrowAnimation()
        {
            // 각 화살표에 대한 애니메이션 생성 (5개)
            DoubleAnimation arrow1Animation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0),
                FillBehavior = FillBehavior.HoldEnd
            };
            
            DoubleAnimation arrow1FadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.4),
                FillBehavior = FillBehavior.HoldEnd
            };

            DoubleAnimation arrow2Animation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.1),
                FillBehavior = FillBehavior.HoldEnd
            };
            
            DoubleAnimation arrow2FadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.5),
                FillBehavior = FillBehavior.HoldEnd
            };

            DoubleAnimation arrow3Animation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.2),
                FillBehavior = FillBehavior.HoldEnd
            };
            
            DoubleAnimation arrow3FadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.6),
                FillBehavior = FillBehavior.HoldEnd
            };

            DoubleAnimation arrow4Animation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.3),
                FillBehavior = FillBehavior.HoldEnd
            };
            
            DoubleAnimation arrow4FadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.7),
                FillBehavior = FillBehavior.HoldEnd
            };

            DoubleAnimation arrow5Animation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.4),
                FillBehavior = FillBehavior.HoldEnd
            };
            
            DoubleAnimation arrow5FadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.8),
                FillBehavior = FillBehavior.HoldEnd
            };

            Storyboard storyboard = new Storyboard
            {
                RepeatBehavior = RepeatBehavior.Forever,
                Duration = new Duration(TimeSpan.FromSeconds(1.0))
            };

            lbl_levDownArrow1.BeginAnimation(UIElement.OpacityProperty, null);
            lbl_levDownArrow2.BeginAnimation(UIElement.OpacityProperty, null);
            lbl_levDownArrow3.BeginAnimation(UIElement.OpacityProperty, null);
            lbl_levDownArrow4.BeginAnimation(UIElement.OpacityProperty, null);
            lbl_levDownArrow5.BeginAnimation(UIElement.OpacityProperty, null);

            // 하강: 위(arrow1)에서 아래(arrow5)로 흐르도록 순서 유지 (이미 맞음)
            Storyboard.SetTarget(arrow1Animation, lbl_levDownArrow1); // 위쪽 먼저
            Storyboard.SetTargetProperty(arrow1Animation, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow1Animation);
            
            Storyboard.SetTarget(arrow1FadeOut, lbl_levDownArrow1);
            Storyboard.SetTargetProperty(arrow1FadeOut, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow1FadeOut);

            Storyboard.SetTarget(arrow2Animation, lbl_levDownArrow2);
            Storyboard.SetTargetProperty(arrow2Animation, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow2Animation);
            
            Storyboard.SetTarget(arrow2FadeOut, lbl_levDownArrow2);
            Storyboard.SetTargetProperty(arrow2FadeOut, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow2FadeOut);

            Storyboard.SetTarget(arrow3Animation, lbl_levDownArrow3);
            Storyboard.SetTargetProperty(arrow3Animation, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow3Animation);
            
            Storyboard.SetTarget(arrow3FadeOut, lbl_levDownArrow3);
            Storyboard.SetTargetProperty(arrow3FadeOut, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow3FadeOut);

            Storyboard.SetTarget(arrow4Animation, lbl_levDownArrow4);
            Storyboard.SetTargetProperty(arrow4Animation, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow4Animation);
            
            Storyboard.SetTarget(arrow4FadeOut, lbl_levDownArrow4);
            Storyboard.SetTargetProperty(arrow4FadeOut, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow4FadeOut);

            Storyboard.SetTarget(arrow5Animation, lbl_levDownArrow5); // 아래쪽 나중에
            Storyboard.SetTargetProperty(arrow5Animation, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow5Animation);
            
            Storyboard.SetTarget(arrow5FadeOut, lbl_levDownArrow5);
            Storyboard.SetTargetProperty(arrow5FadeOut, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow5FadeOut);

            storyboard.Begin();
        }

        // FORK 화살표 흐르는 애니메이션 시작
        // 포크 좌측 화살표 흐르는 애니메이션 시작 (좌측에서 우측으로 흐름)
        private void StartForkLeftArrowAnimation()
        {
            // 각 화살표에 대한 애니메이션 생성 (5개)
            DoubleAnimation arrow1Animation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0),
                FillBehavior = FillBehavior.HoldEnd
            };
            
            DoubleAnimation arrow1FadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.4),
                FillBehavior = FillBehavior.HoldEnd
            };

            DoubleAnimation arrow2Animation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.1),
                FillBehavior = FillBehavior.HoldEnd
            };
            
            DoubleAnimation arrow2FadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.5),
                FillBehavior = FillBehavior.HoldEnd
            };

            DoubleAnimation arrow3Animation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.2),
                FillBehavior = FillBehavior.HoldEnd
            };
            
            DoubleAnimation arrow3FadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.6),
                FillBehavior = FillBehavior.HoldEnd
            };

            DoubleAnimation arrow4Animation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.3),
                FillBehavior = FillBehavior.HoldEnd
            };
            
            DoubleAnimation arrow4FadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.7),
                FillBehavior = FillBehavior.HoldEnd
            };

            DoubleAnimation arrow5Animation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.4),
                FillBehavior = FillBehavior.HoldEnd
            };
            
            DoubleAnimation arrow5FadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.8),
                FillBehavior = FillBehavior.HoldEnd
            };

            Storyboard storyboard = new Storyboard
            {
                RepeatBehavior = RepeatBehavior.Forever,
                Duration = new Duration(TimeSpan.FromSeconds(1.0))
            };

            lbl_forkLeftArrow1.BeginAnimation(UIElement.OpacityProperty, null);
            lbl_forkLeftArrow2.BeginAnimation(UIElement.OpacityProperty, null);
            lbl_forkLeftArrow3.BeginAnimation(UIElement.OpacityProperty, null);
            lbl_forkLeftArrow4.BeginAnimation(UIElement.OpacityProperty, null);
            lbl_forkLeftArrow5.BeginAnimation(UIElement.OpacityProperty, null);

            // 좌측: 우측(arrow1, Left=24)에서 좌측(arrow5, Left=-24)으로 흐르도록
            Storyboard.SetTarget(arrow1Animation, lbl_forkLeftArrow1); // 우측 먼저
            Storyboard.SetTargetProperty(arrow1Animation, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow1Animation);
            
            Storyboard.SetTarget(arrow1FadeOut, lbl_forkLeftArrow1);
            Storyboard.SetTargetProperty(arrow1FadeOut, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow1FadeOut);

            Storyboard.SetTarget(arrow2Animation, lbl_forkLeftArrow2);
            Storyboard.SetTargetProperty(arrow2Animation, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow2Animation);
            
            Storyboard.SetTarget(arrow2FadeOut, lbl_forkLeftArrow2);
            Storyboard.SetTargetProperty(arrow2FadeOut, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow2FadeOut);

            Storyboard.SetTarget(arrow3Animation, lbl_forkLeftArrow3);
            Storyboard.SetTargetProperty(arrow3Animation, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow3Animation);
            
            Storyboard.SetTarget(arrow3FadeOut, lbl_forkLeftArrow3);
            Storyboard.SetTargetProperty(arrow3FadeOut, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow3FadeOut);

            Storyboard.SetTarget(arrow4Animation, lbl_forkLeftArrow4);
            Storyboard.SetTargetProperty(arrow4Animation, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow4Animation);
            
            Storyboard.SetTarget(arrow4FadeOut, lbl_forkLeftArrow4);
            Storyboard.SetTargetProperty(arrow4FadeOut, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow4FadeOut);

            Storyboard.SetTarget(arrow5Animation, lbl_forkLeftArrow5); // 좌측 나중에
            Storyboard.SetTargetProperty(arrow5Animation, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow5Animation);
            
            Storyboard.SetTarget(arrow5FadeOut, lbl_forkLeftArrow5);
            Storyboard.SetTargetProperty(arrow5FadeOut, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow5FadeOut);

            storyboard.Begin();
        }

        // 포크 우측 화살표 흐르는 애니메이션 시작 (위에서 아래로 흐름)
        private void StartForkRightArrowAnimation()
        {
            // 각 화살표에 대한 애니메이션 생성 (5개)
            DoubleAnimation arrow1Animation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0),
                FillBehavior = FillBehavior.HoldEnd
            };
            
            DoubleAnimation arrow1FadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.4),
                FillBehavior = FillBehavior.HoldEnd
            };

            DoubleAnimation arrow2Animation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.1),
                FillBehavior = FillBehavior.HoldEnd
            };
            
            DoubleAnimation arrow2FadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.5),
                FillBehavior = FillBehavior.HoldEnd
            };

            DoubleAnimation arrow3Animation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.2),
                FillBehavior = FillBehavior.HoldEnd
            };
            
            DoubleAnimation arrow3FadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.6),
                FillBehavior = FillBehavior.HoldEnd
            };

            DoubleAnimation arrow4Animation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.3),
                FillBehavior = FillBehavior.HoldEnd
            };
            
            DoubleAnimation arrow4FadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.7),
                FillBehavior = FillBehavior.HoldEnd
            };

            DoubleAnimation arrow5Animation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.4),
                FillBehavior = FillBehavior.HoldEnd
            };
            
            DoubleAnimation arrow5FadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                BeginTime = TimeSpan.FromSeconds(0.8),
                FillBehavior = FillBehavior.HoldEnd
            };

            Storyboard storyboard = new Storyboard
            {
                RepeatBehavior = RepeatBehavior.Forever,
                Duration = new Duration(TimeSpan.FromSeconds(1.0))
            };

            lbl_forkRightArrow1.BeginAnimation(UIElement.OpacityProperty, null);
            lbl_forkRightArrow2.BeginAnimation(UIElement.OpacityProperty, null);
            lbl_forkRightArrow3.BeginAnimation(UIElement.OpacityProperty, null);
            lbl_forkRightArrow4.BeginAnimation(UIElement.OpacityProperty, null);
            lbl_forkRightArrow5.BeginAnimation(UIElement.OpacityProperty, null);

            // 우측: 좌측(arrow1, Left=-24)에서 우측(arrow5, Left=24)으로 흐르도록
            Storyboard.SetTarget(arrow1Animation, lbl_forkRightArrow1); // 좌측 먼저
            Storyboard.SetTargetProperty(arrow1Animation, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow1Animation);
            
            Storyboard.SetTarget(arrow1FadeOut, lbl_forkRightArrow1);
            Storyboard.SetTargetProperty(arrow1FadeOut, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow1FadeOut);

            Storyboard.SetTarget(arrow2Animation, lbl_forkRightArrow2);
            Storyboard.SetTargetProperty(arrow2Animation, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow2Animation);
            
            Storyboard.SetTarget(arrow2FadeOut, lbl_forkRightArrow2);
            Storyboard.SetTargetProperty(arrow2FadeOut, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow2FadeOut);

            Storyboard.SetTarget(arrow3Animation, lbl_forkRightArrow3);
            Storyboard.SetTargetProperty(arrow3Animation, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow3Animation);
            
            Storyboard.SetTarget(arrow3FadeOut, lbl_forkRightArrow3);
            Storyboard.SetTargetProperty(arrow3FadeOut, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow3FadeOut);

            Storyboard.SetTarget(arrow4Animation, lbl_forkRightArrow4);
            Storyboard.SetTargetProperty(arrow4Animation, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow4Animation);
            
            Storyboard.SetTarget(arrow4FadeOut, lbl_forkRightArrow4);
            Storyboard.SetTargetProperty(arrow4FadeOut, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow4FadeOut);

            Storyboard.SetTarget(arrow5Animation, lbl_forkRightArrow5); // 우측 나중에
            Storyboard.SetTargetProperty(arrow5Animation, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow5Animation);
            
            Storyboard.SetTarget(arrow5FadeOut, lbl_forkRightArrow5);
            Storyboard.SetTargetProperty(arrow5FadeOut, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(arrow5FadeOut);

            storyboard.Begin();
        }

    }
}


