using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using LogAnalyzer.Models;

namespace LogAnalyzer.Services
{
    /// <summary>
    /// udpClientClass SaveLogFile 형식 로그 파싱
    /// - 시간 형식: HH:mm:ss:fff (또는 HH:mm:ss:ff 등)
    /// - TX: "SEND: XXYY / hex..."
    /// - RX: "RECV: CMD(XXYY) / hex..."
    /// </summary>
    public static class LogParser
    {
        // "14:32:01:123 " 또는 "14:32:01:12 " 등 (시간 뒤 공백 1개)
        private static readonly Regex TimePrefixRegex = new Regex(
            @"^(\d{2}:\d{2}:\d{2}:\d{2,3})\s+",
            RegexOptions.Compiled);

        private static readonly Regex SendRegex = new Regex(
            @"SEND:\s*([0-9A-Fa-f]{4})\s*/\s*(.*)",
            RegexOptions.Compiled);

        private static readonly Regex RecvRegex = new Regex(
            @"RECV:\s*CMD\(([0-9A-Fa-f]{4})\)\s*/\s*(.*)",
            RegexOptions.Compiled);

        /// <summary>
        /// 전체 텍스트(파일 내용 또는 붙여넣기)를 줄 단위로 파싱하여 TX/RX 목록 생성
        /// </summary>
        public static List<LogEntry> ParseLines(string text)
        {
            var entries = new List<LogEntry>();
            if (string.IsNullOrWhiteSpace(text)) return entries;

            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                var entry = ParseLine(line.TrimEnd());
                if (entry != null)
                    entries.Add(entry);
            }
            return entries;
        }

        /// <summary>
        /// 한 줄 파싱. TX/RX가 아니면 null 반환(Other는 TX-RX 쌍 분석에 불필요하면 제외 가능)
        /// </summary>
        public static LogEntry? ParseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;

            string time = "";
            string rest = line;

            var timeMatch = TimePrefixRegex.Match(line);
            if (timeMatch.Success)
            {
                time = timeMatch.Groups[1].Value;
                rest = line.Substring(timeMatch.Length).TrimStart();
            }

            var sendMatch = SendRegex.Match(rest);
            if (sendMatch.Success)
            {
                return new LogEntry
                {
                    Time = time,
                    Type = LogEntryType.TX,
                    Cmd = sendMatch.Groups[1].Value.ToUpperInvariant(),
                    DataHex = sendMatch.Groups[2].Value.Trim(),
                    RawLine = line
                };
            }

            var recvMatch = RecvRegex.Match(rest);
            if (recvMatch.Success)
            {
                return new LogEntry
                {
                    Time = time,
                    Type = LogEntryType.RX,
                    Cmd = recvMatch.Groups[1].Value.ToUpperInvariant(),
                    DataHex = recvMatch.Groups[2].Value.Trim(),
                    RawLine = line
                };
            }

            return null;
        }

        /// <summary>
        /// 로그 항목 목록에서 TX-RX 쌍 구성 (TX 다음에 오는 RX를 해당 TX의 응답으로 매칭)
        /// </summary>
        public static List<TxRxPair> BuildTxRxPairs(List<LogEntry> entries)
        {
            var pairs = new List<TxRxPair>();
            var list = entries ?? new List<LogEntry>();
            int index = 0;
            int pairIndex = 0;

            while (index < list.Count)
            {
                if (list[index].Type != LogEntryType.TX)
                {
                    index++;
                    continue;
                }

                pairIndex++;
                var tx = list[index];
                LogEntry? rx = null;
                int next = index + 1;
                while (next < list.Count)
                {
                    if (list[next].Type == LogEntryType.TX)
                        break;
                    if (list[next].Type == LogEntryType.RX)
                    {
                        rx = list[next];
                        break;
                    }
                    next++;
                }

                pairs.Add(new TxRxPair
                {
                    Tx = tx,
                    Rx = rx,
                    Index = pairIndex
                });
                index++;
            }

            return pairs;
        }
    }
}
