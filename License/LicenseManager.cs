using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace gcp_Wpf.License
{
    /// <summary>
    /// 라이선스 검증 및 관리 클래스
    /// </summary>
    public class LicenseManager
    {
        // 라이선스 파일 경로 (실행 파일과 같은 디렉토리)
        private static string LicenseFilePath => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "license.lic");

        // RSA 공개키 파일 경로 (서명 검증용)
        private static string PublicKeyFilePath => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Keys",
            "public_key.xml");

        // 마지막 실행 시간 저장 파일 경로 (암호화됨)
        private static string LastRunTimeFilePath => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Keys",
            ".lastrun");

        /// <summary>
        /// RSA 공개키를 파일에서 로드합니다.
        /// </summary>
        private static string LoadPublicKey()
        {
            try
            {
                if (File.Exists(PublicKeyFilePath))
                {
                    return File.ReadAllText(PublicKeyFilePath);
                }
            }
            catch { }

            // 파일이 없으면 기본값 반환 (실제로는 오류 처리 필요)
            return null;
        }

        /// <summary>
        /// 라이선스가 유효한지 검증합니다.
        /// </summary>
        /// <returns>유효하면 true, 그렇지 않으면 false</returns>
        public static bool ValidateLicense()
        {
            LastLicenseErrorMessage = null;
            try
            {
                // 1. 라이선스 파일 존재 확인
                if (!File.Exists(LicenseFilePath))
                {
                    ShowLicenseError("라이선스 파일을 찾을 수 없습니다.\n\n라이선스 파일(license.lic)이 필요합니다.");
                    return false;
                }

                // 2. 라이선스 파일 읽기
                string encryptedLicense = File.ReadAllText(LicenseFilePath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(encryptedLicense))
                {
                    ShowLicenseError("라이선스 파일이 비어있습니다.");
                    return false;
                }

                // 3. 라이선스 복호화
                string decryptedLicense = DecryptLicense(encryptedLicense);
                if (string.IsNullOrEmpty(decryptedLicense))
                {
                    ShowLicenseError("라이선스 파일이 손상되었거나 유효하지 않습니다.");
                    return false;
                }

                // 4. 라이선스 데이터 파싱
                LicenseData licenseData = ParseLicenseData(decryptedLicense);
                if (licenseData == null)
                {
                    ShowLicenseError("라이선스 데이터를 읽을 수 없습니다.");
                    return false;
                }

                // 5. 하드웨어 ID 검증
                string currentHardwareId = HardwareIdGenerator.GenerateHardwareId();
                Console.WriteLine($"HWID : {currentHardwareId.Substring(0, 16)}");
                if (licenseData.HardwareId != currentHardwareId)
                {
                    ShowLicenseError($"라이선스가 이 컴퓨터에 등록되지 않았습니다.\n\n현재 하드웨어 ID: {currentHardwareId.Substring(0, 16)}...\n등록된 하드웨어 ID: {licenseData.HardwareId.Substring(0, 16)}...");
                    return false;
                }

                DateTime currentTime = DateTime.Now;

                // 6. PC 시간 검증: 발급일보다 5일 이상 이전이면 시간 조작 의심 → 동작 안 함
                DateTime minAllowedTime = licenseData.IssueDate.Date.AddDays(-5);
                if (currentTime < minAllowedTime)
                {
                    ShowLicenseError("시스템 날짜/시간이 맞지 않습니다.\n\n라이센스를 다시 발급해 주세요.");
                    return false;
                }

                // 7. 만료일 검증
                if (licenseData.ExpiryDate.HasValue)
                {
                    // 만료일이 발급일보다 이전이면 잘못된 라이선스
                    if (licenseData.ExpiryDate.Value < licenseData.IssueDate)
                    {
                        ShowLicenseError("라이선스 데이터가 잘못되었습니다.\n\n만료일이 발급일보다 이전입니다.");
                        return false;
                    }
                    
                    // 현재 시간이 만료일을 초과했는지 확인
                    // 만료일의 끝 시간(23:59:59)까지 허용
                    DateTime expiryEndOfDay = licenseData.ExpiryDate.Value.Date.AddDays(1).AddSeconds(-1);
                    
                    if (currentTime > expiryEndOfDay)
                    {
                        ShowLicenseError($"라이선스가 만료되었습니다.\n\n발급일: {licenseData.IssueDate:yyyy-MM-dd}\n만료일: {licenseData.ExpiryDate.Value:yyyy-MM-dd}");
                        return false;
                    }
                }

                // 8. 모든 검증 통과 (마지막 실행 시간 저장 제거 - 시간 검증 불필요)
                return true;
            }
            catch (Exception ex)
            {
                ShowLicenseError($"라이선스 검증 중 오류가 발생했습니다.\n\n오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 라이선스를 복호화하고 검증합니다. (AES 암호화 + RSA 서명 방식)
        /// 형식: [RSA 서명 길이(4바이트)] + [RSA 서명] + [AES로 암호화된 데이터]
        /// </summary>
        private static string DecryptLicense(string encryptedData)
        {
            try
            {
                string publicKeyXml = LoadPublicKey();
                if (string.IsNullOrEmpty(publicKeyXml))
                {
                    return null;
                }

                byte[] allData = Convert.FromBase64String(encryptedData);
                
                // 첫 4바이트는 RSA 서명의 길이
                int signatureLength = BitConverter.ToInt32(allData, 0);
                
                // RSA 서명 추출
                byte[] signature = new byte[signatureLength];
                Array.Copy(allData, 4, signature, 0, signatureLength);
                
                // AES로 암호화된 데이터 추출
                byte[] encryptedLicenseData = new byte[allData.Length - 4 - signatureLength];
                Array.Copy(allData, 4 + signatureLength, encryptedLicenseData, 0, encryptedLicenseData.Length);

                // RSA 서명 검증
                using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(2048))
                {
                    rsa.FromXmlString(publicKeyXml);
                    
                    // 서명 검증 (SHA256)
                    using (SHA256 sha256 = SHA256.Create())
                    {
                        byte[] hash = sha256.ComputeHash(encryptedLicenseData);
                        if (!rsa.VerifyHash(hash, CryptoConfig.MapNameToOID("SHA256"), signature))
                        {
                            // 서명 검증 실패
                            return null;
                        }
                    }
                }

                // AES 복호화 시도
                // 하드웨어 ID는 복호화 후에 검증하므로, 먼저 라이센스에 포함된 하드웨어 ID로 복호화 시도
                // 하지만 암호화 시 사용된 하드웨어 ID를 모르므로, 현재 PC의 하드웨어 ID로 시도
                // 만약 실패하면 하드웨어가 변경되었거나 다른 PC에서 생성된 라이센스일 수 있음
                
                // 현재 PC의 하드웨어 ID로 복호화 시도
                string currentHardwareId = HardwareIdGenerator.GenerateHardwareId();
                byte[] aesKey = DeriveAesKeyFromHardwareId(currentHardwareId);
                byte[] iv = new byte[16];
                Array.Copy(aesKey, 16, iv, 0, 16); // 키의 일부를 IV로 사용

                string decryptedText = null;
                try
                {
                    using (Aes aes = Aes.Create())
                    {
                        aes.Key = aesKey;
                        aes.IV = iv;
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;

                        using (ICryptoTransform decryptor = aes.CreateDecryptor())
                        using (MemoryStream ms = new MemoryStream(encryptedLicenseData))
                        using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                        using (StreamReader sr = new StreamReader(cs, Encoding.UTF8))
                        {
                            decryptedText = sr.ReadToEnd();
                        }
                    }
                }
                catch (CryptographicException)
                {
                    // 복호화 실패 - 하드웨어 ID가 일치하지 않거나 데이터가 손상됨
                    // 이 경우 null을 반환하면 하드웨어 ID 검증 단계에서 처리됨
                    return null;
                }

                // 복호화 성공 시 결과 반환
                // 복호화된 데이터에서 하드웨어 ID를 추출하여 검증 (ValidateLicense에서 수행)
                return decryptedText;
            }
            catch (Exception ex)
            {
                // 디버깅용: 실제 오류 정보 로그
                System.Diagnostics.Debug.WriteLine($"DecryptLicense 오류: {ex.GetType().Name} - {ex.Message}");
                return null;
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
        /// 라이선스 데이터를 파싱합니다.
        /// </summary>
        private static LicenseData ParseLicenseData(string licenseText)
        {
            try
            {
                // 간단한 형식: HardwareId|ExpiryDate|IssueDate|CustomerName|AdditionalInfo
                string[] parts = licenseText.Split('|');
                if (parts.Length < 4) return null;

                LicenseData data = new LicenseData
                {
                    HardwareId = parts[0],
                    IssueDate = DateTime.Parse(parts[2]),
                    CustomerName = parts[3],
                    AdditionalInfo = parts.Length > 4 ? parts[4] : ""
                };

                // 만료일 파싱 (EMPTY는 무제한)
                if (parts[1] != "EMPTY" && DateTime.TryParse(parts[1], out DateTime expiryDate))
                {
                    data.ExpiryDate = expiryDate;
                }

                return data;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 백그라운드 스레드에서 검증 실패 시 설정되는 오류 메시지. UI 스레드에서 메시지 박스 표시용.
        /// </summary>
        internal static string LastLicenseErrorMessage { get; private set; }

        /// <summary>
        /// 라이선스 오류 메시지를 표시합니다.
        /// 백그라운드 스레드에서 호출 시 메시지를 저장만 하고, UI 스레드(App)에서 표시하여 데드락을 방지합니다.
        /// </summary>
        private static void ShowLicenseError(string message)
        {
            LastLicenseErrorMessage = message;
            if (Application.Current?.Dispatcher == null)
                return;
            if (Application.Current.Dispatcher.CheckAccess())
            {
                MessageBox.Show(
                    message,
                    "라이선스 오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            // 백그라운드 스레드에서는 메시지 저장만 함. App.OnStartup에서 표시
        }

        /// <summary>
        /// 현재 하드웨어 ID를 가져옵니다 (라이선스 생성 시 사용).
        /// </summary>
        public static string GetCurrentHardwareId()
        {
            return HardwareIdGenerator.GenerateHardwareId();
        }

        /// <summary>
        /// 마지막 실행 시간을 저장합니다 (암호화).
        /// </summary>
        private static void SaveLastRunTime(DateTime runTime)
        {
            try
            {
                string directory = Path.GetDirectoryName(LastRunTimeFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 하드웨어 ID 기반 키로 암호화
                string hardwareId = HardwareIdGenerator.GenerateHardwareId();
                byte[] aesKey = DeriveAesKeyFromHardwareId(hardwareId + "_RUNTIME");
                byte[] iv = new byte[16];
                Array.Copy(aesKey, 16, iv, 0, 16);

                string timeString = runTime.ToString("yyyy-MM-dd HH:mm:ss.fff");

                byte[] encrypted;
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
                            sw.Write(timeString);
                        }
                        encrypted = ms.ToArray();
                    }
                }

                File.WriteAllBytes(LastRunTimeFilePath, encrypted);
            }
            catch
            {
                // 저장 실패해도 무시 (다음 실행 시 경고만 표시)
            }
        }

        /// <summary>
        /// 마지막 실행 시간을 로드합니다 (복호화).
        /// </summary>
        private static DateTime? LoadLastRunTime()
        {
            try
            {
                if (!File.Exists(LastRunTimeFilePath))
                {
                    return null;
                }

                byte[] encrypted = File.ReadAllBytes(LastRunTimeFilePath);
                if (encrypted.Length == 0)
                {
                    return null;
                }

                // 하드웨어 ID 기반 키로 복호화
                string hardwareId = HardwareIdGenerator.GenerateHardwareId();
                byte[] aesKey = DeriveAesKeyFromHardwareId(hardwareId + "_RUNTIME");
                byte[] iv = new byte[16];
                Array.Copy(aesKey, 16, iv, 0, 16);

                using (Aes aes = Aes.Create())
                {
                    aes.Key = aesKey;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (ICryptoTransform decryptor = aes.CreateDecryptor())
                    using (MemoryStream ms = new MemoryStream(encrypted))
                    using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (StreamReader sr = new StreamReader(cs, Encoding.UTF8))
                    {
                        string timeString = sr.ReadToEnd();
                        if (DateTime.TryParse(timeString, out DateTime lastRunTime))
                        {
                            return lastRunTime;
                        }
                    }
                }
            }
            catch
            {
                // 로드 실패 시 null 반환
            }

            return null;
        }
    }
}

