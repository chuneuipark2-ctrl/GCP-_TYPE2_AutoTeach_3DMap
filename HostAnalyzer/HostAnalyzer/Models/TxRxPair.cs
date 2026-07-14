namespace HostAnalyzer.Models
{
    /// <summary>
    /// WCS(RX) 1건과 그에 대응하는 GCP(TX) 한 쌍
    /// </summary>
    public class TxRxPair
    {
        public LogEntry Rx { get; set; } = null!;  // WCS 요청
        public LogEntry? Tx { get; set; }           // GCP 응답 (있을 경우)
        public int Index { get; set; }

        public string Time => Rx.Time;
        public bool HasResponse => Tx != null;
    }
}
