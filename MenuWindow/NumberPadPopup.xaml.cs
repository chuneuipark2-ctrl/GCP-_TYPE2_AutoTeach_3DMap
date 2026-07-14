using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace gcp_Wpf.MenuWindow
{
    /// <summary>
    /// NumberPadPopup.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class NumberPadPopup : Window
    {
        private TextBox _targetTextBox;
        private PasswordBox _targetPasswordBox;
        private string _dValueStr = "";
        private Window _ownerWindow;

        public NumberPadPopup()
        {
            InitializeComponent();
            this.Hide();
        }

        public void AttachTo(TextBox textBox, Window ownerWindow = null, Point? winPos = null, double? screenWidth = null, double? screenHeight = null)
        {
            _targetTextBox = textBox;
            _targetPasswordBox = null;
            _ownerWindow = ownerWindow;

            // Owner 설정 (Deactivated 이벤트 처리 개선)
            if (ownerWindow != null)
            {
                this.Owner = ownerWindow;
            }

            // 기존 EditBox 초기화
            if (_targetTextBox != null)
            {
                _targetTextBox.Background = null;
            }

            // 배경색 표시
            if (_targetTextBox != null)
            {
                Color color = (Color)ColorConverter.ConvertFromString("#4CFF0000");
                _targetTextBox.Background = new SolidColorBrush(color);
            }

            // 넘버패드 열 때 이전 값 지우고 빈 문자열로 시작
            _dValueStr = "";
            if (_targetTextBox != null)
            {
                _targetTextBox.Text = "";
            }

            // 툴팁에서 max값 읽어서 표시
            UpdateMaxValueDisplay();

            // 위치 계산
            Point editPos = textBox.PointToScreen(new Point(0, 0));
            var screen = SystemParameters.WorkArea;

            double targetWidth = screenWidth ?? screen.Width;
            double targetHeight = screenHeight ?? screen.Height;

            // 기본 위치: 에디트 바로 아래
            this.Left = editPos.X;
            this.Top = editPos.Y + textBox.ActualHeight;

            // 화면 경계 체크 및 조정
            if (this.Left + this.Width > targetWidth)
            {
                this.Left = Math.Max(targetWidth - this.Width - 10, 0);
            }

            if (this.Top + this.Height > targetHeight)
            {
                // 위쪽에 표시
                this.Top = editPos.Y - this.Height;
                if (this.Top < 0)
                {
                    this.Top = 0;
                }
            }

            this.Show();
            this.Focus();
        }

        public void AttachTo(PasswordBox passwordBox, Window ownerWindow = null)
        {
            _targetPasswordBox = passwordBox;
            _targetTextBox = null;
            _ownerWindow = ownerWindow;

            // Owner 설정 (Deactivated 이벤트 처리 개선)
            if (ownerWindow != null)
            {
                this.Owner = ownerWindow;
            }

            _dValueStr = "";

            // 위치 계산
            Point editPos = passwordBox.PointToScreen(new Point(0, 0));
            var screen = SystemParameters.WorkArea;

            // 기본 위치: 에디트 바로 아래
            this.Left = editPos.X;
            this.Top = editPos.Y + passwordBox.ActualHeight;

            // 화면 경계 체크 및 조정
            if (this.Left + this.Width > screen.Width)
            {
                this.Left = Math.Max(screen.Width - this.Width - 10, 0);
            }

            if (this.Top + this.Height > screen.Height)
            {
                // 위쪽에 표시
                this.Top = editPos.Y - this.Height;
                if (this.Top < 0)
                {
                    this.Top = 0;
                }
            }

            this.Show();
            this.Focus();
        }

        private void NumberPadButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;

            string content = btn.Content?.ToString();
            if (string.IsNullOrEmpty(content)) return;

            // 소수점 처리: 일반 숫자는 소수점 1개만, Tag가 IP인 필드(IPv4)는 구분점 최대 3개
            if (content == ".")
            {
                if (_targetTextBox != null)
                {
                    int dotCount = 0;
                    foreach (char c in _dValueStr)
                    {
                        if (c == '.') dotCount++;
                    }

                    bool isIpField = string.Equals(_targetTextBox.Tag?.ToString(), "IP", StringComparison.OrdinalIgnoreCase);
                    bool canAddDot = isIpField ? dotCount < 3 : dotCount == 0;

                    if (canAddDot)
                    {
                        _dValueStr += ".";
                    }
                }
                else if (_targetPasswordBox != null)
                {
                    // PasswordBox에는 소수점 추가 안 함
                    return;
                }
            }
            else
            {
                _dValueStr += content;
            }

            // 최대값 체크 (TextBox만)
            if (_targetTextBox != null && _dValueStr.Length > 0)
            {
                if (_targetTextBox.ToolTip != null && _targetTextBox.ToolTip.ToString() != "")
                {
                    if (int.TryParse(_dValueStr, out int value) && int.TryParse(_targetTextBox.ToolTip.ToString(), out int maxValue))
                    {
                        if (value > maxValue)
                        {
                            _dValueStr = _targetTextBox.ToolTip.ToString();
                        }
                    }
                }
            }

            // 값 적용
            if (_targetTextBox != null)
            {
                _targetTextBox.Text = _dValueStr;
                _targetTextBox.CaretIndex = _targetTextBox.Text.Length;
            }
            else if (_targetPasswordBox != null)
            {
                _targetPasswordBox.Password = _dValueStr;
            }
        }

        private void NumberPadBackspace_Click(object sender, RoutedEventArgs e)
        {
            if (_dValueStr.Length > 0)
            {
                _dValueStr = _dValueStr.Substring(0, _dValueStr.Length - 1);
            }

            if (_targetTextBox != null)
            {
                _targetTextBox.Text = _dValueStr;
                _targetTextBox.CaretIndex = _targetTextBox.Text.Length;
            }
            else if (_targetPasswordBox != null)
            {
                _targetPasswordBox.Password = _dValueStr;
            }
        }

        private void NumberPadEnter_Click(object sender, RoutedEventArgs e)
        {
            // 빈 값이면 0으로 설정
            if (_targetTextBox != null)
            {
                if (string.IsNullOrWhiteSpace(_targetTextBox.Text))
                {
                    _targetTextBox.Text = "0";
                }
            }
            
            // Enter 버튼 클릭 시 이벤트 발생
            EnterClicked?.Invoke(sender, e);
            
            this.Hide();
        }

        private void NumberPadClose_Click(object sender, RoutedEventArgs e)
        {
            // 빈 값이면 0으로 설정
            if (_targetTextBox != null)
            {
                if (string.IsNullOrWhiteSpace(_targetTextBox.Text))
                {
                    _targetTextBox.Text = "0";
                }
            }
            
            this.Hide();
        }

        public event RoutedEventHandler EnterClicked;

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 넘버패드 외부(배경)를 클릭한 경우에만 닫기
            if (e.OriginalSource is Grid || e.OriginalSource is Window)
            {
                this.Hide();
            }
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // 창이 숨겨질 때 항상 배경색 제거 및 빈 값 처리
            if (!this.IsVisible)
            {
                // 빈 값이면 0으로 설정
                if (_targetTextBox != null)
                {
                    if (string.IsNullOrWhiteSpace(_targetTextBox.Text))
                    {
                        _targetTextBox.Text = "0";
                    }
                }
                ClearBackground();
            }
        }

        private void ClearBackground()
        {
            // 배경색 제거
            if (_targetTextBox != null)
            {
                _targetTextBox.Background = null;
            }
        }

        private void UpdateMaxValueDisplay()
        {
            // 툴팁에서 max값 읽기
            if (_targetTextBox != null && _targetTextBox.ToolTip != null)
            {
                string toolTipText = _targetTextBox.ToolTip.ToString();
                if (!string.IsNullOrEmpty(toolTipText))
                {
                    // 숫자로 파싱 가능한지 확인
                    if (int.TryParse(toolTipText, out int maxValue))
                    {
                        Lbl_MaxValue.Content = $"Max: {maxValue}";
                        Lbl_MaxValue.Visibility = Visibility.Visible;
                        return;
                    }
                }
            }
            
            // 툴팁이 없거나 숫자가 아닌 경우 숨김
            Lbl_MaxValue.Visibility = Visibility.Collapsed;
        }
    }
}
