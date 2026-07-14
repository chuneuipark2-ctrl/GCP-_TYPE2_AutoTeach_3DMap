using System;
using System.Text;
using HostAnalyzer.Models;

namespace HostAnalyzer.Services
{
    /// <summary>
    /// tcpServerClass Rx_DataParse / Tx_DataParse(WCSTOBUF) 기준으로 WCSFROM/WCSTO Word 배열 상세 해석.
    /// 수신 = WCS → GCP (호스트가 보낸 요청, GCP가 받은 데이터 = WCSFROM)
    /// 송신 = GCP → WCS (GCP가 보낸 응답, 호스트가 받은 데이터 = WCSTO)
    /// </summary>
    public static class HostDataInterpreter
    {
        public static string Interpret(HostParsedPacket p)
        {
            if (p == null || !p.IsValid || p.Words == null || p.Words.Length == 0)
                return "(Data 없음)";

            if (p.IsTx)
                return InterpretTx(p);
            return InterpretRx(p);
        }

        /// <summary>수신: WCS → GCP. 호스트(WCS)가 보낸 데이터 = WCSFROM, Rx_DataParse 매핑 (D7000~)</summary>
        private static string InterpretRx(HostParsedPacket p)
        {
            var sb = new StringBuilder();
            sb.AppendLine("■ 수신 (WCS → GCP) = 호스트가 GCP로 보낸 요청 [WCSFROM]");
            sb.AppendLine($"  EQID(장비ID): {p.EqId}  |  ReqType: {p.ReqType} (0=일반 200워드, 1=보조 100워드)");
            if (p.Words.Length < 21) { sb.AppendLine("  (Word 수 부족, 최소 21워드)"); return sb.ToString().TrimEnd(); }

            // D7000
            sb.AppendLine();
            sb.AppendLine("  [D7000] cmdPriority(우선순위): " + p.Words[0]);

            // Fork1 D7001~D7010
            sb.AppendLine("  ─── Fork1 (D7001~D7010) ───");
            sb.AppendLine("  [D7001] fork1JobNo: " + p.Words[1]);
            AppendJobCmdBits(sb, "D7002", p.Words[2], "fork1");
            sb.AppendLine("  [D7003~D7006] From 위치: St=" + p.Words[3] + " Row=" + p.Words[4] + " Bay=" + p.Words[5] + " Lev=" + p.Words[6]);
            sb.AppendLine("  [D7007~D7010] To   위치: St=" + p.Words[7] + " Row=" + p.Words[8] + " Bay=" + p.Words[9] + " Lev=" + p.Words[10]);

            sb.AppendLine("  ─── Fork2 (D7011~D7020) ───");
            sb.AppendLine("  [D7011] fork2JobNo: " + p.Words[11]);
            AppendJobCmdBits(sb, "D7012", p.Words[12], "fork2");
            sb.AppendLine("  [D7013~D7016] From 위치: St=" + p.Words[13] + " Row=" + p.Words[14] + " Bay=" + p.Words[15] + " Lev=" + p.Words[16]);
            sb.AppendLine("  [D7017~D7020] To   위치: St=" + p.Words[17] + " Row=" + p.Words[18] + " Bay=" + p.Words[19] + " Lev=" + p.Words[20]);

            if (p.Words.Length < 26) return sb.ToString().TrimEnd();

            // D7025 제어비트 (tcpServerClass Rx_DataParse 600~617행)
            ushort w25 = p.Words[25];
            sb.AppendLine("  ─── [D7025] 제어/상태 비트 (WCS→GCP) ───");
            sb.AppendLine("    Bit0  heartBeat=" + ((w25 >> 0) & 1));
            sb.AppendLine("    Bit1  homeReturn(홈복귀요청)=" + ((w25 >> 1) & 1));
            sb.AppendLine("    Bit2  errorReset(이상리셋)=" + ((w25 >> 2) & 1));
            sb.AppendLine("    Bit3  jobDelete(전체작업삭제)=" + ((w25 >> 3) & 1));
            sb.AppendLine("    Bit4  fork1Delete=" + ((w25 >> 4) & 1) + "  Bit5  fork2Delete=" + ((w25 >> 5) & 1));
            sb.AppendLine("    Bit6  timeSynchro=" + ((w25 >> 6) & 1));
            sb.AppendLine("    Bit8  dataReportOK=" + ((w25 >> 8) & 1));
            sb.AppendLine("    Bit10 srmOnline=" + ((w25 >> 10) & 1) + "  Bit11 srmManual=" + ((w25 >> 11) & 1));
            sb.AppendLine("    Bit14 srmCycleStop=" + ((w25 >> 14) & 1) + "  Bit15 srmEmStop=" + ((w25 >> 15) & 1));

            AppendBitWords(sb, p.Words, 26, 4, 7026, "WCS→GCP 추가 제어 영역");

            if (p.Words.Length > 30)
                sb.AppendLine("  [D7030~] 기타: " + WordsToHex(p.Words, 30, Math.Min(20, p.Words.Length - 30)));

            return sb.ToString().TrimEnd();
        }

        private static void AppendJobCmdBits(StringBuilder sb, string reg, ushort w, string fork)
        {
            sb.AppendLine($"  [{reg}] JobCmd Low: 0x{(byte)(w & 0xFF):X2}  |  Move=" + (w & 1) + " Storage=" + ((w >> 1) & 1) + " Retrieval=" + ((w >> 2) & 1) +
                " RackToRack=" + ((w >> 3) & 1) + " StToSt=" + ((w >> 4) & 1) + " ChangeRack=" + ((w >> 5) & 1) + " ChangeSt=" + ((w >> 6) & 1) + " Sticky=" + ((w >> 7) & 1));
        }

        /// <summary>송신: GCP → WCS. GCP가 보낸 데이터 = WCSTO, Tx_DataParse(WCSTOBUF) D75xx/D76xx</summary>
        private static string InterpretTx(HostParsedPacket p)
        {
            var sb = new StringBuilder();
            sb.AppendLine("■ 송신 (GCP → WCS) = GCP가 호스트로 보낸 응답 [WCSTO]");
            sb.AppendLine($"  EQID(장비ID): {p.EqId}  |  ReqType: 0x{p.ReqType:X2} (0x80=일반, 0x81=보조)");
            if (p.Words.Length < 11) { sb.AppendLine("  (Word 수 부족)"); return sb.ToString().TrimEnd(); }

            // D7500 (WCSTOBUF[0])
            ushort w0 = p.Words[0];
            sb.AppendLine();
            sb.AppendLine("  ─── [D7500] 상태 워드 ───");
            sb.AppendLine("    Bit0  jobRequest(SRM작업요구)=" + (w0 & 1));
            sb.AppendLine("    Bit1  fork1작업유무=" + ((w0 >> 1) & 1) + "  Bit2  fork2작업유무=" + ((w0 >> 2) & 1));
            sb.AppendLine("    Bit3  operState(크레인 운전중)=" + ((w0 >> 3) & 1));
            sb.AppendLine("    Bit4  fork1화물유무=" + ((w0 >> 4) & 1) + "  Bit5  fork2화물유무=" + ((w0 >> 5) & 1));
            sb.AppendLine("    Bit6  fork1JobComplete=" + ((w0 >> 6) & 1) + "  Bit7  fork2JobComplete=" + ((w0 >> 7) & 1));
            sb.AppendLine("    Bit10 gcpError=" + ((w0 >> 10) & 1) + "  Bit11 recovError=" + ((w0 >> 11) & 1));
            sb.AppendLine("    Bit12 dSt2homePos(홈위치)=" + ((w0 >> 12) & 1));

            sb.AppendLine("  [D7501] gcpErrorCode(메인에러): " + p.Words[1]);
            sb.AppendLine("  [D7502] gcpSubCode(서브에러): " + p.Words[2]);
            sb.AppendLine("  [D7503~D7506] Fork1 현재위치: St=" + p.Words[3] + " Row=" + p.Words[4] + " Bay=" + p.Words[5] + " Lev=" + p.Words[6]);
            sb.AppendLine("  [D7507] Spare");
            AppendD7508(sb, p.Words);
            sb.AppendLine("  [D7509] 주행동작중=" + (p.Words[9] & 1) + " 승강동작중=" + ((p.Words[9] >> 1) & 1) + " fork1동작중=" + ((p.Words[9] >> 2) & 1) + " fork2동작중=" + ((p.Words[9] >> 3) & 1));
            sb.AppendLine("  [D7510] resMainCode(응답코드): " + p.Words[10]);

            if (p.Words.Length > 15)
            {
                sb.AppendLine("  [D7511~D7514] Spare");
                sb.AppendLine("  [D7515] 원점/정위치: CraneHP=" + (p.Words[15] & 0xF) + " TravOrigin=" + ((p.Words[15] >> 4) & 1) + " LiftOrigin=" + ((p.Words[15] >> 5) & 1) + " Fk1Origin=" + ((p.Words[15] >> 6) & 1) + " Fk2Origin=" + ((p.Words[15] >> 7) & 1));
            }
            if (p.Words.Length > 27)
            {
                sb.AppendLine("  [D7520~D7521] Trav curPos: " + (((long)p.Words[20] << 16) | p.Words[21]));
                sb.AppendLine("  [D7522~D7523] Lift curPos: " + (((long)p.Words[22] << 16) | p.Words[23]));
                sb.AppendLine("  [D7524~D7525] Fork1 curPos: " + (((long)p.Words[24] << 16) | p.Words[25]));
                sb.AppendLine("  [D7526~D7527] Fork2 curPos: " + (((long)p.Words[26] << 16) | p.Words[27]));
            }
            if (p.Words.Length > 37)
            {
                sb.AppendLine("  [D7530~D7531] Trav targetPos  [D7532~D7533] Lift targetPos");
                sb.AppendLine("  [D7534~D7535] Fork1 targetPos  [D7536~D7537] Fork2 targetPos");
            }
            if (p.Words.Length > 47)
            {
                sb.AppendLine("  [D7540~D7541] Trav curSpd  [D7542~D7543] Lift curSpd");
                sb.AppendLine("  [D7544~D7545] Fork1 curSpd  [D7546~D7547] Fork2 curSpd");
            }

            // D76xx (작업데이터 영역, word 100~)
            if (p.Words.Length > 125)
            {
                sb.AppendLine("  ─── [D7600~] 작업/에코 영역 ───");
                sb.AppendLine("  [D7600] cmdPriority: " + p.Words[100]);
                sb.AppendLine("  [D7601] reqJobNoFk1: " + p.Words[101] + "  [D7602] reqWcsCodeFk1: " + p.Words[102]);
                sb.AppendLine("  [D7603~D7610] Fk1 From/To: FromSt=" + p.Words[103] + " Row=" + p.Words[104] + " Bay=" + p.Words[105] + " Lev=" + p.Words[106] + " → ToSt=" + p.Words[107] + " Row=" + p.Words[108] + " Bay=" + p.Words[109] + " Lev=" + p.Words[110]);
                sb.AppendLine("  [D7611] reqJobNoFk2: " + p.Words[111] + "  [D7612] reqWcsCodeFk2: " + p.Words[112]);
                sb.AppendLine("  [D7613~D7620] Fk2 From/To: FromSt=" + p.Words[113] + " Row=" + p.Words[114] + " Bay=" + p.Words[115] + " Lev=" + p.Words[116] + " → ToSt=" + p.Words[117] + " Row=" + p.Words[118] + " Bay=" + p.Words[119] + " Lev=" + p.Words[120]);
                ushort w125 = p.Words[125];
                sb.AppendLine("  [D7625] Bit0 heartBeat Bit1 wcsAckHomeReturn Bit2 wcsAckReset Bit3 wcsAckDeleteAll Bit4 wcsAckDeleteFk1 Bit5 wcsAckDeleteFk2");
                sb.AppendLine("          Bit8 dataReportOK Bit9 startEnable Bit10/11/12 자동/수동/반자동 Bit14 wcsAckCycleStop Bit15 wcsAckEmStop");
                sb.AppendLine("          값: 0x" + w125.ToString("X4"));
                AppendBitWords(sb, p.Words, 126, 4, 7626, "GCP→WCS 추가 제어 영역");
                AppendWordBitLine(sb, p.Words, 130, 7630, "Fork 완료/삭제 요청");
            }

            return sb.ToString().TrimEnd();
        }

        private static void AppendD7508(StringBuilder sb, ushort[] words)
        {
            if (words.Length < 9) return;
            ushort w8 = words[8];
            sb.AppendLine("  [D7508] Fk1: 좌정위=" + (w8 & 1) + " 좌이동중=" + ((w8 >> 1) & 1) + " 센터정위=" + ((w8 >> 2) & 1) + " 센터이동중=" + ((w8 >> 3) & 1) + " 우정위=" + ((w8 >> 4) & 1) + " 우이동중=" + ((w8 >> 5) & 1));
            sb.AppendLine("         Fk2: 좌정위=" + ((w8 >> 8) & 1) + " 좌이동중=" + ((w8 >> 9) & 1) + " 센터정위=" + ((w8 >> 10) & 1) + " 센터이동중=" + ((w8 >> 11) & 1) + " 우정위=" + ((w8 >> 12) & 1) + " 우이동중=" + ((w8 >> 13) & 1));
        }

        private static string WordsToHex(ushort[] words, int start, int count)
        {
            if (words == null || start < 0 || count <= 0 || start + count > words.Length)
                return "";
            var sb = new StringBuilder();
            for (int i = 0; i < count; i++)
                sb.Append(words[start + i].ToString("X4")).Append(" ");
            return sb.ToString().TrimEnd();
        }

        private static void AppendBitWords(StringBuilder sb, ushort[] words, int startIndex, int wordCount, int baseD, string title)
        {
            if (words == null || startIndex < 0 || wordCount <= 0 || startIndex >= words.Length)
                return;

            int endIndex = Math.Min(startIndex + wordCount, words.Length);
            sb.AppendLine($"  ─── [D{baseD}~D{baseD + (endIndex - startIndex - 1)}] {title} (Word별 Bit값) ───");
            for (int i = startIndex; i < endIndex; i++)
                AppendWordBitLine(sb, words, i, baseD + (i - startIndex), "추가 워드");
        }

        private static void AppendWordBitLine(StringBuilder sb, ushort[] words, int index, int dNo, string label)
        {
            if (words == null || index < 0 || index >= words.Length)
                return;

            ushort w = words[index];
            sb.AppendLine($"  [D{dNo}] {label} = 0x{w:X4}");
            sb.AppendLine("         Bit15~8: " +
                ((w >> 15) & 1) + " " + ((w >> 14) & 1) + " " + ((w >> 13) & 1) + " " + ((w >> 12) & 1) + " " +
                ((w >> 11) & 1) + " " + ((w >> 10) & 1) + " " + ((w >> 9) & 1) + " " + ((w >> 8) & 1));
            sb.AppendLine("         Bit7~0 : " +
                ((w >> 7) & 1) + " " + ((w >> 6) & 1) + " " + ((w >> 5) & 1) + " " + ((w >> 4) & 1) + " " +
                ((w >> 3) & 1) + " " + ((w >> 2) & 1) + " " + ((w >> 1) & 1) + " " + ((w >> 0) & 1));
        }
    }
}
