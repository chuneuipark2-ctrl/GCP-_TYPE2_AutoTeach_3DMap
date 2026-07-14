namespace LogAnalyzer.Models
{
    /// <summary>
    /// udpClientClass Send/Recv 헤더와 동일한 필드 (Str_SendHeader / Str_RecvHeader)
    /// 주석: singletonClass, udpClientClass Tx_SetHeader / Rx_CommandParse 참조
    /// </summary>
    public class ParsedPacket
    {
        /// <summary>SYN 헤더 4바이트 (0x16 x 4)</summary>
        public string Syn { get; set; } = "";

        /// <summary>SRC ID - TYPE  TX:0x00 지상반 / RX:0x60 SRM</summary>
        public byte SrcType { get; set; }

        /// <summary>SRC ID - INDEX  TX:0x00 / RX: SRM 호기번호</summary>
        public byte SrcId { get; set; }

        /// <summary>DST ID - TYPE  TX:0x60 SRM / RX:0x00 지상반</summary>
        public byte DstType { get; set; }

        /// <summary>DST ID - INDEX  TX: SRM 호기번호 / RX:0x00</summary>
        public byte DstId { get; set; }

        /// <summary>Sequence Num (통신 시퀀스 번호)</summary>
        public byte SeqNum { get; set; }

        /// <summary>Bypass1</summary>
        public byte ByPass1 { get; set; }

        /// <summary>Bypass2</summary>
        public byte ByPass2 { get; set; }

        /// <summary>CMD1 (RX 시 전송 CMD1+0x80)</summary>
        public byte Cmd1 { get; set; }

        /// <summary>DATA 길이 + 1 (CMD2 길이 포함). len = Command2 ~ Data 까지 길이</summary>
        public ushort Len { get; set; }

        /// <summary>CMD2</summary>
        public byte Cmd2 { get; set; }

        /// <summary>Payload (len-1 바이트). Data 바이트 파싱.</summary>
        public byte[] Data { get; set; } = System.Array.Empty<byte>();

        /// <summary>파싱 성공 여부</summary>
        public bool IsValid { get; set; }

        /// <summary>파싱 실패 시 메시지</summary>
        public string ErrorMessage { get; set; } = "";
    }
}
