using System;
using System.Linq;
using HostAnalyzer.Models;

namespace HostAnalyzer.Services
{
    /// <summary>
    /// tcpServerClass 로그 HEX를 Host 패킷으로 파싱
    /// 패킷: SYN(4) + EQID(1) + REQTYPE(1) + Data(Word 배열) + CRC(2 BigEndian) + ETX(1)
    /// </summary>
    public static class HostPacketParser
    {
        private const int MinPacketLength = 9; // SYN(4) + EQID(1) + REQTYPE(1) + CRC(2) + ETX(1). Data는 0워드 이상.

        public static string ExtractHexFromLogLine(string rawLine, bool isWcs)
        {
            if (string.IsNullOrEmpty(rawLine)) return "";
            if (isWcs)
            {
                int idx = rawLine.IndexOf(")", StringComparison.Ordinal);
                if (idx < 0) return "";
                return NormalizeHex(rawLine.Substring(idx + 1).Trim());
            }
            int gcpIdx = rawLine.IndexOf("GCP:", StringComparison.OrdinalIgnoreCase);
            if (gcpIdx < 0) return "";
            return NormalizeHex(rawLine.Substring(gcpIdx + 4).Trim());
        }

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

        public static HostParsedPacket Parse(byte[] raw, bool isTx)
        {
            var p = new HostParsedPacket { IsTx = isTx };
            if (raw == null || raw.Length < MinPacketLength)
            {
                p.IsValid = false;
                p.ErrorMessage = raw == null ? "데이터 없음" : $"길이 부족 (최소 {MinPacketLength}바이트, 현재 {raw?.Length ?? 0})";
                return p;
            }

            try
            {
                // SYN
                if (raw[0] != 0x16 || raw[1] != 0x16 || raw[2] != 0x16 || raw[3] != 0x16)
                {
                    p.IsValid = false;
                    p.ErrorMessage = "SYN 헤더 오류 (0x16 x 4 아님)";
                    return p;
                }
                p.Syn = $"{raw[0]:X2}{raw[1]:X2}{raw[2]:X2}{raw[3]:X2}";

                p.EqId = raw[4];
                p.ReqType = raw[5];

                if (raw[raw.Length - 1] != 0xF5)
                {
                    p.IsValid = false;
                    p.ErrorMessage = "ETX 오류 (0xF5 아님)";
                    return p;
                }
                p.Etx = 0xF5;

                int dataLen = raw.Length - 9; // 4+1+1+2+1
                if (dataLen < 0 || dataLen % 2 != 0)
                {
                    p.IsValid = false;
                    p.ErrorMessage = "Data 길이 오류";
                    return p;
                }

                p.Crc = (ushort)((raw[raw.Length - 3] << 8) | raw[raw.Length - 2]);

                byte[] packetData = new byte[dataLen];
                Array.Copy(raw, 6, packetData, 0, dataLen);
                ushort calcCrc = Crc16Helper.Crc16Ccitt(packetData);
                if (p.Crc != calcCrc)
                {
                    p.IsValid = false;
                    p.ErrorMessage = $"CRC 불일치 (수신: 0x{p.Crc:X4}, 계산: 0x{calcCrc:X4})";
                    return p;
                }

                int wordCount = dataLen / 2;
                p.Words = new ushort[wordCount];
                for (int i = 0; i < wordCount; i++)
                    p.Words[i] = BitConverter.ToUInt16(raw, 6 + i * 2);

                p.IsValid = true;
            }
            catch (Exception ex)
            {
                p.IsValid = false;
                p.ErrorMessage = ex.Message;
            }
            return p;
        }

        public static HostParsedPacket ParseFromLogLine(string rawLine, bool isTx)
        {
            string hex = ExtractHexFromLogLine(rawLine, isWcs: !isTx);
            byte[] bytes = HexStringToBytes(hex);
            return Parse(bytes, isTx);
        }

        private static string NormalizeHex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return "";
            return new string(hex.Where(c => char.IsLetterOrDigit(c)).ToArray());
        }
    }
}
