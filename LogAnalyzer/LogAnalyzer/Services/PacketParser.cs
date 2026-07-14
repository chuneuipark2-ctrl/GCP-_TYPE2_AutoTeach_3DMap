using System;
using System.Linq;
using LogAnalyzer.Models;

namespace LogAnalyzer.Services
{
    /// <summary>
    /// udpClientClass 로그 HEX 문자열을 파싱하여 ParsedPacket 생성
    /// 패킷 레이아웃: SYN(4) + srcType,srcID,dstType,dstID,seqNum,byPass1,byPass2,cmd1, len(2), cmd2, data...
    /// </summary>
    public static class PacketParser
    {
        private const int MinPacketLength = 15; // SYN(4) + 헤더 11바이트 이상 (len은 2바이트)

        /// <summary>
        /// 로그 한 줄에서 HEX 부분만 추출 ( "SEND: XXXX / " 또는 "RECV: CMD(XXXX) / " 뒤)
        /// </summary>
        public static string ExtractHexFromLogLine(string rawLine)
        {
            if (string.IsNullOrEmpty(rawLine)) return "";
            int idx = rawLine.IndexOf(" / ", StringComparison.Ordinal);
            if (idx < 0) return "";
            string hex = rawLine.Substring(idx + 3).Trim();
            // 공백/하이픈 제거
            return new string(hex.Where(c => char.IsLetterOrDigit(c)).ToArray());
        }

        /// <summary>
        /// HEX 문자열을 바이트 배열로 변환 (2자리 = 1바이트)
        /// </summary>
        public static byte[] HexStringToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length % 2 != 0)
                return Array.Empty<byte>();
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                if (!byte.TryParse(hex.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber, null, out bytes[i]))
                    return Array.Empty<byte>();
            }
            return bytes;
        }

        /// <summary>
        /// 전체 패킷 바이트(SYN 포함)를 ParsedPacket으로 파싱. udpClientClass Rx_CommandParse / Send 파싱과 동일 오프셋.
        /// </summary>
        public static ParsedPacket Parse(byte[] raw)
        {
            var p = new ParsedPacket();
            if (raw == null || raw.Length < MinPacketLength)
            {
                p.IsValid = false;
                p.ErrorMessage = raw == null ? "데이터 없음" : $"길이 부족 (최소 {MinPacketLength}바이트, 현재 {raw?.Length ?? 0})";
                return p;
            }

            try
            {
                p.Syn = raw.Length >= 4
                    ? $"{raw[0]:X2}{raw[1]:X2}{raw[2]:X2}{raw[3]:X2}"
                    : "";
                p.SrcType = raw[4];
                p.SrcId = raw[5];
                p.DstType = raw[6];
                p.DstId = raw[7];
                p.SeqNum = raw[8];
                p.ByPass1 = raw[9];
                p.ByPass2 = raw[10];
                p.Cmd1 = raw[11];
                p.Len = BitConverter.ToUInt16(raw, 12); // Little Endian
                p.Cmd2 = raw[14];

                int dataLen = p.Len > 0 ? p.Len - 1 : 0; // len = CMD2(1) + Data
                if (dataLen > 0 && raw.Length >= 15 + dataLen)
                {
                    p.Data = new byte[dataLen];
                    Array.Copy(raw, 15, p.Data, 0, dataLen);
                }
                else
                    p.Data = Array.Empty<byte>();

                p.IsValid = true;
            }
            catch (Exception ex)
            {
                p.IsValid = false;
                p.ErrorMessage = ex.Message;
            }
            return p;
        }

        /// <summary>
        /// 로그 한 줄(원문)에서 HEX 추출 후 파싱
        /// </summary>
        public static ParsedPacket ParseFromLogLine(string rawLine)
        {
            string hex = ExtractHexFromLogLine(rawLine);
            byte[] bytes = HexStringToBytes(hex);
            return Parse(bytes);
        }
    }
}
