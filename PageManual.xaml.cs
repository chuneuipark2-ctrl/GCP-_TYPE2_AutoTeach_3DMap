using System;
using System.Collections.Generic;
using System.Linq;
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
    /// PageManual.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PageManual : Page
    {
        // Define ImageBrush
        ImageBrush moveR = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/moveR.png")));
        ImageBrush moveL = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/moveL.png")));
        ImageBrush moveRC = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/moveRC.png")));
        ImageBrush moveLC = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/moveLC.png")));
        ImageBrush moveRN = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/moveRN.png")));
        ImageBrush moveLN = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/moveLN.png")));

        ImageBrush fkMN = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/fkMN.png")));
        ImageBrush fkM = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/fkM.png")));

        //Label List
        List<Label> posLabelList;
        List<Label> dirLabelList;

        // 더미 레이블
        Label tmpLabel = new Label();

        //Test Timer
        Timer myTimer = new Timer();

        int manualJobFork = 1;
        int forkMode = 0;               // 0: 포크모드 None, 1: 하프모드, 2: 풀모드 
        int stdPosMode = 0;             // 좌우 정위치 기준        1: 포크1좌 2: 포크1우 3: 포크2좌 4:포크2우
        // #TestCode
        int fmove = 0;


        //Singletone
        singletonClass gClass;
        public PageManual()
        {
            InitializeComponent();
            gClass = singletonClass.Instance;
            posLabelList = new List<Label>()
            {
                    // Label List init
              lbl_fPosL,
              lbl_mPosL,
              lbl_hPosL,
              lbl_fPosR,
              lbl_mPosR,
              lbl_hPosR,
              lbl_cPos
            };
            dirLabelList = new List<Label>()
            {
              lbl_fDirL,
              lbl_mDirL,
              lbl_hDirL,
              lbl_fDirR,
              lbl_mDirR,
              lbl_hDirR
            };
            if (gClass.str.SrmInfo[gClass.srmNum].forkType == 1)
            {
                lbl_fDirL.Visibility = Visibility.Collapsed;
                lbl_mDirL.Visibility = Visibility.Collapsed;
                lbl_fDirR.Visibility = Visibility.Collapsed;
                lbl_mDirR.Visibility = Visibility.Collapsed;
                lbl_fPosL.Visibility = Visibility.Collapsed;
                lbl_mPosL.Visibility = Visibility.Collapsed;
                lbl_fPosR.Visibility = Visibility.Collapsed;
                lbl_mPosR.Visibility = Visibility.Collapsed;
                lbl_hPosL.Content = (object)"F";
                lbl_hPosR.Content = (object)"F";
                // 좌측 F, M 관련 컬럼 숨기기 (0: F, 1: 화살표, 2: M, 3: 화살표)
                Grid_ForkPos.ColumnDefinitions[0].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_ForkPos.ColumnDefinitions[1].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_ForkPos.ColumnDefinitions[2].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_ForkPos.ColumnDefinitions[3].Width = new GridLength(0.0, GridUnitType.Star);
                // 우측 M, F 관련 컬럼 숨기기 (9: 화살표, 10: M, 11: 화살표, 12: F)
                Grid_ForkPos.ColumnDefinitions[9].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_ForkPos.ColumnDefinitions[10].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_ForkPos.ColumnDefinitions[11].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_ForkPos.ColumnDefinitions[12].Width = new GridLength(0.0, GridUnitType.Star);
            }
            else if (gClass.str.SrmInfo[gClass.srmNum].forkType == 2)
            {
                lbl_mDirL.Visibility = Visibility.Collapsed;
                lbl_mDirR.Visibility = Visibility.Collapsed;
                lbl_mPosL.Visibility = Visibility.Collapsed;
                lbl_mPosR.Visibility = Visibility.Collapsed;
                // 좌측 M 관련 컬럼 숨기기 (2: M, 3: 화살표)
                Grid_ForkPos.ColumnDefinitions[2].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_ForkPos.ColumnDefinitions[3].Width = new GridLength(0.0, GridUnitType.Star);
                // 우측 M 관련 컬럼 숨기기 (9: 화살표, 10: M)
                Grid_ForkPos.ColumnDefinitions[9].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_ForkPos.ColumnDefinitions[10].Width = new GridLength(0.0, GridUnitType.Star);
            }
            Btn_Fork1.Click += new RoutedEventHandler(Select_Fork);
            if (gClass.str.SrmInfo[gClass.srmNum].forkCnt > 1)
            {
                Btn_Fork2.IsEnabled = true;
                Btn_Fork2.Click += Select_Fork;
                Btn_Fork2.Visibility = Visibility.Visible;
            }
            else
            {
                Btn_Fork2.Visibility = Visibility.Hidden;
            }

            Btn_Fork1.IsChecked = true;     // Default is Fork1

            // 포크 위치 정보 표시 설정 (관리자 모드에서만)
            UpdateForkPosInfoVisibility();

            // SelectForkMode Event add
            if (gClass.str.SrmInfo[gClass.srmNum].forkType > 1)      // 싱글딥 아닐경우
            {
                Btn_Half.IsChecked = true;
                Btn_Full.IsChecked = false;
                Btn_Full.Visibility = Visibility.Visible;
                Btn_Full.Click += Select_ForkMode;
                Btn_Half.Visibility = Visibility.Visible;
                Btn_Half.Click += Select_ForkMode;
                forkMode = 1;
            }
            else
            {
                Btn_Full.Visibility = Visibility.Hidden;
                Btn_Half.Visibility = Visibility.Hidden;
                forkMode = 0;
            }

            // 좌/우 기준정위치 설정
            Btn_StdLeft.IsEnabled = true;
            Btn_StdRight.IsEnabled = true;
            Btn_StdLeft.IsChecked = true;
            Btn_StdRight.IsChecked = false;
            Btn_StdLeft.Visibility = Visibility.Visible;
            Btn_StdLeft.Click += Select_OnposMode;
            Btn_StdRight.Visibility = Visibility.Visible;
            Btn_StdRight.Click += Select_OnposMode;
            stdPosMode = 1;


            myTimer.Interval = 500; // 1 second
            myTimer.AutoReset = true; // Repeat the timer
            myTimer.Elapsed += TestTimer_Elapsed;
            myTimer.Start();

            // 개별 그리드 Width 조절
            //gridFork.ColumnDefinitions[3].Width = new GridLength(0);

            //lbl_BMove.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/moveRC.png")));
            //lbl_FMove.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/moveLC.png")));
        }

        private void Select_Fork(object sender, RoutedEventArgs e)
        {
            // Handle the button click event here
            ToggleButton toggleButton = sender as ToggleButton;

            if (toggleButton.IsChecked == true)
            {
                if (toggleButton == Btn_Fork1)
                {
                    // Display Fork1 Job
                    manualJobFork = 1;
                }
                else if (toggleButton == Btn_Fork2)
                {
                    // Display Fork2 Job
                    manualJobFork = 2;
                }
                // 포크 선택 변경 시 위치 정보 업데이트
                UpdateForkPosInfo();
            }
            else
            {
                toggleButton.IsChecked = true;
            }
        }

        private void Select_ForkMode(object sender, RoutedEventArgs e)
        {
            // Handle the button click event here
            ToggleButton toggleButton = sender as ToggleButton;

            if (toggleButton.IsChecked == true)
            {
                if (toggleButton == Btn_Half)
                {
                    // ForkType Half
                    forkMode = 1;
                    Btn_Full.IsChecked = false;
                }
                else if (toggleButton == Btn_Full)
                {
                    // ForkType Full
                    forkMode = 2;
                    Btn_Half.IsChecked = false;
                }
            }
            else
            {
                toggleButton.IsChecked = true;
            }
        }

        private void Select_OnposMode(object sender, RoutedEventArgs e)     // 좌/우측 정위치 기준
        {
            // Handle the button click event here
            ToggleButton toggleButton = sender as ToggleButton;

            if (toggleButton.IsChecked == true)
            {
                if (toggleButton == Btn_StdLeft)
                {
                    // 포크1 좌측
                    stdPosMode = 1;
                    Btn_StdRight.IsChecked = false;
                }
                else if (toggleButton == Btn_StdRight)
                {
                    // 포크1 우측
                    stdPosMode = 2;
                    Btn_StdLeft.IsChecked = false;
                }
            }
            else
            {
                toggleButton.IsChecked = true;
            }
        }

        private void TestTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            //Console.WriteLine("TestTimer_Elapsed Page Manual");
            // Execute the code here
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // 관리자 모드 변경 체크 및 위치 정보 업데이트
                    UpdateForkPosInfoVisibility();
                    UpdateForkPosInfo();
                    UpdateTravPosInfo();
                    UpdateHoistPosInfo();

                    // ------------------------------------------------------현재 BAY 표시----------------------------------------------------
                    if (gClass.str.SrmState[gClass.srmNum].trav.trSt1MoveDirec > 0)       // 이동방향 : 후진
                    {
                        if (lbl_FMove.Background != fkMN)
                        {
                            lbl_FMove.Background = fkMN;
                        }

                        if (gClass.str.SrmState[gClass.srmNum].trav.trSt1OperState > 0)          // 주행 동작 중 비트 ON
                        {
                            if (lbl_BMove.Background == moveL)          // 무브동작 블링크
                            {
                                lbl_BMove.Background = moveLC;
                            }
                            else
                            {
                                lbl_BMove.Background = moveL;
                            }

                            if (gClass.str.SrmState[gClass.srmNum].fork1.curBay > 1)
                            {
                                lbl_tarFbay.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curBay + 1).ToString();
                                // ST 우선 체크: curStation > 0이면 ST 형식으로 표시
                                lbl_curBay.Content = gClass.str.SrmState[gClass.srmNum].fork1.curStation > 0 
                                    ? $"ST{gClass.str.SrmState[gClass.srmNum].fork1.curStation}" 
                                    : gClass.str.SrmState[gClass.srmNum].fork1.curBay.ToString();
                                lbl_tarBbay.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curBay - 1).ToString();
                            }
                            else
                            {
                                lbl_tarFbay.Content = gClass.str.SrmState[gClass.srmNum].fork1.curBay + 1;
                                // ST 우선 체크: curStation > 0이면 ST 형식으로 표시
                                lbl_curBay.Content = gClass.str.SrmState[gClass.srmNum].fork1.curStation > 0 
                                    ? $"ST{gClass.str.SrmState[gClass.srmNum].fork1.curStation}" 
                                    : gClass.str.SrmState[gClass.srmNum].fork1.curBay.ToString();
                                lbl_tarBbay.Content = 0;
                            }
                        }
                        else   // 후진 동작 중이 아닐 때 
                        {
                            if (lbl_BMove.Background != fkMN)
                            {
                                lbl_BMove.Background = fkMN;
                            }

                            if (gClass.str.SrmState[gClass.srmNum].trav.trSt1OriginPos > 0)       // 주행정위치일 경우 ON
                            {
                                lbl_tarFbay.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curBay + 1).ToString();
                                // ST 우선 체크: curStation > 0이면 ST 형식으로 표시
                                lbl_curBay.Content = gClass.str.SrmState[gClass.srmNum].fork1.curStation > 0 
                                    ? $"ST{gClass.str.SrmState[gClass.srmNum].fork1.curStation}" 
                                    : gClass.str.SrmState[gClass.srmNum].fork1.curBay.ToString();
                                lbl_tarBbay.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curBay - 1).ToString();
                            }
                            else
                            {
                                if (gClass.str.SrmState[gClass.srmNum].fork1.curBay > 1)
                                {
                                    lbl_tarFbay.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curBay + 1).ToString();
                                    // ST 우선 체크: curStation > 0이면 ST 형식으로 표시
                                    lbl_curBay.Content = gClass.str.SrmState[gClass.srmNum].fork1.curStation > 0 
                                        ? $"ST{gClass.str.SrmState[gClass.srmNum].fork1.curStation}" 
                                        : gClass.str.SrmState[gClass.srmNum].fork1.curBay.ToString();
                                    lbl_tarBbay.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curBay - 1).ToString();
                                    // 현재 위치가 현재베이(중간위치를 지나서 변경됨) 위치값 보다 작을 때 정상이동  
                                    //if (gClass.str.SrmInfo[gClass.srmNum].cellBay[gClass.str.SrmState[gClass.srmNum].fork1.curBay - 1] > (gClass.str.SrmState[gClass.srmNum].trav.curPos + 5))
                                    //{
                                    //    lbl_tarFbay.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curBay + 1).ToString();
                                    //    lbl_curBay.Content = gClass.str.SrmState[gClass.srmNum].fork1.curBay.ToString();
                                    //    lbl_tarBbay.Content = gClass.str.SrmState[gClass.srmNum].fork1.targetBay.ToString();            // 후진 타겟 베이 표시
                                    //}
                                    //else   // 현재 위치가 현재베이 위치값 보다 크거나 같을 때 (중간위치를 지나서 변경수신된 베이 위치 값과 비교)
                                    //{
                                    //    lbl_tarFbay.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curBay + 2).ToString();
                                    //    lbl_curBay.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curBay + 1).ToString();
                                    //    lbl_tarBbay.Content = gClass.str.SrmState[gClass.srmNum].fork1.targetBay.ToString();
                                    //}
                                }
                                else
                                {
                                    lbl_tarFbay.Content = gClass.str.SrmState[gClass.srmNum].fork1.curBay + 1;
                                    // ST 우선 체크: curStation > 0이면 ST 형식으로 표시
                                    lbl_curBay.Content = gClass.str.SrmState[gClass.srmNum].fork1.curStation > 0 
                                        ? $"ST{gClass.str.SrmState[gClass.srmNum].fork1.curStation}" 
                                        : gClass.str.SrmState[gClass.srmNum].fork1.curBay.ToString();
                                    lbl_tarBbay.Content = 0;
                                }
                            }
                        }
                    }
                    else
                    {                                                      // 이동방향 : 전진
                        if (lbl_BMove.Background != fkMN)
                        {
                            lbl_BMove.Background = fkMN;
                        }

                        if (gClass.str.SrmState[gClass.srmNum].trav.trSt1OperState > 0)          // 주행 동작 중 비트 ON
                        {
                            if (lbl_FMove.Background == moveR)
                            {
                                lbl_FMove.Background = moveRC;
                            }
                            else
                            {
                                lbl_FMove.Background = moveR;
                            }

                            if (gClass.str.SrmInfo[gClass.srmNum].bay > gClass.str.SrmState[gClass.srmNum].fork1.curBay)      // 현재 베이가 마지막 베이보다 작을 때
                            {
                                if (gClass.str.SrmState[gClass.srmNum].fork1.curBay > 1)
                                {
                                    lbl_tarFbay.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curBay + 1).ToString();
                                    // ST 우선 체크: curStation > 0이면 ST 형식으로 표시
                                    lbl_curBay.Content = gClass.str.SrmState[gClass.srmNum].fork1.curStation > 0 
                                        ? $"ST{gClass.str.SrmState[gClass.srmNum].fork1.curStation}" 
                                        : gClass.str.SrmState[gClass.srmNum].fork1.curBay.ToString();
                                    lbl_tarBbay.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curBay - 1).ToString();
                                    //if (gClass.str.SrmInfo[gClass.srmNum].cellBay[gClass.str.SrmState[gClass.srmNum].fork1.curBay - 1] < gClass.str.SrmState[gClass.srmNum].trav.curPos - 5)      // 현재 위치가 현재베이(중간위치를 지나서 변경됨) 위치값 보다 클 때 정상이동   
                                    //{
                                    //    lbl_tarFbay.Content = gClass.str.SrmState[gClass.srmNum].fork1.targetBay.ToString();
                                    //    lbl_curBay.Content = gClass.str.SrmState[gClass.srmNum].fork1.curBay.ToString();
                                    //    lbl_tarBbay.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curBay - 1).ToString();
                                    //}
                                    //else   // 현재 위치가 현재베이 위치값 보다 크거나 같을 때 (중간위치를 지나서 변경수신된 베이 위치 값과 비교)          -- 중간지점을 지나서 변경되어 수신되는 curBay/Lev에 상관없이 이동중인 Bay/Lev을 정상적으로 표시하기 위함
                                    //{
                                    //    lbl_tarFbay.Content = gClass.str.SrmState[gClass.srmNum].fork1.targetBay.ToString();
                                    //    lbl_curBay.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curBay - 1).ToString();
                                    //    lbl_tarBbay.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curBay - 2).ToString();
                                    //}
                                }
                                else
                                {
                                    lbl_tarFbay.Content = gClass.str.SrmState[gClass.srmNum].fork1.curBay + 1;
                                    // ST 우선 체크: curStation > 0이면 ST 형식으로 표시
                                    lbl_curBay.Content = gClass.str.SrmState[gClass.srmNum].fork1.curStation > 0 
                                        ? $"ST{gClass.str.SrmState[gClass.srmNum].fork1.curStation}" 
                                        : gClass.str.SrmState[gClass.srmNum].fork1.curBay.ToString();
                                    lbl_tarBbay.Content = gClass.str.SrmState[gClass.srmNum].fork1.curBay - 1;
                                }
                            }
                            else
                            {
                                lbl_tarFbay.Content = gClass.str.SrmState[gClass.srmNum].fork1.curBay + 1;
                                // ST 우선 체크: curStation > 0이면 ST 형식으로 표시
                                lbl_curBay.Content = gClass.str.SrmState[gClass.srmNum].fork1.curStation > 0 
                                    ? $"ST{gClass.str.SrmState[gClass.srmNum].fork1.curStation}" 
                                    : gClass.str.SrmState[gClass.srmNum].fork1.curBay.ToString();
                                lbl_tarBbay.Content = gClass.str.SrmState[gClass.srmNum].fork1.curBay - 1;
                            }
                        }
                        else   // 전진 동작 중이 아닐 때 
                        {
                            if (lbl_FMove.Background != fkMN)
                            {
                                lbl_FMove.Background = fkMN;
                            }

                            if (gClass.str.SrmState[gClass.srmNum].trav.trSt1OriginPos > 0)       // 주행정위치일 경우 ON
                            {
                                lbl_tarFbay.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curBay + 1).ToString();
                                // ST 우선 체크: curStation > 0이면 ST 형식으로 표시
                                lbl_curBay.Content = gClass.str.SrmState[gClass.srmNum].fork1.curStation > 0 
                                    ? $"ST{gClass.str.SrmState[gClass.srmNum].fork1.curStation}" 
                                    : gClass.str.SrmState[gClass.srmNum].fork1.curBay.ToString();
                                lbl_tarBbay.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curBay - 1).ToString();
                            }
                            else
                            {
                                if (gClass.str.SrmInfo[gClass.srmNum].bay > gClass.str.SrmState[gClass.srmNum].fork1.curBay)      // 현재 베이가 마지막 베이보다 작을 때
                                {
                                    lbl_tarFbay.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curBay + 1).ToString();
                                    // ST 우선 체크: curStation > 0이면 ST 형식으로 표시
                                    lbl_curBay.Content = gClass.str.SrmState[gClass.srmNum].fork1.curStation > 0 
                                        ? $"ST{gClass.str.SrmState[gClass.srmNum].fork1.curStation}" 
                                        : gClass.str.SrmState[gClass.srmNum].fork1.curBay.ToString();
                                    lbl_tarBbay.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curBay - 1).ToString();
                                    //if (gClass.str.SrmInfo[gClass.srmNum].cellBay[gClass.str.SrmState[gClass.srmNum].fork1.curBay - 1] < gClass.str.SrmState[gClass.srmNum].trav.curPos - 5)      // 현재 위치가 현재베이(중간위치를 지나서 변경됨) 위치값 보다 클 때 정상이동   
                                    //{
                                    //    lbl_tarFbay.Content = gClass.str.SrmState[gClass.srmNum].fork1.targetBay.ToString();
                                    //    lbl_curBay.Content = gClass.str.SrmState[gClass.srmNum].fork1.curBay.ToString();
                                    //    lbl_tarBbay.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curBay - 1).ToString();
                                    //}
                                    //else   // 현재 위치가 현재베이 위치값 보다 크거나 같을 때 (중간위치를 지나서 변경수신된 베이 위치 값과 비교)          -- 중간지점을 지나서 변경되어 수신되는 curBay/Lev에 상관없이 이동중인 Bay/Lev을 정상적으로 표시하기 위함
                                    //{
                                    //    lbl_tarFbay.Content = gClass.str.SrmState[gClass.srmNum].fork1.targetBay.ToString();
                                    //    lbl_curBay.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curBay - 1).ToString();
                                    //    lbl_tarBbay.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curBay - 2).ToString();
                                    //}
                                }
                                else
                                {
                                    lbl_tarFbay.Content = gClass.str.SrmState[gClass.srmNum].fork1.curBay + 1;
                                    // ST 우선 체크: curStation > 0이면 ST 형식으로 표시
                                    lbl_curBay.Content = gClass.str.SrmState[gClass.srmNum].fork1.curStation > 0 
                                        ? $"ST{gClass.str.SrmState[gClass.srmNum].fork1.curStation}" 
                                        : gClass.str.SrmState[gClass.srmNum].fork1.curBay.ToString();
                                    lbl_tarBbay.Content = gClass.str.SrmState[gClass.srmNum].fork1.curBay - 1;
                                }
                            }
                        }
                    }

                    //-----------------------------------------------주행 정위치 색상표시---------------------------------------------
                    if (gClass.str.SrmState[gClass.srmNum].trav.trSt1OriginPos > 0)       // 정위치 1: 정위치
                    {
                        if (lbl_curBay.Foreground != Brushes.GreenYellow)
                        {
                            lbl_curBay.Foreground = Brushes.GreenYellow;
                        }
                    }
                    else
                    {
                        if (lbl_curBay.Foreground != Brushes.LightGray)
                        {
                            lbl_curBay.Foreground = Brushes.LightGray;
                        }
                    }

                    // ------------------------------------------------------현재 LEV 표시----------------------------------------------------
                    if (gClass.str.SrmState[gClass.srmNum].lift.liSt1MoveDirec > 0)       // 이동방향 : 하강
                    {
                        if (lbl_UMove.Background != fkMN)
                        {
                            lbl_UMove.Background = fkMN;
                        }

                        if (gClass.str.SrmState[gClass.srmNum].lift.liSt1OperState > 0)          // 승강 동작 중 비트 ON
                        {
                            if (lbl_DMove.Background == moveL)
                            {
                                lbl_DMove.Background = moveLC;
                            }
                            else
                            {
                                lbl_DMove.Background = moveL;
                            }

                            if (gClass.str.SrmState[gClass.srmNum].fork1.curLev > 1)
                            {
                                lbl_tarUlev.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curLev + 1).ToString();
                                lbl_curLev.Content = gClass.str.SrmState[gClass.srmNum].fork1.curLev.ToString();
                                lbl_tarDlev.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curLev - 1).ToString();
                                //if (gClass.str.SrmInfo[gClass.srmNum].cellLev[gClass.str.SrmState[gClass.srmNum].fork1.curLev - 1] > (gClass.str.SrmState[gClass.srmNum].lift.curPos + 5))      // 현재 위치가 현재레벨(중간위치를 지나서 변경됨) 위치값 보다 작을 때 정상이동   
                                //{
                                //    lbl_tarUlev.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curLev + 1).ToString();
                                //    lbl_curLev.Content = gClass.str.SrmState[gClass.srmNum].fork1.curLev.ToString();
                                //    lbl_tarDlev.Content = gClass.str.SrmState[gClass.srmNum].fork1.targetLev.ToString();
                                //}
                                //else   // 현재 위치가 현재레벨 위치값 보다 크거나 같을 때 (중간위치를 지나서 변경수신된 레벨 위치 값과 비교)             -- 중간지점을 지나서 변경되어 수신되는 curBay/Lev에 상관없이 이동중인 Bay/Lev을 정상적으로 표시하기 위함
                                //{
                                //    lbl_tarUlev.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curLev + 2).ToString();
                                //    lbl_curLev.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curLev + 1).ToString();
                                //    lbl_tarDlev.Content = gClass.str.SrmState[gClass.srmNum].fork1.targetLev.ToString();
                                //}
                            }
                            else
                            {
                                lbl_tarUlev.Content = 2;
                                lbl_curLev.Content = 1;
                                lbl_tarDlev.Content = 0;
                            }
                        }
                        else   // 하강 동작 중이 아닐 때 
                        {
                            if (lbl_DMove.Background != fkMN)
                            {
                                lbl_DMove.Background = fkMN;
                            }
                            if (gClass.str.SrmState[gClass.srmNum].lift.liSt1OriginPos > 0)       // 하강정위치일 경우 ON
                            {
                                lbl_tarUlev.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curLev + 1).ToString();
                                lbl_curLev.Content = gClass.str.SrmState[gClass.srmNum].fork1.curLev.ToString();
                                lbl_tarDlev.Content = gClass.str.SrmState[gClass.srmNum].fork1.targetLev.ToString();
                            }
                            else
                            {
                                if (gClass.str.SrmState[gClass.srmNum].fork1.curLev > 1)
                                {
                                    lbl_tarUlev.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curLev + 1).ToString();
                                    lbl_curLev.Content = gClass.str.SrmState[gClass.srmNum].fork1.curLev.ToString();
                                    lbl_tarDlev.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curLev - 1).ToString();
                                    //if (gClass.str.SrmInfo[gClass.srmNum].cellLev[gClass.str.SrmState[gClass.srmNum].fork1.curLev - 1] > (gClass.str.SrmState[gClass.srmNum].lift.curPos + 5))      // 현재 위치가 현재레벨(중간위치를 지나서 변경됨) 위치값 보다 작을 때 정상이동   
                                    //{
                                    //    lbl_tarUlev.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curLev + 1).ToString();
                                    //    lbl_curLev.Content = gClass.str.SrmState[gClass.srmNum].fork1.curLev.ToString();
                                    //    lbl_tarDlev.Content = gClass.str.SrmState[gClass.srmNum].fork1.targetLev.ToString();
                                    //}
                                    //else   // 현재 위치가 현재레벨 위치값 보다 크거나 같을 때 (중간위치를 지나서 변경수신된 레벨 위치 값과 비교)             -- 중간지점을 지나서 변경되어 수신되는 curBay/Lev에 상관없이 이동중인 Bay/Lev을 정상적으로 표시하기 위함
                                    //{
                                    //    lbl_tarUlev.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curLev + 2).ToString();
                                    //    lbl_curLev.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curLev + 1).ToString();
                                    //    lbl_tarDlev.Content = gClass.str.SrmState[gClass.srmNum].fork1.targetLev.ToString();
                                    //}
                                }
                                else
                                {
                                    lbl_tarUlev.Content = "2";
                                    lbl_curLev.Content = "1";
                                    lbl_tarDlev.Content = 0;
                                }
                            }
                        }
                    }
                    else
                    {                                                      // 이동방향 : 상승
                        if (lbl_DMove.Background != fkMN)
                        {
                            lbl_DMove.Background = fkMN;
                        }

                        if (gClass.str.SrmState[gClass.srmNum].lift.liSt1OperState > 0)          // 승강 동작 중 비트 ON
                        {
                            if (lbl_UMove.Background == moveR)
                            {
                                lbl_UMove.Background = moveRC;
                            }
                            else
                            {
                                lbl_UMove.Background = moveR;
                            }
                            if (gClass.str.SrmInfo[gClass.srmNum].lev > gClass.str.SrmState[gClass.srmNum].fork1.curLev)      // 현재 레벨이 마지막 레벨보다 작을 때
                            {
                                if (gClass.str.SrmState[gClass.srmNum].fork1.curLev > 0)
                                {
                                    lbl_tarUlev.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curLev + 1).ToString();
                                    lbl_curLev.Content = gClass.str.SrmState[gClass.srmNum].fork1.curLev.ToString();
                                    lbl_tarDlev.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curLev - 1).ToString();

                                    //if (gClass.str.SrmInfo[gClass.srmNum].cellLev[gClass.str.SrmState[gClass.srmNum].fork1.curLev - 1] < gClass.str.SrmState[gClass.srmNum].lift.curPos - 5)      // 현재 위치가 현재레벨(중간위치를 지나서 변경됨) 위치값 보다 클 때 정상이동   
                                    //{
                                    //    lbl_tarUlev.Content = gClass.str.SrmState[gClass.srmNum].fork1.targetLev.ToString();
                                    //    lbl_curLev.Content = gClass.str.SrmState[gClass.srmNum].fork1.curLev.ToString();
                                    //    lbl_tarDlev.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curLev - 1).ToString();
                                    //}
                                    //else   // 현재 위치가 현재베이 위치값 보다 크거나 같을 때 (중간위치를 지나서 변경수신된 레벨 위치 값과 비교)
                                    //{
                                    //    lbl_tarUlev.Content = gClass.str.SrmState[gClass.srmNum].fork1.targetLev.ToString();
                                    //    lbl_curLev.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curLev - 1).ToString();
                                    //    lbl_tarDlev.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curLev - 2).ToString();
                                    //}
                                }
                                else
                                {
                                    lbl_tarUlev.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curLev + 1).ToString();
                                    lbl_curLev.Content = gClass.str.SrmState[gClass.srmNum].fork1.curLev;
                                    lbl_tarDlev.Content = "0";
                                }
                            }
                            else
                            {
                                lbl_tarUlev.Content = gClass.str.SrmInfo[gClass.srmNum].lev;
                                lbl_curLev.Content = gClass.str.SrmState[gClass.srmNum].fork1.curLev;
                                lbl_tarDlev.Content = gClass.str.SrmState[gClass.srmNum].fork1.curLev - 1;
                            }
                        }
                        else   // 상승 동작 중이 아닐 때 
                        {
                            if (lbl_UMove.Background != fkMN)
                            {
                                lbl_UMove.Background = fkMN;
                            }
                            if (gClass.str.SrmState[gClass.srmNum].lift.liSt1OriginPos > 0)       // 승강정위치일 경우 ON
                            {
                                lbl_tarUlev.Content = gClass.str.SrmState[gClass.srmNum].fork1.targetLev.ToString();
                                lbl_curLev.Content = gClass.str.SrmState[gClass.srmNum].fork1.curLev.ToString();
                                lbl_tarDlev.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curLev - 1).ToString();
                            }
                            else
                            {
                                if (gClass.str.SrmInfo[gClass.srmNum].lev > gClass.str.SrmState[gClass.srmNum].fork1.curLev)      // 현재 레벨이 마지막 레벨보다 작을 때
                                {
                                    if (gClass.str.SrmState[gClass.srmNum].fork1.curLev > 0)
                                    {
                                        lbl_tarUlev.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curLev + 1).ToString();
                                        lbl_curLev.Content = gClass.str.SrmState[gClass.srmNum].fork1.curLev.ToString();
                                        lbl_tarDlev.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curLev - 1).ToString();
                                        //if (gClass.str.SrmInfo[gClass.srmNum].cellLev[gClass.str.SrmState[gClass.srmNum].fork1.curLev - 1] < gClass.str.SrmState[gClass.srmNum].lift.curPos - 5)      // 현재 위치가 현재레벨(중간위치를 지나서 변경됨) 위치값 보다 클 때 정상이동   
                                        //{
                                        //    lbl_tarUlev.Content = gClass.str.SrmState[gClass.srmNum].fork1.targetLev.ToString();
                                        //    lbl_curLev.Content = gClass.str.SrmState[gClass.srmNum].fork1.curLev.ToString();
                                        //    lbl_tarDlev.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curLev - 1).ToString();
                                        //}
                                        //else   // 현재 위치가 현재베이 위치값 보다 크거나 같을 때 (중간위치를 지나서 변경수신된 레벨 위치 값과 비교)
                                        //{
                                        //    lbl_tarUlev.Content = gClass.str.SrmState[gClass.srmNum].fork1.targetLev.ToString();
                                        //    lbl_curLev.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curLev - 1).ToString();
                                        //    lbl_tarDlev.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curLev - 2).ToString();
                                        //}
                                    }
                                    else
                                    {
                                        lbl_tarUlev.Content = (gClass.str.SrmState[gClass.srmNum].fork1.curLev + 1).ToString();
                                        lbl_curLev.Content = gClass.str.SrmState[gClass.srmNum].fork1.curLev;
                                        lbl_tarDlev.Content = 0;
                                    }
                                }
                                else
                                {
                                    lbl_tarUlev.Content = gClass.str.SrmInfo[gClass.srmNum].lev;
                                    lbl_curLev.Content = gClass.str.SrmState[gClass.srmNum].fork1.curLev;
                                    lbl_tarDlev.Content = gClass.str.SrmState[gClass.srmNum].fork1.curLev - 1;
                                }
                            }
                        }
                    }

                    //-----------------------------------------------승강 상/하위치 표시---------------------------------------------
                    if (gClass.str.SrmState[gClass.srmNum].fork1.posLeftUp > 0 || gClass.str.SrmState[gClass.srmNum].fork1.posRightUp > 0)       // 정위치1 - 승강상위치(좌측)            주행/승강/상/하/좌/우 정위치   ---------- to do 장치상태 - 승강 - 상태1 정위치 비트 동시에 들어오는지. 확인 후 조건처리 필요
                    {
                        if (lbl_top.BorderBrush != Brushes.Green)
                        {
                            lbl_top.BorderBrush = Brushes.Green;
                        }
                    }
                    else
                    {
                        if (lbl_top.BorderBrush != Brushes.Gray)
                        {
                            lbl_top.BorderBrush = Brushes.Gray;
                        }
                    }

                    if (gClass.str.SrmState[gClass.srmNum].fork1.posLeftBottom > 0 || gClass.str.SrmState[gClass.srmNum].fork1.posRightBottom > 0)       // 정위치1 - 승강하위치(좌측)            주행/승강/상/하/좌/우 정위치   ---------- to do 일단 좌측 상/하 위치로 처리함
                    {
                        if (lbl_bottom.BorderBrush != Brushes.Green)
                        {
                            lbl_bottom.BorderBrush = Brushes.Green;
                        }
                    }
                    else
                    {
                        if (lbl_bottom.BorderBrush != Brushes.Gray)
                        {
                            lbl_bottom.BorderBrush = Brushes.Gray;
                        }
                    }

                    if (gClass.str.SrmState[gClass.srmNum].fork1.forkRightEnable > 0)       // 포크 우측진출가능 표시
                    {
                        lbl_forkREn.Content = cConstDefine.tr("진출가능");
                        if (lbl_forkREn.Foreground != Brushes.GreenYellow)
                        {
                            lbl_forkREn.Foreground = Brushes.GreenYellow;
                        }
                    }
                    else
                    {
                        lbl_forkREn.Content = cConstDefine.tr("진출불가");
                        if (lbl_forkREn.Foreground != Brushes.Gray)
                        {
                            lbl_forkREn.Foreground = Brushes.Gray;
                        }
                    }

                    if (gClass.str.SrmState[gClass.srmNum].fork1.forkLeftEnable > 0)       // 포크 좌측진출가능 표시
                    {
                        lbl_forkLEn.Content = cConstDefine.tr("진출가능");
                        if (lbl_forkLEn.Foreground != Brushes.GreenYellow)
                        {
                            lbl_forkLEn.Foreground = Brushes.GreenYellow;
                        }
                    }
                    else
                    {
                        lbl_forkLEn.Content = cConstDefine.tr("진출불가");
                        if (lbl_forkLEn.Foreground != Brushes.Gray)
                        {
                            lbl_forkLEn.Foreground = Brushes.Gray;
                        }
                    }

                    //-----------------------------------------------포크 현재 위치 표시------------------------------------
                    ForkPositionCheck(gClass.str.SrmState[gClass.srmNum].fork1.originPos, gClass.str.SrmState[gClass.srmNum].fork1.moveDirec, gClass.str.SrmState[gClass.srmNum].fork1.operState, gClass.str.SrmState[gClass.srmNum].fork1.curPosNum);

                    ////-----------------------------------------------주행타겟값 표시-------------------------------------------
                    //if (gClass.str.SrmState[gClass.srmNum].fork1.curBay > gClass.str.SrmState[gClass.srmNum].fork1.targetBay)     // 후진
                    //{
                    //    lbl_tarFbay.Content = "-";
                    //    lbl_tarBbay.Content = gClass.str.SrmState[gClass.srmNum].fork1.targetBay.ToString();
                    //}
                    //else if (gClass.str.SrmState[gClass.srmNum].fork1.curBay < gClass.str.SrmState[gClass.srmNum].fork1.targetBay)  // 전진
                    //{
                    //    lbl_tarFbay.Content = gClass.str.SrmState[gClass.srmNum].fork1.targetBay.ToString();
                    //    lbl_tarBbay.Content = "-";
                    //}
                    //else
                    //{
                    //    // to do 정위치 도착 확인해서 표시 지울 지 처리 고민
                    //}
                    ////-----------------------------------------------승강타겟값 표시-------------------------------------------
                    //if (gClass.str.SrmState[gClass.srmNum].fork1.curLev > gClass.str.SrmState[gClass.srmNum].fork1.targetLev)     // 하강
                    //{
                    //    lbl_tarUlev.Content = "-";
                    //    lbl_tarDlev.Content = gClass.str.SrmState[gClass.srmNum].fork1.targetBay.ToString();
                    //}
                    //else if (gClass.str.SrmState[gClass.srmNum].fork1.curLev < gClass.str.SrmState[gClass.srmNum].fork1.targetLev)  // 상승
                    //{
                    //    lbl_tarUlev.Content = gClass.str.SrmState[gClass.srmNum].fork1.targetBay.ToString();
                    //    lbl_tarDlev.Content = "-";
                    //}
                    //else
                    //{
                    //    // to do 정위치 도착 확인해서 표시 지울 지 처리 고민
                    //}


                    //if (fmove == 0)
                    //{
                    //    fmove = 1;
                    //    //lbl_BMove.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/moveR.png")));
                    //    //lbl_FMove.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/moveL.png")));

                    //    lbl_BMove.Background = moveR;
                    //    lbl_FMove.Background = moveL;
                    //}
                    //else if(fmove == 1)
                    //{
                    //    fmove = 0;
                    //    //lbl_BMove.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/moveRC.png")));
                    //    //lbl_FMove.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/moveLC.png")));

                    //    lbl_BMove.Background = moveRC;
                    //    lbl_FMove.Background = moveLC;
                    //}
                }
                catch (Exception ex)
                {
                    cIniAccess.SaveExLog(0, "EXCEPTION - PageManualTimer : " + ex.Message);
                }
            });

            //Console.WriteLine("MANUAL BUTTON STATE : "+ gClass.srmNum + " - " + gClass.str.SrmPacket[gClass.srmNum].manClicked);
        }



        // 수동 제어 버튼 이벤트 처리 함수
        private void Button_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Button btnDown = sender as Button;

            if (stdPosMode < 1)
            {
                Btn_StdLeft.IsChecked = true;
                Btn_StdRight.IsChecked = false;
                stdPosMode = 1;
            }
            gClass.str.SrmPacket[gClass.srmNum].manPosStd = (byte)stdPosMode;

            // 주행 동작 버튼---------------------------------------------------------
            if (btnDown.Name == "fastF")
            {
                gClass.str.SrmPacket[gClass.srmNum].manCmd = cConstDefine.TRAV_FW_FAST;
                gClass.str.SrmPacket[gClass.srmNum].manAxis = 1;
                gClass.str.SrmPacket[gClass.srmNum].manTrav = 11;
                cIniAccess.SaveJobLog(gClass.srmNum, "GCP == 수동조작 - 전진중속");
                Console.WriteLine("Pressed TRAV_FW_FAST");
            }
            else if (btnDown.Name == "slowF")
            {
                gClass.str.SrmPacket[gClass.srmNum].manCmd = cConstDefine.TRAV_FW_SLOW;
                gClass.str.SrmPacket[gClass.srmNum].manAxis = 1;
                gClass.str.SrmPacket[gClass.srmNum].manTrav = 1;
                cIniAccess.SaveJobLog(gClass.srmNum, "GCP == 수동조작 - 전진저속");
                Console.WriteLine("Pressed TRAV_FW_SLOW");
            }
            else if (btnDown.Name == "fastB")
            {
                gClass.str.SrmPacket[gClass.srmNum].manCmd = cConstDefine.TRAV_BW_FAST;
                gClass.str.SrmPacket[gClass.srmNum].manAxis = 1;
                gClass.str.SrmPacket[gClass.srmNum].manTrav = 12;
                cIniAccess.SaveJobLog(gClass.srmNum, "GCP == 수동조작 - 후진중속");
                Console.WriteLine("Pressed TRAV_BW_FAST");
            }
            else if (btnDown.Name == "slowB")
            {
                gClass.str.SrmPacket[gClass.srmNum].manCmd = cConstDefine.TRAV_BW_SLOW;
                gClass.str.SrmPacket[gClass.srmNum].manAxis = 1;
                gClass.str.SrmPacket[gClass.srmNum].manTrav = 2;
                cIniAccess.SaveJobLog(gClass.srmNum, "GCP == 수동조작 - 후진저속");
                Console.WriteLine("Pressed TRAV_BW_SLOW");
            }

            // 승강 동작 버튼---------------------------------------------------------
            else if (btnDown.Name == "fastU")
            {
                gClass.str.SrmPacket[gClass.srmNum].manCmd = cConstDefine.LIFT_UP_FAST;
                gClass.str.SrmPacket[gClass.srmNum].manAxis = 2;
                gClass.str.SrmPacket[gClass.srmNum].manLift = 11;
                cIniAccess.SaveJobLog(gClass.srmNum, "GCP == 수동조작 - 상승중속");
                Console.WriteLine("Pressed LIFT_UP_FAST");
            }
            else if (btnDown.Name == "slowU")
            {
                gClass.str.SrmPacket[gClass.srmNum].manCmd = cConstDefine.LIFT_UP_SLOW;
                gClass.str.SrmPacket[gClass.srmNum].manAxis = 2;
                gClass.str.SrmPacket[gClass.srmNum].manLift = 1;
                cIniAccess.SaveJobLog(gClass.srmNum, "GCP == 수동조작 - 상승저속");
                Console.WriteLine("Pressed LIFT_UP_SLOW");
            }
            else if (btnDown.Name == "fastD")
            {
                gClass.str.SrmPacket[gClass.srmNum].manCmd = cConstDefine.LIFT_DW_FAST;
                gClass.str.SrmPacket[gClass.srmNum].manAxis = 2;
                gClass.str.SrmPacket[gClass.srmNum].manLift = 12;
                cIniAccess.SaveJobLog(gClass.srmNum, "GCP == 수동조작 - 하강중속");
                Console.WriteLine("Pressed LIFT_DW_FAST");
            }
            else if (btnDown.Name == "slowD")
            {
                gClass.str.SrmPacket[gClass.srmNum].manCmd = cConstDefine.LIFT_DW_SLOW;
                gClass.str.SrmPacket[gClass.srmNum].manAxis = 2;
                gClass.str.SrmPacket[gClass.srmNum].manLift = 2;
                cIniAccess.SaveJobLog(gClass.srmNum, "GCP == 수동조작 - 하강저속");
                Console.WriteLine("Pressed LIFT_DW_SLOW");
            }

            // 포크 동작 버튼---------------------------------------------------------
            else if (btnDown.Name == "slowL")
            {
                if (gClass.str.SrmState[gClass.srmNum].fork1.forkLeftEnable == 0)       // 포크 좌측진출가능 표시
                {
                    VarMessageBox.Show(cConstDefine.tr("수동작업"), cConstDefine.tr("포크진출불가"), VarMessageBoxButton.OK);
                    return;
                }
                gClass.str.SrmPacket[gClass.srmNum].manCmd = cConstDefine.FORK_MOVE_L;
                if (manualJobFork == 1)
                {
                    gClass.str.SrmPacket[gClass.srmNum].manFork1 = 2;
                    gClass.str.SrmPacket[gClass.srmNum].manAxis = 4;
                }
                else
                {
                    gClass.str.SrmPacket[gClass.srmNum].manFork2 = 2;
                    gClass.str.SrmPacket[gClass.srmNum].manAxis = 8;
                }

                cIniAccess.SaveJobLog(gClass.srmNum, "GCP == 수동조작 - 포크 좌");
                Console.WriteLine("Pressed FORK_MOVE_L");
            }
            else if (btnDown.Name == "slowR")
            {
                if (gClass.str.SrmState[gClass.srmNum].fork1.forkRightEnable == 0)       // 포크 우측진출가능 표시
                {
                    VarMessageBox.Show(cConstDefine.tr("수동작업"), cConstDefine.tr("포크진출불가"), VarMessageBoxButton.OK);
                    return;
                }
                gClass.str.SrmPacket[gClass.srmNum].manCmd = cConstDefine.FORK_MOVE_R;
                if (manualJobFork == 1)
                {
                    gClass.str.SrmPacket[gClass.srmNum].manFork1 = 3;
                    gClass.str.SrmPacket[gClass.srmNum].manAxis = 4;
                }
                else
                {
                    gClass.str.SrmPacket[gClass.srmNum].manFork2 = 3;
                    gClass.str.SrmPacket[gClass.srmNum].manAxis = 8;
                }

                cIniAccess.SaveJobLog(gClass.srmNum, "GCP == 수동조작 - 포크 우");
                Console.WriteLine("Pressed FORK_MOVE_R");
            }
            else if (btnDown.Name == "moveC")           // 포크 센터이동
            {
                gClass.str.SrmPacket[gClass.srmNum].manCmd = cConstDefine.FORK_MOVE_C;
                if (manualJobFork == 1)
                {
                    gClass.str.SrmPacket[gClass.srmNum].manFork1 = 1;
                    gClass.str.SrmPacket[gClass.srmNum].manAxis = 4;
                }
                else
                {
                    gClass.str.SrmPacket[gClass.srmNum].manFork2 = 1;
                    gClass.str.SrmPacket[gClass.srmNum].manAxis = 8;
                }

                cIniAccess.SaveJobLog(gClass.srmNum, "GCP == 수동조작 - 포크 중심");
                Console.WriteLine("Pressed FORK_MOVE_CENTER");
            }

            if (forkMode == 0)
            {
                gClass.str.SrmPacket[gClass.srmNum].manForkMvType = 3;          // 정지기준 FEL
            }
            else if (forkMode == 1)     // Btn Half
            {
                gClass.str.SrmPacket[gClass.srmNum].manForkMvType = 1;          // 정지기준 FHL
            }
            else
            {                           // Btn Full
                gClass.str.SrmPacket[gClass.srmNum].manForkMvType = 3;          // 정지기준 FEL
            }


            for (int i = 0; i < 3; i++)
            {
                if (i == gClass.srmNum)
                {
                    gClass.str.SrmPacket[i].manClicked = true;          // 수동 버튼 클릭 플래그 ON
                }
                else
                {
                    gClass.str.SrmPacket[i].manStop = true;             // 정지명령 전송
                    gClass.str.SrmPacket[i].manClicked = false;          // 현재 선택한 호기 외 OFF
                }
            }

            //Console.WriteLine("Button_PreviewMouseDown");
        }

        private void Button_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            Button btnUp = sender as Button;

            gClass.str.SrmPacket[gClass.srmNum].manStop = true;              // 정지명령 전송
            gClass.str.SrmPacket[gClass.srmNum].manClicked = false;          // 현재 선택한 호기 OFF
            gClass.str.SrmPacket[gClass.srmNum].manCmd = cConstDefine.MAN_BTN_NONE;         //  MANUAL BUTTON NONE
            //gClass.str.SrmPacket[gClass.srmNum].manAxis = 0;
            gClass.str.SrmPacket[gClass.srmNum].manTrav = 0;
            gClass.str.SrmPacket[gClass.srmNum].manLift = 0;
            gClass.str.SrmPacket[gClass.srmNum].manFork1 = 0;
            gClass.str.SrmPacket[gClass.srmNum].manFork2 = 0;

            //Console.WriteLine("Button_PreviewMouseUp");
        }

        private void ForkPositionCheck(byte onPos, byte direction, byte operation, sbyte posNo)
        {
            Label dirLabel = null;
            Label posLabel = null;
            lbl_fPosL.Foreground = Brushes.GreenYellow;
            switch (posNo)
            {
                case -3:
                    posLabel = lbl_fPosL;
                    dirLabel = lbl_fDirL;
                    break;
                case -2:
                    posLabel = lbl_mPosL;
                    dirLabel = lbl_mDirL;
                    break;
                case -1:
                    posLabel = lbl_hPosL;
                    dirLabel = lbl_hDirL;
                    break;
                case 0:
                    posLabel = lbl_cPos;
                    dirLabel = tmpLabel;
                    break;
                case 1:
                    posLabel = lbl_hPosR;
                    dirLabel = lbl_hDirR;
                    break;
                case 2:
                    posLabel = lbl_mPosR;
                    dirLabel = lbl_mDirR;
                    break;
                case 3:
                    posLabel = lbl_fPosR;
                    dirLabel = lbl_fDirR;
                    break;
            }

            if (onPos > 0)      // Full 정위치
            {
                posLabel.Foreground = Brushes.GreenYellow;
                if (operation > 0)        // 동작상태
                {
                    if (direction == 0)     // 이동방향 우측
                    {
                        if (dirLabel.Background == moveR)
                        {
                            dirLabel.Background = moveRN;
                        }
                        else
                        {
                            dirLabel.Background = moveR;
                        }
                    }
                    else
                    {
                        if (dirLabel.Background == moveL)
                        {
                            dirLabel.Background = moveLN;
                        }
                        else
                        {
                            dirLabel.Background = moveL;
                        }
                    }
                }
                else
                {
                    dirLabel.Background = fkMN;
                }
            }
            else
            {
                posLabel.Foreground = Brushes.Gray;
                if (operation > 0)        // 동작상태
                {
                    if (direction == 0)     // 이동방향 우측
                    {
                        if (dirLabel.Background == moveR)
                        {
                            dirLabel.Background = moveRN;
                        }
                        else
                        {
                            dirLabel.Background = moveR;
                        }
                    }
                    else
                    {
                        if (dirLabel.Background == moveL)
                        {
                            dirLabel.Background = moveLN;
                        }
                        else
                        {
                            dirLabel.Background = moveL;
                        }
                    }
                }
                else
                {
                    if (dirLabel.Background == fkM)
                    {
                        dirLabel.Background = fkMN;
                    }
                    else
                    {
                        dirLabel.Background = fkM;
                    }
                }
            }

            foreach (Label tmp in posLabelList)
            {
                if (tmp != posLabel)
                {
                    tmp.Foreground = Brushes.Gray;
                }
            }

            foreach (Label tmp in dirLabelList)
            {
                if (tmp != dirLabel)
                {
                    tmp.Background = fkMN;
                }
            }
        }

        // 포크 위치 정보 표시 여부 업데이트 (관리자 모드 체크)
        private void UpdateForkPosInfoVisibility()
        {
            if (gClass.str.GcpInfo.isAdminMode)
            {
                Grid_ForkPosInfo.Visibility = Visibility.Visible;
                lbl_travCurPos.Visibility = Visibility.Visible;
                lbl_travTargetPos.Visibility = Visibility.Visible;
                lbl_hoistCurPos.Visibility = Visibility.Visible;
                lbl_hoistTargetPos.Visibility = Visibility.Visible;
                // 관리자 모드일 때 중앙 구분선 숨기기 (포크 위치 정보가 표시되므로)
                if (lbl_centerDivider != null)
                {
                    lbl_centerDivider.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                Grid_ForkPosInfo.Visibility = Visibility.Collapsed;
                lbl_travCurPos.Visibility = Visibility.Collapsed;
                lbl_travTargetPos.Visibility = Visibility.Collapsed;
                lbl_hoistCurPos.Visibility = Visibility.Collapsed;
                lbl_hoistTargetPos.Visibility = Visibility.Collapsed;
                // 일반 모드일 때 중앙 구분선 표시
                if (lbl_centerDivider != null)
                {
                    lbl_centerDivider.Visibility = Visibility.Visible;
                }
            }
        }

        // 포크 위치 정보 업데이트
        private void UpdateForkPosInfo()
        {
            if (!gClass.str.GcpInfo.isAdminMode)
            {
                return;
            }

            if (manualJobFork == 1)
            {
                // Fork1 정보 표시
                lbl_forkCurPos.Content = gClass.str.SrmState[gClass.srmNum].fork1.curPos.ToString();
                lbl_forkTargetPos.Content = gClass.str.SrmState[gClass.srmNum].fork1.targetPos.ToString();
            }
            else if (manualJobFork == 2)
            {
                // Fork2 정보 표시
                lbl_forkCurPos.Content = gClass.str.SrmState[gClass.srmNum].fork2.curPos.ToString();
                lbl_forkTargetPos.Content = gClass.str.SrmState[gClass.srmNum].fork2.targetPos.ToString();
            }
        }

        // 주행 위치 정보 업데이트
        private void UpdateTravPosInfo()
        {
            if (!gClass.str.GcpInfo.isAdminMode)
            {
                return;
            }

            lbl_travCurPos.Content = gClass.str.SrmState[gClass.srmNum].trav.curPos.ToString();
            lbl_travTargetPos.Content = gClass.str.SrmState[gClass.srmNum].trav.targetPos.ToString();
        }

        // 승강 위치 정보 업데이트
        private void UpdateHoistPosInfo()
        {
            if (!gClass.str.GcpInfo.isAdminMode)
            {
                return;
            }

            lbl_hoistCurPos.Content = gClass.str.SrmState[gClass.srmNum].lift.curPos.ToString();
            lbl_hoistTargetPos.Content = gClass.str.SrmState[gClass.srmNum].lift.targetPos.ToString();
        }
    }
}
