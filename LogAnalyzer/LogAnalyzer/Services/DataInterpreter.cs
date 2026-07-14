using System;
using System.Text;

namespace LogAnalyzer.Services
{
    /// <summary>
    /// udpClientClass Rx_DataParse / CMD_Req_* 참조. cmd2별 Data 영역을 필드 단위로 해석하여 친절한 문자열 반환.
    /// </summary>
    public static class DataInterpreter
    {
        public static string Interpret(byte cmd2, byte[] data, bool isTx)
        {
            if (data == null || data.Length == 0)
                return "(Data 없음)";

            if (isTx)
                return InterpretTx(cmd2, data);
            return InterpretRx(cmd2, data);
        }

        private static string InterpretTx(byte cmd2, byte[] d)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[TX CMD2=0x{cmd2:X2}]");
            switch (cmd2)
            {
                case 0x30: // 상태조회 요구
                    if (d.Length >= 11) sb.AppendLine($"  지상반유효: 0x{d[0]:X2} (0x01=유효)");
                    if (d.Length >= 10) sb.AppendLine($"  UTC Time(4): {BytesToHex(d, 6, 4)} (지상반 PC 시간)");
                    if (d.Length >= 12) sb.AppendLine($"  지상반모드: {d[10]} (1:수동,2:반자동,3:자동)");
                    if (d.Length >= 12) sb.AppendLine($"  지상반상태: 0x{d[11]:X2} (HeartBeat, Reset, EM_SW, SF_PLUG)");
                    if (d.Length > 12) sb.AppendLine($"  기타: {BytesToHex(d, 12, Math.Min(21, d.Length - 12))}");
                    break;
                case 0x41: // 반송지령 (CMD_Req_Operation)
                    Interpret_0x41_Tx(sb, d);
                    break;
                case 0x25: // 장치구조 조회 0x0125
                    sb.AppendLine("  (요청 바디 없음 또는 Reserved)");
                    break;
                case 0x92: sb.AppendLine("  (렉 기본설정 요청)"); break;
                case 0x94: sb.AppendLine("  (셀설정 조회 요청)"); break;
                case 0x98: sb.AppendLine("  (스테이션 조회 요청)"); break;
                case 0x9C: sb.AppendLine("  (금지렉 조회 요청)"); break;
                case 0xA7: sb.AppendLine("  (포크데이터 조회 요청)"); break;
                case 0x80: sb.AppendLine("  수동명령: " + (d.Length > 0 ? BytesToHex(d, 0, d.Length) : "-")); break;
                default:
                    sb.AppendLine("  " + BytesToHex(d, 0, d.Length));
                    break;
            }
            return sb.ToString().TrimEnd();
        }

        private static string InterpretRx(byte cmd2, byte[] d)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[RX CMD2=0x{cmd2:X2}]");
            switch (cmd2)
            {
                case 0x30: Interpret_0x30_State(sb, d); break;
                case 0x25: Interpret_0x25_SrmStruct(sb, d); break;
                case 0x41: Interpret_0x41_Data(sb, d); break;
                case 0x80: Interpret_0x80_Manual(sb, d); break;
                case 0x92: Interpret_0x92_Rack(sb, d); break;
                case 0x94: Interpret_0x94_Cell(sb, d); break;
                case 0x98: Interpret_0x98_Station(sb, d); break;
                case 0x9C: Interpret_0x9C_Proh(sb, d); break;
                case 0xA7: Interpret_0xA7_Fork(sb, d); break;
                case 0x50:
                case 0x84:
                case 0x85:
                    if (d.Length > 0) sb.AppendLine($"  Result: {d[0]} (0=정상)");
                    break;
                default:
                    sb.AppendLine("  " + BytesToHex(d, 0, Math.Min(64, d.Length)) + (d.Length > 64 ? "..." : ""));
                    break;
            }
            return sb.ToString().TrimEnd();
        }

        private static void Interpret_0x30_State(StringBuilder sb, byte[] d)
        {
            if (d.Length < 24) { sb.AppendLine("  (길이 부족)"); return; }
            sb.AppendLine("  ─── 공통 ───");
            sb.AppendLine($"  ProtocolVer: {(d.Length > 2 ? $"Ver{(d[2] >> 4)}.{(d[2] & 0x0F)}" : "-")}");
            sb.AppendLine($"  FirmwareVer: {(d.Length > 6 ? $"Ver{d[3]:X2}[0].{d[3]:X2}[1]-{d[4]:X2}{d[5]:X2}{d[6]:X2}" : "-")}");
            if (d.Length >= 11) sb.AppendLine($"  UTC Time: {BitConverter.ToUInt32(d, 7)}");
            if (d.Length >= 17) sb.AppendLine($"  ProjNo: {System.Text.Encoding.ASCII.GetString(d, 11, 6)}");
            sb.AppendLine($"  GroupNo: {(d.Length > 17 ? d[17].ToString() : "-")}");
            if (d.Length >= 22) sb.AppendLine($"  SrmNo: {BitConverter.ToUInt16(d, 18)}, SrmType: {BitConverter.ToUInt16(d, 20)}");
            sb.AppendLine($"  GcpRxMode: {(d.Length > 22 ? d[22].ToString() : "-")}");
            if (d.Length > 23)
            {
                byte b23 = d[23];
                sb.AppendLine($"  [Byte23] HeartBeat:{(b23 >> 3) & 1}, SafetyPlug:{(b23 & 1)}, FaultReset:{(b23 & 4) != 0}, EmStop:{(b23 & 2) != 0}");
            }
            if (d.Length >= 48) sb.AppendLine($"  CVOK[0..7]: {BytesToHex(d, 24, 8)} / CVNO[0..7]: {BytesToHex(d, 32, 8)}");
            if (d.Length > 43)
            {
                sb.AppendLine("  ─── 디바이스 ───");
                byte b40 = d[40];
                sb.AppendLine($"  DevMode: Setup={(b40 >> 3) & 1}, Forced={(b40 >> 2) & 1}, Manual={(b40 >> 1) & 1}, Auto={b40 & 1}");
                sb.AppendLine($"  DevState1(41): 0x{d[41]:X2} (ReqCmd, InvConn, Abnormal, Warning, EmStop, StartSt)");
                sb.AppendLine($"  DevState2(42): 0x{d[42]:X2} (EmSwitch, ManAutoSw, maintPos, homePos)");
                sb.AppendLine($"  OperCode: {d[43]}, ErrCode: H={d[44]} M={d[45]} L={BitConverter.ToUInt16(d, 46)}");
            }
            if (d.Length >= 55)
            {
                sb.AppendLine("  ─── Fork1 위치 ───");
                sb.AppendLine($"  curStation={d[48]}, curBay={BitConverter.ToUInt16(d, 49)}, curLev={d[51]}, curPosNum={d[52]}");
                sb.AppendLine($"  curPos1(53): 0x{d[53]:X2}, curPos2(54): 0x{d[54]:X2}");
            }
            if (d.Length >= 66)
            {
                sb.AppendLine("  ─── Fork2 위치 ───");
                sb.AppendLine($"  curStation={d[57]}, curBay={BitConverter.ToUInt16(d, 58)}, curLev={d[60]}, curPosNum={d[61]}");
                sb.AppendLine($"  curPos1(62): 0x{d[62]:X2}, curPos2(63): 0x{d[63]:X2}");
            }
            if (d.Length >= 78)
            {
                sb.AppendLine("  ─── 목표 Fork1 ───");
                sb.AppendLine($"  targetStation={d[66]}, targetRow={d[67]}, targetBay={BitConverter.ToUInt16(d, 68)}, targetLev={d[70]}");
                sb.AppendLine("  ─── 목표 Fork2 ───");
                sb.AppendLine($"  targetStation={d[73]}, targetRow={d[74]}, targetBay={BitConverter.ToUInt16(d, 75)}, targetLev={d[77]}");
            }
            if (d.Length >= 107)
            {
                sb.AppendLine("  ─── 주행(Trav) ───");
                sb.AppendLine($"  state1(91): 0x{d[91]:X2}, state2(92): 0x{d[92]:X2}");
                sb.AppendLine($"  fwDecNo={d[93]}, bwDecNo={d[94]}, curPos={BitConverter.ToInt32(d, 95)}, curSpd={BitConverter.ToInt16(d, 99)}");
                sb.AppendLine($"  targetPos={BitConverter.ToInt32(d, 101)}, targetSpd={BitConverter.ToInt16(d, 105)}");
                sb.AppendLine("  ─── 승강(Lift) ───");
                sb.AppendLine($"  state1(107): 0x{d[107]:X2}, state2(108): 0x{d[108]:X2}");
                sb.AppendLine($"  upDecNo={d[109]}, dnDecNo={d[110]}, curPos={BitConverter.ToInt32(d, 111)}, curSpd={BitConverter.ToInt16(d, 115)}");
                sb.AppendLine($"  targetPos={BitConverter.ToInt32(d, 117)}, targetSpd={BitConverter.ToInt16(d, 121)}");
            }
            if (d.Length >= 126)
            {
                sb.AppendLine("  ─── 포크1 상태 ───");
                sb.AppendLine($"  state1(123): 0x{d[123]:X2}, state2(124): 0x{d[124]:X2}, loadType={d[125]}");
                sb.AppendLine($"  curPos={BitConverter.ToInt32(d, 127)}, curSpd={BitConverter.ToInt16(d, 131)}, targetPos={BitConverter.ToInt32(d, 133)}, targetSpd={BitConverter.ToInt16(d, 137)}");
            }
            if (d.Length >= 155)
            {
                sb.AppendLine("  ─── 포크2 상태 ───");
                sb.AppendLine($"  state1(139): 0x{d[139]:X2}, state2(140): 0x{d[140]:X2}, loadType={d[141]}");
                sb.AppendLine($"  curPos={BitConverter.ToInt32(d, 143)}, targetPos={BitConverter.ToInt32(d, 149)}");
                sb.AppendLine("  ─── 작업정보 Fork1 ───");
                sb.AppendLine($"  jobNo={BitConverter.ToUInt32(d, 155)}, taskIdx={d[159]}, fromStn={d[160]},{d[161]},{BitConverter.ToUInt16(d, 162)},{d[164]} → toStn={d[165]},{d[166]},{BitConverter.ToUInt16(d, 167)},{d[169]}");
            }
            if (d.Length > 175)
                sb.AppendLine("  ─── Fork2 작업정보 및 기타 ─── (생략)");
            AppendIoByName(sb, d);
        }

        /// <summary>RX 0x30 상태 패킷의 dInput/dOutput을 이름별로 파싱 (udpClientClass IoNameMap 참조)</summary>
        private static void AppendIoByName(StringBuilder sb, byte[] d)
        {
            int diOff = IoNameMap.DInputOffset;
            int doOff = IoNameMap.DOutputOffset;
            if (d.Length < diOff + IoNameMap.DInputBytes) return;

            sb.AppendLine("  ─── DI (dInput) 이름별 ───");
            for (int bi = 0; bi < IoNameMap.DInputBytes && (diOff + bi) < d.Length; bi++)
            {
                byte b = d[diOff + bi];
                for (int bit = 0; bit < 8; bit++)
                {
                    string name = bi < IoNameMap.DiNames.Length && bit < IoNameMap.DiNames[bi].Length
                        ? IoNameMap.DiNames[bi][bit]
                        : "";
                    if (string.IsNullOrEmpty(name)) continue;
                    bool on = (b & (1 << bit)) != 0;
                    sb.AppendLine($"    {name}: {(on ? "ON" : "OFF")}");
                }
            }
            if (d.Length < doOff + IoNameMap.DOutputBytes) return;
            sb.AppendLine("  ─── DO (dOutput) 이름별 ───");
            for (int bi = 0; bi < IoNameMap.DOutputBytes && (doOff + bi) < d.Length; bi++)
            {
                byte b = d[doOff + bi];
                for (int bit = 0; bit < 8; bit++)
                {
                    string name = bi < IoNameMap.DoNames.Length && bit < IoNameMap.DoNames[bi].Length
                        ? IoNameMap.DoNames[bi][bit]
                        : "";
                    if (string.IsNullOrEmpty(name)) continue;
                    bool on = (b & (1 << bit)) != 0;
                    sb.AppendLine($"    {name}: {(on ? "ON" : "OFF")}");
                }
            }
        }

        private static void Interpret_0x25_SrmStruct(StringBuilder sb, byte[] d)
        {
            if (d.Length < 4) return;
            sb.AppendLine($"  forkCnt: {d[0]} (1:싱글, 2:트윈)");
            sb.AppendLine($"  forkType: {d[3]} (1:싱글딥, 2:더블딥2POS, 3:더블딥3POS, 4:2POS베리언트, 5:3POS베리언트)");
            sb.AppendLine($"  row: 2/4/6 (forkType에 따라)");
        }

        private static void Interpret_0x41_Tx(StringBuilder sb, byte[] d)
        {
            if (d.Length < 6) return;
            sb.AppendLine($"  작업코드(reqJobCodeFk1): 0x{d[0]:X2} (0x01=이동 등)");
            sb.AppendLine($"  옵션(1): 0x{d[1]:X2} (IgnoreCV Bit1, IgnoreGoods Bit0)");
            sb.AppendLine($"  Reserved(2~5): {BytesToHex(d, 2, 4)}");
            if (d.Length >= 10)
            {
                sb.AppendLine("  ─── Fork1 ───");
                sb.AppendLine($"  JobNo: {BitConverter.ToUInt32(d, 6)}");
                sb.AppendLine($"  From: St={d[10]} Row={d[11]} Bay={BitConverter.ToUInt16(d, 12)} Lev={d[14]}");
                sb.AppendLine($"  To:   St={d[15]} Row={d[16]} Bay={BitConverter.ToUInt16(d, 17)} Lev={d[19]}");
                sb.AppendLine($"  GoodsType: {d[20]}");
            }
            if (d.Length >= 41)
            {
                sb.AppendLine("  ─── Fork2 ───");
                sb.AppendLine($"  JobNo: {BitConverter.ToUInt32(d, 26)}");
                sb.AppendLine($"  From: St={d[30]} Row={d[31]} Bay={BitConverter.ToUInt16(d, 32)} Lev={d[34]}");
                sb.AppendLine($"  To:   St={d[35]} Row={d[36]} Bay={BitConverter.ToUInt16(d, 37)} Lev={d[39]}");
                sb.AppendLine($"  GoodsType: {d[40]}");
            }
        }

        private static void Interpret_0x41_Data(StringBuilder sb, byte[] d)
        {
            if (d.Length < 10) return;
            uint job1 = BitConverter.ToUInt32(d, 1);
            uint job2 = BitConverter.ToUInt32(d, 5);
            sb.AppendLine($"  Result(작업코드): {d[0]}");
            sb.AppendLine($"  Fork1 JobNo: {job1}");
            sb.AppendLine($"  Fork2 JobNo: {job2}");
            sb.AppendLine($"  ResultCode: {d[9]} (0=성공, 비0=SRM 실패응답)");
        }

        private static void Interpret_0x80_Manual(StringBuilder sb, byte[] d)
        {
            if (d.Length > 0) sb.AppendLine($"  Result: {d[0]} (0=성공)");
        }

        private static void Interpret_0x92_Rack(StringBuilder sb, byte[] d)
        {
            if (d.Length < 17) return;
            sb.AppendLine($"  RACK Type: {d[0]} (1:SRM, 2:RTV)");
            sb.AppendLine($"  Bay Count: {BitConverter.ToUInt16(d, 1)} (1~256)");
            sb.AppendLine($"  Lev Count: {BitConverter.ToUInt16(d, 3)} (1~128)");
            sb.AppendLine($"  row: {d[5]} (2:싱글딥, 4:더블딥, 6:더블딥3POS)");
            sb.AppendLine($"  offsetUp: {d[15]}, offsetDown: {d[16]}");
        }

        private static void Interpret_0x94_Cell(StringBuilder sb, byte[] d)
        {
            if (d.Length < 8) return;
            sb.AppendLine($"  RACK Type: {d[0]} (1:SRM, 2:RTV)");
            sb.AppendLine($"  Bay: {BitConverter.ToUInt16(d, 1)}, Lev: {BitConverter.ToUInt16(d, 3)}");
            sb.AppendLine($"  DataType: {d[5]} (1:BAY, 2:LEV), Start: {d[6]}, End: {d[7]}");
            if (d.Length > 8) sb.AppendLine($"  Cell Data: {BytesToHex(d, 8, Math.Min(32, d.Length - 8))}" + (d.Length > 40 ? "..." : ""));
        }

        private static void Interpret_0x98_Station(StringBuilder sb, byte[] d)
        {
            if (d.Length < 1) return;
            int stn = d[0];
            sb.AppendLine($"  Station Count: {stn}");
            for (int i = 0; i < Math.Min(stn, 5); i++)
            {
                int baseIdx = 18 + i * 20;
                if (d.Length < baseIdx + 16) break;
                sb.AppendLine($"  [{i}] stnType={d[baseIdx]}, goodType={d[baseIdx + 1]}, travPos={BitConverter.ToInt32(d, baseIdx + 2)}, liftPos={BitConverter.ToInt32(d, baseIdx + 6)}, forkPos={BitConverter.ToInt16(d, baseIdx + 10)}, upOff={d[baseIdx + 14]}, dnOff={d[baseIdx + 15]}");
            }
            if (stn > 5) sb.AppendLine($"  ... 외 {stn - 5}개");
        }

        private static void Interpret_0x9C_Proh(StringBuilder sb, byte[] d)
        {
            if (d.Length < 1) return;
            int cnt = d[0];
            sb.AppendLine($"  금지렉 개수: {cnt}");
            for (int i = 0; i < Math.Min(cnt, 8); i++)
            {
                int baseIdx = 2 + i * 5;
                if (d.Length < baseIdx + 5) break;
                sb.AppendLine($"  [{i}] {d[baseIdx]}-{BitConverter.ToInt16(d, baseIdx + 1)}-{d[baseIdx + 4]}");
            }
            if (cnt > 8) sb.AppendLine($"  ... 외 {cnt - 8}개");
        }

        private static void Interpret_0xA7_Fork(StringBuilder sb, byte[] d)
        {
            if (d.Length < 140) return;
            sb.AppendLine($"  forkLeftLimit: {BitConverter.ToInt32(d, 132)}");
            sb.AppendLine($"  forkRightLimit: {BitConverter.ToInt32(d, 136)}");
            if (d.Length > 8) sb.AppendLine($"  기타: {BytesToHex(d, 0, Math.Min(16, d.Length))} ...");
        }

        private static string BytesToHex(byte[] d, int start, int count)
        {
            if (d == null || start < 0 || count <= 0 || start + count > d.Length)
                return "";
            var sb = new StringBuilder();
            for (int i = 0; i < count; i++)
                sb.Append(d[start + i].ToString("X2")).Append(" ");
            return sb.ToString().TrimEnd();
        }
    }
}
