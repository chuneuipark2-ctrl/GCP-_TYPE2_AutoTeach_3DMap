using System;
using System.Windows;
using System.Windows.Controls;

namespace gcp_Wpf.License
{
    /// <summary>
    /// WindowLicenseGenerator.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class WindowLicenseGenerator : Window
    {
        public WindowLicenseGenerator()
        {
            InitializeComponent();
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

