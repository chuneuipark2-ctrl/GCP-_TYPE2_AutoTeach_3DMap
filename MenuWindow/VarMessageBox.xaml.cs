using System;
using System.Windows;
using System.Windows.Threading;
using gcp_Wpf;

namespace gcp_Wpf.MenuWindow
{
    /// <summary>MessageBox 대체용 다이얼로그 - 제목/메시지/버튼 문구가 앱 번역(tr) 적용</summary>
    public enum VarMessageBoxResult
    {
        None = 0,
        OK = 1,
        Cancel = 2,
        Yes = 6,
        No = 7
    }

    /// <summary>MessageBoxButton와 대응</summary>
    public enum VarMessageBoxButton
    {
        OK = 0,
        OKCancel = 1,
        YesNo = 4
    }

    public partial class VarMessageBox : Window
    {
        private DispatcherTimer _timer;
        private int _timeCnt;
        private readonly VarMessageBoxButton _buttonType;

        public VarMessageBoxResult Result { get; private set; } = VarMessageBoxResult.None;

        public VarMessageBox(string title, string message, VarMessageBoxButton buttonType, int timeoutCnt = 0)
        {
            InitializeComponent();

            _buttonType = buttonType;
            _timeCnt = timeoutCnt;
            Title = title;

            if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsLoaded)
            {
                Owner = Application.Current.MainWindow;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            // 리소스 실제 Enter 및 코드/리소스 내 \n 모두 줄바꿈으로 적용 (짤림 방지)
            string msg = message ?? "";
            msg = msg.Replace("\\r\\n", Environment.NewLine).Replace("\\n", Environment.NewLine)
                     .Replace("\r\n", Environment.NewLine).Replace("\n", Environment.NewLine);
            lbl_infoText.Text = msg;

            // 버튼 문구·표시를 번역 및 타입에 맞게 설정
            Btn_OK.Content = _buttonType == VarMessageBoxButton.YesNo ? cConstDefine.tr("예") : cConstDefine.tr("확인");
            Btn_Cancel.Content = _buttonType == VarMessageBoxButton.YesNo ? cConstDefine.tr("아니오") : cConstDefine.tr("취소");

            if (_buttonType == VarMessageBoxButton.OK)
            {
                Btn_Cancel.Visibility = Visibility.Collapsed;
            }
            else
            {
                Btn_Cancel.Visibility = Visibility.Visible;
            }
            Btn_OK.Visibility = Visibility.Visible;

            if (timeoutCnt > 0)
            {
                Btn_Cancel.Visibility = Visibility.Collapsed;
                lbl_timeCnt.Content = _timeCnt.ToString();
                _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _timer.Tick += Timer_Tick;
                _timer.Start();
            }
            else
            {
                lbl_timeCnt.Content = "";
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            _timeCnt -= 1;
            if (_timeCnt < 0)
            {
                _timer?.Stop();
                Result = VarMessageBoxResult.OK;
                Close();
            }
            else
            {
                lbl_timeCnt.Content = _timeCnt.ToString();
            }
        }

        /// <summary>MessageBox 대체: 결과 반환. 버튼 문구는 tr()로 번역됨.</summary>
        public static VarMessageBoxResult Show(string title, string message, VarMessageBoxButton buttonType = VarMessageBoxButton.OK, int timeoutCnt = 0)
        {
            var dlg = new VarMessageBox(title, message, buttonType, timeoutCnt);
            dlg.ShowDialog();
            return dlg.Result;
        }

        /// <summary>기존 호환: 타임아웃만 있는 경우 (결과 없이 표시)</summary>
        public static void Show(string title, string message, int timeoutCnt)
        {
            Show(title, message, VarMessageBoxButton.OK, timeoutCnt);
        }

        private void Btn_OK_Click(object sender, RoutedEventArgs e)
        {
            _timer?.Stop();
            Result = _buttonType == VarMessageBoxButton.YesNo ? VarMessageBoxResult.Yes : VarMessageBoxResult.OK;
            DialogResult = true;
            Close();
        }

        private void Btn_Cancel_Click(object sender, RoutedEventArgs e)
        {
            _timer?.Stop();
            Result = _buttonType == VarMessageBoxButton.YesNo ? VarMessageBoxResult.No : VarMessageBoxResult.Cancel;
            DialogResult = false;
            Close();
        }
    }
}
