using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
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
using System.Windows.Shapes;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace gcp_Wpf.MenuWindow
{
    /// <summary>
    /// WindowLogin.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class WindowLogin : Window
    {
        private NumberPadPopup _numberPadPopup;
        public string Password => PasswordBox.Password;
        public event Action<bool> ReturnValue;
        public WindowLogin()
        {
            InitializeComponent();
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            this.Left = screenWidth/2 - this.Width/2;
            this.Top = screenHeight/2 - this.Height/2;
        }

        private void Click_OpenNumpad(object sender, MouseButtonEventArgs e)
        {
            lbl_info.Content = "";
            PasswordBox.Password = "";
            e.Handled = true;
            
            if (_numberPadPopup == null)
            {
                _numberPadPopup = new NumberPadPopup();
                // Enter 버튼 클릭 시 비밀번호 확인
                _numberPadPopup.EnterClicked += NumberPadEnter_Click;
            }
            _numberPadPopup.AttachTo(PasswordBox, this);
        }

        private void Btn_Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void NumberPadEnter_Click(object sender, RoutedEventArgs e)
        {
            string enteredPassword = Password;
            if (enteredPassword == "1234")
            {
                if (_numberPadPopup != null)
                {
                    _numberPadPopup.Hide();
                }
                DialogResult = true;
                Close();
            }
            else
            {
                lbl_info.Content = "Invalid PassWord";
                PasswordBox.Password = "";
                if (_numberPadPopup != null)
                {
                    _numberPadPopup.Hide();
                }
            }
        }

        private void Window_TouchDown(object sender, TouchEventArgs e)
        {
            // 터치 이벤트 처리 - NumberPadPopup이 열려있으면 닫기
            if (_numberPadPopup != null && _numberPadPopup.IsVisible)
            {
                // NumberPadPopup 영역이 아닌 곳을 터치했을 때만 닫기
                Point touchPoint = e.GetTouchPoint(this).Position;
                Point screenPoint = this.PointToScreen(touchPoint);
                
                // NumberPadPopup의 위치와 크기 확인
                if (!_numberPadPopup.IsMouseOver)
                {
                    _numberPadPopup.Hide();
                }
            }
        }
    }
}
