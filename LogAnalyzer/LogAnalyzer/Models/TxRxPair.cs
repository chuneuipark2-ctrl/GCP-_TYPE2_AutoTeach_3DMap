namespace LogAnalyzer.Models
{
    /// <summary>
    /// TX 1건과 그에 대응하는 RX(있을 경우) 한 쌍
    /// </summary>
    public class TxRxPair
    {
        public LogEntry Tx { get; set; } = null!;
        public LogEntry? Rx { get; set; }
        public int Index { get; set; }  // 1-based 표시용

        public string Time => Tx.Time;
        public string Cmd => Tx.Cmd;
        public bool HasResponse => Rx != null;
    }
}
