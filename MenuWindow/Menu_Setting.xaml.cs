using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace gcp_Wpf.MenuWindow
{
    /// <summary>
    /// Menu_Setting.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Menu_Setting : Window
    {
        int menuType = 0;

        MainWindow pMain;
        public Menu_Setting()
        {
            InitializeComponent();
        }

        public Menu_Setting(MainWindow parent)
        {
            pMain = parent;
            InitializeComponent();
            // Owner / 클램프 쓰지 않음 — Left 가 좌측으로 튕기는 원인
        }

        public void Open_RelativeMenu(Point btnPos, Point parentPos, int type)
        {
            menuType = type;

            if (menuType == 1)       // Setting Page Init
            {
                Btn_Menu1.Content = cConstDefine.tr("지상반설정");
                Btn_Menu2.Content = cConstDefine.tr("크레인정보");
                Btn_Menu3.Content = cConstDefine.tr("스테이션설정");
                Btn_Menu4.Content = cConstDefine.tr("금지랙설정");
                Btn_Menu5.Visibility = Visibility.Collapsed;
                Col4.Width = new GridLength(0);
                Width = 440;
            }
            else
            {
                // Monitor: I/O | 3D Map | SRM 상태 | JOB Monitor | Ext Monitor
                Btn_Menu1.Content = "I/O";
                Btn_Menu2.Content = "3D Map";
                Btn_Menu3.Content = cConstDefine.tr("SRM 상태");
                Btn_Menu4.Content = "JOB" + "\nMonitor";
                Btn_Menu5.Content = "Ext\nMonitor";
                Btn_Menu5.Visibility = Visibility.Visible;
                Col4.Width = new GridLength(1, GridUnitType.Star);
                Width = 550;
            }

            // MainWindow 와 동일한 GetDpi 환산 (수년간 설정 메뉴에 쓰던 방식)
            Visual dpiSrc = pMain != null ? (Visual)pMain : this;
            double dpiX = VisualTreeHelper.GetDpi(dpiSrc).PixelsPerInchX;
            double dpiY = VisualTreeHelper.GetDpi(dpiSrc).PixelsPerInchY;
            if (dpiX < 1) dpiX = 96;
            if (dpiY < 1) dpiY = 96;

            double btnX = btnPos.X / dpiX * 96.0;
            double btnY = btnPos.Y / dpiY * 96.0;

            // 설정/모니터 동일 오프셋 — 5칸(550)은 기존 440대비 약간만 더 왼쪽
            double leftOffset = (menuType == 1) ? 120 : 160;
            this.Left = btnX - leftOffset;
            this.Top = btnY - 80;
            this.Show();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            this.Hide();
        }

        private void Btn_Menu_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();

            Button btn = sender as Button;

            if (menuType == 1)       // Setting Page Init
            {
                if (btn == Btn_Menu1)
                {
                    pMain.Page_Change(4);
                }
                else if (btn == Btn_Menu2)
                {
                    pMain.Page_Change(5);
                }
                else if (btn == Btn_Menu3)
                {
                    pMain.Page_Change(6);
                }
                else if (btn == Btn_Menu4)
                {
                    pMain.Page_Change(3);
                }
            }
            else
            {
                if (btn == Btn_Menu1)
                {
                    pMain.Page_Change(cConstDefine.PAGE_DIO);
                }
                else if (btn == Btn_Menu2)
                {
                    pMain.Page_Change(cConstDefine.PAGE_3DMAP);
                }
                else if (btn == Btn_Menu3)
                {
                    pMain.Page_Change(cConstDefine.PAGE_SRMIO);
                }
                else if (btn == Btn_Menu4)
                {
                    pMain.Page_Change(cConstDefine.PAGE_TOWCS);
                }
                else if (btn == Btn_Menu5)
                {
                    pMain.Page_Change(cConstDefine.PAGE_FROMWCS);
                }
            }
        }
    }
}
