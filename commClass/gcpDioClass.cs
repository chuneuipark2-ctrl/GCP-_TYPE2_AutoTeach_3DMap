using FASTECH;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace gcp_Wpf.commClass;

internal class gcpDioClass
{
    private Thread _dioThread;
    private int srmNum;
    private string dioIP;
    private int dioID;
    private bool _isRunning;
    public bool _isConnected;
    private static Mutex mutex = new Mutex();
    private string pathString;
    private singletonClass gClass;
    public bool[] DIBUF;
    public bool[] DOBUF;
    public bool[] DOCMDBUF;

    public gcpDioClass(int srmNum, string dioIP, int dioID)
    {
        gClass = singletonClass.Instance;
        this.srmNum = srmNum;
        _isConnected = false;
        this.dioIP = dioIP;
        this.dioID = dioID;
        DIBUF = new bool[8];
        DOBUF = new bool[8];
        DOCMDBUF = new bool[8];
        pathString = Path.Combine(Environment.CurrentDirectory, "SRM" + srmNum.ToString(), "LOG", "DIO");
    }

    public void StartDioClient()
    {
        IPAddress ipaddr = IPAddress.Parse(dioIP);
        if (gClass.str.SrmInfo[srmNum].dioUse == 1)
        {

            try
            {
                Console.WriteLine("Connecting FASTECH DIO");
                EziMOTIONPlusELib.FAS_Close(0);

                if (EziMOTIONPlusELib.FAS_Connect(ipaddr, dioID))
                {
                    Console.WriteLine("Connected FASTECH");
                    SaveLogFile("Connected DIO");
                    _isConnected = true;
                    _dioThread = new Thread(new ThreadStart(dioThreadFunction));
                    _dioThread.Start();
                    if (_dioThread == null || !_dioThread.IsAlive)
                        return;
                    SaveLogFile("DIO Thread is Running");
                    _isRunning = true;
                }
                else
                {
                    _isConnected = false;
                    Console.WriteLine("Connection Failed FASTECH");
                    SaveLogFile("Connection Failed FASTECH");
                }
            }
            catch (Exception ex)
            {
                _isConnected = false;
                _isRunning = false;
                Console.WriteLine("DIO Connect Error: " + ex.Message);
                SaveLogFile("DIO Connect Error: " + ex.Message);
            }
        }
        else
            Console.WriteLine("Not Used DIO - SRM" + srmNum.ToString());
    }

    private void dioThreadFunction()
    {
        bool dioParse = false;

        // Check Main Process
        int watchDogCnt = 0;
        int watchCnt = 3;

        bool sPlugTmp = true;
        int sPlugCnt = 0;

        int commFltCnt = 0;

        uint buf1 = 0;
        uint buf2 = 0;
        int boolToInt = 0;

        // Log 카운트
        int logCnt = 0;


        // Stopwatch 객체 생성
        Stopwatch stopwatch = new Stopwatch();

        while (_isRunning)
        {
            if (watchCnt < 0)
            {
                if (watchDogCnt <= cIniAccess.watchDogCnt)
                {
                    if (cIniAccess.watchDogCnt > 1)
                    {
                        watchDogCnt = cIniAccess.watchDogCnt;       // 메인타이머 시작 확인 후
                    }
                    watchCnt = 3;
                }
                else
                {
                    Console.WriteLine("DIO Thread Exit - " + watchDogCnt + " " + cIniAccess.watchDogCnt);
                    _isRunning = false;
                    gClass.str.SrmInfo[srmNum].dioAliveCnt = -99;
                    continue;
                }
            }
            else
            {
                watchCnt -= 1;
            }

            try
            {
                gClass.str.DioPacket[srmNum].DOSET[(int)DOSTATE.MODEM_PW].value = gClass.str.SrmInfo[srmNum].sfUse == 0 || gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.SF_PLUG].value;
                gClass.str.DioPacket[srmNum].DOCOMMAND = 0;
                for (int index = 0; index < Enum.GetValues(typeof(DOSTATE)).Length; ++index)
                {
                    if (gClass.str.DioPacket[srmNum].DOSET[index].value)
                        gClass.str.DioPacket[srmNum].DOCOMMAND |= gClass.str.DioPacket[srmNum].DOSET[index].pin;
                }
                stopwatch.Start();
                int input = EziMOTIONPlusELib.FAS_GetInput(0, ref buf1, ref buf2);
                if (input != 0)
                {
                    ParseReturnState(input);
                    if (input == EziMOTIONPlusELib.FMC_DISCONNECTED)
                    {
                    }
                    _isRunning = false;
                    continue;
                }
                else
                {
                    gClass.str.DioPacket[srmNum].DISTATUS = buf1;
                    for (int index = 0; index < 8; ++index)
                        gClass.str.DioPacket[srmNum].DIBIT[index] = (buf1 >> index & 1U) > 0U;
                    gClass.str.SrmPacket[srmNum].rxDioComm = true;
                }
                buf1 = 0;
                buf2 = 0;
                for (int index = 0; index < 8; ++index)
                {
                    boolToInt = 1;
                    if (gClass.str.DioPacket[srmNum].DOCMD[index])
                        buf1 |= (uint)(boolToInt << index + 16 /*0x10*/);
                    else
                        buf2 |= (uint)(boolToInt << index + 16 /*0x10*/);
                }
                EziMOTIONPlusELib.FAS_SetOutput(0, gClass.str.DioPacket[srmNum].DOCOMMAND << 16 /*0x10*/, ~gClass.str.DioPacket[srmNum].DOCOMMAND << 16 /*0x10*/);
                if (input != 0)
                {
                    ParseReturnState(input);
                    if (input == EziMOTIONPlusELib.FMC_DISCONNECTED)
                    {
                    }
                        _isRunning = false;
                        continue;
                }
                else
                    gClass.str.SrmPacket[srmNum].txDioComm = true;
                buf1 = 0U;
                buf2 = 0U;
                EziMOTIONPlusELib.FAS_GetOutput(0, ref buf1, ref buf2);
                gClass.str.DioPacket[srmNum].DOSTATUS = buf1 >> 16 /*0x10*/;
                if (input != EziMOTIONPlusELib.FMM_OK)
                {
                    ParseReturnState(input);
                    if (input == EziMOTIONPlusELib.FMC_DISCONNECTED)
                    {
                    }
                        _isRunning = false;
                        continue;
                }
                else
                {
                    for (int index = 0; index < 8; ++index)
                        gClass.str.DioPacket[srmNum].DOBIT[index] = (buf1 >> index + 16 /*0x10*/ & 1U) > 0U;
                }
                stopwatch.Stop();
                stopwatch.Reset();
                Thread.Sleep(300);

                dioParse = true;
            }
            catch (Exception ex)
            {
                dioParse = false;
                SaveLogFile($"DIO Read Exception : {ex.Message} {srmNum.ToString()}");
                Console.WriteLine($"DIO error occurred: {ex.Message} {srmNum.ToString()}");
                gClass.str.SrmPacket[srmNum].txDioComm = false;
                gClass.str.SrmPacket[srmNum].rxDioComm = false;
                ++commFltCnt;
            }
            finally
            {
                // For Test Mode
                //dioParse = true;

                if (dioParse)
                {

                    gClass.str.SrmPacket[srmNum].dioCommDiscCnt = 2;
                    if (gClass.str.SrmInfo[srmNum].dioUse == 0)
                    {
                        gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.EM_SW].value = true;
                        gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.SF_PLUG].value = true;
                        gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.MODEM_EN].value = true;
                        gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.REQ_STOP].value = true;
                    }
                    else if (!gClass.str.DioPacket[srmNum].DO_TESTMODE)
                    {
                        for (int index = 0; index < Enum.GetValues(typeof(DISTATE)).Length; ++index)
                        {
                            if (gClass.str.DioPacket[srmNum].DISET[index].pin == 0)
                            {
                                if (index == (int)DISTATE.SF_PLUG)
                                    sPlugTmp = true;
                                else if (index == (int)DISTATE.SEMI_AUTO)
                                    continue;
                                else
                                    gClass.str.DioPacket[srmNum].DISET[index].value = false;
                            }
                            else
                            {
                                uint distatus = gClass.str.DioPacket[srmNum].DISTATUS;
                                uint pin = gClass.str.DioPacket[srmNum].DISET[index].pin;
                                bool isOn = gClass.str.DioPacket[srmNum].DISET[index].mask ? ((int)distatus & (int)pin) == 0 : ((int)distatus & (int)pin) == (int)pin;

                                if (index == (int)DISTATE.SF_PLUG)
                                {
                                    sPlugTmp = isOn;
                                }
                                else
                                {
                                    gClass.str.DioPacket[srmNum].DISET[index].value = isOn;
                                    if (index == (int)DISTATE.MAINT || index == (int)DISTATE.AUTO)
                                    {
                                        if (isOn && gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.SEMI_AUTO].pin == 0)    // SEMI 키 없는데 MAINT나 AUTO 켜진경우
                                        {
                                            // 키 상태가 바뀌었기 때문에 SEMI MODE 강제 해제
                                            gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.SEMI_AUTO].value = false;
                                        }
                                    }
                                }
                            }
                        }
                        if (gClass.str.SrmInfo[srmNum].sfUse == 0)
                            gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.SF_PLUG].value = true;
                        else if (!sPlugTmp)
                        {
                            ++sPlugCnt;
                            if (sPlugCnt > 3)
                            {
                                gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.SF_PLUG].value = false;
                                sPlugCnt = 0;
                            }
                        }
                        else
                        {
                            gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.SF_PLUG].value = sPlugTmp;
                            sPlugCnt = 0;
                        }
                    }
                }
                else if (gClass.str.SrmInfo[srmNum].dioUse == 0)
                {
                    gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.EM_SW].value = true;
                    gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.SF_PLUG].value = true;
                    gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.MODEM_EN].value = true;
                    gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.REQ_STOP].value = true;
                }
                else if (!gClass.str.DioPacket[srmNum].DO_TESTMODE && commFltCnt > 4)
                {
                    gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.EM_SW].value = false;
                    gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.SF_PLUG].value = gClass.str.SrmInfo[srmNum].sfUse == 0;
                    gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.MODEM_EN].value = false;
                    gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.REQ_STOP].value = false;
                    commFltCnt = 0;
                }

                if (gClass.str.SrmInfo[srmNum].dioAliveCnt > 0)          // 프로그램 종료 시 음수 값으로 변경 필
                {
                    gClass.str.SrmInfo[srmNum].dioAliveCnt += 1;
                }

                string logStr = ParseChangeLog();
                if (logStr != String.Empty)
                {
                    SaveLogFile(logStr);
                    logCnt = 0;
                }
                else
                {
                    logCnt += 1;
                    if (logCnt > 5)
                    {
                        logCnt = 0;
                        SaveLogFile("DIO Connection is Alive...");
                    }
                }
            }
        }
        try
        {
            EziMOTIONPlusELib.FAS_Close(0);
            _isConnected = false;
            SaveLogFile("DIO Thread End");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Thread Exit FAS_Close Exception: {ex.Message} {srmNum.ToString()}");
        }
        finally
        {
            gClass.str.SrmPacket[srmNum].txDioComm = false;
            gClass.str.SrmPacket[srmNum].rxDioComm = false;
        }
    }

    public void ParseReturnState(int returnValue)
    {
        switch (returnValue)
        {
            case 1:
                SaveLogFile("DIO FMM_NOT_OPEN");
                break;
            case 2:
                SaveLogFile("DIO FMM_INVALID_PORT_NUM");
                break;
            case 3:
                SaveLogFile("DIO FMM_INVALID_SLAVE_NUM");
                break;
            case 5:
                SaveLogFile("DIO FMC_DISCONNECTED");
                break;
            case 6:
                SaveLogFile("DIO FMM_UNKNOWN_ERROR");
                break;
            case 7:
                SaveLogFile("DIO FMM_UNKNOWN_ERROR");
                break;
            case 8:
                SaveLogFile("DIO FMM_UNKNOWN_ERROR");
                break;
            case (int)byte.MaxValue:
                SaveLogFile("DIO FMM_UNKNOWN_ERROR");
                break;
            default:
                SaveLogFile("DIO UNKNOWN RESULT ERROR - " + returnValue.ToString());
                break;
        }
    }

    public void Close()
    {
        _isRunning = false;
        _isConnected = false;
        gClass.str.SrmInfo[srmNum].dioAliveCnt = -99L;
        if (_dioThread != null)
        {
            Console.WriteLine($"DIO thread Exist - Join {_dioThread?.ToString()} {srmNum.ToString()}");
            _dioThread.Join();
        }
        try
        {
            EziMOTIONPlusELib.FAS_Close(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAS_Close Exception: {ex.Message} {srmNum.ToString()}");
        }
        Console.WriteLine($"DIO thread After Join {_dioThread?.ToString()} {srmNum.ToString()}");
    }

    private string ParseChangeLog()
    {
        string str1 = string.Empty;
        string str2 = string.Empty;
        string str3 = string.Empty;
        string empty1 = string.Empty;
        string empty2 = string.Empty;
        for (int index = 0; index < Enum.GetValues(typeof(DISTATE)).Length; ++index)
        {
            string str4 = ((DISTATE)index).ToString();
            if (gClass.str.DioPacket[srmNum].DISET[index].value != gClass.str.DioPacket[srmNum].DISET[index].prevalue)
            {
                str1 += $"[{str4}]= {gClass.str.DioPacket[srmNum].DISET[index].prevalue.ToString()} -> {gClass.str.DioPacket[srmNum].DISET[index].value.ToString()} / ";
                
                // DI RESET 신호 처리 (OFF -> ON 엣지 감지)
                if (index == (int)DISTATE.RESET && 
                    gClass.str.DioPacket[srmNum].DISET[index].value == true && 
                    gClass.str.DioPacket[srmNum].DISET[index].prevalue == false)
                {
                    cIniAccess.SaveJobLog(srmNum, "GCP == 리셋버튼 클릭");
                    gClass.str.SrmPacket[srmNum].gcpError = false;
                    gClass.str.SrmPacket[srmNum].recovError = false;
                    gClass.str.DioPacket[srmNum].DOSET[(int)DOSTATE.BUZZER].value = false;
                    gClass.str.SrmPacket[srmNum].jobError = false;
                    gClass.str.SrmPacket[srmNum].gcpModemFlt = false;

                    // SRM 통신 정상일 때만 명령리셋 전송
                    if (gClass.str.SrmPacket[srmNum].srmCommDiscCnt > 0)
                    {
                        gClass.str.SrmPacket[srmNum].pulseClicked = true;
                        gClass.str.SrmPacket[srmNum].resetCmd = 1;
                    }
                }
                
                gClass.str.DioPacket[srmNum].DISET[index].prevalue = gClass.str.DioPacket[srmNum].DISET[index].value;
            }
        }
        for (int index = 0; index < Enum.GetValues(typeof(DOSTATE)).Length; ++index)
        {
            string str5 = ((DOSTATE)index).ToString();
            if (gClass.str.DioPacket[srmNum].DOSET[index].value != gClass.str.DioPacket[srmNum].DOSET[index].prevalue)
            {
                str2 += $"[{str5}] = {gClass.str.DioPacket[srmNum].DOSET[index].prevalue.ToString()} → {gClass.str.DioPacket[srmNum].DOSET[index].value.ToString()} / ";
                gClass.str.DioPacket[srmNum].DOSET[index].prevalue = gClass.str.DioPacket[srmNum].DOSET[index].value;
            }
        }
        if (str1 != string.Empty)
            str1 = "INPUT : " + str1;
        if (str3 != string.Empty)
        {
            str3 = "OUTCMD : " + str3;
            if (str1 != string.Empty)
                str3 = "\n" + str3;
        }
        if (str2 != string.Empty)
        {
            str2 = "OUTPUT : " + str2;
            if (str3 != string.Empty)
                str2 = "\n" + str2;
        }
        return str1 + str3 + str2;
    }

    private async void SaveLogFile(string text)
    {
        await Task.Run((Action)(() =>
        {
            if (text.Contains("DIO Connection is Alive"))
                return;
            text = DateTime.Now.ToString("HH:mm:ss:fff ") + text;
            gcpDioClass.mutex.WaitOne();
            if (!Directory.Exists(pathString))
            {
                Directory.CreateDirectory(pathString);
                Console.WriteLine("Folder created at: " + pathString);
            }
            string path = Path.Combine(pathString, $"DIOLOG_{DateTime.Now.ToString("yyyyMMdd")}.log");
            if (!File.Exists(path))
            {
                using (StreamWriter text1 = File.CreateText(path))
                {
                    text1.WriteLine("File created on " + DateTime.Now.ToString());
                    cIniAccess.DeleteOldFiles(srmNum, pathString, 15);
                }
            }
            using (StreamWriter streamWriter = new StreamWriter(path, true))
                streamWriter.WriteLine(text);
            gcpDioClass.mutex.ReleaseMutex();
        }));

        // SRM Log Dialog SendMessage
        IntPtr WindowToFind = cConstDefine.FindWindow(null, "WindowDioLog" + (srmNum + 1));                // 로그 별 타이틀 설정 참조
                                                                                                            //IntPtr WindowToFind = win_SrmLog.GetHandle();
        if (WindowToFind != IntPtr.Zero)
        {
            //Console.WriteLine("Send Udp Message " + WindowToFind);
            IntPtr hwnd = WindowToFind;
            var copyData = new cConstDefine.COPYDATASTRUCT();
            copyData.dwData = IntPtr.Zero;
            copyData.lpData = text;
            copyData.cbData = Encoding.Unicode.GetBytes(text).Length + 1; // add 1 for null-terminator
            cConstDefine.SendMessage(WindowToFind, cConstDefine.WM_USER, IntPtr.Zero, ref copyData);               // Send - Post 차이 비교 필요
                                                                                                                   //PostMessage(WindowToFind, cConstDefine.WM_USER, IntPtr.Zero, ref copyData);

        }
        else
        {
            Console.WriteLine("Find Srm Window Fail WindowDioLog " + srmNum + " " + text);
        }
    }
}
