using System;

namespace gcp_Wpf.License
{
    /// <summary>
    /// 라이선스 데이터 구조
    /// </summary>
    [Serializable]
    public class LicenseData
    {
        /// <summary>
        /// 하드웨어 ID
        /// </summary>
        public string HardwareId { get; set; }

        /// <summary>
        /// 라이선스 만료일 (null이면 무제한)
        /// </summary>
        public DateTime? ExpiryDate { get; set; }

        /// <summary>
        /// 발급일
        /// </summary>
        public DateTime IssueDate { get; set; }

        /// <summary>
        /// 고객명 또는 회사명
        /// </summary>
        public string CustomerName { get; set; }

        /// <summary>
        /// 라이선스 키 (암호화된 전체 데이터)
        /// </summary>
        public string LicenseKey { get; set; }

        /// <summary>
        /// 추가 정보 (JSON 형식으로 확장 가능)
        /// </summary>
        public string AdditionalInfo { get; set; }

        public LicenseData()
        {
            IssueDate = DateTime.Now;
        }
    }
}

