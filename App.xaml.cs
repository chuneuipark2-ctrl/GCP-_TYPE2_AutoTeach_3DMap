using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using gcp_Wpf.License;

namespace gcp_Wpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // ★ 전역 최후 예외 핸들러 — 가드 못 한 async void/이벤트핸들러의 예외가 지상반 전체를 죽이지 않도록 하는 백스톱.
            //   운영 HMI 특성상 기본정책: 로그 + 운영자 알림 후 ★앱 유지★(HMI 창이 죽으면 크레인 감시/조작을 잃어 더 위험).
            //   ※ 사이트 정책상 "오류 시 즉시 종료"가 필요하면 아래 DispatcherUnhandledException 핸들러의 `ex.Handled = true;`만 제거하면 됨.
            this.DispatcherUnhandledException += (s, ex) =>
            {
                LogCrash("DispatcherUnhandledException", ex.Exception);
                try
                {
                    System.Windows.MessageBox.Show(
                        "예기치 못한 오류가 발생했지만 프로그램은 계속 동작합니다.\n로그(CrashLog 폴더)를 확인하세요.\n\n" + ex.Exception?.Message,
                        "오류", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
                catch { }
                ex.Handled = true;   // 앱 유지(창 종료 방지)
            };
            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
                LogCrash("AppDomain.UnhandledException", ex.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (s, ex) =>
            {
                LogCrash("UnobservedTaskException", ex.Exception);
                ex.SetObserved();   // 관측처리 — 미관측 태스크 예외로 인한 프로세스 종료 방지
            };

            // LicenseGeneratorTool.exe가 있으면 자동으로 무제한 라이센스 생성 후 삭제
            if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LicenseGeneratorTool.exe")))
            {
                Console.WriteLine("라이센스 생성");
                AutoGenerateUnlimitedLicense();
            }

            // 라이선스 검증을 백그라운드 스레드에서 실행 (System.Management/WMI가 UI 스레드에서 실행되지 않도록 함 → 터치 입력 정상 동작)
            bool isValid = Task.Run(() => LicenseManager.ValidateLicense()).GetAwaiter().GetResult();
            if (!isValid)
            {
                if (!string.IsNullOrEmpty(LicenseManager.LastLicenseErrorMessage))
                {
                    System.Windows.MessageBox.Show(
                        LicenseManager.LastLicenseErrorMessage,
                        "라이선스 오류",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
                Shutdown();
                return;
            }

            // 라이선스 검증 성공 시 정상적으로 애플리케이션 시작
        }

        /// <summary>전역 예외를 CrashLog 폴더에 일자별로 기록(자체 실패는 조용히 무시).</summary>
        private static void LogCrash(string source, Exception ex)
        {
            try
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CrashLog");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, $"Crash_{DateTime.Now:yyyyMMdd}.log");
                bool isNew = !File.Exists(file);
                File.AppendAllText(file, $"[{DateTime.Now:HH:mm:ss.fff}] [{source}] {ex}{Environment.NewLine}{Environment.NewLine}");
                // 새 일자파일 생성 시에만 15일 정리(다른 로그와 동일 정책 — 매 append마다 GetFiles 하지 않음)
                if (isNew)
                {
                    DateTime threshold = DateTime.Now.AddDays(-15);
                    foreach (string f in Directory.GetFiles(dir, "Crash_*.log"))
                        if (File.GetLastWriteTime(f) < threshold) File.Delete(f);
                }
            }
            catch { }
        }

        /// <summary>
        /// LicenseGeneratorTool을 자동으로 실행하여 무제한 라이센스를 생성합니다.
        /// </summary>
        private void AutoGenerateUnlimitedLicense()
        {
            try
            {
                string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string toolExePath = Path.Combine(exeDirectory, "LicenseGeneratorTool.exe");
                
                if (!File.Exists(toolExePath))
                {
                    return;
                }

                // LicenseGeneratorTool을 /auto 모드로 실행 (자동 무제한 라이센스 생성)
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = toolExePath,
                    Arguments = "/auto",
                    WorkingDirectory = exeDirectory,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                Process process = Process.Start(psi);
                process?.WaitForExit(10000); // 최대 10초 대기

                // 라이센스 생성 완료 후 LicenseGeneratorTool 삭제
                DeleteLicenseGeneratorToolIfExists();
            }
            catch
            {
                // 자동 생성 실패해도 무시 (수동 생성 가능)
            }
        }

        /// <summary>
        /// LicenseGeneratorTool.exe가 존재하면 삭제합니다 (보안을 위해).
        /// </summary>
        private void DeleteLicenseGeneratorToolIfExists()
        {
            try
            {
                string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string toolExePath = Path.Combine(exeDirectory, "LicenseGeneratorTool.exe");
                string toolDllPath = Path.Combine(exeDirectory, "LicenseGeneratorTool.dll");
                string toolPdbPath = Path.Combine(exeDirectory, "LicenseGeneratorTool.pdb");
                string toolDepsPath = Path.Combine(exeDirectory, "LicenseGeneratorTool.deps.json");
                string toolRuntimeConfigPath = Path.Combine(exeDirectory, "LicenseGeneratorTool.runtimeconfig.json");

                bool fileExists = File.Exists(toolExePath) || 
                                 File.Exists(toolDllPath) || 
                                 File.Exists(toolPdbPath) || 
                                 File.Exists(toolDepsPath) || 
                                 File.Exists(toolRuntimeConfigPath);

                if (!fileExists)
                {
                    return; // 파일이 없으면 종료
                }

                // 배치 파일 생성하여 삭제
                string batchFile = Path.Combine(exeDirectory, $"delete_tool_{Guid.NewGuid():N}.bat");
                string batchContent = $@"@echo off
setlocal enabledelayedexpansion
REM 프로그램 종료 대기
timeout /t 1 /nobreak >nul 2>&1

REM LicenseGeneratorTool 파일 삭제 시도 (최대 5회)
set /a count=0
:delete_loop
";

                // 각 파일에 대해 삭제 명령 추가
                if (File.Exists(toolExePath))
                {
                    batchContent += $@"if exist ""{toolExePath}"" (
    del /f /q ""{toolExePath}"" >nul 2>&1
    if exist ""{toolExePath}"" (
        set /a count+=1
        if !count! LSS 5 (
            timeout /t 1 /nobreak >nul 2>&1
            goto delete_loop
        )
    )
)
";
                }

                // 나머지 파일들 삭제
                batchContent += $@"if exist ""{toolDllPath}"" del /f /q ""{toolDllPath}"" >nul 2>&1
if exist ""{toolPdbPath}"" del /f /q ""{toolPdbPath}"" >nul 2>&1
if exist ""{toolDepsPath}"" del /f /q ""{toolDepsPath}"" >nul 2>&1
if exist ""{toolRuntimeConfigPath}"" del /f /q ""{toolRuntimeConfigPath}"" >nul 2>&1

REM 배치 파일 자신도 삭제
if exist ""%~f0"" (
    timeout /t 1 /nobreak >nul 2>&1
    del /f /q ""%~f0"" >nul 2>&1
)
endlocal
";

                File.WriteAllText(batchFile, batchContent, System.Text.Encoding.Default);

                // 배치 파일 실행 (비동기, 숨김)
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{batchFile}\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WorkingDirectory = exeDirectory
                };

                Process.Start(psi);
            }
            catch
            {
                // 삭제 실패해도 무시 (조용히 실패)
            }
        }
    }
}
