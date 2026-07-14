using System;
using System.IO;
using System.Windows;
using gcp_Wpf.License;

namespace LicenseGeneratorTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>빌드일 기준 2주 동안만 라이선스 생성 기능 허용</summary>
        private static bool IsWithinAllowedPeriod()
        {
            var expiryUtc = BuildInfo.BuildDateUtc.AddDays(14);
            return DateTime.UtcNow <= expiryUtc;
        }

        public MainWindow()
        {
            InitializeComponent();
            
            // 프로그램이 실행되는 디렉토리로 작업 디렉토리 설정
            string exeDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(exeDirectory))
            {
                Directory.SetCurrentDirectory(exeDirectory);
            }
        }

        private void BtnGenerateKeys_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                KeyGenerator.GenerateAndSaveKeys();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"키 생성 중 오류가 발생했습니다.\n\n오류: {ex.Message}", 
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnGetCurrentHardwareId_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string hardwareId = HardwareIdGenerator.GenerateHardwareId();
                txtHardwareId.Text = hardwareId;
                txtStatus.Text = "하드웨어 ID를 가져왔습니다.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"하드웨어 ID를 가져오는 중 오류가 발생했습니다.\n\n오류: {ex.Message}", 
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChkUnlimited_Checked(object sender, RoutedEventArgs e)
        {
            dpExpiryDate.IsEnabled = false;
        }

        private void ChkUnlimited_Unchecked(object sender, RoutedEventArgs e)
        {
            dpExpiryDate.IsEnabled = true;
        }

        private void BtnGenerateLicense_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 입력 검증
                if (string.IsNullOrWhiteSpace(txtHardwareId.Text))
                {
                    MessageBox.Show("하드웨어 ID를 입력하거나 가져와주세요.", "입력 오류", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtCustomerName.Text))
                {
                    MessageBox.Show("고객명을 입력해주세요.", "입력 오류", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 만료일 설정
                DateTime? expiryDate = null;
                if (!chkUnlimited.IsChecked.Value && dpExpiryDate.SelectedDate.HasValue)
                {
                    expiryDate = dpExpiryDate.SelectedDate.Value;
                }

                // 라이선스 생성 (개인키 삭제 옵션 포함)
                bool success = LicenseGenerator.GenerateLicenseFile(
                    txtHardwareId.Text.Trim(),
                    expiryDate,
                    txtCustomerName.Text.Trim(),
                    outputPath: null,
                    deletePrivateKeyAfterGeneration: chkDeletePrivateKey.IsChecked ?? true
                );

                if (success)
                {
                    txtStatus.Text = "라이선스 파일이 성공적으로 생성되었습니다.\n생성된 파일: license.lic";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"라이선스 생성 중 오류가 발생했습니다.\n\n오류: {ex.Message}", 
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

