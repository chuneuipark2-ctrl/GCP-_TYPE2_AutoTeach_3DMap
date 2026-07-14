using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace gcp_Wpf.License
{
    /// <summary>
    /// 라이선스 생성 유틸리티 (관리자용)
    /// 실제 운영 시에는 별도 관리자 프로그램으로 분리하는 것을 권장합니다.
    /// </summary>
    public class LicenseGenerator
    {
        // RSA 개인키 파일 경로
        private static string PrivateKeyFilePath => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Keys",
            "private_key.xml");

        /// <summary>
        /// RSA 개인키를 파일에서 로드합니다.
        /// </summary>
        private static string LoadPrivateKey()
        {
            try
            {
                if (File.Exists(PrivateKeyFilePath))
                {
                    return File.ReadAllText(PrivateKeyFilePath);
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// 라이선스 파일을 생성합니다.
        /// </summary>
        /// <param name="hardwareId">하드웨어 ID</param>
        /// <param name="expiryDate">만료일 (null이면 무제한)</param>
        /// <param name="customerName">고객명</param>
        /// <param name="outputPath">출력 파일 경로</param>
        /// <param name="deletePrivateKeyAfterGeneration">생성 후 개인키 삭제 여부 (기본값: true)</param>
        /// <param name="silentMode">자동 모드 (UI 없이 실행, 기본값: false)</param>
        /// <returns>성공 여부</returns>
        public static bool GenerateLicenseFile(
            string hardwareId, 
            DateTime? expiryDate, 
            string customerName, 
            string outputPath = null,
            bool deletePrivateKeyAfterGeneration = true,
            bool silentMode = false)
        {
            try
            {
                // 라이선스 데이터 생성
                string licenseData = BuildLicenseData(hardwareId, expiryDate, customerName);

                // RSA 암호화
                string encryptedLicense = EncryptLicense(licenseData, silentMode);
                if (string.IsNullOrEmpty(encryptedLicense))
                {
                    if (!silentMode)
                    {
                        MessageBox.Show("라이선스 암호화에 실패했습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    return false;
                }

                // 파일 저장
                if (string.IsNullOrEmpty(outputPath))
                {
                    outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.lic");
                }

                File.WriteAllText(outputPath, encryptedLicense, Encoding.UTF8);
                
                // 라이선스 생성 후 개인키 삭제 (보안을 위해)
                if (deletePrivateKeyAfterGeneration && File.Exists(PrivateKeyFilePath))
                {
                    try
                    {
                        File.Delete(PrivateKeyFilePath);
                        if (!silentMode)
                        {
                            MessageBox.Show(
                                $"라이선스 파일이 생성되었습니다.\n경로: {outputPath}\n\n보안을 위해 개인키 파일(private_key.xml)이 삭제되었습니다.",
                                "성공",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!silentMode)
                        {
                            MessageBox.Show(
                                $"라이선스 파일이 생성되었습니다.\n경로: {outputPath}\n\n경고: 개인키 파일 삭제에 실패했습니다.\n수동으로 삭제해주세요.\n오류: {ex.Message}",
                                "성공 (경고)",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    }
                }
                else
                {
                    if (!silentMode)
                    {
                        MessageBox.Show($"라이선스 파일이 생성되었습니다.\n경로: {outputPath}", "성공", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                if (!silentMode)
                {
                    MessageBox.Show($"라이선스 생성 중 오류가 발생했습니다.\n\n오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                throw; // 자동 모드에서는 예외를 던져서 호출자가 처리하도록
            }
        }

        /// <summary>
        /// 라이선스 데이터 문자열을 생성합니다.
        /// </summary>
        private static string BuildLicenseData(string hardwareId, DateTime? expiryDate, string customerName)
        {
            string expiryDateStr = expiryDate.HasValue ? expiryDate.Value.ToString("yyyy-MM-dd HH:mm:ss") : "EMPTY";
            string issueDateStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            // 형식: HardwareId|ExpiryDate|IssueDate|CustomerName|AdditionalInfo
            return $"{hardwareId}|{expiryDateStr}|{issueDateStr}|{customerName}|";
        }

        /// <summary>
        /// 라이선스를 암호화하고 서명합니다. (AES 암호화 + RSA 서명 방식)
        /// </summary>
        private static string EncryptLicense(string licenseData, bool silentMode = false)
        {
            try
            {
                string privateKeyXml = LoadPrivateKey();
                if (string.IsNullOrEmpty(privateKeyXml))
                {
                    if (!silentMode)
                    {
                        MessageBox.Show("개인키 파일을 찾을 수 없습니다.\n\nKeys/private_key.xml 파일이 필요합니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    return null;
                }

                // 1. 하드웨어 ID에서 AES 키 생성 (라이선스 데이터에서 추출)
                // 라이선스 데이터 형식: HardwareId|ExpiryDate|IssueDate|CustomerName|AdditionalInfo
                string[] parts = licenseData.Split('|');
                if (parts.Length < 1) return null;
                
                string hardwareId = parts[0];
                byte[] aesKey = DeriveAesKeyFromHardwareId(hardwareId);
                byte[] iv = new byte[16];
                Array.Copy(aesKey, 16, iv, 0, 16); // 키의 일부를 IV로 사용

                // 2. AES로 라이선스 데이터 암호화
                byte[] encryptedLicenseData;
                using (Aes aes = Aes.Create())
                {
                    aes.Key = aesKey;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (ICryptoTransform encryptor = aes.CreateEncryptor())
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                        using (StreamWriter sw = new StreamWriter(cs, Encoding.UTF8))
                        {
                            sw.Write(licenseData);
                        }
                        encryptedLicenseData = ms.ToArray();
                    }
                }

                // 3. RSA로 서명 생성 (SHA256)
                byte[] signature;
                using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(2048))
                {
                    rsa.FromXmlString(privateKeyXml);
                    using (SHA256 sha256 = SHA256.Create())
                    {
                        byte[] hash = sha256.ComputeHash(encryptedLicenseData);
                        signature = rsa.SignHash(hash, CryptoConfig.MapNameToOID("SHA256"));
                    }
                }

                // 4. 최종 데이터 조합: [서명 길이(4바이트)] + [서명] + [암호화된 데이터]
                byte[] finalData = new byte[4 + signature.Length + encryptedLicenseData.Length];
                BitConverter.GetBytes(signature.Length).CopyTo(finalData, 0);
                signature.CopyTo(finalData, 4);
                encryptedLicenseData.CopyTo(finalData, 4 + signature.Length);

                return Convert.ToBase64String(finalData);
            }
            catch (Exception ex)
            {
                if (!silentMode)
                {
                    MessageBox.Show($"암호화 중 오류가 발생했습니다.\n\n오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                throw; // 자동 모드에서는 예외를 던져서 호출자가 처리하도록
            }
        }

        /// <summary>
        /// 하드웨어 ID에서 AES 키를 생성합니다.
        /// </summary>
        private static byte[] DeriveAesKeyFromHardwareId(string hardwareId)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(hardwareId + "LICENSE_SALT_2024"));
                // 32바이트 키 반환
                return hash;
            }
        }

        /// <summary>
        /// RSA 키 쌍을 생성합니다 (최초 1회만 실행하여 키 생성).
        /// </summary>
        public static void GenerateRSAKeyPair(out string publicKey, out string privateKey)
        {
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(2048))
            {
                publicKey = rsa.ToXmlString(false);
                privateKey = rsa.ToXmlString(true);
            }
        }
    }
}

