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
    /// NumPad.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class NumPad : Window
    {
        public double dValue;

        double pixelWidth;
        double pixelHeight;

        String dValueStr;

        public TextBox pEditBox;

        // Current Screen Size
        Point windowPoint = new Point();
        Size windowSize = new Size();

        public NumPad()
        {
            InitializeComponent();
            this.Hide();
        }

        private void GetWindowPos(IntPtr hwnd, ref Point point, ref Size size)
        {
            cConstDefine.WINDOWPLACEMENT placement = new cConstDefine.WINDOWPLACEMENT();
            placement.length = System.Runtime.InteropServices.Marshal.SizeOf(placement);

            cConstDefine.GetWindowPlacement(hwnd, ref placement);

            size = new Size(placement.normal_position.Right - (placement.normal_position.Left * 2), placement.normal_position.Bottom - (placement.normal_position.Top * 2));
            point = new Point(placement.normal_position.Left, placement.normal_position.Top);
        }

        public void open_RelativePos(TextBox editTbox, Point winPos, double width, double height)
        {
            double editHeight = 0;

            Point editPos = editTbox.PointToScreen(new Point(0, 0));

            // 기존 EditBox 초기화
            if (pEditBox != null)
            {
                pEditBox.Background = null;
            }
            

            pEditBox = editTbox;
            Color color = (Color)ColorConverter.ConvertFromString("#4CFF0000");
            pEditBox.Background = new SolidColorBrush(color);
           // dValueStr = editTbox.Text;
            dValueStr = "";

            //double dpiX = VisualTreeHelper.GetDpi(this).PixelsPerInchX;
            //double dpiY = VisualTreeHelper.GetDpi(this).PixelsPerInchY;

            //pixelWidth = this.Width * dpiX / 96;    // position to pixel
            //pixelHeight = this.Height * dpiX / 96;  // position to pixel
            //editHeight = (editTbox.Height) * dpiY / 96;            // position to pixel

            ////IntPtr WindowToFind = cConstDefine.FindWindow("MainWindow",null);
            ////if (WindowToFind == IntPtr.Zero)
            ////{
            ////    Console.WriteLine("get MainWindow Failed");
            ////}

            ////GetWindowPos(WindowToFind, ref windowPoint, ref windowSize);

            //double screenWidth = width * dpiX / 96;
            //double screenHeight = height * dpiX / 96;

            //double editX = (editPos.X) * dpiX / 96;
            //double editY = (editPos.Y) * dpiY / 96;
            ////double screenWidth = SystemParameters.PrimaryScreenWidth * dpiX / 96;  // 2560 * dpiX / 96  position to pixel
            ////double screenHeight = SystemParameters.PrimaryScreenHeight * dpiY / 96; // 1440

            //this.Left = (editX) / dpiX * 96.0;    // pixel to position
            //this.Top = (editY + editHeight) / dpiY * 96.0;


            this.Left = editPos.X + winPos.X;
            this.Top = editPos.Y + winPos.Y + editTbox.ActualHeight;

            //Console.WriteLine("TEST POS = " + editTbox.ActualHeight);

            if (this.Left + this.ActualWidth > width)
            {
                this.Left = this.Left - ((this.Left + this.ActualWidth) - width+10);
            }

            if (this.Top + this.ActualHeight > height)
            {
                this.Top = editPos.Y - this.ActualHeight;
                //this.Top = (editPos.Y - pixelHeight) / dpiX * 96.0;
            }


            //Console.WriteLine("Get Screen Position " + editPos.X + " " + editPos.Y + " " + this.Left + " " + this.Top + " " + this.Width + " " + screenWidth + " " + screenHeight + " " + windowSize.Width + " " + windowSize.Height);
            //this.ShowDialog();
            this.Show();
            this.Activate();
            //this.Activate();

            ////menu_Setting
            //Point position = Btn_Setting.PointToScreen(new Point(0, 0));
            //Point m_position = this.PointToScreen(new Point(0, 0));

            //menu_Setting.Left = position.X / dpiX * 96.0 - 120;
            //menu_Setting.Top = position.Y / dpiY * 96.0 - 80;
            //menu_Setting.Show();

            //Console.WriteLine("Get Screen Position " + SystemParameters.PrimaryScreenWidth + " " + SystemParameters.PrimaryScreenHeight + " " + m_position.X + " " + m_position.Y);
        }

        private void Btn_Enter_Click(object sender, RoutedEventArgs e)
        {
            pEditBox.Background = null;
            this.Hide();
        }

        private void Btn_Append_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;

            if (btn == Btn_No0)
            {
                // Button 0
                dValueStr = dValueStr + "0";
            }
            else if (btn == Btn_No1)
            {
                // Button 1
                dValueStr = dValueStr + "1"; 
            }
            else if (btn == Btn_No2)
            {
                // Button 2
                dValueStr = dValueStr + "2";
            }
            else if (btn == Btn_No3)
            {
                // Button 3
                dValueStr = dValueStr + "3";
            }
            else if (btn == Btn_No4)
            {
                // Button 4
                dValueStr = dValueStr + "4";
            }
            else if (btn == Btn_No5)
            {
                // Button 5
                dValueStr = dValueStr + "5";
            }
            else if (btn == Btn_No6)
            {
                // Button 6
                dValueStr = dValueStr + "6";
            }
            else if (btn == Btn_No7)
            {
                // Button 7
                dValueStr = dValueStr + "7";
            }
            else if (btn == Btn_No8)
            {
                // Button 8
                dValueStr = dValueStr + "8";
            }
            else if (btn == Btn_No9)
            {
                // Button 9
                dValueStr = dValueStr + "9";
            }
            else if (btn == Btn_Dot)
            {
                // Button 9
                dValueStr = dValueStr + ".";
            }
            else if (btn == Btn_Back)
            {
                // Button Back
                if (dValueStr.Length > 0)
                {
                    dValueStr = dValueStr.Remove(dValueStr.Length - 1);
                }
                
            }

            // 최대값 보다 큰 값일 경우 최대값으로 변경
            //Console.WriteLine("NumPad Maximum " + pEditBox.ToolTip.ToString());

            if (dValueStr.Length > 0)
            {
                if (pEditBox.ToolTip != null)
                {
                    if (pEditBox.ToolTip.ToString() != "")
                    {
                        // dValueStr이 "." 또는 "1.2" 같은 미완성/소수 입력일 수 있어 int.Parse는 FormatException 발생.
                        // 둘 다 숫자로 파싱될 때만 최대값 클램프, 아니면 입력 유지(크래시 방지, 정상 동작 보존).
                        if (double.TryParse(dValueStr, out double curVal)
                            && double.TryParse(pEditBox.ToolTip.ToString(), out double maxVal)
                            && curVal > maxVal)
                        {
                            dValueStr = pEditBox.ToolTip.ToString();
                        }
                    }
                }
            }

            pEditBox.Text = dValueStr;
        }

    }
}
