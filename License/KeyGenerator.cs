using System;
using System.IO;
using System.Security.Cryptography;
using System.Windows;

namespace gcp_Wpf.License
{
    /// <summary>
    /// RSA 키 쌍 생성 유틸리티
    /// 최초 1회 실행하여 키를 생성한 후, 생성된 키를 LicenseManager와 LicenseGenerator에 적용해야 합니다.
    /// </summary>
    public class KeyGenerator
    {
        /// <summary>
        /// RSA 키 쌍을 생성하고 파일로 저장합니다 (자동 모드, UI 없음).
        /// 기존 키가 있으면 자동으로 덮어씁니다.
        /// </summary>
        public static void GenerateAndSaveKeysAuto()
        {
            try
            {
                string keyDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Keys");
                string publicKeyPath = Path.Combine(keyDirectory, "public_key.xml");
                string privateKeyPath = Path.Combine(keyDirectory, "private_key.xml");

                // 키 디렉토리 생성
                if (!Directory.Exists(keyDirectory))
                {
                    Directory.CreateDirectory(keyDirectory);
                }

                // 새로운 키 생성 (기존 키가 있어도 덮어쓰기)
                using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(2048))
                {
                    string publicKey = rsa.ToXmlString(false);
                    string privateKey = rsa.ToXmlString(true);

                    File.WriteAllText(publicKeyPath, publicKey);
                    File.WriteAllText(privateKeyPath, privateKey);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"키 생성 중 오류가 발생했습니다: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// RSA 키 쌍을 생성하고 파일로 저장합니다.
        /// 기존 키가 있으면 경고를 표시하고 확인 후 진행합니다.
        /// </summary>
        public static void GenerateAndSaveKeys()
        {
            try
            {
                string keyDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Keys");
                string publicKeyPath = Path.Combine(keyDirectory, "public_key.xml");
                string privateKeyPath = Path.Combine(keyDirectory, "private_key.xml");

                // 기존 키 파일 존재 확인
                bool publicKeyExists = File.Exists(publicKeyPath);
                bool privateKeyExists = File.Exists(privateKeyPath);

                if (publicKeyExists || privateKeyExists)
                {
                    var result = MessageBox.Show(
                        "경고: 기존 키 파일이 이미 존재합니다!\n\n" +
                        "새로운 키를 생성하면 기존에 생성된 모든 라이선스가 무효화됩니다.\n" +
                        "기존 라이선스는 새로운 공개키로 검증할 수 없게 됩니다.\n\n" +
                        "정말로 새로운 키를 생성하시겠습니까?\n\n" +
                        "권장: 기존 키를 백업한 후 진행하세요.",
                        "기존 키 발견",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes)
                    {
                        MessageBox.Show("키 생성이 취소되었습니다.", "취소", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // 기존 키 백업 제안
                    var backupResult = MessageBox.Show(
                        "기존 키를 백업하시겠습니까?\n\n" +
                        "백업하면 나중에 기존 라이선스를 사용할 수 있습니다.",
                        "키 백업",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (backupResult == MessageBoxResult.Yes)
                    {
                        try
                        {
                            string backupDir = Path.Combine(keyDirectory, "Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                            Directory.CreateDirectory(backupDir);

                            if (publicKeyExists)
                            {
                                File.Copy(publicKeyPath, Path.Combine(backupDir, "public_key.xml"), true);
                            }
                            if (privateKeyExists)
                            {
                                File.Copy(privateKeyPath, Path.Combine(backupDir, "private_key.xml"), true);
                            }

                            MessageBox.Show(
                                $"기존 키가 백업되었습니다.\n백업 경로: {backupDir}",
                                "백업 완료",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                $"백업 중 오류가 발생했습니다.\n\n오류: {ex.Message}\n\n계속 진행하시겠습니까?",
                                "백업 오류",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);

                            if (MessageBox.Show(
                                "백업 실패. 그래도 새로운 키를 생성하시겠습니까?",
                                "확인",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question) != MessageBoxResult.Yes)
                            {
                                return;
                            }
                        }
                    }
                }

                // 키 디렉토리 생성
                if (!Directory.Exists(keyDirectory))
                {
                    Directory.CreateDirectory(keyDirectory);
                }

                // 새로운 키 생성
                using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(2048))
                {
                    string publicKey = rsa.ToXmlString(false);
                    string privateKey = rsa.ToXmlString(true);

                    File.WriteAllText(publicKeyPath, publicKey);
                    File.WriteAllText(privateKeyPath, privateKey);

                    string message = "RSA 키 쌍이 생성되었습니다.\n\n" +
                                    $"공개키: {publicKeyPath}\n" +
                                    $"개인키: {privateKeyPath}\n\n";

                    if (publicKeyExists || privateKeyExists)
                    {
                        message += "⚠️ 주의: 기존 키가 덮어씌워졌습니다.\n" +
                                  "기존에 생성된 라이선스는 이제 사용할 수 없습니다.\n" +
                                  "새로운 라이선스를 생성해야 합니다.\n\n";
                    }

                    message += "이제 라이선스를 생성할 수 있습니다.";

                    MessageBox.Show(
                        message,
                        "키 생성 완료",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"키 생성 중 오류가 발생했습니다.\n\n오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

