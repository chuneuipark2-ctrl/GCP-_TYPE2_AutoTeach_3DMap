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
    /// PageDevState.xaml??????곹샇 ?묒슜 ?쇰━
    /// </summary>
    /// 
    public enum ForkPosition
    {
        LeftFull = -3, // 0xFFFFFFFD
        LeftHalf = -2, // 0xFFFFFFFE
        LeftMiddle = -1, // 0xFFFFFFFF
        Center = 0,
        RightMiddle = 1,
        RightHalf = 2,
        RightFull = 3,
    }
    public partial class PageDevState : Page
    {
        // Define ImageBrush
        ImageBrush moveR = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/moveR.png")));
        ImageBrush moveL = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/moveL.png")));
        ImageBrush moveRC = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/moveRC.png")));
        ImageBrush moveLC = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/moveLC.png")));
        ImageBrush moveRN = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/moveRN.png")));
        ImageBrush moveLN = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/moveLN.png")));
        ImageBrush moveU = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/moveU.png")));
        ImageBrush moveUC = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/moveUC.png")));
        ImageBrush moveUN = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/moveUN.png")));
        ImageBrush moveD = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/moveD.png")));
        ImageBrush moveDC = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/moveDC.png")));
        ImageBrush moveDN = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/moveDN.png")));
        ImageBrush fkC = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/fkM.png")));
        ImageBrush fkCN = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/fkMN.png")));
        private List<Label> posLabelListFork1;
        private List<Label> posLabelListFork2;
        private List<Label> dirLabelListFork1;
        private List<Label> dirLabelListFork2;
        // ?붾? ?덉씠釉?
        Label tmpLabel = new Label();

        //State Timer
        Timer stateTimer = new Timer();

        // ?ы겕 ?묒뾽?쒖떆 愿??蹂??
        private uint curJobNo;
        private int curFromStn;
        private int curFromRow;
        private int curFromBay;
        private int curFromLev;
        private int curToStn;
        private int curToRow;
        private int curToBay;
        private int curToLev;
        private int curJobType;
        private int curJobState;
        private int curJobProc;
        private string curFrom = "";
        private string curTo = "";
        private string curJob = "";
        private string curState = "";
        private string curProc = "";
        private int procCnt;
        private int fmove;
        private int bmove;

        int srmNum;

        //Singletone
        singletonClass gClass;

        public PageDevState(int srmNum)
        {
            InitializeComponent();
            posLabelListFork1 = new List<Label>()
    {
      lbl_fPosC,
      lbl_fPosLF,
      lbl_fPosLH,
      lbl_fPosLM,
      lbl_fPosRF,
      lbl_fPosRH,
      lbl_fPosRM
    };
            posLabelListFork2 = new List<Label>()
    {
      lbl_fPosC2,
      lbl_fPosLF2,
      lbl_fPosLH2,
      lbl_fPosLM2,
      lbl_fPosRF2,
      lbl_fPosRH2,
      lbl_fPosRM2
    };
            dirLabelListFork1 = new List<Label>()
    {
      lbl_fDirLF,
      lbl_fDirLH,
      lbl_fDirLM,
      lbl_fDirRF,
      lbl_fDirRH,
      lbl_fDirRM
    };
            dirLabelListFork2 = new List<Label>()
    {
      lbl_fDirLF2,
      lbl_fDirLH2,
      lbl_fDirLM2,
      lbl_fDirRF2,
      lbl_fDirRH2,
      lbl_fDirRM2
    };
            gClass = singletonClass.Instance;
            srmNum = srmNum;
            lbl_SrmNum.Content = (object)("SRM #" + gClass.str.SrmInfo[srmNum].srmID.ToString());
            Btn_Fork1.Click += new RoutedEventHandler(Select_ForkJob);
            Btn_Fork1.IsEnabled = true;
            Btn_Fork2.Click += new RoutedEventHandler(Select_ForkJob);
            Btn_Fork2.IsEnabled = true;
            if (gClass.str.SrmInfo[gClass.srmNum].forkCnt == 2)
            {
                Grid_Fork2State.Visibility = Visibility.Visible;
            }
            else
            {
                Grid_Fork2State.Visibility = Visibility.Collapsed;
                Grid_DevMain.RowDefinitions[0].Height = new GridLength(6.0, GridUnitType.Star);
                Grid_DevMain.RowDefinitions[1].Height = new GridLength(4.0, GridUnitType.Star);
                Grid_JobState.RowDefinitions[2].Height = new GridLength(0.0, GridUnitType.Star);
            }
            if (gClass.str.SrmInfo[gClass.srmNum].forkType == 1)
            {
                lbl_fDirLF.Visibility = Visibility.Collapsed;
                lbl_fDirLF2.Visibility = Visibility.Collapsed;
                lbl_fDirLM.Visibility = Visibility.Collapsed;
                lbl_fDirLM2.Visibility = Visibility.Collapsed;
                lbl_fPosLF.Visibility = Visibility.Collapsed;
                lbl_fPosLF2.Visibility = Visibility.Collapsed;
                lbl_fPosLM.Visibility = Visibility.Collapsed;
                lbl_fPosLM2.Visibility = Visibility.Collapsed;
                lbl_fDirRF.Visibility = Visibility.Collapsed;
                lbl_fDirRF2.Visibility = Visibility.Collapsed;
                lbl_fDirRM.Visibility = Visibility.Collapsed;
                lbl_fDirRM2.Visibility = Visibility.Collapsed;
                lbl_fPosRF.Visibility = Visibility.Collapsed;
                lbl_fPosRF2.Visibility = Visibility.Collapsed;
                lbl_fPosRM.Visibility = Visibility.Collapsed;
                lbl_fPosRM2.Visibility = Visibility.Collapsed;
                lbl_fPosLH.Content = (object)"F";
                lbl_fPosRH.Content = (object)"F";
                lbl_fPosLH2.Content = (object)"F";
                lbl_fPosRH2.Content = (object)"F";
                Grid_Fork1Position.ColumnDefinitions[0].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_Fork1Position.ColumnDefinitions[1].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_Fork1Position.ColumnDefinitions[2].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_Fork1Position.ColumnDefinitions[3].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_Fork1Position.ColumnDefinitions[9].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_Fork1Position.ColumnDefinitions[10].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_Fork1Position.ColumnDefinitions[11].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_Fork1Position.ColumnDefinitions[12].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_Fork2Position.ColumnDefinitions[0].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_Fork2Position.ColumnDefinitions[1].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_Fork2Position.ColumnDefinitions[2].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_Fork2Position.ColumnDefinitions[3].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_Fork2Position.ColumnDefinitions[9].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_Fork2Position.ColumnDefinitions[10].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_Fork2Position.ColumnDefinitions[11].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_Fork2Position.ColumnDefinitions[12].Width = new GridLength(0.0, GridUnitType.Star);
            }
            else if (gClass.str.SrmInfo[gClass.srmNum].forkType == 2)
            {
                lbl_fDirLM.Visibility = Visibility.Collapsed;
                lbl_fDirLM2.Visibility = Visibility.Collapsed;
                lbl_fPosLM.Visibility = Visibility.Collapsed;
                lbl_fPosLM2.Visibility = Visibility.Collapsed;
                lbl_fDirRM.Visibility = Visibility.Collapsed;
                lbl_fDirRM2.Visibility = Visibility.Collapsed;
                lbl_fPosRM.Visibility = Visibility.Collapsed;
                lbl_fPosRM2.Visibility = Visibility.Collapsed;
                Grid_Fork1Position.ColumnDefinitions[2].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_Fork1Position.ColumnDefinitions[3].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_Fork1Position.ColumnDefinitions[9].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_Fork1Position.ColumnDefinitions[10].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_Fork2Position.ColumnDefinitions[2].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_Fork2Position.ColumnDefinitions[3].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_Fork2Position.ColumnDefinitions[9].Width = new GridLength(0.0, GridUnitType.Star);
                Grid_Fork2Position.ColumnDefinitions[10].Width = new GridLength(0.0, GridUnitType.Star);
            }
            stateTimer.Interval = 500.0;
            stateTimer.AutoReset = true;
            stateTimer.Elapsed += new ElapsedEventHandler(StateTimer_Elapsed);
            stateTimer.Start();
        }

        private void StateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    if (gClass.str.SrmState[srmNum].autoMode > 0)
                    {
                        if (gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.SEMI_AUTO].value)
                            lbl_SemiAuto.Content = "SEMIAUTO";
                        else
                            lbl_SemiAuto.Content = "AUTO";
                    }
                    else if (gClass.str.SrmState[srmNum].manualMode > 0)
                    {
                        if (gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.SEMI_AUTO].value)
                            lbl_SemiAuto.Content = "SEMIAUTO";
                        else
                            lbl_SemiAuto.Content = "MANUAL";
                    }
                    else if (gClass.str.SrmState[srmNum].setupMode > 0)
                        lbl_SemiAuto.Content = "SETUP";
                    else if (gClass.str.SrmState[srmNum].forcedMode > 0)
                        lbl_SemiAuto.Content = "FORCED";
                    else
                        lbl_SemiAuto.Content = "NONE";
                    lbl_JobStep.Content = Enum.GetName(typeof(JOBSTATE), gClass.str.SrmPacket[gClass.srmNum].jobState);
                    if (procCnt < 0)
                    {
                        if (Grid_Fork1ProcBtn.Visibility == Visibility.Visible)
                        {
                            Grid.SetRowSpan(Grid_Fork1JobArea, 2);
                            Grid_Fork1ProcBtn.Visibility = Visibility.Collapsed;
                            Btn_Fork1.IsChecked = new bool?(false);
                        }
                        if (Grid_Fork2ProcBtn.Visibility == Visibility.Visible)
                        {
                            Grid.SetRowSpan(Grid_Fork2JobArea, 2);
                            Grid_Fork2ProcBtn.Visibility = Visibility.Collapsed;
                            Btn_Fork2.IsChecked = new bool?(false);
                        }
                        procCnt = 10;
                    }
                    else
                        --procCnt;
                    startStateCheck();
                    bool flag = false;
                    if (gClass.str.SrmState[srmNum].autoMode > 0)
                    {
                        if (gClass.str.SrmPacket[srmNum].fork1JobComplete > 0 || gClass.str.SrmPacket[srmNum].fork2JobComplete > 0)
                            lampEllipse.Fill = true ? Brushes.LimeGreen : Brushes.Red;
                        else
                            lampEllipse.Fill = false ? Brushes.LimeGreen : Brushes.Red;
                        if (gClass.str.SrmInfo[gClass.srmNum].forkCnt > 1)
                        {
                            if (gClass.str.SrmPacket[srmNum].fork1JobComplete > 0 && gClass.str.SrmPacket[srmNum].fork2JobComplete > 0)
                                lampEllipse.Fill = true ? Brushes.LimeGreen : Brushes.Red;
                            else
                                lampEllipse.Fill = false ? (Brush)Brushes.LimeGreen : (Brush)Brushes.Red;
                        }
                        if (gClass.str.SrmPacket[srmNum].jobRequest)
                            lampEllipse.Fill = true ? Brushes.White : Brushes.Red;
                        else
                            lampEllipse.Fill = false ? Brushes.White : Brushes.Red;
                    }
                    else if (gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.SEMI_AUTO].value)
                    {
                        if (gClass.str.SrmPacket[srmNum].fork1JobComplete > 0 || gClass.str.SrmPacket[srmNum].fork2JobComplete > 0)
                            lampEllipse.Fill = true ? Brushes.LimeGreen : Brushes.Red;
                        else
                            lampEllipse.Fill = false ? Brushes.LimeGreen : Brushes.Red;
                        if (gClass.str.SrmInfo[gClass.srmNum].forkCnt > 1)
                        {
                            if (gClass.str.SrmPacket[srmNum].fork1JobComplete > 0 && gClass.str.SrmPacket[srmNum].fork2JobComplete > 0)
                                lampEllipse.Fill = true ? Brushes.LimeGreen : Brushes.Red;
                            else
                                lampEllipse.Fill = false ? Brushes.LimeGreen : Brushes.Red;
                        }
                        if (!gClass.str.DioPacket[srmNum].DO_TESTMODE)
                        {
                            if (gClass.str.SrmPacket[srmNum].jobRequest)
                            {
                                flag = true;
                                gClass.str.DioPacket[srmNum].DOSET[6].value = true;
                                lampEllipse.Fill = Brushes.White;
                            }
                            else
                            {
                                flag = false;
                                gClass.str.DioPacket[srmNum].DOSET[6].value = false;
                                lampEllipse.Fill = Brushes.Red;
                            }
                        }
                    }
                    if (gClass.str.SrmState[srmNum].manualMode > 0 && gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.MANUAL].value)
                    {
                        Btn_Fork1.IsEnabled = true;
                        Btn_Fork2.IsEnabled = true;
                    }
                    else
                    {
                        //For Test
                        //Btn_Fork1.IsEnabled = true;
                        //Btn_Fork2.IsEnabled = true;

                        Btn_Fork1.IsEnabled = false;
                        Btn_Fork2.IsEnabled = false;
                    }
                    if (gClass.str.SrmState[srmNum].trav.trSt1MoveDirec > 0)
                    {
                        if (lbl_FMove.Background != fkCN)
                            lbl_FMove.Background = (Brush)fkCN;
                        if (gClass.str.SrmState[srmNum].trav.trSt1OperState > 0)
                        {
                            if (lbl_BMove.Background == moveL)
                                lbl_BMove.Background = moveLC;
                            else
                                lbl_BMove.Background = moveL;
                        }
                        else if (lbl_BMove.Background != fkCN)
                            lbl_BMove.Background = (Brush)fkCN;
                    }
                    else
                    {
                        if (lbl_BMove.Background != fkCN)
                            lbl_BMove.Background = fkCN;
                        if (gClass.str.SrmState[srmNum].trav.trSt1OperState > 0)
                        {
                            if (lbl_FMove.Background == moveR)
                                lbl_FMove.Background = moveRC;
                            else
                                lbl_FMove.Background = moveR;
                        }
                        else if (lbl_FMove.Background != fkCN)
                            lbl_FMove.Background = fkCN;
                    }
                    if (gClass.str.SrmState[srmNum].trav.trSt1OriginPos > 0)
                    {
                        if (lbl_curBay.Foreground != Brushes.GreenYellow)
                            lbl_curBay.Foreground = Brushes.GreenYellow;
                    }
                    else if (lbl_curBay.Foreground != Brushes.LightGray)
                        lbl_curBay.Foreground = Brushes.LightGray;
                    if (gClass.str.SrmState[srmNum].lift.liSt1MoveDirec > 0)
                    {
                        if (lbl_UMove.Background != fkCN)
                            lbl_UMove.Background = fkCN;
                        if (gClass.str.SrmState[srmNum].lift.liSt1OperState > 0)
                        {
                            if (lbl_DMove.Background == moveD)
                                lbl_DMove.Background = moveDC;
                            else
                                lbl_DMove.Background = moveD;
                        }
                        else if (lbl_DMove.Background != fkCN)
                            lbl_DMove.Background = fkCN;
                    }
                    else
                    {
                        if (lbl_DMove.Background != fkCN)
                            lbl_DMove.Background = (Brush)fkCN;
                        if (gClass.str.SrmState[srmNum].lift.liSt1OperState > 0)
                        {
                            if (lbl_UMove.Background == moveU)
                                lbl_UMove.Background = moveUC;
                            else
                                lbl_UMove.Background = moveU;
                        }
                        else if (lbl_UMove.Background != fkCN)
                            lbl_UMove.Background = fkCN;
                    }
                    // ST ?곗꽑 泥댄겕: curStation > 0?대㈃ ST ?쒖떆
                    if (gClass.str.SrmState[gClass.srmNum].fork1.curStation > 0)
                    {
                        lbl_nextBay.Content = "-";
                        lbl_prevBay.Content = "-";
                        lbl_nextLev.Content = "-";
                        lbl_prevLev.Content = "-";
                        lbl_curBay.Content = "ST";
                        lbl_curLev.Content = gClass.str.SrmState[srmNum].fork1.curStation.ToString();
                    }
                    else
                    {
                        lbl_prevBay.Content = ((int)gClass.str.SrmState[gClass.srmNum].fork1.curBay - 1);
                        lbl_nextBay.Content = ((int)gClass.str.SrmState[gClass.srmNum].fork1.curBay + 1);
                        lbl_curBay.Content = gClass.str.SrmState[gClass.srmNum].fork1.curBay;
                        lbl_prevLev.Content = ((int)gClass.str.SrmState[gClass.srmNum].fork1.curLev - 1);
                        lbl_nextLev.Content = ((int)gClass.str.SrmState[gClass.srmNum].fork1.curLev + 1);
                        lbl_curLev.Content = gClass.str.SrmState[gClass.srmNum].fork1.curLev;
                    }
                    if (gClass.str.SrmState[srmNum].lift.liSt1OriginPos > 0)
                    {
                        if (lbl_curLev.Foreground != Brushes.GreenYellow)
                            lbl_curLev.Foreground = Brushes.GreenYellow;
                    }
                    else if (lbl_curLev.Foreground != Brushes.LightGray)
                        lbl_curLev.Foreground = Brushes.LightGray;
                    if (gClass.str.SrmState[srmNum].fork1.posLeftUp > 0 || gClass.str.SrmState[srmNum].fork1.posRightUp > 0)
                    {
                        if (lbl_top.BorderBrush != Brushes.Green)
                            lbl_top.BorderBrush = Brushes.Green;
                    }
                    else if (lbl_top.BorderBrush != Brushes.Gray)
                        lbl_top.BorderBrush = Brushes.Gray;
                    if (gClass.str.SrmState[srmNum].fork1.posLeftBottom > 0 || gClass.str.SrmState[srmNum].fork1.posRightBottom > 0)
                    {
                        if (lbl_bottom.BorderBrush != Brushes.Green)
                            lbl_bottom.BorderBrush = (Brush)Brushes.Green;
                    }
                    else if (lbl_bottom.BorderBrush != Brushes.Gray)
                        lbl_bottom.BorderBrush = (Brush)Brushes.Gray;
                    ForkPositionCheck(gClass.str.SrmState[srmNum].fork1.originPos, gClass.str.SrmState[srmNum].fork1.moveDirec, gClass.str.SrmState[srmNum].fork1.operState, gClass.str.SrmState[srmNum].fork1.curPosNum, lbl_fDirLF, lbl_fDirLM, lbl_fDirLH, lbl_fDirRH, lbl_fDirRM, lbl_fDirRF, lbl_fPosLF, lbl_fPosLM, lbl_fPosLH, lbl_fPosC, lbl_fPosRH, lbl_fPosRM, lbl_fPosRF, posLabelListFork1, dirLabelListFork1);
                    ForkPositionCheck(gClass.str.SrmState[srmNum].fork2.originPos, gClass.str.SrmState[srmNum].fork2.moveDirec, gClass.str.SrmState[srmNum].fork2.operState, gClass.str.SrmState[srmNum].fork2.curPosNum, lbl_fDirLF2, lbl_fDirLM2, lbl_fDirLH2, lbl_fDirRH2, lbl_fDirRM2, lbl_fDirRF2, lbl_fPosLF2, lbl_fPosLM2, lbl_fPosLH2, lbl_fPosC2, lbl_fPosRH2, lbl_fPosRM2, lbl_fPosRF2, posLabelListFork2, dirLabelListFork2);

                    // WCS 수신 작업 (윗줄) - Fork1

                    if (gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1JobNo > 0)
                    {
                        ushort reqJobNo = gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1JobNo;
                        int reqFromStn = (int)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1FromSt;
                        int reqFromRow = (int)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1FromRow;
                        int reqFromBay = (int)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1FromBay;
                        int reqFromLev = (int)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1FromLev;
                        int reqToStn = (int)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1ToSt;
                        int reqToRow = (int)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1ToRow;
                        int reqToBay = (int)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1ToBay;
                        int reqToLev = (int)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1ToLev;
                        string reqJob = GetWcsJobNameFork1(srmNum);
                        string reqFrom = reqFromStn <= 0 ? $"R:{reqFromRow}/{reqFromBay}/{reqFromLev}" : $"ST:{reqFromStn}";
                        string reqTo = reqToStn <= 0 ? $"R:{reqToRow}/{reqToBay}/{reqToLev}" : $"ST:{reqToStn}";
                        lbl_Fork1WcsJob.Text = "WCS: " + $"{reqJob} #{reqJobNo} {reqFrom}→{reqTo}";
                    }
                    else
                        lbl_Fork1WcsJob.Text = "WCS: " + cConstDefine.tr("작업없음");

                    if (gClass.str.SrmState[srmNum].fork1.jobNo > 0U || gClass.str.SrmState[srmNum].fork1.mvJobNo > 0U)
                    {
                        curJobNo = gClass.str.SrmState[srmNum].fork1.jobNo;
                        curFromStn = (int)gClass.str.SrmState[srmNum].fork1.fromStation;
                        curFromRow = (int)gClass.str.SrmState[srmNum].fork1.fromRow;
                        curFromBay = (int)gClass.str.SrmState[srmNum].fork1.fromBay;
                        curFromLev = (int)gClass.str.SrmState[srmNum].fork1.fromLev;
                        curToStn = (int)gClass.str.SrmState[srmNum].fork1.toStation;
                        curToRow = (int)gClass.str.SrmState[srmNum].fork1.toRow;
                        curToBay = (int)gClass.str.SrmState[srmNum].fork1.toBay;
                        curToLev = (int)gClass.str.SrmState[srmNum].fork1.toLev;
                        curJobType = (int)gClass.str.SrmState[srmNum].fork1.cmdCode;
                        curJobState = (int)gClass.str.SrmState[srmNum].fork1.procState;
                        curJobProc = (int)gClass.str.SrmState[srmNum].fork1.procStep;
                        if (gClass.str.SrmState[srmNum].fork1.mvJobNo > 0U)
                        {
                            curJobNo = gClass.str.SrmState[srmNum].fork1.mvJobNo;
                            if (curJobType == 0)
                                curJobType = 1;
                        }
                        else
                            curJobNo = gClass.str.SrmState[srmNum].fork1.jobNo;
                        switch (curJobType)
                        {
                            case 0:
                                curJob = cConstDefine.tr("작업없음");
                                break;
                            case 1:
                                curJob = cConstDefine.tr("이동");
                                curToStn = (int)gClass.str.SrmState[srmNum].fork1.mvToStation;
                                curToRow = (int)gClass.str.SrmState[srmNum].fork1.mvToRow;
                                curToBay = (int)gClass.str.SrmState[srmNum].fork1.mvToBay;
                                curToLev = (int)gClass.str.SrmState[srmNum].fork1.mvToLev;
                                curJobState = (int)gClass.str.SrmState[srmNum].fork1.mvProcState;
                                curJobProc = (int)gClass.str.SrmState[srmNum].fork1.mvProcStep;
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
                                curFromRow = (int)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1FromRow;
                                curFromBay = (int)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1FromBay;
                                curFromLev = (int)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1FromLev;
                                break;
                            default:
                                curJob = "-";
                                break;
                        }

                        curFrom = curFromStn <= 0 ? $"R: {curFromRow}/{curFromBay}/{curFromLev}" : "ST: " + curFromStn.ToString();
                        curTo = curToStn <= 0 ? $"R: {curToRow}/{curToBay}/{curToLev}" : "ST: " + curToStn.ToString();
                        lbl_Fork1Job.Text = "SRM: " + $"{curJob}: {curJobNo} - {curFrom} -> {curTo}";
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
                        lbl_jobState.Content = (object)curState;
                        lbl_jobProc.Content = (object)curProc;
                    }
                    else
                    {
                        lbl_Fork1Job.Text = "SRM: " + cConstDefine.tr("작업없음");
                        lbl_jobState.Content = (object)"-";
                        lbl_jobProc.Content = (object)"-";
                    }

                    // WCS 수신 작업 (윗줄) - Fork2
                    if (gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2JobNo > 0)
                    {
                        ushort reqJobNo = gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2JobNo;
                        int reqFromStn = (int)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2FromSt;
                        int reqFromRow = (int)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2FromRow;
                        int reqFromBay = (int)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2FromBay;
                        int reqFromLev = (int)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2FromLev;
                        int reqToStn = (int)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2ToSt;
                        int reqToRow = (int)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2ToRow;
                        int reqToBay = (int)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2ToBay;
                        int reqToLev = (int)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2ToLev;
                        string reqJob = GetWcsJobNameFork2(srmNum);
                        string reqFrom = reqFromStn <= 0 ? $"R:{reqFromRow}/{reqFromBay}/{reqFromLev}" : $"ST:{reqFromStn}";
                        string reqTo = reqToStn <= 0 ? $"R:{reqToRow}/{reqToBay}/{reqToLev}" : $"ST:{reqToStn}";
                        lbl_Fork2WcsJob.Text = "WCS: " + $"{reqJob} #{reqJobNo} {reqFrom}→{reqTo}";
                    }
                    else
                        lbl_Fork2WcsJob.Text = "WCS: " + cConstDefine.tr("작업없음");

                    if (gClass.str.SrmState[srmNum].fork2.jobNo > 0U || gClass.str.SrmState[srmNum].fork2.mvJobNo > 0U)
                    {
                        curJobNo = gClass.str.SrmState[srmNum].fork2.jobNo;
                        curFromStn = (int)gClass.str.SrmState[srmNum].fork2.fromStation;
                        curFromRow = (int)gClass.str.SrmState[srmNum].fork2.fromRow;
                        curFromBay = (int)gClass.str.SrmState[srmNum].fork2.fromBay;
                        curFromLev = (int)gClass.str.SrmState[srmNum].fork2.fromLev;
                        curToStn = (int)gClass.str.SrmState[srmNum].fork2.toStation;
                        curToRow = (int)gClass.str.SrmState[srmNum].fork2.toRow;
                        curToBay = (int)gClass.str.SrmState[srmNum].fork2.toBay;
                        curToLev = (int)gClass.str.SrmState[srmNum].fork2.toLev;
                        curJobType = (int)gClass.str.SrmState[srmNum].fork2.cmdCode;
                        curJobState = (int)gClass.str.SrmState[srmNum].fork2.procState;
                        curJobProc = (int)gClass.str.SrmState[srmNum].fork2.procStep;
                        if (gClass.str.SrmState[srmNum].fork2.mvJobNo > 0U)
                        {
                            curJobNo = gClass.str.SrmState[srmNum].fork2.mvJobNo;
                            if (curJobType == 0)
                                curJobType = 1;
                        }
                        else
                            curJobNo = gClass.str.SrmState[srmNum].fork2.jobNo;
                        switch (curJobType)
                        {
                            case 0:
                                curJob = cConstDefine.tr("작업없음");
                                break;
                            case 1:
                                curJob = cConstDefine.tr("이동");
                                curToStn = (int)gClass.str.SrmState[srmNum].fork2.mvToStation;
                                curToRow = (int)gClass.str.SrmState[srmNum].fork2.mvToRow;
                                curToBay = (int)gClass.str.SrmState[srmNum].fork2.mvToBay;
                                curToLev = (int)gClass.str.SrmState[srmNum].fork2.mvToLev;
                                curJobState = (int)gClass.str.SrmState[srmNum].fork2.mvProcState;
                                curJobProc = (int)gClass.str.SrmState[srmNum].fork2.mvProcStep;
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
                            case 24:
                                curJob = cConstDefine.tr("Sticky 명령");
                                break;
                            default:
                                curJob = "-";
                                break;
                        }
                        curFrom = curFromStn <= 0 ? $"R: {curFromRow}/{curFromBay}/{curFromLev}" : "ST: " + curFromStn.ToString();
                        curTo = curToStn <= 0 ? $"R: {curToRow}/{curToBay}/{curToLev}" : "ST: " + curToStn.ToString();
                        lbl_Fork2Job.Text = "SRM: " + $"{curJob}: {curJobNo} - {curFrom} -> {curTo}";
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
                        lbl_jobState2.Content = (object)curState;
                        lbl_jobProc2.Content = (object)curProc;
                    }
                    else
                    {
                        lbl_Fork2Job.Text = "SRM: " + cConstDefine.tr("작업없음");
                        lbl_jobState2.Content = (object)"-";
                        lbl_jobProc2.Content = (object)"-";
                    }
                }
                catch (Exception ex)
                {
                    cIniAccess.SaveExLog(0, "EXCEPTION - PageDevStateTimer : " + ex.Message);
                }
            });
        }

        private void ForkPositionCheck(
          byte onPos,
          byte direction,
          byte operation,
          sbyte posNo,
          Label fDirLF,
          Label fDirLM,
          Label fDirLH,
          Label fDirRH,
          Label fDirRM,
          Label fDirRF,
          Label fPosLF,
          Label fPosLM,
          Label fPosLH,
          Label fPosC,
          Label fPosRH,
          Label fPosRM,
          Label fPosRF,
          List<Label> posList,
          List<Label> dirList)
        {
            Label label1 = (Label)null;
            Label label2 = (Label)null;
            switch (posNo)
            {
                case -3:
                    label2 = fPosLF;
                    label1 = fDirLF;
                    break;
                case -2:
                    label2 = fPosLM;
                    label1 = fDirLM;
                    break;
                case -1:
                    label2 = fPosLH;
                    label1 = fDirLH;
                    break;
                case 0:
                    label2 = fPosC;
                    label1 = tmpLabel;
                    break;
                case 1:
                    label2 = fPosRH;
                    label1 = fDirRH;
                    break;
                case 2:
                    label2 = fPosRM;
                    label1 = fDirRM;
                    break;
                case 3:
                    label2 = fPosRF;
                    label1 = fDirRF;
                    break;
            }
            if (onPos > 0)
            {
                label2.Foreground = (Brush)Brushes.GreenYellow;
                if (operation > 0)
                {
                    if (direction == 0)
                    {
                        if (label1.Background == moveR)
                            label1.Background = (Brush)moveRN;
                        else
                            label1.Background = (Brush)moveR;
                    }
                    else if (label1.Background == moveL)
                        label1.Background = (Brush)moveLN;
                    else
                        label1.Background = (Brush)moveL;
                }
                else
                    label1.Background = (Brush)fkCN;
            }
            else
            {
                label2.Foreground = (Brush)Brushes.Gray;
                if (operation > 0)
                {
                    if (direction == 0)
                    {
                        if (label1.Background == moveR)
                            label1.Background = (Brush)moveRN;
                        else
                            label1.Background = (Brush)moveR;
                    }
                    else if (label1.Background == moveL)
                        label1.Background = (Brush)moveLN;
                    else
                        label1.Background = (Brush)moveL;
                }
                else if (label1.Background == fkC)
                    label1.Background = (Brush)fkCN;
                else
                    label1.Background = (Brush)fkC;
            }
            foreach (Label pos in posList)
            {
                if (pos != label2)
                    pos.Foreground = (Brush)Brushes.Gray;
            }
            foreach (Label dir in dirList)
            {
                if (dir != label1)
                    dir.Background = (Brush)fkCN;
            }
        }

        private void Select_ForkJob(object sender, RoutedEventArgs e)
        {
            ToggleButton toggleButton = sender as ToggleButton;
            if (toggleButton.IsChecked.GetValueOrDefault())
            {
                procCnt = 10;
                if (toggleButton == Btn_Fork1)
                {
                    Grid.SetRowSpan(Grid_Fork1JobArea, 1);
                    Grid_Fork1ProcBtn.Visibility = Visibility.Visible;
                }
                else
                {
                    if (toggleButton != Btn_Fork2)
                        return;
                    Grid.SetRowSpan(Grid_Fork2JobArea, 1);
                    Grid_Fork2ProcBtn.Visibility = Visibility.Visible;
                }
            }
            else if (toggleButton == Btn_Fork1)
            {
                Grid.SetRowSpan(Grid_Fork1JobArea, 2);
                Grid_Fork1ProcBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (toggleButton != Btn_Fork2)
                    return;
                Grid.SetRowSpan(Grid_Fork2JobArea, 2);
                Grid_Fork2ProcBtn.Visibility = Visibility.Collapsed;
            }
        }

        private void Btn_Home_Click(object sender, RoutedEventArgs e)
        {
            if (gClass.str.SrmPacket[srmNum].srmCommDiscCnt <= 0)
            {
                VarMessageBox.Show(cConstDefine.tr("통신확인"), cConstDefine.tr("크레인 통신상태 확인"), VarMessageBoxButton.OK);
            }
            else if (gClass.str.SrmState[srmNum].autoMode > 0)
            {
                if (gClass.str.SrmState[srmNum].dSt1Abnormal > 0 || gClass.str.SrmPacket[srmNum].gcpError)
                {
                    int num2 = (int)VarMessageBox.Show(cConstDefine.tr("장치이상"), cConstDefine.tr("장치 이상을 해제해 주십시오."), VarMessageBoxButton.OK);
                }
                else if (gClass.str.SrmState[srmNum].gcpState.gcpTxMode <= 1)
                {
                    VarMessageBox.Show(cConstDefine.tr("홈복귀"), cConstDefine.tr("장치가 수동 상태입니다."), VarMessageBoxButton.OK);
                }
                else if (gClass.str.SrmState[srmNum].dSt2homePos > 0)
                {
                    VarMessageBox.Show(cConstDefine.tr("홈복귀"), cConstDefine.tr("장치가 현재 홈 위치에 있습니다."), VarMessageBoxButton.OK);
                    gClass.str.SrmPacket[srmNum].homeCmd = 0;
                }
                else if (gClass.str.SrmPacket[srmNum].operState)
                {
                    VarMessageBox.Show(cConstDefine.tr("동작 중"), cConstDefine.tr("장치가 현재 동작 중 입니다."), VarMessageBoxButton.OK);
                }
                else
                {
                    if (VarMessageBox.Show(cConstDefine.tr("홈복귀"), cConstDefine.tr("홈복귀 동작을 진행하시겠습니까?"), VarMessageBoxButton.OKCancel) != VarMessageBoxResult.OK)
                        return;
                    cIniAccess.SaveJobLog(srmNum, "GCP 홈복귀 클릭");
                    gClass.str.SrmPacket[srmNum].pulseClicked = true;
                    gClass.str.SrmPacket[srmNum].homeCmd = 1;
                }
            }
            else
            {
                int num4 = (int)VarMessageBox.Show(cConstDefine.tr("실패"), cConstDefine.tr("크레인 자동모드가 아닙니다."), VarMessageBoxButton.OK);
            }
        }

        private void Btn_Reset_Click(object sender, RoutedEventArgs e)
        {

            // 吏?곷컲 ?먮윭??由ъ뀑???섏뼱?쇳븯誘濡? 二쇱꽍泥섎━ ??
            //if (gClass.str.SrmPacket[srmNum].srmCommDiscCnt <= 0)
            //{
            //    int num = (int)MessageBox.Show(cConstDefine.tr("?щ젅???듭떊?곹깭 ?뺤씤"), cConstDefine.tr("?듭떊?뺤씤"), MessageBoxButton.OK, MessageBoxImage.Asterisk);
            //}
            //else
            //{
            cIniAccess.SaveJobLog(srmNum, "GCP 이상리셋 클릭");
            gClass.str.SrmPacket[srmNum].gcpError = false;
            gClass.str.SrmPacket[srmNum].recovError = false;
            gClass.str.DioPacket[srmNum].DOSET[(int)DOSTATE.BUZZER].value = false;
            gClass.str.SrmPacket[srmNum].jobError = false;
            gClass.str.SrmPacket[srmNum].gcpModemFlt = false;

            // SRM ?듭떊 ?뺤긽???뚮쭔 紐낅졊由ъ뀑 ?꾩넚
            if (gClass.str.SrmPacket[srmNum].srmCommDiscCnt > 0)
            {
                gClass.str.SrmPacket[srmNum].pulseClicked = true;
                gClass.str.SrmPacket[srmNum].resetCmd = 1;
            }
            //}
        }

        private void Btn_BuzzStop_Click(object sender, RoutedEventArgs e)
        {
            cIniAccess.SaveJobLog(srmNum, "GCP 부저정지 클릭");
            gClass.str.SrmPacket[srmNum].buzzerStop = true;
            gClass.str.DioPacket[srmNum].DOSET[3].value = false;
        }

        public void StartBtn_State(int state)
        {
            switch (state)
            {
                case 0:
                    Btn_StartOnOff.IsEnabled = false;
                    Btn_StartOnOff.Background = Brushes.LightGray;
                    Btn_StartOnOff.Content = "OFFLINE";
                    break;
                case 1:
                    Btn_StartOnOff.IsEnabled = true;
                    Btn_StartOnOff.Content = cConstDefine.tr("시작가능");
                    if (Btn_StartOnOff.Background == Brushes.GreenYellow)
                    {
                        Btn_StartOnOff.Background = Brushes.LightGray;
                        break;
                    }
                    Btn_StartOnOff.Background = Brushes.GreenYellow;
                    break;
                case 2:
                    Btn_StartOnOff.IsEnabled = true;
                    Btn_StartOnOff.Content = "ONLINE";
                    Btn_StartOnOff.Background = Brushes.GreenYellow;
                    break;
            }
        }

        private void Btn_StartOnOff_Click(object sender, RoutedEventArgs e)
        {
            if (gClass.str.SrmPacket[srmNum].srmCommDiscCnt <= 0)
            {
                int num1 = (int)VarMessageBox.Show(cConstDefine.tr("통신확인"), cConstDefine.tr("크레인 통신상태 확인"), VarMessageBoxButton.OK);
            }
            else if (gClass.str.SrmState[srmNum].dSt1StartSt > 0)
            {
                if (VarMessageBox.Show(cConstDefine.tr("OFFLINE"), cConstDefine.tr("OFFLINE 으로 변경하시겠습니까?"), VarMessageBoxButton.OKCancel) != VarMessageBoxResult.OK)
                    return;
                cIniAccess.SaveJobLog(srmNum, "GCP == OFFLINE 클릭");
                gClass.str.SrmPacket[srmNum].pulseClicked = true;
                gClass.str.SrmPacket[srmNum].startCmd = 1;
                gClass.str.SrmPacket[srmNum].startOnOff = 0;
            }
            else if (gClass.str.SrmState[srmNum].dSt1Abnormal > 0 || gClass.str.SrmPacket[srmNum].gcpError)
            {
                int num2 = (int)VarMessageBox.Show(cConstDefine.tr("장치이상"), cConstDefine.tr("장치 이상을 해제해 주십시오."), VarMessageBoxButton.OK);
            }
            else if (!gClass.str.SrmPacket[srmNum].startEnable)
            {
                int num3 = (int)VarMessageBox.Show(cConstDefine.tr("시작불가"), cConstDefine.tr("장치 시작가능 상태가 아닙니다."), VarMessageBoxButton.OK);
            }
            else
            {

                if (gClass.str.SrmPacket[srmNum].wcsReqCompleteFork1 > 0 || gClass.str.SrmPacket[srmNum].wcsReqCompleteFork2 > 0)
                {
                    if (VarMessageBox.Show(cConstDefine.tr("확인"), cConstDefine.tr("수동 작업완료 처리 대기 중 입니다\n취소하고 작업을 시작하시겠습니까?"), VarMessageBoxButton.OKCancel) != VarMessageBoxResult.OK)
                        return;
                    gClass.str.SrmPacket[srmNum].wcsReqCompleteFork1 = 0;
                    gClass.str.SrmPacket[srmNum].wcsReqCompleteFork2 = 0;
                }

                if (gClass.str.SrmPacket[srmNum].wcsReqDeleteFork1 > 0 || gClass.str.SrmPacket[srmNum].wcsReqDeleteFork2 > 0)
                {
                    if (VarMessageBox.Show(cConstDefine.tr("확인"), cConstDefine.tr("수동 작업삭제 처리 대기 중 입니다\n취소하고 작업을 시작하시겠습니까?"), VarMessageBoxButton.OKCancel) != VarMessageBoxResult.OK)
                        return;
                    gClass.str.SrmPacket[srmNum].wcsReqDeleteFork1 = 0;
                    gClass.str.SrmPacket[srmNum].wcsReqDeleteFork2 = 0;
                }

                if (gClass.str.SrmState[srmNum].fork1.jobNo > 0 || gClass.str.SrmState[srmNum].fork2.jobNo > 0 || gClass.str.SrmState[srmNum].fork1.mvJobNo > 0 || gClass.str.SrmState[srmNum].fork2.mvJobNo > 0)
                {
                    string message = cConstDefine.tr("SRM 작업이 존재합니다. 아래 작업을 수행하시겠습니까?\nSRM작업: ") + GetCurrentSrmJobMessage();
                    if (VarMessageBox.Show(cConstDefine.tr("확인"), message, VarMessageBoxButton.OKCancel) != VarMessageBoxResult.OK)
                        return;
                }
                else
                {
                    if (VarMessageBox.Show(cConstDefine.tr("ONLINE"), cConstDefine.tr("ONLINE 으로 변경하시겠습니까?"), VarMessageBoxButton.OKCancel) != VarMessageBoxResult.OK)
                        return;

                }
                cIniAccess.SaveJobLog(srmNum, "GCP == ONLINE 클릭");
                gClass.str.SrmPacket[srmNum].pulseClicked = true;
                gClass.str.SrmPacket[srmNum].startCmd = 1;
                gClass.str.SrmPacket[srmNum].startOnOff = 1;
            }
        }

        private void Btn_Complete_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (gClass.str.SrmPacket[srmNum].srmCommDiscCnt <= 0)
            {
                int num1 = (int)VarMessageBox.Show(cConstDefine.tr("통신확인"), cConstDefine.tr("크레인 통신상태 확인"), VarMessageBoxButton.OK);
            }
            else if (gClass.str.SrmPacket[srmNum].operState)
            {
                int num2 = (int)VarMessageBox.Show(cConstDefine.tr("동작 중"), cConstDefine.tr("장치가 현재 동작 중 입니다."), VarMessageBoxButton.OK);
            }
            else
            {
                if (VarMessageBox.Show(cConstDefine.tr("작업완료"), cConstDefine.tr("현재 작업을 완료처리 하시겠습니까?"), VarMessageBoxButton.YesNo) != VarMessageBoxResult.Yes)
                    return;
                if (button == Btn_Fork1Complete)
                {
                    if (gClass.str.SrmPacket[srmNum].reqJobNoFk1 < 30000 && gClass.str.SrmPacket[srmNum].reqJobNoFk1 > 0)
                    {
                        gClass.str.SrmPacket[srmNum].wcsReqCompleteFork1 = 1;
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == Fork1 완료처리요청");
                    }
                    else
                    {
                        cIniAccess.SaveJobLog(srmNum, "GCP == Fork1 수동완료");
                        gClass.str.SrmPacket[srmNum].manuFork1JobComplete = 1;
                        cIniAccess.ChangeJobState(srmNum, JOBSTATE.COMPLETE, "(삭제대기)");
                    }
                }
                else
                {
                    if (button != Btn_Fork2Complete)
                        return;
                    if (gClass.str.SrmPacket[srmNum].reqJobNoFk2 < 30000 && gClass.str.SrmPacket[srmNum].reqJobNoFk2 > 0)
                    {
                        gClass.str.SrmPacket[srmNum].wcsReqCompleteFork2 = 1;
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == Fork2 완료처리요청");
                    }
                    else
                    {
                        cIniAccess.SaveJobLog(srmNum, "GCP == Fork2 수동완료");
                        gClass.str.SrmPacket[srmNum].manuFork2JobComplete = 1;
                        cIniAccess.ChangeJobState(srmNum, JOBSTATE.COMPLETE, "(삭제대기)");
                    }
                }
            }
        }

        private void Btn_Delete_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (gClass.str.SrmPacket[srmNum].srmCommDiscCnt <= 0)
            {
                int num1 = (int)VarMessageBox.Show(cConstDefine.tr("통신확인"), cConstDefine.tr("크레인 통신상태 확인"), VarMessageBoxButton.OK);
            }
            else if (gClass.str.SrmPacket[srmNum].operState)
            {
                int num2 = (int)VarMessageBox.Show(cConstDefine.tr("동작 중"), cConstDefine.tr("장치가 현재 동작 중 입니다."), VarMessageBoxButton.OK);
            }
            else
            {
                if (VarMessageBox.Show(cConstDefine.tr("작업삭제"), cConstDefine.tr("작업을 삭제하시겠습니까?"), VarMessageBoxButton.YesNo) != VarMessageBoxResult.Yes)
                    return;
                if (button == Btn_Fork1Delete)
                {
                    if (gClass.str.SrmPacket[srmNum].reqJobNoFk1 < 30000 && gClass.str.SrmPacket[srmNum].reqJobNoFk1 > 0)
                    {
                        gClass.str.SrmPacket[srmNum].wcsReqDeleteFork1 = 1;
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == Fork1 삭제처리요청");
                    }
                    else
                    {
                        cIniAccess.SaveJobLog(srmNum, "GCP == Fork1 수동삭제");
                        gClass.str.SrmPacket[srmNum].manuFork1JobDelete = 1;
                        cIniAccess.ChangeJobState(srmNum, JOBSTATE.COMPLETE, "(삭제대기)");
                    }
                }
                else
                {
                    if (button != Btn_Fork2Delete)
                        return;
                    if (gClass.str.SrmPacket[srmNum].reqJobNoFk2 < 30000 && gClass.str.SrmPacket[srmNum].reqJobNoFk2 > 0)
                    {
                        gClass.str.SrmPacket[srmNum].wcsReqDeleteFork2 = 1;
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == Fork2 삭제처리요청");
                    }
                    else
                    {
                        cIniAccess.SaveJobLog(srmNum, "GCP == Fork2 수동삭제");
                        gClass.str.SrmPacket[srmNum].manuFork2JobDelete = 1;
                        cIniAccess.ChangeJobState(srmNum, JOBSTATE.COMPLETE, "(삭제대기)");
                    }
                }
            }
        }

        public void startStateCheck()
        {
            //Console.WriteLine("?쒖옉?곹깭 ?붾쾭洹?);
            //gClass.str.SrmPacket[srmNum].srmCommDiscCnt = 1;
            if (gClass.str.SrmPacket[srmNum].srmCommDiscCnt <= 0)
            {
                lbl_travInfo.Content = "";
                lbl_liftInfo.Content = "";
                lbl_forkInfo.Content = "";
                //lbl_startInfo.Content = cConstDefine.tr("크레인 통신 상태 확인");
                lbl_notifyJob.Visibility = Visibility.Collapsed;
                grid_Block.Visibility = Visibility.Visible;
                Grid.SetRowSpan(grid_Block, 3);
                //Panel.SetZIndex(grid_Block, 1);

                // For Test
                //Panel.SetZIndex(grid_Block, -1);
            }
            else
            {

                if (gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.REQ_STOP].value == true && gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.REQ_STOP].pin > 0)       // 251107 shk ?붿옱媛먯?
                {
                    lbl_travInfo.Content = "";
                    lbl_liftInfo.Content = "";
                    lbl_forkInfo.Content = "";
                    lbl_startInfo.Content = cConstDefine.tr("비상상황 발생\n");
                    lbl_notifyJob.Visibility = Visibility.Collapsed;
                    grid_Block.Visibility = Visibility.Visible;
                    Grid.SetRowSpan(grid_Block, 2);
                    Panel.SetZIndex(grid_Block, 1);
                    return;
                }

                if (gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.MAINT].value == true && gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.MAINT].pin > 0)       // 251107 shk 硫붿씤??紐⑤뱶 ??
                {
                    lbl_travInfo.Content = "";
                    lbl_liftInfo.Content = "";
                    lbl_forkInfo.Content = "";
                    lbl_startInfo.Content = cConstDefine.tr("정비모드 진입\n");
                    lbl_notifyJob.Visibility = Visibility.Collapsed;
                    grid_Block.Visibility = Visibility.Visible;
                    Grid.SetRowSpan(grid_Block, 3);
                    Panel.SetZIndex(grid_Block, 1);
                    return;
                }


                bool flag = true;
                if (gClass.str.SrmState[srmNum].trav.trSt2HomeCheck > 0)
                {
                    lbl_travInfo.Content = "";
                }
                else
                {
                    lbl_travInfo.Content = cConstDefine.tr("주행 원점 미확인");
                    flag = false;
                }
                if (gClass.str.SrmState[srmNum].lift.liSt2HomeCheck > 0)
                {
                    lbl_liftInfo.Content = "";
                }
                else
                {
                    lbl_liftInfo.Content = cConstDefine.tr("승강 원점 미확인");
                    flag = false;
                }
                if (gClass.str.SrmState[srmNum].fork1.homeCheck > 0)
                {
                    if (gClass.str.SrmInfo[srmNum].forkCnt == 2)
                    {
                        if (gClass.str.SrmState[srmNum].fork2.homeCheck > 0)
                        {
                            lbl_forkInfo.Content = "";
                        }
                        else
                        {
                            lbl_forkInfo.Content = cConstDefine.tr("포크 원점 미확인");
                            flag = false;
                        }
                    }
                    else
                        lbl_forkInfo.Content = "";
                }
                else
                {
                    lbl_forkInfo.Content = cConstDefine.tr("포크 원점 미확인");
                    flag = false;
                }

                // 媛뺤젣濡?二쇨린
                //flag = true;
                if (flag)
                {
                    // 媛뺤젣濡?二쇨린 - 250919
                    //gClass.str.SrmState[srmNum].dSt1StartSt = 1;

                    lbl_startInfo.Content = "";
                    grid_Block.Visibility = Visibility.Collapsed;
                    Grid.SetRowSpan(grid_Block, 2);
                    Panel.SetZIndex(grid_Block, -1);
                    if (gClass.str.SrmState[srmNum].dSt1StartSt > 0)
                        StartBtn_State(2);
                    else if (gClass.str.SrmPacket[srmNum].startEnable)
                    {
                        StartBtn_State(1);
                        if (lbl_startInfo.Foreground == Brushes.Green)
                            lbl_startInfo.Foreground = Brushes.White;
                        else
                            lbl_startInfo.Foreground = Brushes.Green;
                    }
                    else
                        StartBtn_State(0);
                }
                else
                {
                    lbl_notifyJob.Visibility = Visibility.Collapsed;
                    Panel.SetZIndex(lbl_notifyJob, -1);
                    Grid.SetRowSpan(grid_Block, 2);
                    grid_Block.Visibility = Visibility.Visible;
                    Panel.SetZIndex(grid_Block, 1);
                    lbl_startInfo.Content = "";
                    StartBtn_State(0);
                }
            }
        }

        private string GetWcsJobNameFork1(int srmNum)
        {
            var w = gClass.str.WcsPacket[srmNum].WCS_PARSE;
            if (w.fork1Sticky == 1) return cConstDefine.tr("Sticky 명령");
            if (w.fork1ChangeSt == 1) return cConstDefine.tr("스테이션변경");
            if (w.fork1ChangeRack == 1) return cConstDefine.tr("렉변경");
            if (w.fork1StToSt == 1) return cConstDefine.tr("스테이션이동");
            if (w.fork1RackToRack == 1) return cConstDefine.tr("렉간반송");
            if (w.fork1Retrieval == 1) return cConstDefine.tr("출고");
            if (w.fork1Storage == 1) return cConstDefine.tr("입고");
            if (w.fork1Move == 1) return cConstDefine.tr("이동");
            return cConstDefine.tr("작업없음");
        }

        private string GetWcsJobNameFork2(int srmNum)
        {
            var w = gClass.str.WcsPacket[srmNum].WCS_PARSE;
            if (w.fork2Sticky == 1) return cConstDefine.tr("Sticky 명령");
            if (w.fork2ChangeSt == 1) return cConstDefine.tr("스테이션변경");
            if (w.fork2ChangeRack == 1) return cConstDefine.tr("렉변경");
            if (w.fork2StToSt == 1) return cConstDefine.tr("스테이션이동");
            if (w.fork2RackToRack == 1) return cConstDefine.tr("렉간반송");
            if (w.fork2Retrieval == 1) return cConstDefine.tr("출고");
            if (w.fork2Storage == 1) return cConstDefine.tr("입고");
            if (w.fork2Move == 1) return cConstDefine.tr("이동");
            return cConstDefine.tr("작업없음");
        }

        private string GetCurrentSrmJobMessage()
        {
            List<string> jobs = new List<string>();
            string noJobText = cConstDefine.tr("작업없음");

            if (!string.IsNullOrWhiteSpace(lbl_Fork1Job.Text) &&
                !lbl_Fork1Job.Text.Contains(noJobText, StringComparison.Ordinal))
            {
                jobs.Add("Fork1 - " + lbl_Fork1Job.Text.Replace("SRM: ", string.Empty));
            }

            if (!string.IsNullOrWhiteSpace(lbl_Fork2Job.Text) &&
                !lbl_Fork2Job.Text.Contains(noJobText, StringComparison.Ordinal))
            {
                jobs.Add("Fork2 - " + lbl_Fork2Job.Text.Replace("SRM: ", string.Empty));
            }

            if (jobs.Count == 0)
            {
                return noJobText;
            }

            return string.Join("\n", jobs);
        }
    }
}
