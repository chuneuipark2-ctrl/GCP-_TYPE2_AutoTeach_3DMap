namespace HostAnalyzer.Models
{
    /// <summary>
    /// tcpServerClass Rx_DataCheck / Tx_SetData와 동일한 Host 패킷 필드
    /// 레이아웃: SYN(4) + EQID(1) + REQTYPE(1) + Data(Word 배열) + CRC(2 BigEndian) + ETX(1)
    /// </summary>
    public class HostParsedPacket
    {
        /// <summary>SYN 헤더 4바이트 (0x16 x 4)</summary>
        public string Syn { get; set; } = "";

        /// <summary>EQID (장비 ID, SRM 호기)</summary>
        public byte EqId { get; set; }

        /// <summary>REQ Type. RX: 0 또는 1, TX: 0x80 또는 0x81 (reqType+0x80)</summary>
        public byte ReqType { get; set; }

        /// <summary>Data 영역 (Word 단위, Little Endian). WCSFROM 또는 WCSTO</summary>
        public ushort[] Words { get; set; } = System.Array.Empty<ushort>();

        /// <summary>CRC (BigEndian 수신값)</summary>
        public ushort Crc { get; set; }

        /// <summary>ETX 0xF5</summary>
        public byte Etx { get; set; }

        /// <summary>파싱 성공 여부</summary>
        public bool IsValid { get; set; }

        /// <summary>파싱 실패 시 메시지</summary>
        public string ErrorMessage { get; set; } = "";

        /// <summary>RX(WCS)인지 TX(GCP)인지. true = TX(GCP 응답)</summary>
        public bool IsTx { get; set; }
    }
}
