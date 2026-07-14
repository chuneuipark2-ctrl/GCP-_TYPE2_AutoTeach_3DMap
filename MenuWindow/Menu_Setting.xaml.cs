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
using System.Windows.Shapes;

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
                //Btn_Menu4.Visibility = Visibility.Visible;
            }
            else
            {
                Btn_Menu1.Content = "I/O";
                Btn_Menu2.Content = cConstDefine.tr("SRM 상태");
                Btn_Menu3.Content = "JOB" + "\nMonitor";
                Btn_Menu4.Content = "Ext\nMonitor";
                //Btn_Menu4.Visibility= Visibility.Collapsed;
            }

            double dpiX = VisualTreeHelper.GetDpi(this).PixelsPerInchX;
            double dpiY = VisualTreeHelper.GetDpi(this).PixelsPerInchY;

            this.Left = btnPos.X / dpiX * 96.0 - 120;
            this.Top = btnPos.Y / dpiY * 96.0 - 80;
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
                if(btn == Btn_Menu1)
                {
                    pMain.Page_Change(4);
                }
                else if(btn == Btn_Menu2)
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
                    pMain.Page_Change(9);
                }
                else if (btn == Btn_Menu2)
                {
                    pMain.Page_Change(12);
                }
                else if (btn == Btn_Menu3)
                {
                    pMain.Page_Change(10);
                }
                else if (btn == Btn_Menu4)
                {
                    pMain.Page_Change(11);
                }
            }
        }
    }
}
