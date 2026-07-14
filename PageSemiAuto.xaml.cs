using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    enum SEMIJOB
    {
        NONE,
        MOVE,
        STORE,
        RETRIEVE,
        RTOR,
        RACKCHANGE,
        STOS,
        STCHANGE,
        STICKY
    }
    /// <summary>
    /// PageSemiAuto.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PageSemiAuto : Page
    {
        //Singletone
        singletonClass gClass;
        MainWindow pMain;

        SEMIJOB curSemiJob = SEMIJOB.NONE;
        public PageSemiAuto(MainWindow parent)
        {
            InitializeComponent();
            pMain = parent;
            gClass = singletonClass.Instance;

            Page_Init();

            // Fork 1 초기화
            Edit_Fk1FromStn.IsEnabled = false;
            Edit_Fk1FromRow.IsEnabled = false;
            Edit_Fk1FromBay.IsEnabled = false;
            Edit_Fk1FromLev.IsEnabled = false;
            Edit_Fk1ToStn.IsEnabled = false;
            Edit_Fk1ToRow.IsEnabled = false;
            Edit_Fk1ToBay.IsEnabled = false;
            Edit_Fk1ToLev.IsEnabled = false;

            // Fork 2 초기화
            Edit_Fk2FromStn.IsEnabled = false;
            Edit_Fk2FromRow.IsEnabled = false;
            Edit_Fk2FromBay.IsEnabled = false;
            Edit_Fk2FromLev.IsEnabled = false;
            Edit_Fk2ToStn.IsEnabled = false;
            Edit_Fk2ToRow.IsEnabled = false;
            Edit_Fk2ToBay.IsEnabled = false;
            Edit_Fk2ToLev.IsEnabled = false;
        }

        public void Page_Init()         // 페이지 전환 시 마다 호출
        {
            //  현재 선택한 크레인의 포크타입에 따라 화면 사용설정
            if (gClass.str.SrmInfo[gClass.srmNum].forkCnt == 1)            // 싱글포크        
            {
                //Bdr_Fork2.IsEnabled = false;
                Btn_Fork2Sel.IsEnabled = false;
                Btn_Fork2Sel.Background = Brushes.Transparent;
                Btn_Fork2Sel.Foreground = Brushes.Gray;
                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork1)           // 포크 사용 선택에 따라서 버튼 이미지 전환
                {
                    Btn_Fork1Sel.Background = Brushes.White;
                    Btn_Fork1Sel.Foreground = Brushes.Black;
                }
                else
                {
                    Btn_Fork1Sel.Background = Brushes.Transparent;
                    Btn_Fork1Sel.Foreground = Brushes.Gray;
                }

                Console.WriteLine("Page Init Fork1");
            }
            else if (gClass.str.SrmInfo[gClass.srmNum].forkCnt == 2)
            {
                //Bdr_Fork2.IsEnabled = true;                                 // 트윈포크
                Btn_Fork2Sel.IsEnabled = true;
                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork1)           // 포크 사용 선택에 따라서 버튼 이미지 전환
                {
                    Btn_Fork1Sel.Background = Brushes.White;
                    Btn_Fork1Sel.Foreground = Brushes.Black;
                }
                else
                {
                    Btn_Fork1Sel.Background = Brushes.Transparent;
                    Btn_Fork1Sel.Foreground = Brushes.Gray;
                }
                
                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork2)
                {
                    Btn_Fork2Sel.Background = Brushes.White;
                    Btn_Fork2Sel.Foreground = Brushes.Black;
                }
                else
                {
                    Btn_Fork2Sel.Background = Brushes.Transparent;
                    Btn_Fork2Sel.Foreground = Brushes.Gray;
                }

                Console.WriteLine("Page Init Fork2");
            }
            else
            {
                Console.WriteLine("Page Init Else");
                //Bdr_Fork2.IsEnabled = false;
                Btn_Fork2Sel.IsEnabled = false;
                Btn_Fork1Sel.Foreground = Brushes.Gray;
                Btn_Fork2Sel.Foreground = Brushes.Gray;
                Btn_Fork1Sel.Background = Brushes.Transparent;
                Btn_Fork2Sel.Background = Brushes.Transparent;
            }

            // NumberPad Max Limit 정의
            Edit_Fk1FromStn.ToolTip = gClass.str.SrmInfo[gClass.srmNum].stn.ToString();
            Edit_Fk1FromRow.ToolTip = gClass.str.SrmInfo[gClass.srmNum].row.ToString();
            Edit_Fk1FromBay.ToolTip = gClass.str.SrmInfo[gClass.srmNum].bay.ToString();
            Edit_Fk1FromLev.ToolTip = gClass.str.SrmInfo[gClass.srmNum].lev.ToString();
            Edit_Fk1ToStn.ToolTip = gClass.str.SrmInfo[gClass.srmNum].stn.ToString();
            Edit_Fk1ToRow.ToolTip = gClass.str.SrmInfo[gClass.srmNum].row.ToString();
            Edit_Fk1ToBay.ToolTip = gClass.str.SrmInfo[gClass.srmNum].bay.ToString();
            Edit_Fk1ToLev.ToolTip = gClass.str.SrmInfo[gClass.srmNum].lev.ToString();

            Edit_Fk2FromStn.ToolTip = gClass.str.SrmInfo[gClass.srmNum].stn.ToString();
            Edit_Fk2FromRow.ToolTip = gClass.str.SrmInfo[gClass.srmNum].row.ToString();
            Edit_Fk2FromBay.ToolTip = gClass.str.SrmInfo[gClass.srmNum].bay.ToString();
            Edit_Fk2FromLev.ToolTip = gClass.str.SrmInfo[gClass.srmNum].lev.ToString();
            Edit_Fk2ToStn.ToolTip = gClass.str.SrmInfo[gClass.srmNum].stn.ToString();
            Edit_Fk2ToRow.ToolTip = gClass.str.SrmInfo[gClass.srmNum].row.ToString();
            Edit_Fk2ToBay.ToolTip = gClass.str.SrmInfo[gClass.srmNum].bay.ToString();
            Edit_Fk2ToLev.ToolTip = gClass.str.SrmInfo[gClass.srmNum].lev.ToString();

            // 최대값 표시 업데이트
            UpdateMaxValuesDisplay();

            if (gClass.str.DioPacket[gClass.srmNum].DISET[(int)DISTATE.SEMI_AUTO].pin > 0)      // DIO 키 설정 중 SEMI 없을 경우 버튼활성화
            {
                Btn_SemiAutoBT.IsEnabled = false;
            }
            else
            {
                Btn_SemiAutoBT.IsChecked = gClass.str.DioPacket[gClass.srmNum].DISET[(int)DISTATE.SEMI_AUTO].value;
                Btn_SemiAutoBT.IsEnabled = true;
            }

            if (gClass.str.SrmInfo[gClass.srmNum].ignoreCV)
            {
                Btn_IgnoreCV.IsChecked = true;
            }
            else
            {
                Btn_IgnoreCV.IsChecked = false;
            }
            if (gClass.str.SrmInfo[gClass.srmNum].ignoreGoods)
            {
                Btn_IgnoreGoods.IsChecked = true;
            }
            else
            {
                Btn_IgnoreGoods.IsChecked = false;
            }
        }

        private void Btn_CommandClick(object sender, RoutedEventArgs e)
        {
            ToggleButton cmdButton = sender as ToggleButton;

            // Uncheck 시 초기화 후 리턴
            if (cmdButton.IsChecked == false)
            {
                curSemiJob = SEMIJOB.NONE;
                lbl_fromStr.Content = "From";
                lbl_toStr.Content = "To";
            }
            else
            {
                if (cmdButton.Name == "Btn_Move")
                {
                    curSemiJob = SEMIJOB.MOVE;
                    Console.WriteLine("Click Btn_Move");
                    lbl_fromStr.Content = "-";
                    lbl_toStr.Content = "Dest";
                }
                else
                {
                    lbl_fromStr.Content = "From";
                    lbl_toStr.Content = "To";
                    if (cmdButton.Name == "Btn_Store")
                    {
                        curSemiJob = SEMIJOB.STORE;
                        Console.WriteLine("Click Btn_Store");
                    }
                    else if (cmdButton.Name == "Btn_Retrieve")
                    {
                        curSemiJob = SEMIJOB.RETRIEVE;
                        Console.WriteLine("Click Btn_Retrieve");
                    }
                    else if (cmdButton.Name == "Btn_RtoR")
                    {
                        curSemiJob = SEMIJOB.RTOR;
                        Console.WriteLine("Click Btn_RtoR");
                    }
                    else if (cmdButton.Name == "Btn_StoS")
                    {
                        curSemiJob = SEMIJOB.STOS;
                        Console.WriteLine("Click Btn_StoS");
                    }
                    else if (cmdButton.Name == "Btn_DestRackChange")
                    {
                        curSemiJob = SEMIJOB.RACKCHANGE;
                        Console.WriteLine("Click Btn_DestRackChange");
                    }
                    else if (cmdButton.Name == "Btn_DestStationChange")
                    {
                        curSemiJob = SEMIJOB.STCHANGE;
                        Console.WriteLine("Click Btn_DestStationChange");
                    }
                    else if (cmdButton.Name == "Btn_Sticky")
                    {
                        curSemiJob = SEMIJOB.STICKY;
                        Console.WriteLine("Click Btn_Sticky");
                    }
                }
            }
            visibleSelectJob();
            // 명령에 해당되지 않는 나머지 에디트들을 0으로 설정
            ResetUnusedEdits();
        }

        private void ResetUnusedEdits()
        {
            // 모든 에디트를 먼저 0으로 초기화
            if (curSemiJob == SEMIJOB.MOVE)
            {
                // 이동 명령: From 필드들은 사용 안 함
                Edit_Fk1FromStn.Text = "0";
                Edit_Fk1FromRow.Text = "0";
                Edit_Fk1FromBay.Text = "0";
                Edit_Fk1FromLev.Text = "0";
                Edit_Fk2FromStn.Text = "0";
                Edit_Fk2FromRow.Text = "0";
                Edit_Fk2FromBay.Text = "0";
                Edit_Fk2FromLev.Text = "0";
            }
            else if (curSemiJob == SEMIJOB.STORE)
            {
                // 입고 명령: FromRow/FromBay/FromLev, ToStn 사용 안 함
                Edit_Fk1FromRow.Text = "0";
                Edit_Fk1FromBay.Text = "0";
                Edit_Fk1FromLev.Text = "0";
                Edit_Fk1ToStn.Text = "0";
                Edit_Fk2FromRow.Text = "0";
                Edit_Fk2FromBay.Text = "0";
                Edit_Fk2FromLev.Text = "0";
                Edit_Fk2ToStn.Text = "0";
            }
            else if (curSemiJob == SEMIJOB.RETRIEVE)
            {
                // 출고 명령: FromStn, ToRow/ToBay/ToLev 사용 안 함
                Edit_Fk1FromStn.Text = "0";
                Edit_Fk1ToRow.Text = "0";
                Edit_Fk1ToBay.Text = "0";
                Edit_Fk1ToLev.Text = "0";
                Edit_Fk2FromStn.Text = "0";
                Edit_Fk2ToRow.Text = "0";
                Edit_Fk2ToBay.Text = "0";
                Edit_Fk2ToLev.Text = "0";
            }
            else if (curSemiJob == SEMIJOB.RTOR)
            {
                // 랙간이동: FromStn, ToStn 사용 안 함
                Edit_Fk1FromStn.Text = "0";
                Edit_Fk1ToStn.Text = "0";
                Edit_Fk2FromStn.Text = "0";
                Edit_Fk2ToStn.Text = "0";
            }
            else if (curSemiJob == SEMIJOB.STOS)
            {
                // 스테이션간이동: FromRow/FromBay/FromLev, ToRow/ToBay/ToLev 사용 안 함
                Edit_Fk1FromRow.Text = "0";
                Edit_Fk1FromBay.Text = "0";
                Edit_Fk1FromLev.Text = "0";
                Edit_Fk1ToRow.Text = "0";
                Edit_Fk1ToBay.Text = "0";
                Edit_Fk1ToLev.Text = "0";
                Edit_Fk2FromRow.Text = "0";
                Edit_Fk2FromBay.Text = "0";
                Edit_Fk2FromLev.Text = "0";
                Edit_Fk2ToRow.Text = "0";
                Edit_Fk2ToBay.Text = "0";
                Edit_Fk2ToLev.Text = "0";
            }
            else if (curSemiJob == SEMIJOB.RACKCHANGE)
            {
                // 목적지 랙 변경: 모든 From 필드, ToStn 사용 안 함
                Edit_Fk1FromStn.Text = "0";
                Edit_Fk1FromRow.Text = "0";
                Edit_Fk1FromBay.Text = "0";
                Edit_Fk1FromLev.Text = "0";
                Edit_Fk1ToStn.Text = "0";
                Edit_Fk2FromStn.Text = "0";
                Edit_Fk2FromRow.Text = "0";
                Edit_Fk2FromBay.Text = "0";
                Edit_Fk2FromLev.Text = "0";
                Edit_Fk2ToStn.Text = "0";
            }
            else if (curSemiJob == SEMIJOB.STCHANGE)
            {
                // 목적지 스테이션 변경: 모든 From 필드, ToRow/ToBay/ToLev 사용 안 함
                Edit_Fk1FromStn.Text = "0";
                Edit_Fk1FromRow.Text = "0";
                Edit_Fk1FromBay.Text = "0";
                Edit_Fk1FromLev.Text = "0";
                Edit_Fk1ToRow.Text = "0";
                Edit_Fk1ToBay.Text = "0";
                Edit_Fk1ToLev.Text = "0";
                Edit_Fk2FromStn.Text = "0";
                Edit_Fk2FromRow.Text = "0";
                Edit_Fk2FromBay.Text = "0";
                Edit_Fk2FromLev.Text = "0";
                Edit_Fk2ToRow.Text = "0";
                Edit_Fk2ToBay.Text = "0";
                Edit_Fk2ToLev.Text = "0";
            }
            else if (curSemiJob == SEMIJOB.STICKY)
            {
                // 자재 재위치: FromStn, ToStn, ToRow/ToBay/ToLev 사용 안 함
                Edit_Fk1FromStn.Text = "0";
                Edit_Fk1ToStn.Text = "0";
                Edit_Fk1ToRow.Text = "0";
                Edit_Fk1ToBay.Text = "0";
                Edit_Fk1ToLev.Text = "0";
                Edit_Fk2FromStn.Text = "0";
                Edit_Fk2ToStn.Text = "0";
                Edit_Fk2ToRow.Text = "0";
                Edit_Fk2ToBay.Text = "0";
                Edit_Fk2ToLev.Text = "0";
            }
            else
            {
                // 명령이 없으면 모든 필드를 0으로 초기화
                Edit_Fk1FromStn.Text = "0";
                Edit_Fk1FromRow.Text = "0";
                Edit_Fk1FromBay.Text = "0";
                Edit_Fk1FromLev.Text = "0";
                Edit_Fk1ToStn.Text = "0";
                Edit_Fk1ToRow.Text = "0";
                Edit_Fk1ToBay.Text = "0";
                Edit_Fk1ToLev.Text = "0";
                Edit_Fk2FromStn.Text = "0";
                Edit_Fk2FromRow.Text = "0";
                Edit_Fk2FromBay.Text = "0";
                Edit_Fk2FromLev.Text = "0";
                Edit_Fk2ToStn.Text = "0";
                Edit_Fk2ToRow.Text = "0";
                Edit_Fk2ToBay.Text = "0";
                Edit_Fk2ToLev.Text = "0";
            }
        }

        private void visibleSelectJob()
        {
            Edit_Fk1FromStn.IsEnabled = false;
            Edit_Fk1FromRow.IsEnabled = false;
            Edit_Fk1FromBay.IsEnabled = false;
            Edit_Fk1FromLev.IsEnabled = false;
            Edit_Fk1ToStn.IsEnabled = false;
            Edit_Fk1ToRow.IsEnabled = false;
            Edit_Fk1ToBay.IsEnabled = false;
            Edit_Fk1ToLev.IsEnabled = false;

            Edit_Fk2FromStn.IsEnabled = false;
            Edit_Fk2FromRow.IsEnabled = false;
            Edit_Fk2FromBay.IsEnabled = false;
            Edit_Fk2FromLev.IsEnabled = false;
            Edit_Fk2ToStn.IsEnabled = false;
            Edit_Fk2ToRow.IsEnabled = false;
            Edit_Fk2ToBay.IsEnabled = false;
            Edit_Fk2ToLev.IsEnabled = false;

            Edit_Fk1FromStn.Visibility = Visibility.Visible;
            Edit_Fk1FromRow.Visibility = Visibility.Visible;
            Edit_Fk1FromBay.Visibility = Visibility.Visible;
            Edit_Fk1FromLev.Visibility = Visibility.Visible;

            if (curSemiJob == SEMIJOB.MOVE)
            {
                Btn_Store.IsChecked = false;
                Btn_Retrieve.IsChecked = false;
                Btn_RtoR.IsChecked = false;
                Btn_StoS.IsChecked = false;
                Btn_DestRackChange.IsChecked = false;
                Btn_DestStationChange.IsChecked=false;
                Btn_Sticky.IsChecked = false;

                Edit_Fk1FromStn.Visibility = Visibility.Collapsed;
                Edit_Fk1FromRow.Visibility = Visibility.Collapsed;
                Edit_Fk1FromBay.Visibility = Visibility.Collapsed;
                Edit_Fk1FromLev.Visibility = Visibility.Collapsed;

                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork1)           // 포크 사용 선택에 R/B/L Enable 설정
                {
                    Edit_Fk1ToStn.IsEnabled = true;
                    Edit_Fk1ToBay.IsEnabled = true;
                    Edit_Fk1ToLev.IsEnabled = true;
                    Edit_Fk1ToRow.IsEnabled = true;
                    Edit_Fk1ToRow.Text = "0";         // Default Row 기준 좌측
                }

                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork2)           // 포크 사용 선택에 R/B/L Enable 설정
                {
                    Edit_Fk2ToStn.IsEnabled = true;
                    Edit_Fk2ToBay.IsEnabled = true;
                    Edit_Fk2ToLev.IsEnabled = true;
                    Edit_Fk2ToRow.IsEnabled = true;
                    Edit_Fk2ToRow.Text = "0";         // Default Row 기준 좌측
                }
            }
            else if (curSemiJob == SEMIJOB.STORE)
            {
                Btn_Move.IsChecked = false;
                Btn_Retrieve.IsChecked = false;
                Btn_RtoR.IsChecked = false;
                Btn_StoS.IsChecked = false;
                Btn_DestRackChange.IsChecked = false;
                Btn_DestStationChange.IsChecked = false;
                Btn_Sticky.IsChecked = false;

                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork1)           // 포크 사용 선택에 R/B/L Enable 설정
                {
                    Edit_Fk1FromStn.IsEnabled = true;
                    Edit_Fk1ToRow.IsEnabled = true;
                    Edit_Fk1ToBay.IsEnabled = true;
                    Edit_Fk1ToLev.IsEnabled = true;
                }

                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork2)           // 포크 사용 선택에 R/B/L Enable 설정
                {
                    Edit_Fk2FromStn.IsEnabled = true;
                    Edit_Fk2ToRow.IsEnabled = true;
                    Edit_Fk2ToBay.IsEnabled = true;
                    Edit_Fk2ToLev.IsEnabled = true;
                }
            }
            else if (curSemiJob == SEMIJOB.RETRIEVE)
            {
                Btn_Move.IsChecked = false;
                Btn_Store.IsChecked = false;
                Btn_RtoR.IsChecked = false;
                Btn_StoS.IsChecked = false;
                Btn_DestRackChange.IsChecked = false;
                Btn_DestStationChange.IsChecked = false;
                Btn_Sticky.IsChecked = false;

                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork1)           // 포크 사용 선택에 R/B/L Enable 설정
                {
                    Edit_Fk1FromRow.IsEnabled = true;
                    Edit_Fk1FromBay.IsEnabled = true;
                    Edit_Fk1FromLev.IsEnabled = true;
                    Edit_Fk1ToStn.IsEnabled = true;
                }

                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork2)           // 포크 사용 선택에 R/B/L Enable 설정
                {
                    Edit_Fk2FromRow.IsEnabled = true;
                    Edit_Fk2FromBay.IsEnabled = true;
                    Edit_Fk2FromLev.IsEnabled = true;
                    Edit_Fk2ToStn.IsEnabled = true;
                }
            }
            else if (curSemiJob == SEMIJOB.RTOR)
            {
                Btn_Move.IsChecked = false;
                Btn_Store.IsChecked = false;
                Btn_Retrieve.IsChecked = false;
                Btn_StoS.IsChecked = false;
                Btn_DestRackChange.IsChecked = false;
                Btn_DestStationChange.IsChecked = false;
                Btn_Sticky.IsChecked = false;

                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork1)           // 포크 사용 선택에 R/B/L Enable 설정
                {
                    Edit_Fk1FromRow.IsEnabled = true;
                    Edit_Fk1FromBay.IsEnabled = true;
                    Edit_Fk1FromLev.IsEnabled = true;
                    Edit_Fk1ToRow.IsEnabled = true;
                    Edit_Fk1ToBay.IsEnabled = true;
                    Edit_Fk1ToLev.IsEnabled = true;
                }

                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork2)           // 포크 사용 선택에 R/B/L Enable 설정
                {
                    Edit_Fk2FromRow.IsEnabled = true;
                    Edit_Fk2FromBay.IsEnabled = true;
                    Edit_Fk2FromLev.IsEnabled = true;
                    Edit_Fk2ToRow.IsEnabled = true;
                    Edit_Fk2ToBay.IsEnabled = true;
                    Edit_Fk2ToLev.IsEnabled = true;
                }
            }
            else if (curSemiJob == SEMIJOB.STOS)
            {
                Btn_Move.IsChecked = false;
                Btn_Store.IsChecked = false;
                Btn_Retrieve.IsChecked = false;
                Btn_RtoR.IsChecked = false;
                Btn_DestRackChange.IsChecked = false;
                Btn_DestStationChange.IsChecked = false;
                Btn_Sticky.IsChecked = false;

                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork1)           // 포크 사용 선택에 R/B/L Enable 설정
                {
                    Edit_Fk1FromStn.IsEnabled = true;
                    Edit_Fk1ToStn.IsEnabled = true;
                }

                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork2)           // 포크 사용 선택에 R/B/L Enable 설정
                {
                    Edit_Fk2FromStn.IsEnabled = true;
                    Edit_Fk2ToStn.IsEnabled = true;
                }
            }
            else if (curSemiJob == SEMIJOB.RACKCHANGE)
            {
                Btn_Move.IsChecked = false;
                Btn_Store.IsChecked = false;
                Btn_Retrieve.IsChecked = false;
                Btn_RtoR.IsChecked = false;
                Btn_StoS.IsChecked = false;
                Btn_DestStationChange.IsChecked = false;
                Btn_Sticky.IsChecked = false;

                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork1)           // 포크 사용 선택에 R/B/L Enable 설정
                {
                    Edit_Fk1ToRow.IsEnabled = true;
                    Edit_Fk1ToBay.IsEnabled = true;
                    Edit_Fk1ToLev.IsEnabled = true;
                }

                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork2)           // 포크 사용 선택에 R/B/L Enable 설정
                {
                    Edit_Fk2ToRow.IsEnabled = true;
                    Edit_Fk2ToBay.IsEnabled = true;
                    Edit_Fk2ToLev.IsEnabled = true;
                }
            }
            else if (curSemiJob == SEMIJOB.STCHANGE)
            {
                Btn_Move.IsChecked = false;
                Btn_Store.IsChecked = false;
                Btn_Retrieve.IsChecked = false;
                Btn_RtoR.IsChecked = false;
                Btn_StoS.IsChecked = false;
                Btn_DestRackChange.IsChecked = false;
                Btn_Sticky.IsChecked = false;

                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork1)           // 포크 사용 선택에 R/B/L Enable 설정
                {
                    Edit_Fk1ToStn.IsEnabled = true;
                }

                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork2)           // 포크 사용 선택에 R/B/L Enable 설정
                {
                    Edit_Fk2ToStn.IsEnabled = true;
                }
            }
            else if (curSemiJob == SEMIJOB.STICKY)
            {
                Btn_Move.IsChecked = false;
                Btn_Store.IsChecked = false;
                Btn_Retrieve.IsChecked = false;
                Btn_RtoR.IsChecked = false;
                Btn_StoS.IsChecked = false;
                Btn_DestRackChange.IsChecked = false;
                Btn_DestStationChange.IsChecked = false;

                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork1)           // 포크 사용 선택에 R/B/L Enable 설정
                {
                    Edit_Fk1FromRow.IsEnabled = true;
                    Edit_Fk1FromBay.IsEnabled = true;
                    Edit_Fk1FromLev.IsEnabled = true;
                }

                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork2)           // 포크 사용 선택에 R/B/L Enable 설정
                {
                    Edit_Fk2FromRow.IsEnabled = true;
                    Edit_Fk2FromBay.IsEnabled = true;
                    Edit_Fk2FromLev.IsEnabled = true;
                }
            }
        }

        private void Btn_FromTo_Select(object sender, MouseButtonEventArgs e)
        {

            TextBox edit = sender as TextBox;
            edit.PointToScreen(new Point(0, 0));

            //if (edit.Name == "Edit_Fk1FromStn")
            //{
            //}
            //else if (edit.Name == "Btn_Store")
            //{
            //}
            //else if (edit.Name == "Btn_Retrieve")
            //}
            //{
            //else if (edit.Name == "Btn_RtoR")
            //{
            //}
            //else if (edit.Name == "Btn_StoS")
            //{
            //}
            //else if (edit.Name == "Btn_RackChange")
            //{
            //}

            pMain.tmpNumPad.AttachTo(edit, Window.GetWindow(this), pMain.PointToScreen(new Point(0, 0)), pMain.ActualWidth, pMain.ActualHeight);
            Console.WriteLine("Btn_FromTo_Select : " + edit.Name);
        }

        private void Btn_Fork1Sel_Click(object sender, RoutedEventArgs e)
        {
            if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork1)           // 포크 사용 선택에 따라서 버튼 이미지 전환
            {
                gClass.str.SrmInfo[gClass.srmNum].bUse_fork1 = false;
                Btn_Fork1Sel.Background = Brushes.Transparent;
                Btn_Fork1Sel.Foreground = Brushes.Gray;
            }
            else
            {
                gClass.str.SrmInfo[gClass.srmNum].bUse_fork1 = true;
                Btn_Fork1Sel.Background = Brushes.White;
                Btn_Fork1Sel.Foreground = Brushes.Black;
            }
            visibleSelectJob();
            Console.WriteLine("Btn_Fork1Sel_Click");
        }

        private void Btn_Fork2Sel_Click(object sender, RoutedEventArgs e)
        {
            if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork2)           // 포크 사용 선택에 따라서 버튼 이미지 전환
            {
                gClass.str.SrmInfo[gClass.srmNum].bUse_fork2 = false;
                Btn_Fork2Sel.Background = Brushes.Transparent;
                Btn_Fork2Sel.Foreground = Brushes.Gray;
            }
            else
            {
                gClass.str.SrmInfo[gClass.srmNum].bUse_fork2 = true;
                Btn_Fork2Sel.Background = Brushes.White;
                Btn_Fork2Sel.Foreground = Brushes.Black;
            }
            visibleSelectJob();
            Console.WriteLine("Btn_Fork2Sel_Click");
        }

        private void Btn_SemiAutoBT_Click(object sender, RoutedEventArgs e)
        {
            gClass.str.DioPacket[gClass.srmNum].DISET[(int)DISTATE.SEMI_AUTO].value = (sender as ToggleButton).IsChecked.GetValueOrDefault();
            Console.WriteLine(nameof(Btn_SemiAutoBT_Click));
        }

        private void Btn_Init_Click(object sender, RoutedEventArgs e)
        {
            Edit_Fk1FromStn.Text = "0";
            Edit_Fk1FromRow.Text = "0";
            Edit_Fk1FromBay.Text = "0";
            Edit_Fk1FromLev.Text = "0";
            Edit_Fk1ToStn.Text = "0";
            Edit_Fk1ToRow.Text = "0";
            Edit_Fk1ToBay.Text = "0";
            Edit_Fk1ToLev.Text = "0";

            Edit_Fk2FromStn.Text = "0";
            Edit_Fk2FromRow.Text = "0";
            Edit_Fk2FromBay.Text = "0";
            Edit_Fk2FromLev.Text = "0";
            Edit_Fk2ToStn.Text = "0";
            Edit_Fk2ToRow.Text = "0";
            Edit_Fk2ToBay.Text = "0";
            Edit_Fk2ToLev.Text = "0";
        }

        private void Btn_IgnoreCV_Checked(object sender, RoutedEventArgs e)
        {
            //Btn_IgnoreCV.Content = "ON";
            gClass.str.SrmInfo[gClass.srmNum].ignoreCV = true;
        }

        private void Btn_IgnoreCV_Unchecked(object sender, RoutedEventArgs e)
        {
            //Btn_IgnoreCV.Content = "OFF";
            gClass.str.SrmInfo[gClass.srmNum].ignoreCV = false;
        }

        private void Btn_IgnoreGoods_Checked(object sender, RoutedEventArgs e)
        {
            //Btn_IgnoreGoods.Content = "ON";
            gClass.str.SrmInfo[gClass.srmNum].ignoreGoods = true;
        }

        private void Btn_IgnoreGoods_Unchecked(object sender, RoutedEventArgs e)
        {
            //Btn_IgnoreGoods.Content = "OFF";
            gClass.str.SrmInfo[gClass.srmNum].ignoreGoods = false;
        }

        private void UpdateMaxValuesDisplay()
        {
            // 최대값 표시: "최대값: Stn={stn} Row={row} Bay={bay} Lev={lev}"
            Lbl_MaxValues.Content = string.Format("Stn={0}  Row={1}  Bay={2}  Lev={3}",
                gClass.str.SrmInfo[gClass.srmNum].stn,
                gClass.str.SrmInfo[gClass.srmNum].row,
                gClass.str.SrmInfo[gClass.srmNum].bay,
                gClass.str.SrmInfo[gClass.srmNum].lev);
        }

        private void Btn_Start_Click(object sender, RoutedEventArgs e)
        {
            //MessageBoxResult result = MessageBox.Show(Properties.Resources.ResourceManager.GetString("저장문구", TranslationSource.Instance.CurrentCulture), Properties.Resources.ResourceManager.GetString("시작", TranslationSource.Instance.CurrentCulture),

            if (!gClass.str.SrmInfo[gClass.srmNum].bUse_fork1 && !gClass.str.SrmInfo[gClass.srmNum].bUse_fork2)           // 포크 사용 선택
            {
                VarMessageBox.Show(cConstDefine.tr("확인"), cConstDefine.tr("작업 포크를 선택해주세요"), VarMessageBoxButton.OK);
                return;
            }

            if (gClass.str.SrmPacket[gClass.srmNum].notPrecessedJob)
            {
                VarMessageBox.Show(cConstDefine.tr("확인"), cConstDefine.tr("미처리 작업이 존재합니다.\r\n처리 후 진행해주세요."), VarMessageBoxButton.OK);
                return;
            }

            if ((gClass.str.SrmState[gClass.srmNum].fork1.jobNo > 0 || gClass.str.SrmState[gClass.srmNum].fork2.jobNo > 0))
            {
                VarMessageBox.Show(cConstDefine.tr("확인"), cConstDefine.tr("크레인에 작업이 존재합니다.\r\n시작 ON시 해당 명령이 수행됩니다."), VarMessageBoxButton.OK);
                return;
            }

            if (gClass.str.SrmPacket[gClass.srmNum].operState)
            {
                VarMessageBox.Show(cConstDefine.tr("확인"), cConstDefine.tr("크레인이 동작 중 입니다.\r\n동작 완료 후 시도해주세요."), VarMessageBoxButton.OK);
                return;
            }

            if (gClass.str.SrmInfo[gClass.srmNum].ignoreCV)     // 만약에 인터록 무시가 걸려있으면 확인체크
            {
                VarMessageBoxResult result = VarMessageBox.Show(cConstDefine.tr("전송"), cConstDefine.tr("C/V 인터록무시 조건이 켜져있습니다, 진행하시겠습니까?"), VarMessageBoxButton.OKCancel);
                if (result == VarMessageBoxResult.Cancel)
                {
                    return;
                }
            }

            if (gClass.str.SrmState[gClass.srmNum].autoMode > 0)                       // 크레인(차상반) 자동상태
            {
                if (gClass.str.SrmState[gClass.srmNum].gcpState.gcpTxMode == 2)             // 지상반 자동 or 반자동 상태   1:수동 2:반자동 3:자동
                {
                    // ---장비 시작상태 이거나, 랙변경/스테이션변경 명령일 경우 전송
                    if ((gClass.str.SrmState[gClass.srmNum].dSt1StartSt > 0) || (curSemiJob == SEMIJOB.RACKCHANGE || curSemiJob == SEMIJOB.STCHANGE))
                    {
                        bool dataChk1 = false;
                        bool dataChk2 = false;

                        string msgText = cConstDefine.tr("반자동 명령을 전송하시겠습니까?");

                        if (gClass.str.SrmInfo[gClass.srmNum].ignoreCV)     // 만약에 인터록 무시가 걸려있으면 확인체크
                        {
                            msgText = cConstDefine.tr("C/V 인터록무시 조건이 켜져있습니다, 진행하시겠습니까?");
                        }

                        VarMessageBoxResult result = VarMessageBox.Show(cConstDefine.tr("전송"), msgText, VarMessageBoxButton.OKCancel);
                        if (result == VarMessageBoxResult.OK)
                        {
                            gClass.str.SrmPacket[gClass.srmNum].reqJobNoFk1 = 0;

                            gClass.str.SrmPacket[gClass.srmNum].reqFromStFk1 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].reqFromRowFk1 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].reqFromBayFk1 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].reqFromLevFk1 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].reqToStFk1 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].reqToRowFk1 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].reqToBayFk1 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].reqToLevFk1 = 0;

                            // Fork2
                            gClass.str.SrmPacket[gClass.srmNum].reqJobNoFk2 = 0;

                            gClass.str.SrmPacket[gClass.srmNum].reqFromStFk2 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].reqFromRowFk2 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].reqFromBayFk2 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].reqFromLevFk2 = 0;

                            gClass.str.SrmPacket[gClass.srmNum].reqToStFk2 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].reqToRowFk2 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].reqToBayFk2 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].reqToLevFk2 = 0;
                            //---------------------------------------



                            // Fork1
                            gClass.str.SrmPacket[gClass.srmNum].semiJobNoFk1 = 0;

                            gClass.str.SrmPacket[gClass.srmNum].semiJobStepFk1 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].semiFromStFk1 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].semiFromRowFk1 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].semiFromBayFk1 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].semiFromLevFk1 = 0;

                            gClass.str.SrmPacket[gClass.srmNum].semiToStFk1 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].semiToRowFk1 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].semiToBayFk1 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].semiToLevFk1 = 0;

                            gClass.str.SrmPacket[gClass.srmNum].semiGoodsTypeFk1 = 0;

                            // Fork2
                            gClass.str.SrmPacket[gClass.srmNum].semiJobNoFk2 = 0;

                            gClass.str.SrmPacket[gClass.srmNum].semiJobStepFk2 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].semiFromStFk2 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].semiFromRowFk2 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].semiFromBayFk2 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].semiFromLevFk2 = 0;

                            gClass.str.SrmPacket[gClass.srmNum].semiToStFk2 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].semiToRowFk2 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].semiToBayFk2 = 0;
                            gClass.str.SrmPacket[gClass.srmNum].semiToLevFk2 = 0;

                            gClass.str.SrmPacket[gClass.srmNum].semiGoodsTypeFk2 = 0;
                            bool IsValidByteInput(string text, out byte value)
                            {
                                return byte.TryParse(text.Trim(), out value) && value > 0;
                            }

                            bool IsValidUshortInput(string text, out ushort value)
                            {
                                return ushort.TryParse(text.Trim(), out value) && value > 0;
                            }

                            if (curSemiJob == SEMIJOB.MOVE)         //      이동명령
                            {
                                gClass.str.SrmPacket[gClass.srmNum].semiJobNoFk1 = 30000;
                                gClass.str.SrmPacket[gClass.srmNum].semiJobCodeFk1 = 1;
                                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork1)           // 포크1 사용 선택
                                {
                                    bool fk1StnValid = IsValidByteInput(Edit_Fk1ToStn.Text, out byte fk1ToStn);
                                    bool fk1RowValid = IsValidByteInput(Edit_Fk1ToRow.Text, out byte fk1ToRow);
                                    bool fk1BayValid = IsValidUshortInput(Edit_Fk1ToBay.Text, out ushort fk1ToBay);
                                    bool fk1LevValid = IsValidByteInput(Edit_Fk1ToLev.Text, out byte fk1ToLev);

                                    bool fk1RblAllValid = fk1RowValid && fk1BayValid && fk1LevValid;
                                    bool fk1RblAnyValid = fk1RowValid || fk1BayValid || fk1LevValid;

                                    // MOVE 목적지는 "S만 입력" 또는 "R/B/L 모두 입력" 두 가지 형태만 허용
                                    if (fk1StnValid && !fk1RblAnyValid)
                                    {
                                        gClass.str.SrmPacket[gClass.srmNum].semiToStFk1 = fk1ToStn;
                                        dataChk1 = true;
                                    }
                                    else if (!fk1StnValid && fk1RblAllValid)
                                    {
                                        gClass.str.SrmPacket[gClass.srmNum].semiToRowFk1 = fk1ToRow;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToBayFk1 = fk1ToBay;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToLevFk1 = fk1ToLev;
                                        dataChk1 = true;
                                    }
                                    else
                                    {
                                        dataChk1 = false;
                                    }
                                }
                                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork2)           // 포크2 사용 선택
                                {
                                    bool fk2StnValid = IsValidByteInput(Edit_Fk2ToStn.Text, out byte fk2ToStn);
                                    bool fk2RowValid = IsValidByteInput(Edit_Fk2ToRow.Text, out byte fk2ToRow);
                                    bool fk2BayValid = IsValidUshortInput(Edit_Fk2ToBay.Text, out ushort fk2ToBay);
                                    bool fk2LevValid = IsValidByteInput(Edit_Fk2ToLev.Text, out byte fk2ToLev);

                                    bool fk2RblAllValid = fk2RowValid && fk2BayValid && fk2LevValid;
                                    bool fk2RblAnyValid = fk2RowValid || fk2BayValid || fk2LevValid;

                                    // MOVE 목적지는 "S만 입력" 또는 "R/B/L 모두 입력" 두 가지 형태만 허용
                                    if (fk2StnValid && !fk2RblAnyValid)
                                    {
                                        gClass.str.SrmPacket[gClass.srmNum].semiToStFk2 = fk2ToStn;
                                        dataChk2 = true;
                                    }
                                    else if (!fk2StnValid && fk2RblAllValid)
                                    {
                                        gClass.str.SrmPacket[gClass.srmNum].semiToRowFk2 = fk2ToRow;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToBayFk2 = fk2ToBay;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToLevFk2 = fk2ToLev;
                                        dataChk2 = true;
                                    }
                                    else
                                    {
                                        dataChk2 = false;
                                    }
                                }
                            }
                            else if (curSemiJob == SEMIJOB.STORE)       //      입고명령
                            {
                                gClass.str.SrmPacket[gClass.srmNum].semiJobNoFk1 = 30001;
                                gClass.str.SrmPacket[gClass.srmNum].semiJobCodeFk1 = 2;
                                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork1)           // 포크1 사용 선택
                                {
                                    bool fk1FromStnValid = IsValidByteInput(Edit_Fk1FromStn.Text, out byte fk1FromStn);
                                    bool fk1ToRowValid = IsValidByteInput(Edit_Fk1ToRow.Text, out byte fk1ToRow);
                                    bool fk1ToBayValid = IsValidUshortInput(Edit_Fk1ToBay.Text, out ushort fk1ToBay);
                                    bool fk1ToLevValid = IsValidByteInput(Edit_Fk1ToLev.Text, out byte fk1ToLev);

                                    if (!(fk1FromStnValid && fk1ToRowValid && fk1ToBayValid && fk1ToLevValid))
                                    {
                                        dataChk1 = false;
                                    }
                                    else
                                    {
                                        gClass.str.SrmPacket[gClass.srmNum].semiFromStFk1 = fk1FromStn;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToRowFk1 = fk1ToRow;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToBayFk1 = fk1ToBay;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToLevFk1 = fk1ToLev;
                                        dataChk1 = true;
                                    }

                                }
                                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork2)           // 포크2 사용 선택
                                {
                                    bool fk2FromStnValid = IsValidByteInput(Edit_Fk2FromStn.Text, out byte fk2FromStn);
                                    bool fk2ToRowValid = IsValidByteInput(Edit_Fk2ToRow.Text, out byte fk2ToRow);
                                    bool fk2ToBayValid = IsValidUshortInput(Edit_Fk2ToBay.Text, out ushort fk2ToBay);
                                    bool fk2ToLevValid = IsValidByteInput(Edit_Fk2ToLev.Text, out byte fk2ToLev);

                                    if (!(fk2FromStnValid && fk2ToRowValid && fk2ToBayValid && fk2ToLevValid))
                                    {
                                        dataChk2 = false;
                                    }
                                    else
                                    {
                                        gClass.str.SrmPacket[gClass.srmNum].semiFromStFk2 = fk2FromStn;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToRowFk2 = fk2ToRow;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToBayFk2 = fk2ToBay;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToLevFk2 = fk2ToLev;
                                        dataChk2 = true;
                                    }
                                }
                            }
                            else if (curSemiJob == SEMIJOB.RETRIEVE)        //      출고명령
                            {
                                gClass.str.SrmPacket[gClass.srmNum].semiJobNoFk1 = 30002;
                                gClass.str.SrmPacket[gClass.srmNum].semiJobCodeFk1 = 4;
                                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork1)           // 포크1 사용 선택
                                {
                                    bool fk1FromRowValid = IsValidByteInput(Edit_Fk1FromRow.Text, out byte fk1FromRow);
                                    bool fk1FromBayValid = IsValidUshortInput(Edit_Fk1FromBay.Text, out ushort fk1FromBay);
                                    bool fk1FromLevValid = IsValidByteInput(Edit_Fk1FromLev.Text, out byte fk1FromLev);
                                    bool fk1ToStnValid = IsValidByteInput(Edit_Fk1ToStn.Text, out byte fk1ToStn);

                                    if (!(fk1FromRowValid && fk1FromBayValid && fk1FromLevValid && fk1ToStnValid))
                                    {
                                        dataChk1 = false;
                                    }
                                    else
                                    {
                                        gClass.str.SrmPacket[gClass.srmNum].semiFromRowFk1 = fk1FromRow;
                                        gClass.str.SrmPacket[gClass.srmNum].semiFromBayFk1 = fk1FromBay;
                                        gClass.str.SrmPacket[gClass.srmNum].semiFromLevFk1 = fk1FromLev;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToStFk1 = fk1ToStn;
                                        dataChk1 = true;
                                    }
                                }
                                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork2)           // 포크2 사용 선택
                                {
                                    bool fk2FromRowValid = IsValidByteInput(Edit_Fk2FromRow.Text, out byte fk2FromRow);
                                    bool fk2FromBayValid = IsValidUshortInput(Edit_Fk2FromBay.Text, out ushort fk2FromBay);
                                    bool fk2FromLevValid = IsValidByteInput(Edit_Fk2FromLev.Text, out byte fk2FromLev);
                                    bool fk2ToStnValid = IsValidByteInput(Edit_Fk2ToStn.Text, out byte fk2ToStn);

                                    if (!(fk2FromRowValid && fk2FromBayValid && fk2FromLevValid && fk2ToStnValid))
                                    {
                                        dataChk2 = false;
                                    }
                                    else
                                    {
                                        gClass.str.SrmPacket[gClass.srmNum].semiFromRowFk2 = fk2FromRow;
                                        gClass.str.SrmPacket[gClass.srmNum].semiFromBayFk2 = fk2FromBay;
                                        gClass.str.SrmPacket[gClass.srmNum].semiFromLevFk2 = fk2FromLev;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToStFk2 = fk2ToStn;
                                        dataChk2 = true;
                                    }
                                }
                            }
                            else if (curSemiJob == SEMIJOB.RTOR)        //      랙간반송
                            {
                                gClass.str.SrmPacket[gClass.srmNum].semiJobNoFk1 = 30003;
                                gClass.str.SrmPacket[gClass.srmNum].semiJobCodeFk1 = 8;
                                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork1)           // 포크1 사용 선택
                                {
                                    bool fk1FromRowValid = IsValidByteInput(Edit_Fk1FromRow.Text, out byte fk1FromRow);
                                    bool fk1FromBayValid = IsValidUshortInput(Edit_Fk1FromBay.Text, out ushort fk1FromBay);
                                    bool fk1FromLevValid = IsValidByteInput(Edit_Fk1FromLev.Text, out byte fk1FromLev);
                                    bool fk1ToRowValid = IsValidByteInput(Edit_Fk1ToRow.Text, out byte fk1ToRow);
                                    bool fk1ToBayValid = IsValidUshortInput(Edit_Fk1ToBay.Text, out ushort fk1ToBay);
                                    bool fk1ToLevValid = IsValidByteInput(Edit_Fk1ToLev.Text, out byte fk1ToLev);

                                    if (!(fk1FromRowValid && fk1FromBayValid && fk1FromLevValid && fk1ToRowValid && fk1ToBayValid && fk1ToLevValid))
                                    {
                                        dataChk1 = false;
                                    }
                                    else
                                    {
                                        gClass.str.SrmPacket[gClass.srmNum].semiFromRowFk1 = fk1FromRow;
                                        gClass.str.SrmPacket[gClass.srmNum].semiFromBayFk1 = fk1FromBay;
                                        gClass.str.SrmPacket[gClass.srmNum].semiFromLevFk1 = fk1FromLev;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToRowFk1 = fk1ToRow;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToBayFk1 = fk1ToBay;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToLevFk1 = fk1ToLev;
                                        dataChk1 = true;
                                    }

                                }
                                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork2)           // 포크2 사용 선택
                                {
                                    bool fk2FromRowValid = IsValidByteInput(Edit_Fk2FromRow.Text, out byte fk2FromRow);
                                    bool fk2FromBayValid = IsValidUshortInput(Edit_Fk2FromBay.Text, out ushort fk2FromBay);
                                    bool fk2FromLevValid = IsValidByteInput(Edit_Fk2FromLev.Text, out byte fk2FromLev);
                                    bool fk2ToRowValid = IsValidByteInput(Edit_Fk2ToRow.Text, out byte fk2ToRow);
                                    bool fk2ToBayValid = IsValidUshortInput(Edit_Fk2ToBay.Text, out ushort fk2ToBay);
                                    bool fk2ToLevValid = IsValidByteInput(Edit_Fk2ToLev.Text, out byte fk2ToLev);

                                    if (!(fk2FromRowValid && fk2FromBayValid && fk2FromLevValid && fk2ToRowValid && fk2ToBayValid && fk2ToLevValid))
                                    {
                                        dataChk2 = false;
                                    }
                                    else
                                    {
                                        gClass.str.SrmPacket[gClass.srmNum].semiFromRowFk2 = fk2FromRow;
                                        gClass.str.SrmPacket[gClass.srmNum].semiFromBayFk2 = fk2FromBay;
                                        gClass.str.SrmPacket[gClass.srmNum].semiFromLevFk2 = fk2FromLev;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToRowFk2 = fk2ToRow;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToBayFk2 = fk2ToBay;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToLevFk2 = fk2ToLev;
                                        dataChk2 = true;
                                    }
                                }
                            }
                            else if (curSemiJob == SEMIJOB.STOS)        //      스테이션간 반송
                            {
                                gClass.str.SrmPacket[gClass.srmNum].semiJobNoFk1 = 30004;
                                gClass.str.SrmPacket[gClass.srmNum].semiJobCodeFk1 = 16;
                                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork1)           // 포크1 사용 선택
                                {
                                    bool fk1FromStnValid = IsValidByteInput(Edit_Fk1FromStn.Text, out byte fk1FromStn);
                                    bool fk1ToStnValid = IsValidByteInput(Edit_Fk1ToStn.Text, out byte fk1ToStn);

                                    if (!(fk1FromStnValid && fk1ToStnValid))
                                    {
                                        dataChk1 = false;
                                    }
                                    else
                                    {
                                        gClass.str.SrmPacket[gClass.srmNum].semiFromStFk1 = fk1FromStn;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToStFk1 = fk1ToStn;
                                        dataChk1 = true;
                                    }
                                }
                                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork2)           // 포크2 사용 선택
                                {
                                    bool fk2FromStnValid = IsValidByteInput(Edit_Fk2FromStn.Text, out byte fk2FromStn);
                                    bool fk2ToStnValid = IsValidByteInput(Edit_Fk2ToStn.Text, out byte fk2ToStn);

                                    if (!(fk2FromStnValid && fk2ToStnValid))
                                    {
                                        dataChk2 = false;
                                    }
                                    else
                                    {
                                        gClass.str.SrmPacket[gClass.srmNum].semiFromStFk2 = fk2FromStn;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToStFk2 = fk2ToStn;
                                        dataChk2 = true;
                                    }

                                }
                            }
                            else if (curSemiJob == SEMIJOB.RACKCHANGE)      //      랙 목적지 변경
                            {
                                gClass.str.SrmPacket[gClass.srmNum].semiJobNoFk1 = 30005;
                                gClass.str.SrmPacket[gClass.srmNum].semiJobCodeFk1 = 32;
                                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork1)           // 포크1 사용 선택
                                {
                                    bool fk1ToRowValid = IsValidByteInput(Edit_Fk1ToRow.Text, out byte fk1ToRow);
                                    bool fk1ToBayValid = IsValidUshortInput(Edit_Fk1ToBay.Text, out ushort fk1ToBay);
                                    bool fk1ToLevValid = IsValidByteInput(Edit_Fk1ToLev.Text, out byte fk1ToLev);

                                    if (!(fk1ToRowValid && fk1ToBayValid && fk1ToLevValid))
                                    {
                                        dataChk1 = false;
                                    }
                                    else
                                    {
                                        gClass.str.SrmPacket[gClass.srmNum].semiToRowFk1 = fk1ToRow;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToBayFk1 = fk1ToBay;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToLevFk1 = fk1ToLev;
                                        dataChk1 = true;
                                    }
                                }
                                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork2)           // 포크2 사용 선택
                                {
                                    bool fk2ToRowValid = IsValidByteInput(Edit_Fk2ToRow.Text, out byte fk2ToRow);
                                    bool fk2ToBayValid = IsValidUshortInput(Edit_Fk2ToBay.Text, out ushort fk2ToBay);
                                    bool fk2ToLevValid = IsValidByteInput(Edit_Fk2ToLev.Text, out byte fk2ToLev);

                                    if (!(fk2ToRowValid && fk2ToBayValid && fk2ToLevValid))
                                    {
                                        dataChk2 = false;
                                    }
                                    else
                                    {
                                        gClass.str.SrmPacket[gClass.srmNum].semiToRowFk2 = fk2ToRow;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToBayFk2 = fk2ToBay;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToLevFk2 = fk2ToLev;
                                        dataChk2 = true;
                                    }
                                }
                            }
                            else if (curSemiJob == SEMIJOB.STCHANGE)      //      스테이션 목적지 변경
                            {
                                gClass.str.SrmPacket[gClass.srmNum].semiJobNoFk1 = 30006;
                                gClass.str.SrmPacket[gClass.srmNum].semiJobCodeFk1 = 64;
                                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork1)           // 포크1 사용 선택
                                {
                                    bool fk1ToStnValid = IsValidByteInput(Edit_Fk1ToStn.Text, out byte fk1ToStn);
                                    if (!fk1ToStnValid)
                                    {
                                        dataChk1 = false;
                                    }
                                    else
                                    {
                                        gClass.str.SrmPacket[gClass.srmNum].semiToStFk1 = fk1ToStn;
                                        dataChk1 = true;
                                    }
                                }
                                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork2)           // 포크2 사용 선택
                                {
                                    bool fk2ToStnValid = IsValidByteInput(Edit_Fk2ToStn.Text, out byte fk2ToStn);
                                    if (!fk2ToStnValid)
                                    {
                                        dataChk2 = false;
                                    }
                                    else
                                    {
                                        gClass.str.SrmPacket[gClass.srmNum].semiToStFk2 = fk2ToStn;
                                        dataChk2 = true;
                                    }
                                }
                            }
                            else if (curSemiJob == SEMIJOB.STICKY)      //      자재 재위치 명령
                            {
                                gClass.str.SrmPacket[gClass.srmNum].semiJobNoFk1 = 30007;
                                gClass.str.SrmPacket[gClass.srmNum].semiJobCodeFk1 = 128;
                                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork1)           // 포크1 사용 선택
                                {
                                    bool fk1ToRowValid = IsValidByteInput(Edit_Fk1ToRow.Text, out byte fk1ToRow);
                                    bool fk1ToBayValid = IsValidUshortInput(Edit_Fk1ToBay.Text, out ushort fk1ToBay);
                                    bool fk1ToLevValid = IsValidByteInput(Edit_Fk1ToLev.Text, out byte fk1ToLev);

                                    if (!(fk1ToRowValid && fk1ToBayValid && fk1ToLevValid))
                                    {
                                        dataChk1 = false;
                                    }
                                    else
                                    {
                                        gClass.str.SrmPacket[gClass.srmNum].semiToRowFk1 = fk1ToRow;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToBayFk1 = fk1ToBay;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToLevFk1 = fk1ToLev;
                                        dataChk1 = true;
                                    }
                                }
                                if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork2)           // 포크2 사용 선택
                                {
                                    bool fk2ToRowValid = IsValidByteInput(Edit_Fk2ToRow.Text, out byte fk2ToRow);
                                    bool fk2ToBayValid = IsValidUshortInput(Edit_Fk2ToBay.Text, out ushort fk2ToBay);
                                    bool fk2ToLevValid = IsValidByteInput(Edit_Fk2ToLev.Text, out byte fk2ToLev);

                                    if (!(fk2ToRowValid && fk2ToBayValid && fk2ToLevValid))
                                    {
                                        dataChk2 = false;
                                    }
                                    else
                                    {
                                        gClass.str.SrmPacket[gClass.srmNum].semiToRowFk2 = fk2ToRow;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToBayFk2 = fk2ToBay;
                                        gClass.str.SrmPacket[gClass.srmNum].semiToLevFk2 = fk2ToLev;
                                        dataChk2 = true;
                                    }
                                }
                            }
                            else
                            {
                                VarMessageBox.Show(cConstDefine.tr("확인"), cConstDefine.tr("작업을 선택해주세요"), VarMessageBoxButton.OK);
                                return;
                            }

                            // 포크 작업 사용여부 와 데이터 체크 상태가 동일해야함 - 사용 & 데이터 OK
                            if (gClass.str.SrmInfo[gClass.srmNum].bUse_fork1 == dataChk1 && gClass.str.SrmInfo[gClass.srmNum].bUse_fork2 == dataChk2)
                            {
                                byte jobCode = 1;
                                ref Srm_Packet refState = ref gClass.str.SrmPacket[gClass.srmNum];
                                refState.recvJobString = cIniAccess.getJobString((byte)(jobCode << ((int)curSemiJob -1)));
                                // to do 자동 스텝 커맨드와 공용으로 사용할 지... 
                                // gClass.str.SrmPacket[srmNum].jobState = (int)JOBSTATE.RECEIVE;       // 수신 으로 처리
                                cIniAccess.SaveJobLog(gClass.srmNum, string.Format("GCP -> SRM == 반송작업 - 송신 - {0} \r\n" +
                                "Fork1 - {1} From = Stn:{2} Row:{3} Bay:{4} Lev:{5} To = Stn:{6} Row:{7} Bay:{8} Lev:{9} \r\n" +
                                "Fork2 - {10} From = Stn:{11} Row:{12} Bay:{13} Lev:{14} To = Stn:{15} Row:{16} Bay:{17} Lev:{18}"
                                , refState.recvJobString, refState.semiJobNoFk1, refState.semiFromStFk1, refState.semiFromRowFk1, refState.semiFromBayFk1, refState.semiFromLevFk1,
                                refState.semiToStFk1, refState.semiToRowFk1, refState.semiToBayFk1, refState.semiToLevFk1, refState.semiJobNoFk2, refState.semiFromStFk2, refState.semiFromRowFk2,
                                refState.semiFromBayFk2, refState.semiFromLevFk2, refState.semiToStFk2, refState.semiToRowFk2, refState.semiToBayFk2, refState.semiToLevFk2));

                                string tmpStr = "";
                                gClass.str.SrmPacket[gClass.srmNum].resSubCode = pMain.Srm_JobEnableParse(ref tmpStr, gClass.srmNum, false);        // wcsFlag = false 반자동 명령은 저장하지 않음
                                if (gClass.str.SrmPacket[gClass.srmNum].resSubCode == 0)       // 작업 유효성 체크 확인 시
                                {
                                    // SRM 요청 버퍼 초기화-----------------
                                    // Fork1
                                    gClass.str.SrmPacket[gClass.srmNum].reqWcsCodeFk1 = gClass.str.SrmPacket[gClass.srmNum].semiJobCodeFk1;
                                    gClass.str.SrmPacket[gClass.srmNum].reqJobCodeFk1 = gClass.str.SrmPacket[gClass.srmNum].semiSendCodeFk1;
                                    gClass.str.SrmPacket[gClass.srmNum].reqJobNoFk1 = gClass.str.SrmPacket[gClass.srmNum].semiJobNoFk1;

                                    gClass.str.SrmPacket[gClass.srmNum].reqJobStepFk1 = 0;
                                    gClass.str.SrmPacket[gClass.srmNum].reqFromStFk1 = gClass.str.SrmPacket[gClass.srmNum].semiFromStFk1;
                                    gClass.str.SrmPacket[gClass.srmNum].reqFromRowFk1 = gClass.str.SrmPacket[gClass.srmNum].semiFromRowFk1;
                                    gClass.str.SrmPacket[gClass.srmNum].reqFromBayFk1 = gClass.str.SrmPacket[gClass.srmNum].semiFromBayFk1;
                                    gClass.str.SrmPacket[gClass.srmNum].reqFromLevFk1 = gClass.str.SrmPacket[gClass.srmNum].semiFromLevFk1;

                                    gClass.str.SrmPacket[gClass.srmNum].reqToStFk1 = gClass.str.SrmPacket[gClass.srmNum].semiToStFk1;
                                    gClass.str.SrmPacket[gClass.srmNum].reqToRowFk1 = gClass.str.SrmPacket[gClass.srmNum].semiToRowFk1;
                                    gClass.str.SrmPacket[gClass.srmNum].reqToBayFk1 = gClass.str.SrmPacket[gClass.srmNum].semiToBayFk1;
                                    gClass.str.SrmPacket[gClass.srmNum].reqToLevFk1 = gClass.str.SrmPacket[gClass.srmNum].semiToLevFk1;

                                    // Fork2
                                    gClass.str.SrmPacket[gClass.srmNum].reqWcsCodeFk2 = gClass.str.SrmPacket[gClass.srmNum].semiJobCodeFk2;
                                    gClass.str.SrmPacket[gClass.srmNum].reqJobCodeFk2 = gClass.str.SrmPacket[gClass.srmNum].semiSendCodeFk2;
                                    gClass.str.SrmPacket[gClass.srmNum].reqJobNoFk2 = gClass.str.SrmPacket[gClass.srmNum].semiJobNoFk2;

                                    gClass.str.SrmPacket[gClass.srmNum].reqJobStepFk2 = 0;
                                    gClass.str.SrmPacket[gClass.srmNum].reqFromStFk2 = gClass.str.SrmPacket[gClass.srmNum].semiFromStFk2;
                                    gClass.str.SrmPacket[gClass.srmNum].reqFromRowFk2 = gClass.str.SrmPacket[gClass.srmNum].semiFromRowFk2;
                                    gClass.str.SrmPacket[gClass.srmNum].reqFromBayFk2 = gClass.str.SrmPacket[gClass.srmNum].semiFromBayFk2;
                                    gClass.str.SrmPacket[gClass.srmNum].reqFromLevFk2 = gClass.str.SrmPacket[gClass.srmNum].semiFromLevFk2;

                                    gClass.str.SrmPacket[gClass.srmNum].reqToStFk2 = gClass.str.SrmPacket[gClass.srmNum].semiToStFk2;
                                    gClass.str.SrmPacket[gClass.srmNum].reqToRowFk2 = gClass.str.SrmPacket[gClass.srmNum].semiToRowFk2;
                                    gClass.str.SrmPacket[gClass.srmNum].reqToBayFk2 = gClass.str.SrmPacket[gClass.srmNum].semiToBayFk2;
                                    gClass.str.SrmPacket[gClass.srmNum].reqToLevFk2 = gClass.str.SrmPacket[gClass.srmNum].semiToLevFk2;

                                    gClass.str.SrmPacket[gClass.srmNum].resMainCode = 00;
                                    gClass.str.SrmPacket[gClass.srmNum].semiJobClicked = true;
                                    cIniAccess.SaveJobLog(gClass.srmNum, "GCP -> SRM == 반송작업 - 송신");
                                }
                                else
                                {
                                    gClass.str.SrmPacket[gClass.srmNum].resMainCode = 01;  // to do KCTC 01
#if DONGWON
                                    gClass.str.SrmPacket[gClass.srmNum].resMainCode = 66;
#endif
                                    gClass.str.SrmPacket[gClass.srmNum].resSubCode = 06;
                                    //gClass.str.SrmPacket[srmNum].recovError = true;
                                    cIniAccess.SaveJobLog(gClass.srmNum, "GCP -> SRM == 반송작업 - 실패:데이터 이상(" + tmpStr + ")");
                                    VarMessageBox.Show(cConstDefine.tr("확인"), cConstDefine.tr("실패:데이터 이상(") + tmpStr + ")", VarMessageBoxButton.OK);
                                }
                            }
                            else
                            {
                                VarMessageBox.Show(cConstDefine.tr("확인"), cConstDefine.tr("입력 데이터를 확인해주세요"), VarMessageBoxButton.OK);
                            }
                        }
                    }
                    else
                    {
                        VarMessageBox.Show(cConstDefine.tr("확인"), cConstDefine.tr("모드 / 시작 상태를 확인해주세요1"), VarMessageBoxButton.OK);
                    }
                }
                else
                {
                    VarMessageBox.Show(cConstDefine.tr("확인"), cConstDefine.tr("모드 / 시작 상태를 확인해주세요2"), VarMessageBoxButton.OK);
                }
            }
            else
            {
                VarMessageBox.Show(cConstDefine.tr("확인"), cConstDefine.tr("모드 / 시작 상태를 확인해주세요3"), VarMessageBoxButton.OK);
            }
        }
        
    }
}
