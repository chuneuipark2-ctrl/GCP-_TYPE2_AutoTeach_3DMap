namespace HostAnalyzer.Models
{
    /// <summary>
    /// tcpServerClass SaveLogFile 형식과 동일한 한 줄 로그 항목
    /// 형식: "WCS: (port)hex" (RX), "GCP: hex" (TX)
    /// </summary>
    public class LogEntry
    {
        public string Time { get; set; } = "";
        public LogEntryType Type { get; set; }
        public string PortOrEqId { get; set; } = "";  // WCS 시 포트 번호, GCP 시 빈 문자열
        public string DataHex { get; set; } = "";
        public string RawLine { get; set; } = "";

        public bool IsTx => Type == LogEntryType.TX;
        public bool IsRx => Type == LogEntryType.RX;
    }

    public enum LogEntryType
    {
        Other,
        TX,   // GCP: (서버 응답)
        RX    // WCS: (호스트 요청)
    }
}
