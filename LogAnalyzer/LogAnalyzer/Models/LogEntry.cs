namespace LogAnalyzer.Models
{
    /// <summary>
    /// udpClientClass SaveLogFile 형식과 동일한 한 줄 로그 항목
    /// 형식: "HH:mm:ss:fff " + "SEND: XXYY / hex" 또는 "RECV: CMD(XXYY) / hex"
    /// </summary>
    public class LogEntry
    {
        public string Time { get; set; } = "";
        public LogEntryType Type { get; set; }
        public string Cmd { get; set; } = "";   // "0030", "8030" 등
        public string DataHex { get; set; } = "";
        public string RawLine { get; set; } = "";

        public bool IsTx => Type == LogEntryType.TX;
        public bool IsRx => Type == LogEntryType.RX;
    }

    public enum LogEntryType
    {
        Other,
        TX,   // SEND:
        RX    // RECV:
    }
}
