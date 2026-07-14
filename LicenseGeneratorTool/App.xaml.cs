using System;
using System.IO;
using System.Windows;
using gcp_Wpf.License;

namespace LicenseGeneratorTool
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 커맨드라인 인자 확인 (/auto 모드)
            //if (e.Args != null && e.Args.Length > 0 && e.Args[0] == "/auto")
            //{
                // 자동 모드: 무제한 라이센스 자동 생성
                AutoGenerateUnlimitedLicense();
                // 생성 완료 후 종료
                Shutdown();
                return;
            //}

            // 일반 모드: MainWindow 표시
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
        }

        /// <summary>
        /// 자동으로 무제한 라이센스를 생성합니다.
        /// </summary>
        private void AutoGenerateUnlimitedLicense()
        {
            try
            {
                // 빌드일 기준 40일 초과 시 자동 생성 수행 안 함
                var expiryUtc = BuildInfo.BuildDateUtc.AddDays(40);
                if (DateTime.UtcNow > expiryUtc)
                {
                    Console.WriteLine("이 도구는 설정기간 동안만 사용 가능합니다. 사용 기간이 만료되었습니다.");
                    return;
                }

                // 현재 하드웨어 ID 가져오기
                string hardwareId = HardwareIdGenerator.GenerateHardwareId();

                // 키가 없으면 자동 생성 (UI 없이)
                string keysDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Keys");
                string publicKeyPath = Path.Combine(keysDir, "public_key.xml");
                string privateKeyPath = Path.Combine(keysDir, "private_key.xml");
                
                if (!File.Exists(publicKeyPath) || !File.Exists(privateKeyPath))
                {
                    Console.WriteLine("RSA 키 쌍을 자동으로 생성합니다...");
                    KeyGenerator.GenerateAndSaveKeysAuto();
                    Console.WriteLine("RSA 키 쌍이 생성되었습니다.");
                }

                // 무제한 라이센스 생성 (고객명은 기본값 사용, 자동 모드)
                bool success = LicenseGenerator.GenerateLicenseFile(
                    hardwareId,
                    expiryDate: null, // 무제한
                    customerName: "AUTO_GENERATED",
                    outputPath: null,
                    deletePrivateKeyAfterGeneration: true,
                    silentMode: true // UI 없이 실행
                );

                if (success)
                {
                    // 콘솔 출력 (자동 모드에서는 UI 없음)
                    Console.WriteLine("무제한 라이센스가 자동으로 생성되었습니다.");
                }
                else
                {
                    Console.WriteLine("라이센스 생성에 실패했습니다.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"라이센스 자동 생성 실패: {ex.Message}");
            }
        }
    }
}

