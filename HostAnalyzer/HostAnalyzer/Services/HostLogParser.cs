using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HostAnalyzer.Models;

namespace HostAnalyzer.Services
{
    /// <summary>
    /// tcpServerClass SaveLogFile 형식 로그 파싱
    /// - RX: "WCS: (port)hex..."
    /// - TX: "GCP: hex..."
    /// </summary>
    public static class HostLogParser
    {
        private static readonly Regex TimePrefixRegex = new Regex(
            @"^(\d{2}:\d{2}:\d{2}:\d{2,3})\s+",
            RegexOptions.Compiled);

        private static readonly Regex WcsRegex = new Regex(
            @"WCS:\s*\((\d+)\)\s*([0-9A-Fa-f]*)",
            RegexOptions.Compiled);

        private static readonly Regex GcpRegex = new Regex(
            @"GCP:\s*([0-9A-Fa-f]*)",
            RegexOptions.Compiled);

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

            var wcsMatch = WcsRegex.Match(rest);
            if (wcsMatch.Success)
            {
                return new LogEntry
                {
                    Time = time,
                    Type = LogEntryType.RX,
                    PortOrEqId = wcsMatch.Groups[1].Value,
                    DataHex = NormalizeHex(wcsMatch.Groups[2].Value),
                    RawLine = line
                };
            }

            var gcpMatch = GcpRegex.Match(rest);
            if (gcpMatch.Success)
            {
                return new LogEntry
                {
                    Time = time,
                    Type = LogEntryType.TX,
                    PortOrEqId = "",
                    DataHex = NormalizeHex(gcpMatch.Groups[1].Value),
                    RawLine = line
                };
            }

            return null;
        }

        /// <summary>
        /// 로그 항목 목록에서 WCS-RX / GCP-TX 쌍 구성 (WCS 다음에 오는 GCP를 해당 응답으로 매칭)
        /// </summary>
        public static List<TxRxPair> BuildTxRxPairs(List<LogEntry> entries)
        {
            var pairs = new List<TxRxPair>();
            var list = entries ?? new List<LogEntry>();
            int index = 0;
            int pairIndex = 0;

            while (index < list.Count)
            {
                if (list[index].Type != LogEntryType.RX)
                {
                    index++;
                    continue;
                }

                pairIndex++;
                var rx = list[index];
                LogEntry? tx = null;
                int next = index + 1;
                while (next < list.Count)
                {
                    if (list[next].Type == LogEntryType.RX)
                        break;
                    if (list[next].Type == LogEntryType.TX)
                    {
                        tx = list[next];
                        break;
                    }
                    next++;
                }

                pairs.Add(new TxRxPair
                {
                    Rx = rx,
                    Tx = tx,
                    Index = pairIndex
                });
                index++;
            }

            return pairs;
        }

        private static string NormalizeHex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return "";
            return new string(hex.Where(c => char.IsLetterOrDigit(c)).ToArray());
        }
    }
}
