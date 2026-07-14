using System;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace gcp_Wpf.License
{
    /// <summary>
    /// 하드웨어 고유 ID를 생성하는 클래스
    /// System.Management(WMI) 호출은 스레드 풀에서만 실행하여 UI/터치 스레드에 영향을 주지 않도록 함.
    /// </summary>
    public class HardwareIdGenerator
    {
        /// <summary>
        /// 현재 컴퓨터의 고유 하드웨어 ID를 생성합니다.
        /// MAC 주소, CPU ID, 하드디스크 시리얼을 조합하여 생성합니다.
        /// WMI(System.Management) 호출은 백그라운드 스레드에서 실행되어 터치 입력에 영향을 주지 않습니다.
        /// </summary>
        /// <returns>하드웨어 ID (SHA256 해시값)</returns>
        public static string GenerateHardwareId()
        {
            return Task.Run(() => GenerateHardwareIdCore()).GetAwaiter().GetResult();
        }

        /// <summary>
        /// WMI를 사용하는 실제 하드웨어 ID 생성 로직. 스레드 풀에서만 호출됨.
        /// </summary>
        private static string GenerateHardwareIdCore()
        {
            try
            {
                StringBuilder hardwareInfo = new StringBuilder();

                // 1. MAC 주소 수집
                string macAddress = GetMacAddress();
                hardwareInfo.Append($"MAC:{macAddress};");

                // 2. CPU ID 수집
                string cpuId = GetCpuId();
                hardwareInfo.Append($"CPU:{cpuId};");

                // 3. 하드디스크 시리얼 번호 수집
                string diskSerial = GetDiskSerial();
                hardwareInfo.Append($"DISK:{diskSerial};");

                // 4. 머신 이름 추가
                string machineName = Environment.MachineName;
                hardwareInfo.Append($"MACHINE:{machineName};");

                // 5. SHA256 해시로 고유 ID 생성
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(hardwareInfo.ToString()));
                    return BitConverter.ToString(hashBytes).Replace("-", "").ToUpper();
                }
            }
            catch (Exception ex)
            {
                // 오류 발생 시 기본값 반환 (날짜 포함하지 않음 - 날짜 변경 시 하드웨어 ID 변경 방지)
                return $"ERROR_{Environment.MachineName}_FIXED";
            }
        }

        /// <summary>
        /// MAC 주소를 가져옵니다.
        /// </summary>
        private static string GetMacAddress()
        {
            try
            {
                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up 
                              && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .OrderBy(ni => ni.GetPhysicalAddress().ToString())
                    .FirstOrDefault();

                if (networkInterfaces != null)
                {
                    return networkInterfaces.GetPhysicalAddress().ToString();
                }
            }
            catch { }

            return "UNKNOWN_MAC";
        }

        /// <summary>
        /// CPU ID를 가져옵니다.
        /// </summary>
        private static string GetCpuId()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string processorId = obj["ProcessorId"]?.ToString();
                        if (!string.IsNullOrEmpty(processorId))
                        {
                            return processorId;
                        }
                    }
                }
            }
            catch { }

            return "UNKNOWN_CPU";
        }

        /// <summary>
        /// 하드디스크 시리얼 번호를 가져옵니다.
        /// </summary>
        private static string GetDiskSerial()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive WHERE MediaType='Fixed hard disk media'"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string serialNumber = obj["SerialNumber"]?.ToString();
                        if (!string.IsNullOrEmpty(serialNumber))
                        {
                            return serialNumber.Trim();
                        }
                    }
                }
            }
            catch { }

            return "UNKNOWN_DISK";
        }

        /// <summary>
        /// 하드웨어 ID의 원본 정보를 가져옵니다 (디버깅용).
        /// </summary>
        public static string GetHardwareInfo()
        {
            return Task.Run(() =>
            {
                StringBuilder info = new StringBuilder();
                info.AppendLine($"MAC Address: {GetMacAddress()}");
                info.AppendLine($"CPU ID: {GetCpuId()}");
                info.AppendLine($"Disk Serial: {GetDiskSerial()}");
                info.AppendLine($"Machine Name: {Environment.MachineName}");
                info.AppendLine($"Hardware ID: {GenerateHardwareIdCore()}");
                return info.ToString();
            }).GetAwaiter().GetResult();
        }
    }
}

