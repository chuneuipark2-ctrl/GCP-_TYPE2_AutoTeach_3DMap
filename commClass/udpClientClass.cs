using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Timers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows;

namespace gcp_Wpf.commClass
{
    internal class udpClientClass
    {
        private byte[] sendBuffer = new byte[1024];
        private string sendStr;
        private uint oldNum;                             // 이전 통신 시퀀스 번호
        private uint seqNum;                             // 현재 통신 시퀀스 번호
        private int srmNum;                              // 클래스 별 호기번호
        private bool isRunning;
        private bool fClose;

        private string ipAddress;
        private int port;
        private UdpClient udpClient;
        IPEndPoint remoteEndPoint;
        IPEndPoint localEndPoint;
        private List<byte[]> txDataList = new List<byte[]>();
        private Task receiveTask = null;
        private CancellationTokenSource receiveCts = null;
        private readonly object receiveTaskLock = new object();
        System.Timers.Timer sendTimer = new System.Timers.Timer();

        static System.Threading.Mutex mutex = new System.Threading.Mutex();
        string pathString;

        /// <summary>COMPLETE 상태에서 dataReportOK==0 대기 최대 시간(초). 초과 시 에러 표시.</summary>
        private const double DATA_REPORT_OK_WAIT_TIMEOUT_SEC = 60.0;

        //Singletone
        singletonClass gClass;
        //public delegate void UdpCallbackDelegate(string message, IPEndPoint remoteEndpoint);
        public delegate void UdpCallbackDelegate(string message, IPEndPoint remoteEndpoint);

        public udpClientClass(int srmNum)
        {
            gClass = singletonClass.Instance;
            this.srmNum = srmNum;

            // type 이 달라질 경우 대비  -------------------- 230614 현재는 미사용
            int type = 1;

            switch (type)
            {
                case 1:             // SRM 로그 저장을 위함
                    pathString = System.IO.Path.Combine(Environment.CurrentDirectory, "SRM" + srmNum, cConstDefine.PATH_LOG, cConstDefine.PATH_SRMLOG);
                    break;
                case 2:
                    //pathString = System.IO.Path.Combine(Environment.CurrentDirectory, "SRM" + srmNum, cConstDefine.PATH_LOG, cConstDefine.PATH_SRMLOG);
                    break;
                case 3:
                    //pathString = System.IO.Path.Combine(Environment.CurrentDirectory, "SRM" + srmNum, cConstDefine.PATH_LOG, cConstDefine.PATH_DIOLOG);
                    break;
            }
        }

        public void connect(string ipAddress, int port)
        {
            // 시퀀스 번호 초기화
            seqNum = 1;
            gClass.str.SrmPacket[srmNum].recvStr.seqNum = 0;
            // 마지막 UDP 수신 시간 초기화
            gClass.str.SrmPacket[srmNum].lastUdpReceiveTime = DateTime.MinValue;

            StopReceiveTask(true);      // 중첩 태스크 정리
            Console.WriteLine("UdpClient thread Restart Connect " + srmNum);

            this.ipAddress = ipAddress;
            this.port = port;

            udpClient = new UdpClient();
            udpClient.Client.ReceiveTimeout = 1000;  // 1초로 변경

            remoteEndPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
            localEndPoint = new IPEndPoint(IPAddress.Any, 53022);

            try
            {
                udpClient.Client.Bind(localEndPoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Udp Bind 실패 ({ipAddress}:{port}) - {ex.Message}");
                cIniAccess.SaveExLog(srmNum, $"EXCEPTION - UDP Bind : {ipAddress}:{port} - {ex.Message}");
                udpClient.Close();
                return;
            }

            //client.BeginSend(data, data.Length, hostname, port, callback, client);
            // udpClient.Connect(remoteEndPoint);
            //udpClient.BeginReceive(new AsyncCallback(ReceiveCallback), null);


            fClose = false;
            isRunning = true;
            StartReceiveTask();                   // 230517 Receive 콜백함수

            sendTimer.Interval = 1000; // 1 second
            sendTimer.AutoReset = true; // Repeat the timer
            sendTimer.Elapsed += sendTimer_Elapsed;

            sendTimer.Start();


            // Test
            //Tx_SendCmd(0x00, 0x50);
        }

        private async void SaveLogFile(string text, bool poll)
        {
            // pollingStop ON이면 0x30 폴링 로그(poll=true)는 ★파일쓰기까지★ 스킵.
            //   과거엔 이 가드가 파일쓰기 뒤(IPC 앞)에 있어, 토글해도 디스크엔 초당 ~6~7줄이 계속 쌓였음(토글 무의미).
            if (poll && gClass.str.SrmInfo[srmNum].pollingStop) return;
            await Task.Run(() =>
            {
                mutex.WaitOne();
                // ★ try/finally — 파일 IO 예외(디스크풀/권한/공유위반)가 나도 뮤텍스를 반드시 풀어
                //   영구점유(→이후 모든 로그/SaveRackData/SaveStationData가 WaitOne에서 영구 블록)를 방지.
                try
                {
                    if (!Directory.Exists(pathString))
                    {
                        Directory.CreateDirectory(pathString);
                        Console.WriteLine("Folder created at: " + pathString);
                    }


                    string filePath = System.IO.Path.Combine(pathString, "SRMLOG_" + srmNum + "_" + DateTime.Now.ToString("yyyyMMdd_HH") + ".log");

                    if (!File.Exists(filePath))
                    {
                        using (StreamWriter writer = File.CreateText(filePath))
                        {
                            writer.WriteLine("File created on " + DateTime.Now.ToString());
                            cIniAccess.DeleteOldFiles(srmNum, pathString, 15);
                        }
                    }

                    text = DateTime.Now.ToString("HH:mm:ss:fff ") + text;           // 현재시간 추가

                    // Write the text to the file
                    using (StreamWriter writer = new StreamWriter(filePath, true))
                    {
                        writer.WriteLine(text);
                        //Console.WriteLine(text + "  " + srmNum);                  // 로그 콘솔화면 출력
                    }
                }
                finally
                {
                    try { mutex.ReleaseMutex(); } catch { }
                }
                if (this.gClass.str.SrmInfo[this.srmNum].pollingStop && poll)
                    return;

                // SRM Log Dialog SendMessage
                IntPtr WindowToFind = cConstDefine.FindWindow(null, "WindowSrmLog" + (srmNum + 1));
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
                    Console.WriteLine("Find Srm Window Fail WindowSrmLog " + srmNum + " " + text);
                }
            });


        }

        private async void SaveRackData(int type, int start, int end)                       // 랙 설정 Cell 데이터 ini 저장
        {
            await Task.Run(() =>
            {
                mutex.WaitOne();

                // Total Bay / Lev 저장
                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + srmNum + "\\RACK" + "\\Rack.ini", "RACKINFO", "Maxbay", gClass.str.SrmInfo[srmNum].bay.ToString());
                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + srmNum + "\\RACK" + "\\Rack.ini", "RACKINFO", "MaxLev", gClass.str.SrmInfo[srmNum].lev.ToString());



                // Bay / Lev 데이터저장 

                if (type == 1)                                              // BAY
                {
                    for (int i = start; i <= end; i++)
                    {
                        // RACK.ini
                        cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + srmNum + "\\RACK" + "\\Rack.ini", "RACK_BAY", "BAY" + i, gClass.str.SrmInfo[srmNum].cellBay[i].ToString());
                    }
                }
                else if (type == 2)                                         // LEV
                {
                    for (int i = start; i <= end; i++)
                    {
                        // RACK.ini
                        cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + srmNum + "\\RACK" + "\\Rack.ini", "RACK_LEV", "LEV" + i, gClass.str.SrmInfo[srmNum].cellLev[i].ToString());
                    }
                }

                mutex.ReleaseMutex();
            });
        }

        private async void SaveProhRackData(int count)                       // 금지 랙 설정 데이터 ini 저장
        {
            await Task.Run(() =>
            {
                mutex.WaitOne();

                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + srmNum + "\\PRHRACK" + "\\ProhRack.ini", "PROH_INFO", "COUNT", count.ToString());
                string tmpStr = "";
                for (int i = 0; i <= 100; i++)
                {
                    // ProhRacK.ini
                    if (i < count)
                    {
                        tmpStr = gClass.str.SrmInfo[srmNum].prohRack[i];
                    }
                    else
                    {
                        tmpStr = "";
                    }
                    cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + srmNum + "\\PRHRACK" + "\\ProhRack.ini", "PROH_LIST", i.ToString(), tmpStr);
                }
                mutex.ReleaseMutex();
            });
        }

        private async void SaveStationData()                       // 스테이션 데이터 저장
        {
            await Task.Run(() =>
            {
                mutex.WaitOne();
                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + srmNum + "\\Station.ini", "STATION", "COUNT", gClass.str.SrmInfo[srmNum].stn.ToString());

                for (int i = 0; i < gClass.str.SrmInfo[srmNum].stn; i++)
                {
                    cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + srmNum + "\\Station.ini", "STATION" + i, "TYPE", gClass.str.SrmInfo[srmNum].SrmStation[i].stnType.ToString());
                    cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + srmNum + "\\Station.ini", "STATION" + i, "GOODTYPE", gClass.str.SrmInfo[srmNum].SrmStation[i].goodType.ToString());
                    cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + srmNum + "\\Station.ini", "STATION" + i, "TRAVPOS", gClass.str.SrmInfo[srmNum].SrmStation[i].travPos.ToString());
                    cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + srmNum + "\\Station.ini", "STATION" + i, "LIFTPOS", gClass.str.SrmInfo[srmNum].SrmStation[i].liftPos.ToString());
                    cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + srmNum + "\\Station.ini", "STATION" + i, "FORKPOS", gClass.str.SrmInfo[srmNum].SrmStation[i].forkPos.ToString());
                    cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + srmNum + "\\Station.ini", "STATION" + i, "UPOFFSET", gClass.str.SrmInfo[srmNum].SrmStation[i].upOffset.ToString());
                    cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + srmNum + "\\Station.ini", "STATION" + i, "DOWNOFFSET", gClass.str.SrmInfo[srmNum].SrmStation[i].downOffset.ToString());
                    cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + srmNum + "\\Station.ini", "STATION" + i, "INTNUM", gClass.str.SrmInfo[srmNum].SrmStation[i].intNum.ToString());
                }
                mutex.ReleaseMutex();
            });
        }


        private ushort crc16_ccitt(byte[] bufBytes)
        {
            ushort crc = 0;

            for (int i = 0; i < bufBytes.Length; i++)
            {
                crc = (ushort)((crc << 8) ^ cConstDefine.crc16tab[((crc >> 8) ^ bufBytes[i]) & 0x00FF]);
            }
            return crc;
        }

        private byte bcc_check(byte[] bufBytes)
        {
            byte bcc = 0;
            foreach (byte b in bufBytes)
            {
                bcc ^= b;
            }
            return bcc;
        }

        private void sendTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (receiveTask == null || receiveTask.IsCompleted)
            {
                if (isRunning)
                {
                    SaveLogFile("UdpClient Thread Restart ", false);
                    gClass.str.SrmPacket[srmNum].srmCommDiscCnt = 3;
                    StartReceiveTask();                   // 230517 Receive 콜백함수
                }
            }


            // for test
            //byte[] myBytes = Encoding.ASCII.GetBytes(sendStr);
            //string asciiString = Encoding.ASCII.GetString(bytes);


            //IntPtr WindowToFind = cConstDefine.FindWindow(null, "WindowSrmLog"+(srmNum + 1));
            ////IntPtr WindowToFind = win_SrmLog.GetHandle();
            //if (WindowToFind != IntPtr.Zero)
            //{
            //    string message = "SEND: " + sendStr;
            //    IntPtr hwnd = WindowToFind;
            //    var copyData = new cConstDefine.COPYDATASTRUCT();
            //    copyData.dwData = IntPtr.Zero;
            //    copyData.lpData = message;
            //    copyData.cbData = Encoding.Unicode.GetBytes(message).Length + 1; // add 1 for null-terminator
            //    cConstDefine.SendMessage(WindowToFind, cConstDefine.WM_USER, IntPtr.Zero, ref copyData);               // Send - Post 차이 비교 필요
            //    //PostMessage(WindowToFind, cConstDefine.WM_USER, IntPtr.Zero, ref copyData);

            //}
            //else
            //{
            //    Console.WriteLine("Find Srm Window Fail " + WindowToFind);
            //}
        }

        //---------------------------미사용-----------------------------------------------------
        //private void ReceiveCallback(IAsyncResult ar)
        //{
        //    try
        //    {
        //        //IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
        //        byte[] bytes = udpClient.EndReceive(ar, ref remoteEndPoint);

        //        string message = Encoding.ASCII.GetString(bytes);
        //        // process the received message
        //        Console.WriteLine($"Received message from {remoteEndPoint}: {message}");

        //        udpClient.BeginReceive(new AsyncCallback(ReceiveCallback), null);
        //    }
        //    catch (Exception e)
        //    {
        //        Console.WriteLine(e.ToString());
        //    }
        //}
        //------------------------------------------------------------------------------------------

        public void Send(byte[] data)
        {
            //byte[] data = Encoding.ASCII.GetBytes(message);
            //udpClient = new UdpClient();
            int sendByte = udpClient.Send(data, data.Length, remoteEndPoint);

            if (sendByte != 0)
            {
                gClass.str.SrmPacket[srmNum].txSrmComm = true;                // SRM TX STATE ON
                // Send Data Parse --------------------------------------------------
                gClass.str.SrmPacket[srmNum].sendStr.srcType = data[4];
                gClass.str.SrmPacket[srmNum].sendStr.srcID = data[5];
                gClass.str.SrmPacket[srmNum].sendStr.dstType = data[6];
                gClass.str.SrmPacket[srmNum].sendStr.dstID = data[7];
                gClass.str.SrmPacket[srmNum].sendStr.seqNum = data[8];
                gClass.str.SrmPacket[srmNum].sendStr.byPass1 = data[9];
                gClass.str.SrmPacket[srmNum].sendStr.byPass2 = data[10];
                gClass.str.SrmPacket[srmNum].sendStr.cmd1 = data[11];
                gClass.str.SrmPacket[srmNum].sendStr.len = BitConverter.ToUInt16(data, 12);
                gClass.str.SrmPacket[srmNum].sendStr.cmd2 = data[14];

                string dataString = BitConverter.ToString(data).Replace("-", string.Empty);
                bool poll = false;
                if (this.gClass.str.SrmPacket[this.srmNum].sendStr.cmd2.ToString("X2") == "30")
                {
                    poll = true;
                }
                SaveLogFile("SEND: " + gClass.str.SrmPacket[srmNum].sendStr.cmd1.ToString("X2") + gClass.str.SrmPacket[srmNum].sendStr.cmd2.ToString("X2") + " / " + dataString, poll);
            }
            else
            {
                gClass.str.SrmPacket[srmNum].txSrmComm = false;                // SRM TX STATE ON
                Console.WriteLine("Send Byte Failed : " + ipAddress);
            }

            //byte[] responseData = udpClient.Receive(ref remoteEndPoint);
            //string responseMessage = Encoding.UTF8.GetString(responseData);
            //Console.WriteLine($"Received response from server: {responseMessage}");
        }

        private void StartReceiveTask()
        {
            lock (receiveTaskLock)
            {
                if (receiveTask != null && !receiveTask.IsCompleted)
                    return;

                receiveCts?.Dispose();
                receiveCts = new CancellationTokenSource();
                CancellationToken token = receiveCts.Token;
                receiveTask = Task.Run(() => ReceiveData(token), token);
            }
        }

        private void StopReceiveTask(bool closeUdp)
        {
            Task taskToWait = null;
            lock (receiveTaskLock)
            {
                isRunning = false;
                if (receiveCts != null)
                {
                    try { receiveCts.Cancel(); } catch { }
                }
                taskToWait = receiveTask;
            }

            if (taskToWait != null)
            {
                try
                {
                    taskToWait.Wait(2000);       // 기존 Join 과 유사하게 종료 대기
                }
                catch (AggregateException ae)
                {
                    bool hasUnhandled = ae.InnerExceptions.Any(ex => !(ex is OperationCanceledException || ex is TaskCanceledException));
                    if (hasUnhandled)
                        Console.WriteLine("UdpClient task stop exception: " + ae.Message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("UdpClient task stop exception: " + ex.Message);
                }
            }

            if (closeUdp && udpClient != null)
            {
                udpClient.Close();
            }

            lock (receiveTaskLock)
            {
                if (receiveTask != null && receiveTask.IsCompleted)
                {
                    receiveTask = null;
                }
                receiveCts?.Dispose();
                receiveCts = null;
            }
        }

        private void ReceiveData(CancellationToken token)
        {
            IPEndPoint clientEndpoint = null;
            // Check Main Process (메인 타이머가 돌지 않으면 통신 스레드 정리)
            int watchDogCnt = 0;
            const int WatchDogCheckInterval = 30;   // 검사 주기: (1+30)회 루프 ≈ 10초 (루프당 Sleep 300ms 기준)
            int watchCnt = WatchDogCheckInterval;
            int timerMs = 10000;
            try
            {
                while (isRunning && !token.IsCancellationRequested)
                {
                    // Blocks until a client sends a message
                    if (watchCnt < 0)
                    {
                        if (watchDogCnt < cIniAccess.watchDogCnt)
                        {
                            if (cIniAccess.watchDogCnt > 1)
                            {
                                watchDogCnt = cIniAccess.watchDogCnt;       // 메인타이머 시작 확인 후
                            }
                            watchCnt = WatchDogCheckInterval;
                        }
                        else
                        {
                            cIniAccess.SaveJobLog(srmNum, "COMM == Udp 스레드 WatchDog 종료");
                            SaveLogFile("UdpClient Thread WatchDog Clear", false);
                            Console.WriteLine("UDP Thread WatchDog Exit");
                            sendTimer.Stop();
                            isRunning = false;
                            continue;
                        }
                    }
                    else
                    {
                        watchCnt -= 1;
                    }

                    try
                    {
                        if (token.WaitHandle.WaitOne(timerMs))
                        {
                            break;
                        }
                        Send_Command();
                        

                        //Console.WriteLine("Receive Wait UDP data: " + ipAddress + " " + port);
                        byte[] data = udpClient.Receive(ref remoteEndPoint);
                        //Console.WriteLine("Receive Info : " + remoteEndPoint.Address);

                        timerMs = 300;
                        gClass.str.SrmPacket[srmNum].rxSrmComm = true;                // SRM RX STATE ON
                        gClass.str.SrmPacket[srmNum].srmCommDiscCnt = 2;            // Receive 여부 확인 후 접속 카운트 초기화
                        gClass.str.SrmPacket[srmNum].lastUdpReceiveTime = DateTime.Now;  // 마지막 UDP 수신 시간 업데이트
                        //gClass.str.SrmPacket[srmNum].srmCommDiscCnt = 2099999999;     // simulate
                        //remoteEndPoint = localEndPoint;
                        //string message = Encoding.ASCII.GetString(data);
                        string message = BitConverter.ToString(data).Replace("-", string.Empty);
                        // Handle the incoming data here, e.g. raise an event or call a callback


                        // 전송받은 데이터 파싱
                        bool result = Rx_CommandParse(data);

                        //로그 수신 저장 먼저
                        OnUdpDataReceived(message);

                        //seqNum++;
                        //if (seqNum > 255) seqNum = 0;   // 수신 데이터 파싱 정상 시 seqNum 증가

                        if (result)         // 리시브 헤더, CRC 정상확인
                        {
                            result = Rx_DataParse(gClass.str.SrmPacket[srmNum].recvStr.cmd1, gClass.str.SrmPacket[srmNum].recvStr.cmd2);
                            if (result)         // 리시브 데이터 정상 확인
                            {

                            }
                            else
                            {

                                //Tx_SendCmd(0x00, 0x30);
                            }
                        }
                        else
                        {
                            SaveLogFile("UDP Thread ReceiveData Error", false);
                            //Tx_SendCmd(0x00, 0x50);
                        }
                        //Send(message);

                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine("UDP Thread Cancelled");
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (token.IsCancellationRequested || !isRunning)
                        {
                            Console.WriteLine("UDP Receive cancelled " + ex.Message);
                            break;
                        }

                        Console.WriteLine("UDP Receive Method Exception " + ex.Message);
                        timerMs = 2000;

                        gClass.str.SrmPacket[srmNum].srmCommDiscCnt -= 1;
                        gClass.str.SrmPacket[gClass.srmNum].rxSrmComm = false;
                        SaveLogFile("UDP Thread Read Failed " + gClass.str.SrmPacket[srmNum].srmCommDiscCnt, false);

                        if (gClass.str.SrmPacket[srmNum].srmCommDiscCnt <= 0)
                        {
                            // Test UDP Forced Finish
                            //isRunning = false;
                            //Console.WriteLine("UDP Forced Finish");

                            gClass.str.SrmPacket[srmNum].pulseClicked = false;
                            gClass.str.SrmPacket[srmNum].homeCmd = 0;

                            // 정보요청은 통신 끊어져도 유지
                            //gClass.str.SrmPacket[srmNum].rackRequest = false;
                            //gClass.str.SrmPacket[srmNum].stationRequest = false;
                            //gClass.str.SrmPacket[srmNum].prohRackRequest = false;
                            //gClass.str.SrmPacket[srmNum].forkRequest = false;
                            //gClass.str.SrmPacket[srmNum].craneSetRequest = false;

                            gClass.str.SrmPacket[srmNum].startCmd = 0;
                            gClass.str.SrmPacket[srmNum].wcsCmdReset = false;
                            gClass.str.SrmPacket[srmNum].resetCmd = 0;
                        }

                        // Handle any other exceptions here
                        //int errorCode = Marshal.GetLastWin32Error();
                        //Console.WriteLine(ipAddress +" "+ port + " : Receive Timeout - WD Count " + MainWindow.watchDogCount + ex.Message);

                        //if(errorCode == 0)

                        //Console.WriteLine("Error code: " + errorCode);
                        //Console.WriteLine("Error receiving UDP data: " + ex.Message + ipAddress + " " + port);

                        //Console.WriteLine("UDP Conn Exception : " + ex.Message + " " + srmNum);
                    }
                    finally
                    {
                        //Console.WriteLine("Udp Thread Read finally." + srmNum);
                    }
                }

                if (clientEndpoint != null)
                {
                    SaveLogFile("UdpClient Disconnected: " + clientEndpoint.Port, false);
                    Console.WriteLine("UdpClient Disconnected." + clientEndpoint.Port);
                }
                else
                {
                    Console.WriteLine("UdpClient Disconnected.");
                }
                Console.WriteLine("UdpClient Thread Finished " + srmNum);
            }
            catch (Exception ex)
            {
                cIniAccess.SaveExLog(0, "EXCEPTION - UdpClient ReceiveData : " + ex.Message + " " + ex.Source);
                Console.WriteLine("UdpClient Exception Error: " + ex.Message);
                // Handle the exception, e.g. display an error message
                SaveLogFile("UdpClient Exception Error: " + ex.Message, false);
            }
            finally
            {
                Console.WriteLine("Udp Thread Method finally." + srmNum);
            }

            SaveLogFile("UdpClient Thread Clear - " + "TimerState : " + sendTimer.Enabled + " isRunningState : " + isRunning, false);
            if (!sendTimer.Enabled && isRunning)            // 타이머 정지 && 스레드 플래그 ON
            {
                sendTimer.Start();
                SaveLogFile("Thread Timer Restart " + sendTimer.Enabled, false);
            }
        }

        private void OnUdpDataReceived(string message)
        {
            bool poll = false;
            if (gClass.str.SrmPacket[srmNum].recvStr.cmd2.ToString("X2") == "30")
                poll = true;
            // Parse Recieve From SRM Packet
            //SaveLogFile("SEND: " + dataString);
            SaveLogFile("RECV: CMD(" + gClass.str.SrmPacket[srmNum].recvStr.cmd1.ToString("X2") + gClass.str.SrmPacket[srmNum].recvStr.cmd2.ToString("X2") + ") / " + message, poll);
        }

        public void Close()
        {
            //fClose = true;
            Console.WriteLine("UdpClient Class Close " + srmNum);

            sendTimer.Stop();
            try { sendTimer.Elapsed -= sendTimer_Elapsed; sendTimer.Dispose(); } catch { }   // IDisposable 타이머 정석 해제
            StopReceiveTask(true);
            Console.WriteLine("UdpClient thread Finished Close " + srmNum);
            cIniAccess.SaveJobLog(srmNum, "COMM == Udp 스레드 Close 종료");
        }

        //------------------------------------------------RECEIVE DATA PARSE FUNC------------------------------------------------------------
        private bool Rx_CommandParse(byte[] recv)
        {
            bool result = false;
            if (recv.Length < 18)
            {
                SaveLogFile("PacketError - Receive Length Failed", false);           // 패킷 길이가 맞지않음
                return false;
            }
            // Check Header----SYN---0x16 X 4 Check-------------------
            for (int i = 0; i < 4; i++)
            {
                if (recv[i] == 0x16) result = true;
                else result = false;
            }

            if (!result)
            {
                SaveLogFile("PacketError - Start Header Failed", false);             // SYN 헤더 형식이 맞지않음
                return false;
            }

            // Check ETX ------------------------------------------------------
            if (recv[recv.Length - 1] != 0xF5)
            {
                SaveLogFile("PacketError - ETX Failed", false);                        // ETX 를 찾지못함
                return false;
            }

            // Check SRC Type ID ----------------------------------------------
            if ((recv[4] != 0x60) || (recv[5] != Convert.ToByte(gClass.str.SrmInfo[srmNum].srmID)))
            {
                SaveLogFile("PacketError - SRC Device Type Failed", false);             // 장치 타입이 맞지않음 (지상반 통신 SRM TYPE 0x0060)
                return false;
            }

            // Check DST Type ID ----------------------------------------------
            if ((recv[6] != 0x00) || (recv[7] != 0x00))
            {
                SaveLogFile("PacketError - DST Device Type Failed", false);             // 장치 타입이 맞지않음 (지상반 통신 GCP TYPE 0x0000)
                return false;
            }

            // Check SeqNum ---------------------------------------------------
            if ((int)recv[8] != (seqNum - 1))
            {
                //SaveLogFile("PacketError - SeqNum Failed");             // 시퀀스 번호 에러
                // to do SeqNum pass
                //return true;
                //return false;
            }

            // Check Receive Command ---------------------------------------------------
            if ((int)recv[14] != (gClass.str.SrmPacket[srmNum].sendStr.cmd2))    // 전송한 cmd2 가 맞는지 확인
            {
                //SaveLogFile("PacketError - Receive Command2 Failed");             // 리시브 커맨드 에러
                //return false;
            }

            // Check Receive Command ---------------------------------------------------
            if ((int)recv[11] != (gClass.str.SrmPacket[srmNum].sendStr.cmd1 + 0x80))    // 전송한 cmd1 + 0x80 이 맞는지 확인
            {
                //SaveLogFile("PacketError - Receive Command1 Failed");             // 리시브 커맨드 에러
                //return false;
            }

            // Check CRC ------------------------------------------------------
            byte[] packetData = new byte[recv.Length - 7];                        // Receive Length - SYN, CRC, ETX
            Array.Copy(recv, 4, packetData, 0, packetData.Length);              // SYN CRC ETX 를 제외한 Recv 총 길이 = 계산길이
            ushort recvCrc = (ushort)((recv[recv.Length - 3] << 8) | recv[recv.Length - 2]);      // CRC BigEndian   
            ushort calcCrc = crc16_ccitt(packetData);         // 수신데이터 작성데이터 crc16 계산

            if (recvCrc != calcCrc)
            {
                SaveLogFile("PacketError - CRC Data Failed", false);             // CRC 체크 에러
                return false;
            }

            // Get Parse Command 1 --------------------------------------------------
            gClass.str.SrmPacket[srmNum].recvStr.srcType = recv[4];
            gClass.str.SrmPacket[srmNum].recvStr.srcID = recv[5];
            gClass.str.SrmPacket[srmNum].recvStr.dstType = recv[6];
            gClass.str.SrmPacket[srmNum].recvStr.seqNum = recv[8];
            gClass.str.SrmPacket[srmNum].recvStr.byPass1 = recv[9];
            gClass.str.SrmPacket[srmNum].recvStr.byPass2 = recv[10];
            gClass.str.SrmPacket[srmNum].recvStr.cmd1 = recv[11];
            gClass.str.SrmPacket[srmNum].recvStr.len = BitConverter.ToUInt16(recv, 12);
            gClass.str.SrmPacket[srmNum].recvStr.cmd2 = recv[14];

            //Console.WriteLine("CMD"+ gClass.str.SrmPacket[srmNum].recvStr.cmd1.ToString("X2") + gClass.str.SrmPacket[srmNum].recvStr.cmd2.ToString("X2") + " / recvLen : " + gClass.str.SrmPacket[srmNum].recvStr.len + " / recvBuf " + recv[12] + " " + recv[13]);

            if (gClass.str.SrmPacket[srmNum].recvStr.len > 1)
            {
                gClass.str.SrmPacket[srmNum].recvStr.data = Enumerable.Repeat<byte>(0, gClass.str.SrmPacket[srmNum].recvStr.len - 1).ToArray<byte>();     // byte Array 메모리 할당
                Array.Copy(recv, 15, gClass.str.SrmPacket[srmNum].recvStr.data, 0, gClass.str.SrmPacket[srmNum].recvStr.len - 1);     // Data 바이트 파싱  len = Command2 ~ Data 까지 길이
            }

            // TX 보낼 때 보낼지.. to do   비동기식 처리 고민중
            //oldNum = (int)recv[8];
            //seqNum += 1;
            //if (seqNum > 255) seqNum = 0;

            //sendStr = cConstDefine.STX + sendStr + Convert.ToChar(crc);     // 보내는 데이터 
            //string asciiString = Encoding.ASCII.GetString(bytes);
            return true;
        }

        private bool Rx_DataParse(byte cmd1, byte cmd2)
        {
            bool result = false;
            switch (cmd2)
            {
                case 0x10:                      // 기본정보 조회 0x0110
                    cIniAccess.SaveJobLog(srmNum, "SRM RX == 기본정보 조회");
                    //result = Rx_RequestSrmStruct();
                    break;
                case 0x11:                      // 기본정보 제어 0x0111
                    cIniAccess.SaveJobLog(srmNum, "SRM RX == 기본정보 제어");
                    result = Rx_RequestStdInfoControl();
                    break;
                case 0x25:                      // 기본정보 조회 0x0125
                    cIniAccess.SaveJobLog(srmNum, "SRM RX == 장치구조 조회");
                    result = Rx_RequestSrmStruct();
                    break;
                case 0x30:                      // 상태조회 0x30 
                    result = Rx_RequestState();
                    break;
                case 0x41:                      // 반송지령 0x41
                    cIniAccess.SaveJobLog(srmNum, "SRM RX == 반송지령");
                    result = Rx_RequestTransfer();
                    break;
                case 0x50:
                    result = Rx_ResponseCycleStop();
                    break;
                case 0x84:
                    result = Rx_ResponseCycleStop();
                    break;
                case 0x85:
                    result = Rx_ResponseEmStop();
                    break;
                case 128 /*0x80*/:
                    result = Rx_RequestManualCommand();
                    break;
                case 0x92:                      // 렉 기본 설정 조희 0x92
                    cIniAccess.SaveJobLog(srmNum, "SRM RX == 렉 기본 설정 조회");
                    result = Rx_RequestRackSetting();
                    break;
                case 0x94:                      // 셀설정 조회 0x94
                    cIniAccess.SaveJobLog(srmNum, "SRM RX == 셀설정 조회");
                    result = Rx_RequestCellSetting();
                    break;
                case 0x95:                      // 셀위치 설정 응답 0x95
                    cIniAccess.SaveJobLog(srmNum, "SRM RX == 셀위치 설정 응답");
                    result = Rx_CellPositionCtrlRes();
                    break;
                case 0x98:                      // 스테이션 조회 0x98
                    cIniAccess.SaveJobLog(srmNum, "SRM RX == 스테이션 설정 조회");
                    result = Rx_RequestStationSetting();
                    break;
                case 0x9C:                      // 금지렉설정 조회 0x9C
                    cIniAccess.SaveJobLog(srmNum, "SRM RX == 금지렉 설정 조회");
                    result = Rx_RequestProhSetting();
                    break;
                case 0xA7:                      // 포크데이터 조회 0xA7
                    cIniAccess.SaveJobLog(srmNum, "SRM RX == 포크데이터 설정 조회");
                    result = Rx_RequestForkSetting();
                    break;
                case 0x59:                      // 0x8059 지정위치(보수위치) 이동 응답
                    {
                        int respLen = gClass.str.SrmPacket[srmNum].recvStr.len;
                        byte[] respData = gClass.str.SrmPacket[srmNum].recvStr.data;
                        // Probe용
                        if (respData != null && respLen > 0)
                        {
                            gClass.str.SrmPacket[srmNum].probeResp = new byte[respLen];
                            Array.Copy(respData, gClass.str.SrmPacket[srmNum].probeResp, respLen);
                        }
                        else
                        {
                            gClass.str.SrmPacket[srmNum].probeResp = new byte[0];
                        }
                        gClass.str.SrmPacket[srmNum].probeRespLen = respLen;
                        gClass.str.SrmPacket[srmNum].probeDone = true;
                        // 보수위치 이동용
                        gClass.str.SrmPacket[srmNum].maintMoveResult = respData != null && respData.Length >= 1 ? respData[0] : (byte)0xFF;
                        gClass.str.SrmPacket[srmNum].maintMoveDone = true;
                        SaveLogFile($"0x0059 Response len={respLen} result={gClass.str.SrmPacket[srmNum].maintMoveResult} data={BitConverter.ToString(gClass.str.SrmPacket[srmNum].probeResp ?? new byte[0])}", false);
                        result = true;
                    }
                    break;
                case 0xA3:                      // 0x80A3 Drive 파라미터 읽기 응답
                    {
                        int rawLen = gClass.str.SrmPacket[srmNum].recvStr.len;
                        int respLen = rawLen - 1; // CMD2 제외
                        byte[] respData = gClass.str.SrmPacket[srmNum].recvStr.data;
                        int dataArrLen = respData != null ? respData.Length : -1;

                        // 디버그: 원시 응답 헤더 덤프
                        string hexDump = "";
                        if (respData != null)
                        {
                            int dumpLen = Math.Min(respData.Length, 32);
                            for (int di = 0; di < dumpLen; di++) hexDump += $"{respData[di]:X2} ";
                        }
                        SaveLogFile($"0x00A3 RX rawLen={rawLen} respLen={respLen} dataArr={dataArrLen} head=[{hexDump.TrimEnd()}]", false);

                        if (respData != null && respLen >= 1018)
                        {
                            gClass.str.SrmPacket[srmNum].driveParamData = new byte[1018];
                            Array.Copy(respData, gClass.str.SrmPacket[srmNum].driveParamData, 1018);
                            uint maintPos = BitConverter.ToUInt32(respData, 172);
                            SaveLogFile($"0x00A3 Drive OK Maintance_Pos={maintPos}mm", false);
                        }
                        else if (respData != null && respLen > 0)
                        {
                            // 응답은 있지만 짧음 — 전체 저장 (디버그용)
                            gClass.str.SrmPacket[srmNum].driveParamData = new byte[respLen];
                            Array.Copy(respData, gClass.str.SrmPacket[srmNum].driveParamData, respLen);
                            SaveLogFile($"0x00A3 Drive SHORT data len={respLen}", false);
                        }
                        gClass.str.SrmPacket[srmNum].driveParamReadDone = true;
                        result = true;
                    }
                    break;
                case 0xA4:                      // 0x80A4 Drive 파라미터 쓰기 응답
                    {
                        byte[] respData = gClass.str.SrmPacket[srmNum].recvStr.data;
                        gClass.str.SrmPacket[srmNum].driveParamWriteResult = respData != null && respData.Length >= 1 ? respData[0] : (byte)0xFF;
                        gClass.str.SrmPacket[srmNum].driveParamWriteDone = true;
                        SaveLogFile($"0x00A4 Drive Param Write Result={gClass.str.SrmPacket[srmNum].driveParamWriteResult}", false);
                        result = true;
                    }
                    break;
                case 0xA5:                      // 0x80A5 Lift 파라미터 읽기 응답
                    {
                        int rawLen = gClass.str.SrmPacket[srmNum].recvStr.len;
                        int respLen = rawLen - 1;
                        byte[] respData = gClass.str.SrmPacket[srmNum].recvStr.data;
                        int dataArrLen = respData != null ? respData.Length : -1;

                        string hexDump = "";
                        if (respData != null)
                        {
                            int dumpLen = Math.Min(respData.Length, 32);
                            for (int di = 0; di < dumpLen; di++) hexDump += $"{respData[di]:X2} ";
                        }
                        SaveLogFile($"0x00A5 RX rawLen={rawLen} respLen={respLen} dataArr={dataArrLen} head=[{hexDump.TrimEnd()}]", false);

                        if (respData != null && respLen >= 176)
                        {
                            gClass.str.SrmPacket[srmNum].liftParamData = new byte[respLen];
                            Array.Copy(respData, gClass.str.SrmPacket[srmNum].liftParamData, respLen);
                            uint maintPos = BitConverter.ToUInt32(respData, 172);
                            SaveLogFile($"0x00A5 Lift OK Maintance_Pos={maintPos}mm", false);
                        }
                        else if (respData != null && respLen > 0)
                        {
                            gClass.str.SrmPacket[srmNum].liftParamData = new byte[respLen];
                            Array.Copy(respData, gClass.str.SrmPacket[srmNum].liftParamData, respLen);
                            SaveLogFile($"0x00A5 Lift SHORT data len={respLen}", false);
                        }
                        gClass.str.SrmPacket[srmNum].liftParamReadDone = true;
                        result = true;
                    }
                    break;
                case 0xA6:                      // 0x80A6 Lift 파라미터 쓰기 응답
                    {
                        byte[] respData = gClass.str.SrmPacket[srmNum].recvStr.data;
                        gClass.str.SrmPacket[srmNum].liftParamWriteResult = respData != null && respData.Length >= 1 ? respData[0] : (byte)0xFF;
                        gClass.str.SrmPacket[srmNum].liftParamWriteDone = true;
                        SaveLogFile($"0x00A6 Lift Param Write Result={gClass.str.SrmPacket[srmNum].liftParamWriteResult}", false);
                        result = true;
                    }
                    break;
            }
            return result;
        }

        unsafe private bool Rx_RequestStdInfo()          //  0x0110 기본정보 조회
        {
            return true;
        }

        unsafe private bool Rx_RequestStdInfoControl()          //  0x0111 기본정보 제어 (SRM 시간설정)
        {
            return true;
        }

        unsafe private bool Rx_RequestSrmStruct()          //  0x0125 장치구조 조회
        {

            if (gClass.str.SrmPacket[srmNum].recvStr.len >= cConstDefine.DATACOUNT_0X25)
            {
                ref byte[] dataArray = ref gClass.str.SrmPacket[srmNum].recvStr.data;    // 코드 가독성을 위해 ref 사용
                //ref SharedStruct refState = ref gClass.str;

                gClass.str.SrmInfo[srmNum].forkCnt = dataArray[0];         // 포크 수    1: 싱글, 2: 트윈
                gClass.str.SrmInfo[srmNum].forkType = dataArray[3];         // 포크 구동타입  1: 싱글딥, 2: 더블딥 2POS, 3: 더블딥 3POS, 4: 더블딥 2POS 베리언트, 5: 더블딥 3POS 베리언트 

                if (gClass.str.SrmInfo[srmNum].forkType == 1)
                {
                    gClass.str.SrmInfo[srmNum].row = 2;
                }
                else if (gClass.str.SrmInfo[srmNum].forkType == 2 || gClass.str.SrmInfo[srmNum].forkType == 4)
                {
                    gClass.str.SrmInfo[srmNum].row = 4;
                }
                else if (gClass.str.SrmInfo[srmNum].forkType == 3 || gClass.str.SrmInfo[srmNum].forkType == 5)
                {
                    gClass.str.SrmInfo[srmNum].row = 6;
                }


                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + srmNum + "\\RACK" + "\\Rack.ini", "RACKINFO", "MaxRow", gClass.str.SrmInfo[srmNum].row.ToString());
                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + srmNum + "\\ForkInfo.ini", "FORKINFO", "FORKCNT", gClass.str.SrmInfo[srmNum].forkCnt.ToString());
                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + srmNum + "\\ForkInfo.ini", "FORKINFO", "FORKTYPE", gClass.str.SrmInfo[srmNum].forkType.ToString());
                return true;
            }
            else
            {
                SaveLogFile("PacketError - 0x0125 Receive Data Length Failed " + gClass.str.SrmPacket[srmNum].recvStr.len + "<>" + cConstDefine.DATACOUNT_0X25, false);             // 리시브 데이터 길이 에러
                return false;
            }
        }


        unsafe private bool Rx_RequestTransfer()          //  0x0041 반송지령
        {
            // to do 반송지령 응답 결과에 따른 처리 필요
            if (gClass.str.SrmPacket[srmNum].recvStr.len >= cConstDefine.DATACOUNT_0X41)
            {
                ref byte[] dataArray = ref gClass.str.SrmPacket[srmNum].recvStr.data;   // 코드 가독성을 위해 ref 사용

                uint fork1JobNo = BitConverter.ToUInt32(dataArray, 1);   // 포크1 작업번호;
                uint fork2JobNo = BitConverter.ToUInt32(dataArray, 5);   // 포크1 작업번호;
                if (dataArray[9] != 0)      // 반송명령 SRM 실패응답
                {
                    // [오토티칭 격리 — 2026-06-30] 반자동 이동(reqJobNo=30000)은 WCS 작업 상태머신과 분리한다.
                    //   SRM이 실패응답(result)을 줘도 GCP 에러(67-XX)/JOBSTATE.STOP/오프라인으로 가지 않는다.
                    //   오토티칭 이동 실패는 PageAutoTeaching의 stall/타임아웃이 자체 처리(WCS와 무관) → 한 셀 실패가
                    //   시스템을 오프라인으로 떨궈 나머지 셀까지 죽이는 연쇄를 차단. 실제 WCS 작업(reqJobNo<30000)은 아래 기존 로직 그대로.
                    if (gClass.str.SrmPacket[srmNum].reqJobNoFk1 == 30000 || gClass.str.SrmPacket[srmNum].reqJobNoFk2 == 30000)
                    {
                        cIniAccess.SaveJobLog(srmNum, "[TEACH] 반자동(0x41) 이동 실패 result=" + dataArray[10] + " — 오토티칭 내부이동, WCS 에러처리 생략(오프라인 전환 안 함)");
                        return false;
                    }

                    // 요청작업 Factor Different
                    if ((dataArray[0] != gClass.str.SrmPacket[srmNum].reqJobCodeFk1) || (fork1JobNo != gClass.str.SrmPacket[srmNum].reqJobNoFk1) || (fork2JobNo != gClass.str.SrmPacket[srmNum].reqJobNoFk2))         // 작업코드 비교 
                    {
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == 반송작업 응답상이" + "\nREQ : " + gClass.str.SrmPacket[srmNum].reqJobNoFk1 + " " + gClass.str.SrmPacket[srmNum].reqJobNoFk2
                            + "\nRECV : " + fork1JobNo + " " + fork2JobNo);         // 작업코드 다름처리

                        if ((gClass.str.SrmPacket[srmNum].reqJobNoFk1 < 30000 && gClass.str.SrmPacket[srmNum].reqJobNoFk1 > 0) ||
                        (gClass.str.SrmPacket[srmNum].reqJobNoFk2 < 30000 && gClass.str.SrmPacket[srmNum].reqJobNoFk2 > 0))
                        {
                            gClass.str.SrmPacket[srmNum].recovError = true;
                        }

                        gClass.str.SrmPacket[srmNum].jobError = true;
                        gClass.str.SrmPacket[srmNum].gcpErrorCode = 66;            // 지상반 에러코드 확인
                        gClass.str.SrmPacket[srmNum].gcpSubCode = 98;              // 지상반 서브코드 확인       // FLT66-98
                        // FLT 66-08 : 요청-수신 작업번호 상이
                        cIniAccess.ChangeJobState(srmNum, JOBSTATE.STOP, "(SEND FAIL (요청-수신 작업번호 상이))");
                        return false;
                    }

                    if (dataArray[10] == 14)
                    {
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == COMMAND SEND FAIL (이전 명령 작업 수행 중 - 14)");
                        if (gClass.str.SrmPacket[srmNum].recovError)
                        {
                            gClass.str.SrmPacket[srmNum].gcpErrorCode = 00;            // 지상반 에러코드 확인
                            gClass.str.SrmPacket[srmNum].gcpSubCode = 00;              // 지상반 서브코드 확인
                        }
                        gClass.str.SrmPacket[srmNum].jobError = false;
                        gClass.str.SrmPacket[srmNum].recovError = false;
                        cIniAccess.ChangeJobState(srmNum, JOBSTATE.PEND, "(작업스텝 복구)");
                        return true;
                    }
                    else
                    {
                        if (dataArray[10] == 1)
                        {
                            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == COMMAND SEND FAIL (장애 상태 - 1)");
                        }
                        else if (dataArray[10] == 2)
                        {
                            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == COMMAND SEND FAIL (포크가 중심상태가 아님 - 2)");
                        }
                        else if (dataArray[10] == 3)
                        {
                            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == COMMAND SEND FAIL (주행 위치 거리값(mm)가 설정 범위 아님 - 3)");
                        }
                        else if (dataArray[10] == 4)
                        {
                            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == COMMAND SEND FAIL (승강 위치 거리값(mm)가 설정 범위 아님 - 4)");
                        }
                        else if (dataArray[10] == 5)
                        {
                            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == COMMAND SEND FAIL (자동모드가 아닌 상태 - 5)");
                        }
                        else if (dataArray[10] == 6)
                        {
                            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == COMMAND SEND FAIL (시작 OFF 상태 - 6)");
                        }
                        else if (dataArray[10] == 9)
                        {
                            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == COMMAND SEND FAIL (포크 위치값 설정 에러)");
                        }
                        else if (dataArray[10] == 10)
                        {
                            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == COMMAND SEND FAIL (명령 위치가 설정 위치가 아님. - 10)");
                        }
                        else if (dataArray[10] == 11)
                        {
                            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == COMMAND SEND FAIL (반송 명령 수신 시, 화물감지 상태 - 11)");
                        }
                        else if (dataArray[10] == 12)
                        {
                            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == COMMAND SEND FAIL (반송 작업 중지 상태(Start Off 전달 시) - 12)");
                        }
                        else if (dataArray[10] == 13)
                        {
                            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == COMMAND SEND FAIL (반송 작업 실패 상태(이상 발생 시) - 13)");
                        }
                        else if (dataArray[10] == 14)
                        {
                            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == COMMAND SEND FAIL (이전 명령 작업 수행 중 - 14)");
                        }
                        else if (dataArray[10] == 15)
                        {
                            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == COMMAND SEND FAIL (작업 번호 '0' 으로 수신 - 15)");
                        }
                        else if (dataArray[10] == 16)
                        {
                            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == COMMAND SEND FAIL (랙, 스테이션 목적지 변경 작업번호 이상 - 16)");
                        }
                        else if (dataArray[10] == 17)
                        {
                            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == COMMAND SEND FAIL (동일한 작업번호 완료 상태인데 명령 수신 - 17)");
                        }
                        else if (dataArray[10] == 19)
                        {
                            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == COMMAND SEND FAIL (시작 ON 상태에서 목적지 변경 명령 수신 - 19)");
                        }
                        else
                        {
                            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == COMMAND SEND FAIL (" + dataArray[10] + ")");
                        }

                        if ((gClass.str.SrmPacket[srmNum].reqJobNoFk1 < 30000 && gClass.str.SrmPacket[srmNum].reqJobNoFk1 > 0) ||
                        (gClass.str.SrmPacket[srmNum].reqJobNoFk2 < 30000 && gClass.str.SrmPacket[srmNum].reqJobNoFk2 > 0))
                        {
                            gClass.str.SrmPacket[srmNum].recovError = true;
                        }

                        gClass.str.SrmPacket[srmNum].jobError = true;
                        gClass.str.SrmPacket[srmNum].gcpErrorCode = 67;            // 지상반 에러코드 확인
                        gClass.str.SrmPacket[srmNum].gcpSubCode = dataArray[10];   // 지상반 서브코드 확인
                        // FLT 67-XX : 반송 명령 실패 응답에 따른 상세 처리
                        cIniAccess.ChangeJobState(srmNum, JOBSTATE.STOP, "(SEND FAIL)");
                        return false;

                    }

                }

                if (gClass.str.SrmPacket[srmNum].recovError)
                {
                    gClass.str.SrmPacket[srmNum].gcpErrorCode = 00;            // 지상반 에러코드 확인
                    gClass.str.SrmPacket[srmNum].gcpSubCode = 00;              // 지상반 서브코드 확인
                }
                gClass.str.SrmPacket[srmNum].jobError = false;
                gClass.str.SrmPacket[srmNum].recovError = false;
                cIniAccess.ChangeJobState(srmNum, JOBSTATE.PEND);
                return true;
            }
            else
            {
                cIniAccess.SaveJobLog(srmNum, "작업스텝 - COMMAND SEND FAIL (수신 패킷 길이 이상)");
                //SaveLogFile("PacketError - 0x0041 Receive Data Length Failed");         // 리시브 데이터 길이 에러
                return false;
            }
        }

        unsafe private bool Rx_RequestManualCommand()          //  0x0080 수동명령 응답조회
        {
            if (gClass.str.SrmPacket[srmNum].recvStr.len >= cConstDefine.DATACOUNT_0X80)
            {
                ref byte[] dataArray = ref gClass.str.SrmPacket[srmNum].recvStr.data;   // 코드 가독성을 위해 ref 사용
                if (dataArray[0] != 0)         // Result
                {
                    SaveLogFile("PacketError - 0x0080 Manual Command Failed", false);         // 리시브 데이터 길이 에러
                    return false;
                }
                else
                {
                    if (gClass.str.SrmPacket[srmNum].manStop)
                    {
                        gClass.str.SrmPacket[srmNum].manStop = false;       // 제어성공
                    }
                    return true;
                }
            }
            else
            {
                SaveLogFile("PacketError - 0x0080 Manual Command Failed Length Failed", false);         // 리시브 데이터 길이 에러
                return false;
            }
        }

        unsafe private bool Rx_RequestRackSetting()          //  0x0092 렉 기본 설정 조회
        {
            if (gClass.str.SrmPacket[srmNum].recvStr.len >= cConstDefine.DATACOUNT_0X92)
            {
                ref byte[] dataArray = ref gClass.str.SrmPacket[srmNum].recvStr.data;   // 코드 가독성을 위해 ref 사용
                // offsetDown=dataArray[16]까지 접근하므로 최소 17바이트 필요(len>=18). 가드는 len>=17이라 len==17(16바이트)이면 [16]에서 IndexOutOfRange → 사전 차단
                if (dataArray == null || dataArray.Length < 17)
                {
                    SaveLogFile("PacketError - 0x0092 Receive Data Length Short", false);
                    return false;
                }
                if (dataArray[0] != 1)         // RACK Type  1: SRM, 2: RTV
                {
                    SaveLogFile("PacketError - 0x0092 RACK Type is Different", false);         // 리시브 데이터 길이 에러
                    return false;
                }

                gClass.str.SrmInfo[srmNum].bay = BitConverter.ToUInt16(dataArray, 1);   // Bay Count  1~256
                gClass.str.SrmInfo[srmNum].lev = BitConverter.ToUInt16(dataArray, 3);   // Lev Count  1~128
                gClass.str.SrmInfo[srmNum].row = dataArray[5];                          // 2: 싱글딥, 4: 더블딥, 6: 더블딥 3POS
                gClass.str.SrmInfo[srmNum].offsetUp = dataArray[15];                    // 상위치 오프셋
                gClass.str.SrmInfo[srmNum].offsetDown = dataArray[16];                  // 하위치 오프셋
                return true;
            }
            else
            {
                SaveLogFile("PacketError - 0x0092 Receive Data Length Failed", false);         // 리시브 데이터 길이 에러
                return false;
            }
        }

        unsafe private bool Rx_RequestCellSetting()          //  0x0094 셀설정 조회
        {
            if (gClass.str.SrmPacket[srmNum].recvStr.len >= cConstDefine.DATACOUNT_0X94)
            {
                ref byte[] dataArray = ref gClass.str.SrmPacket[srmNum].recvStr.data;   // 코드 가독성을 위해 ref 사용
                if (dataArray[0] != 1)         // RACK Type  1: SRM, 2: RTV
                {
                    SaveLogFile("PacketError - 0x0094 RACK Type is Different", false);         // 리시브 데이터 길이 에러
                    return false;
                }

                // [길이 가드] 잘린/조작 0x94 프레임에서 dataArray[5..7] 및 셀 인덱싱이 OOR 나지 않도록(수신 스레드 예외 방지)
                if (dataArray.Length < 8)
                {
                    SaveLogFile($"PacketError - 0x0094 data too short len={dataArray.Length}", false);
                    return false;
                }

                // Total Bay / Lev 저장
                gClass.str.SrmInfo[srmNum].bay = BitConverter.ToUInt16(dataArray, 1);   // Bay Count  1~256
                gClass.str.SrmInfo[srmNum].lev = BitConverter.ToUInt16(dataArray, 3);   // Lev Count  1~128
                Console.WriteLine("0x94 셀 설정 조회 : BAY CNT : " + gClass.str.SrmInfo[srmNum].bay + " LEV CNT : " + gClass.str.SrmInfo[srmNum].lev + " TYPE : " + dataArray[5] + " Start : " + dataArray[6] + " End : " + dataArray[7]);

                int idx = 0;                    // 배열 카운트 인덱스
                if (dataArray[5] == 1)          // 요청한 Data Type 1: BAY
                {
                    for (int i = dataArray[6]; i <= dataArray[7]; i++)          // Start -> End
                    {
                        int off = 8 + (idx * 4);
                        if (i < 0 || i >= gClass.str.SrmInfo[srmNum].cellBay.Length || off + 4 > dataArray.Length)
                        { SaveLogFile($"PacketError - 0x0094 BAY OOR i={i} off={off} len={dataArray.Length}", false); break; }
                        gClass.str.SrmInfo[srmNum].cellBay[i] = BitConverter.ToInt32(dataArray, off);
                        idx++;
                    }

                    // 자동으로 다음 렉정보 요청
                    gClass.str.SrmPacket[srmNum].rackRequest = true;
                    gClass.str.SrmPacket[srmNum].rackReqType = 2;
                    gClass.str.SrmPacket[srmNum].rackReqCount = 127;             // 요청갯수 구분
                }
                else if (dataArray[5] == 2)      // 요청한 Data Type 2: LEV
                {
                    for (int i = dataArray[6]; i <= dataArray[7]; i++)          // Start -> End
                    {
                        int off = 8 + (idx * 4);
                        if (i < 0 || i >= gClass.str.SrmInfo[srmNum].cellLev.Length || off + 4 > dataArray.Length)
                        { SaveLogFile($"PacketError - 0x0094 LEV OOR i={i} off={off} len={dataArray.Length}", false); break; }
                        gClass.str.SrmInfo[srmNum].cellLev[i] = BitConverter.ToInt32(dataArray, off);
                        idx++;
                    }
                }
                SaveRackData(dataArray[5], dataArray[6], dataArray[7]);         // Type, Start, End         Cell 설정 데이터 ini 저장
                return true;
            }
            else
            {
                SaveLogFile("PacketError - 0x0094 Receive Data Length Failed", false);         // 리시브 데이터 길이 에러
                return false;
            }
        }

        private bool Rx_CellPositionCtrlRes()          //  0x0095 셀 위치 설정 응답
        {
            ref byte[] dataArray = ref gClass.str.SrmPacket[srmNum].recvStr.data;
            int recvLen = gClass.str.SrmPacket[srmNum].recvStr.len - 1;  // len은 CMD2 포함이므로 -1이 실제 data 크기

            // 디버깅: raw 바이트 로그
            int dumpLen = Math.Min(recvLen, 16);
            string rawHex = "";
            for (int i = 0; i < dumpLen; i++)
                rawHex += $"{dataArray[i]:X2} ";
            SaveLogFile($"0x0095 RX raw len={recvLen} data=[{rawHex.TrimEnd()}]", false);

            // 응답 구조: RackType(1) + BayCount(2) + LevCount(2) + DataType(1) + StartNo(1) + EndNo(1) + CtrlResult(1) + NackReason(1) = 10
            if (recvLen >= 10)
            {
                byte rackType = dataArray[0];
                ushort bayCount = BitConverter.ToUInt16(dataArray, 1);
                ushort levCount = BitConverter.ToUInt16(dataArray, 3);
                byte dataType = dataArray[5];
                byte startNo = dataArray[6];
                byte endNo = dataArray[7];
                byte ctrlResult = dataArray[8];     // 0=ACK, 1=NACK
                byte nackReason = dataArray[9];

                SaveLogFile($"0x0095 parsed: RackType={rackType} BayCnt={bayCount} LevCnt={levCount} DataType={dataType} Start={startNo} End={endNo} CtrlResult={ctrlResult} NackReason={nackReason}", false);

                if (ctrlResult == 0x00)             // ACK (프로토콜: 0=ACK, 1=NACK)
                {
                    gClass.str.SrmPacket[srmNum].cellPosWriteDone = true;
                    gClass.str.SrmPacket[srmNum].cellPosWriteNack = false;
                    SaveLogFile("0x0095 CellPosition Write ACK", false);
                }
                else                                // NACK
                {
                    gClass.str.SrmPacket[srmNum].cellPosWriteDone = true;
                    gClass.str.SrmPacket[srmNum].cellPosWriteNack = true;
                    gClass.str.SrmPacket[srmNum].cellPosWriteNackReason = nackReason;
                    SaveLogFile($"0x0095 CellPosition Write NACK CtrlResult={ctrlResult} Reason={nackReason}", false);
                }
                return true;
            }
            SaveLogFile($"PacketError - 0x0095 Receive Data Length Failed (len={recvLen})", false);
            return false;
        }

        unsafe private bool Rx_RequestStationSetting()          //  0x0098 스테이션 설정 조회
        {
            if (gClass.str.SrmPacket[srmNum].recvStr.len >= cConstDefine.DATACOUNT_0X98)
            {
                ref byte[] dataArray = ref gClass.str.SrmPacket[srmNum].recvStr.data;   // 코드 가독성을 위해 ref 사용

                gClass.str.SrmInfo[srmNum].stn = dataArray[0];             // Station Count
                for (int i = 0; i < dataArray[0]; i++)
                {
                    // [경계검사] 수신 Station Count/오프셋이 배열·버퍼를 넘으면 중단(수신 스레드 OOR 예외 방지)
                    if (i >= gClass.str.SrmInfo[srmNum].SrmStation.Length || 32 + (i * 20) >= dataArray.Length)
                    { SaveLogFile($"PacketError - 0x0098 OOR i={i} len={dataArray.Length}", false); break; }
                    gClass.str.SrmInfo[srmNum].SrmStation[i].stnType = dataArray[18 + (i * 20)];   // Station Byte Count 20
                    gClass.str.SrmInfo[srmNum].SrmStation[i].goodType = dataArray[19 + (i * 20)];   // 1
                    gClass.str.SrmInfo[srmNum].SrmStation[i].travPos = BitConverter.ToInt32(dataArray, 20 + (i * 20));      // 4               
                    gClass.str.SrmInfo[srmNum].SrmStation[i].liftPos = BitConverter.ToInt32(dataArray, 24 + (i * 20));      // 4
                    gClass.str.SrmInfo[srmNum].SrmStation[i].forkPos = BitConverter.ToInt16(dataArray, 28 + (i * 20));      // 2
                    gClass.str.SrmInfo[srmNum].SrmStation[i].upOffset = dataArray[30 + (i * 20)];
                    gClass.str.SrmInfo[srmNum].SrmStation[i].downOffset = dataArray[31 + (i * 20)];
                    gClass.str.SrmInfo[srmNum].SrmStation[i].intNum = dataArray[32 + (i * 20)];
                }
                SaveStationData();         // 설정 데이터 ini 저장
                return true;
            }
            else
            {
                SaveLogFile("PacketError - 0x0098 Receive Data Length Failed", false);         // 리시브 데이터 길이 에러
                return false;
            }
        }

        unsafe private bool Rx_RequestProhSetting()          //  0x009C 금지렉설정 조회
        {
            if (gClass.str.SrmPacket[srmNum].recvStr.len >= cConstDefine.DATACOUNT_0X9C)
            {
                ref byte[] dataArray = ref gClass.str.SrmPacket[srmNum].recvStr.data;   // 코드 가독성을 위해 ref 사용

                if (dataArray.Length < 1)
                { SaveLogFile($"PacketError - 0x009C data too short len={dataArray.Length}", false); return false; }
                for (int i = 0; i < dataArray[0]; i++)
                {
                    // [경계검사] 수신 Count/오프셋이 prohRack(string[N])·버퍼를 넘으면 중단(수신 스레드 OOR 예외 방지)
                    if (i >= gClass.str.SrmInfo[srmNum].prohRack.Length || i * 5 + 5 >= dataArray.Length)
                    { SaveLogFile($"PacketError - 0x009C OOR i={i} len={dataArray.Length}", false); break; }
                    gClass.str.SrmInfo[srmNum].prohRack[i] = dataArray[i * 5 + 2].ToString() + "-" + BitConverter.ToInt16(dataArray, i * 5 + 3).ToString() + "-" + dataArray[i * 5 + 5].ToString();
                }

                SaveProhRackData(dataArray[0]);         // 설정 데이터 ini 저장
                return true;
            }
            else
            {
                SaveLogFile("PacketError - 0x009C Receive Data Length Failed", false);         // 리시브 데이터 길이 에러
                return false;
            }
        }

        unsafe private bool Rx_RequestForkSetting()          //  0x00A7 포크 설정 조회
        {
            if (gClass.str.SrmPacket[srmNum].recvStr.len >= cConstDefine.DATACOUNT_0XA7)
            {
                ref byte[] dataArray = ref gClass.str.SrmPacket[srmNum].recvStr.data;   // 코드 가독성을 위해 ref 사용

                // 포크 포지션 데이터만 받아처리
                gClass.str.SrmInfo[srmNum].forkLeftLimit = BitConverter.ToInt32(dataArray, 132);        // sw limit
                gClass.str.SrmInfo[srmNum].forkRightLimit = BitConverter.ToInt32(dataArray, 136);

                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + srmNum + "\\ForkInfo.ini", "FORKLIMIT", "LEFT", gClass.str.SrmInfo[srmNum].forkLeftLimit.ToString());
                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\SRM" + srmNum + "\\ForkInfo.ini", "FORKLIMIT", "RIGHT", gClass.str.SrmInfo[srmNum].forkRightLimit.ToString());
                return true;
            }
            else
            {
                SaveLogFile("PacketError - 0x00A7 Receive Data Length Failed", false);         // 리시브 데이터 길이 에러
                return false;
            }
        }

        unsafe private bool Rx_RequestState()          //  0x0030 상태조회
        {

            if (gClass.str.SrmPacket[srmNum].recvStr.len -1 >= cConstDefine.DATACOUNT_0X30)
            {
                ref byte[] dataArray = ref gClass.str.SrmPacket[srmNum].recvStr.data;    // 코드 가독성을 위해 ref 사용
                ref Srm_State refState = ref gClass.str.SrmState[srmNum];

                //fixed (Srm_State* fixedDataPtr = &refState)
                //{
                string tmpChar = string.Format("{0:X2}", dataArray[2]);
                refState.protocolVer = "Ver" + tmpChar[0] + "." + tmpChar[1];        // Protocol Version

                //Marshal.Copy(dataArray, 3, (IntPtr)fixedDataPtr->firmwareVer, 4);        // Firmware Version - Ver
                tmpChar = string.Format("{0:X2}", dataArray[3]);
                refState.firmwareVer = "Ver" + tmpChar[0] + "." + tmpChar[1] + "-" + string.Format("{0:X2}{1:X2}{2:X2}", dataArray[4], dataArray[5], dataArray[6]);
                //refState.buildYear = dataArray[4];          // Firmware Version - Year
                //refState.buildMonth = dataArray[5];         // Firmware Version - Month
                //refState.buildDay = dataArray[6];           // Firmware Version - Day

                refState.utcTime = BitConverter.ToUInt32(dataArray, 7);     // System DateTime

                // Encoding.BigEndianUnicode.GetString()    빅엔디언일 경우 처리
                refState.projNo = System.Text.Encoding.ASCII.GetString(dataArray, 11, 6);       // Project No
                refState.groupNo = dataArray[17];                           // Group No
                refState.srmNo = BitConverter.ToUInt16(dataArray, 18);      // Srm No
                refState.srmType = BitConverter.ToUInt16(dataArray, 20);    // Srm Type
                refState.gcpState.gcpRxMode = dataArray[22];              // Gcp Mode - Return

                refState.gcpState.heartBeat = (byte)((dataArray[23] >> 3) & 0x01);         // Gcp Heartbeat     
                refState.gcpState.safetyPlug = (dataArray[23] & 0x01) != 0;     // Gcp SafetyPlug    true:해제
                refState.gcpState.faultReset = (dataArray[23] & 0x04) != 0;     // Gcp FaultReset    true:눌림
                refState.gcpState.emStop = (dataArray[23] & 0x02) != 0;         // Gcp EmStop        true:눌림

                for (int i = 0; i < 8; i++)
                {
                    refState.CVOK[i] = dataArray[24 + i];                             // CVOK
                    refState.CVNO[i] = dataArray[32 + i];                             // CVNO
                }

                //refState.devMode = dataArray[40];                                   // Device Mode
                refState.setupMode = (byte)((dataArray[40] & 0x08) >> 3);
                refState.forcedMode = (byte)((dataArray[40] & 0x04) >> 2);
                refState.manualMode = (byte)((dataArray[40] & 0x02) >> 1);
                refState.autoMode = (byte)(dataArray[40] & 0x01);

                //refState.devState1 = dataArray[41];                                 // Dev State 1
                refState.dSt1ReqCmd = (byte)((dataArray[41] & 0x20) >> 5);
                if (gClass.str.SrmPacket[srmNum].jobRequestOld != refState.dSt1ReqCmd)
                {
                    gClass.str.SrmPacket[srmNum].jobRequestOld = refState.dSt1ReqCmd;
                    if (refState.dSt1ReqCmd > 0)
                    {
                        cIniAccess.SaveJobLog(srmNum, "SRM -> GCP == 작업요구비트 ON - " + Enum.GetName(typeof(JOBSTATE), gClass.str.SrmPacket[srmNum].jobState));
                    }
                    else
                    {
                        cIniAccess.SaveJobLog(srmNum, "SRM -> GCP == 작업요구비트 OFF - " + Enum.GetName(typeof(JOBSTATE), gClass.str.SrmPacket[srmNum].jobState));
                    }
                }
                refState.dSt1InvConn = (byte)((dataArray[41] & 0x10) >> 4);
                refState.dSt1Abnormal = (byte)((dataArray[41] & 0x08) >> 3);
                refState.dSt1Warning = (byte)((dataArray[41] & 0x04) >> 2);
                refState.dSt1EmStop = (byte)((dataArray[41] & 0x02) >> 1);
                refState.dSt1StartSt = (byte)(dataArray[41] & 0x01);

                //refState.devState2 = dataArray[42];                                 // Dev State 2
                refState.dSt2EmSwitch = (byte)((dataArray[42] & 0x80) >> 7);
                refState.dSt2ManAutoSw = (byte)((dataArray[42] & 0x40) >> 6);       // 0 자동, 1 수동
                refState.dSt2maintPos = (byte)((dataArray[42] & 0x02) >> 1);
                refState.dSt2homePos = (byte)(dataArray[42] & 0x01);

                refState.operCode = dataArray[43];                                  // Operation Code
                refState.errcodeH = dataArray[44];                                  // ErrCode H
                refState.errcodeM = dataArray[45];                                  // ErrCode M
                refState.errcodeL = BitConverter.ToUInt16(dataArray, 46);           // ErrCode L

                // Fork 1 Position Info
                refState.fork1.curStation = dataArray[48];
                refState.fork1.curBay = BitConverter.ToUInt16(dataArray, 49);
                refState.fork1.curLev = dataArray[51];
                refState.fork1.curPosNum = (sbyte)dataArray[52];

                //refState.fork1.curPos1 = dataArray[53];                             // 포크1 기준 주행/승강 정위치
                refState.fork1.posRightBottom = (byte)((dataArray[53] & 0x20) >> 5);
                refState.fork1.posRightUp = (byte)((dataArray[53] & 0x10) >> 4);
                refState.fork1.posRightTravExac = (byte)((dataArray[53] & 0x08) >> 3);
                refState.fork1.posLeftBottom = (byte)((dataArray[53] & 0x04) >> 2);
                refState.fork1.posLeftUp = (byte)((dataArray[53] & 0x02) >> 1);
                refState.fork1.posLeftTravExac = (byte)(dataArray[53] & 0x01);

                //refState.fork1.curPos2 = dataArray[54];
                refState.fork1.posRightExac3 = (byte)((dataArray[54] & 0x40) >> 6);
                refState.fork1.posRightExac2 = (byte)((dataArray[54] & 0x20) >> 5);
                refState.fork1.posRightExac1 = (byte)((dataArray[54] & 0x10) >> 4);
                refState.fork1.posLeftExac3 = (byte)((dataArray[54] & 0x08) >> 3);
                refState.fork1.posLeftExac2 = (byte)((dataArray[54] & 0x04) >> 2);
                refState.fork1.posLeftExac1 = (byte)((dataArray[54] & 0x02) >> 1);
                refState.fork1.posCenterExac = (byte)(dataArray[54] & 0x01);

                // reserved 2  55,56

                // Fork 2 Position Info
                refState.fork2.curStation = dataArray[57];
                refState.fork2.curBay = BitConverter.ToUInt16(dataArray, 58);
                refState.fork2.curLev = dataArray[60];
                refState.fork2.curPosNum = (sbyte)dataArray[61];

                //refState.fork2.curPos1 = dataArray[62];                             // 포크2 기준 주행/승강 정위치
                refState.fork2.posRightBottom = (byte)((dataArray[62] & 0x20) >> 5);
                refState.fork2.posRightUp = (byte)((dataArray[62] & 0x10) >> 4);
                refState.fork2.posRightTravExac = (byte)((dataArray[62] & 0x08) >> 3);
                refState.fork2.posLeftBottom = (byte)((dataArray[62] & 0x04) >> 2);
                refState.fork2.posLeftUp = (byte)((dataArray[62] & 0x02) >> 1);
                refState.fork2.posLeftTravExac = (byte)(dataArray[62] & 0x01);

                //refState.fork2.curPos2 = dataArray[63];
                refState.fork2.posRightExac3 = (byte)((dataArray[63] & 0x40) >> 6);
                refState.fork2.posRightExac2 = (byte)((dataArray[63] & 0x20) >> 5);
                refState.fork2.posRightExac1 = (byte)((dataArray[63] & 0x10) >> 4);
                refState.fork2.posLeftExac3 = (byte)((dataArray[63] & 0x08) >> 3);
                refState.fork2.posLeftExac2 = (byte)((dataArray[63] & 0x04) >> 2);
                refState.fork2.posLeftExac1 = (byte)((dataArray[63] & 0x02) >> 1);
                refState.fork2.posCenterExac = (byte)(dataArray[63] & 0x01);

                // reserver 2 64,65

                refState.fork1.targetStation = dataArray[66];                       // 포크 1 목적 R/B/L
                refState.fork1.targetRow = dataArray[67];
                refState.fork1.targetBay = BitConverter.ToUInt16(dataArray, 68);       // 2
                refState.fork1.targetLev = dataArray[70];

                //reserved 2    71-72

                refState.fork2.targetStation = dataArray[73];                       // 포크 2 목적 R/B/L
                refState.fork2.targetRow = dataArray[74];
                refState.fork2.targetBay = BitConverter.ToUInt16(dataArray, 75);      // 2
                refState.fork2.targetLev = dataArray[77];

                // reserved 2   78-79
                // reserved 11  80,81,82,83,84,85,86.87,88,89,90
                //refState.trav.state1 = dataArray[90];                               // 장치 상태 - 주행

                refState.trav.homeMove = (byte)((dataArray[91] & 0x20) >> 5);
                refState.trav.trSt1OriginPos = (byte)((dataArray[91] & 0x10) >> 4);
                refState.trav.trSt1MoveDirec = (byte)((dataArray[91] & 0x08) >> 3);
                refState.trav.trSt1DecState = (byte)((dataArray[91] & 0x04) >> 2);
                refState.trav.trSt1AccState = (byte)((dataArray[91] & 0x02) >> 1);
                refState.trav.trSt1OperState = (byte)(dataArray[91] & 0x01);

                //refState.trav.state2 = dataArray[91];
                refState.trav.trSt2LoadTunn = (byte)((dataArray[92] & 0x10) >> 4);
                refState.trav.trSt2NoLoadTunn = (byte)((dataArray[92] & 0x08) >> 3);
                refState.trav.trSt2HomeCheck = (byte)((dataArray[92] & 0x04) >> 2);
                refState.trav.trSt2InvAlarmSt = (byte)((dataArray[92] & 0x02) >> 1);
                refState.trav.trSt2InvConnSt = (byte)(dataArray[92] & 0x01);

                refState.trav.fwDecNo = dataArray[93];
                refState.trav.bwDecNo = dataArray[94];
                refState.trav.curPos = BitConverter.ToInt32(dataArray, 95); // 4    // 현재위치
                refState.trav.curSpd = BitConverter.ToInt16(dataArray, 99); // 2
                refState.trav.targetPos = BitConverter.ToInt32(dataArray, 101); // 4
                refState.trav.targetSpd = BitConverter.ToInt16(dataArray, 105); // 2

                //refState.lift.state1 = dataArray[106];                              // 장치 상태 - 승강
                refState.lift.homeMove = (byte)((dataArray[107] & 0x20) >> 5);     // 홈 복귀 중
                refState.lift.liSt1OriginPos = (byte)((dataArray[107] & 0x10) >> 4);
                refState.lift.liSt1MoveDirec = (byte)((dataArray[107] & 0x08) >> 3);
                refState.lift.liSt1DecState = (byte)((dataArray[107] & 0x04) >> 2);
                refState.lift.liSt1AccState = (byte)((dataArray[107] & 0x02) >> 1);
                refState.lift.liSt1OperState = (byte)(dataArray[107] & 0x01);


                //refState.lift.state2 = dataArray[107];
                refState.lift.liSt2LoadTunn = (byte)((dataArray[108] & 0x10) >> 4);
                refState.lift.liSt2NoLoadTunn = (byte)((dataArray[108] & 0x08) >> 3);
                refState.lift.liSt2HomeCheck = (byte)((dataArray[108] & 0x04) >> 2);
                refState.lift.liSt2InvAlarmSt = (byte)((dataArray[108] & 0x02) >> 1);
                refState.lift.liSt2InvConnSt = (byte)(dataArray[108] & 0x01);

                refState.lift.upDecNo = dataArray[109];
                refState.lift.dnDecNo = dataArray[110];
                refState.lift.curPos = BitConverter.ToInt32(dataArray, 111); // 4   // 현재위치
                refState.lift.curSpd = BitConverter.ToInt16(dataArray, 115); // 2
                refState.lift.targetPos = BitConverter.ToInt32(dataArray, 117); // 4
                refState.lift.targetSpd = BitConverter.ToInt16(dataArray, 121); // 2

                //refState.fork1.state1 = dataArray[122];
                refState.fork1.forkRightEnable = (byte)((dataArray[123] & 0x80) >> 7);
                refState.fork1.forkLeftEnable = (byte)((dataArray[123] & 0x40) >> 6);
                refState.fork1.loadState = (byte)((dataArray[123] & 0x20) >> 5);
                refState.fork1.originPos = (byte)((dataArray[123] & 0x10) >> 4);
                refState.fork1.moveDirec = (byte)((dataArray[123] & 0x08) >> 3);
                refState.fork1.decState = (byte)((dataArray[123] & 0x04) >> 2);
                refState.fork1.accState = (byte)((dataArray[123] & 0x02) >> 1);
                refState.fork1.operState = (byte)(dataArray[123] & 0x01);

                //refState.fork1.state2 = dataArray[123];
                refState.fork1.loadTunn = (byte)((dataArray[124] & 0x10) >> 4);
                refState.fork1.noLoadTunn = (byte)((dataArray[124] & 0x08) >> 3);
                refState.fork1.homeCheck = (byte)((dataArray[124] & 0x04) >> 2);
                refState.fork1.invAlarmSt = (byte)((dataArray[124] & 0x02) >> 1);
                refState.fork1.invConnSt = (byte)(dataArray[124] & 0x01);

                refState.fork1.loadType = dataArray[125];

                // reserved 1   126
                refState.fork1.curPos = BitConverter.ToInt32(dataArray, 127); // 4
                refState.fork1.curSpd = BitConverter.ToInt16(dataArray, 131); // 2
                refState.fork1.targetPos = BitConverter.ToInt32(dataArray, 133); // 4
                refState.fork1.targetSpd = BitConverter.ToInt16(dataArray, 137); // 2

                //refState.fork2.state1 = dataArray[138];
                refState.fork2.forkRightEnable = (byte)((dataArray[139] & 0x80) >> 7);
                refState.fork2.forkLeftEnable = (byte)((dataArray[139] & 0x40) >> 6);
                refState.fork2.loadState = (byte)((dataArray[139] & 0x20) >> 5);
                refState.fork2.originPos = (byte)((dataArray[139] & 0x10) >> 4);
                refState.fork2.moveDirec = (byte)((dataArray[139] & 0x08) >> 3);
                refState.fork2.decState = (byte)((dataArray[139] & 0x04) >> 2);
                refState.fork2.accState = (byte)((dataArray[139] & 0x02) >> 1);
                refState.fork2.operState = (byte)(dataArray[139] & 0x01);

                //refState.fork2.state2 = dataArray[139];
                refState.fork2.loadTunn = (byte)((dataArray[140] & 0x10) >> 4);
                refState.fork2.noLoadTunn = (byte)((dataArray[140] & 0x08) >> 3);
                refState.fork2.homeCheck = (byte)((dataArray[140] & 0x04) >> 2);
                refState.fork2.invAlarmSt = (byte)((dataArray[140] & 0x02) >> 1);
                refState.fork2.invConnSt = (byte)(dataArray[140] & 0x01);

                refState.fork2.loadType = dataArray[141];

                // reserved 1   142
                refState.fork2.curPos = BitConverter.ToInt32(dataArray, 143); // 4
                refState.fork2.curSpd = BitConverter.ToInt16(dataArray, 147); // 2
                refState.fork2.targetPos = BitConverter.ToInt32(dataArray, 149); // 4
                refState.fork2.targetSpd = BitConverter.ToInt16(dataArray, 153); // 2

                // FORK1 작업정보 - 반송명령
                refState.fork1.jobNo = BitConverter.ToUInt32(dataArray, 155); // 4
                refState.fork1.taskIdx = dataArray[159];
                refState.fork1.fromStation = dataArray[160];
                refState.fork1.fromRow = dataArray[161];
                refState.fork1.fromBay = BitConverter.ToUInt16(dataArray, 162); // 2
                refState.fork1.fromLev = dataArray[164];
                refState.fork1.toStation = dataArray[165];
                refState.fork1.toRow = dataArray[166];
                refState.fork1.toBay = BitConverter.ToUInt16(dataArray, 167); // 2
                refState.fork1.toLev = dataArray[169];

                refState.fork1.cmdCode = dataArray[170];
                refState.fork1.procState = dataArray[171];
                refState.fork1.procStep = dataArray[172];

                // 작업정보 - 이동명령
                refState.fork1.mvJobNo = BitConverter.ToUInt32(dataArray, 173); // 4
                refState.fork1.mvToStation = dataArray[177];
                refState.fork1.mvToRow = dataArray[178];
                refState.fork1.mvToBay = BitConverter.ToUInt16(dataArray, 179); // 2
                refState.fork1.mvToLev = dataArray[181];

                refState.fork1.mvProcState = dataArray[182];
                refState.fork1.mvProcStep = dataArray[183];


                // FORK2 작업정보 - 반송명령
                refState.fork2.jobNo = BitConverter.ToUInt32(dataArray, 184); // 4
                refState.fork2.taskIdx = dataArray[188];
                refState.fork2.fromStation = dataArray[189];
                refState.fork2.fromRow = dataArray[190];
                refState.fork2.fromBay = BitConverter.ToUInt16(dataArray, 191); // 2
                refState.fork2.fromLev = dataArray[193];
                refState.fork2.toStation = dataArray[194];
                refState.fork2.toRow = dataArray[195];
                refState.fork2.toBay = BitConverter.ToUInt16(dataArray, 196); // 2
                refState.fork2.toLev = dataArray[198];

                refState.fork2.cmdCode = dataArray[199];
                refState.fork2.procState = dataArray[200];
                refState.fork2.procStep = dataArray[201];

                // 작업정보 - 이동명령
                refState.fork2.mvJobNo = BitConverter.ToUInt32(dataArray, 202); // 4
                refState.fork2.mvToStation = dataArray[206];
                refState.fork2.mvToRow = dataArray[207];
                refState.fork2.mvToBay = BitConverter.ToUInt16(dataArray, 208); // 2
                refState.fork2.mvToLev = dataArray[210];

                refState.fork2.mvProcState = dataArray[211];
                refState.fork2.mvProcStep = dataArray[212];

                // Task 작업정보    226 Bytes

                // Digital I/O Input 
                for (int i = 0; i < 16; i++)
                {
                    refState.dInput[i] = dataArray[439 + i];
                }
                // reserved 4
                // Digital I/ O Output
                for (int i = 0; i < 5; i++)
                {
                    refState.dOutput[i] = dataArray[459 + i];
                }
                // reserved 4 464~467
                // 출력 수동제어 모드 5 = 468~472
                // 인버터 에러코드 473
                if ((int)gClass.str.SrmPacket[srmNum].recvStr.len - 1 > 479)
                {
                    refState.invErrorTravMain = dataArray[473];
                    refState.invErrorTravSub = dataArray[474];
                    refState.invErrorLiftMain = dataArray[475];
                    refState.invErrorLiftSub = dataArray[476];
                    refState.invErrorFork1Main = dataArray[477];
                    refState.invErrorFork1Sub = dataArray[478];
                    refState.invErrorFork2Main = dataArray[479];
                    refState.invErrorFork2Sub = dataArray[480];
                }

                // reserved 12 481~491

                if ((int)gClass.str.SrmPacket[srmNum].recvStr.len - 1 > 492)
                {
                    int offset = 492;

                    refState.extState.travSetSpd = BitConverter.ToInt32(dataArray, offset); offset += 4;
                    refState.extState.liftSetSpd = BitConverter.ToInt32(dataArray, offset); offset += 4;
                    refState.extState.fork1SetSpd = BitConverter.ToInt32(dataArray, offset); offset += 4;
                    refState.extState.fork2SetSpd = BitConverter.ToInt32(dataArray, offset); offset += 4;

                    refState.extState.travSetAcc = BitConverter.ToInt32(dataArray, offset); offset += 4;
                    refState.extState.liftSetAcc = BitConverter.ToInt32(dataArray, offset); offset += 4;
                    refState.extState.fork1SetAcc = BitConverter.ToInt32(dataArray, offset); offset += 4;
                    refState.extState.fork2SetAcc = BitConverter.ToInt32(dataArray, offset); offset += 4;

                    refState.extState.travSetDec = BitConverter.ToInt32(dataArray, offset); offset += 4;
                    refState.extState.liftSetDec = BitConverter.ToInt32(dataArray, offset); offset += 4;
                    refState.extState.fork1SetDec = BitConverter.ToInt32(dataArray, offset); offset += 4;
                    refState.extState.fork2SetDec = BitConverter.ToInt32(dataArray, offset); offset += 4;

                    refState.extState.travSetJerk = BitConverter.ToInt32(dataArray, offset); offset += 4;
                    refState.extState.liftSetJerk = BitConverter.ToInt32(dataArray, offset); offset += 4;
                    refState.extState.fork1SetJerk = BitConverter.ToInt32(dataArray, offset); offset += 4;
                    refState.extState.fork2SetJerk = BitConverter.ToInt32(dataArray, offset); offset += 4;

                    refState.extState.preLoadMoveDelay = BitConverter.ToInt16(dataArray, offset); offset += 2;
                    refState.extState.postLoadMoveDelay = BitConverter.ToInt16(dataArray, offset); offset += 2;
                    refState.extState.preLoadForkExtendDelay = BitConverter.ToInt16(dataArray, offset); offset += 2;
                    refState.extState.postLoadForkExtendDelay = BitConverter.ToInt16(dataArray, offset); offset += 2;
                    refState.extState.preLoadForkLiftDelay = BitConverter.ToInt16(dataArray, offset); offset += 2;
                    refState.extState.postLoadForkLiftDelay = BitConverter.ToInt16(dataArray, offset); offset += 2;
                    refState.extState.preLoadForkRetractDelay = BitConverter.ToInt16(dataArray, offset); offset += 2;
                    refState.extState.postLoadForkRetractDelay = BitConverter.ToInt16(dataArray, offset); offset += 2;

                    refState.extState.preUnloadMoveDelay = BitConverter.ToInt16(dataArray, offset); offset += 2;
                    refState.extState.postUnloadMoveDelay = BitConverter.ToInt16(dataArray, offset); offset += 2;
                    refState.extState.preUnloadForkExtendDelay = BitConverter.ToInt16(dataArray, offset); offset += 2;
                    refState.extState.postUnloadForkExtendDelay = BitConverter.ToInt16(dataArray, offset); offset += 2;
                    refState.extState.preUnloadForkLiftDelay = BitConverter.ToInt16(dataArray, offset); offset += 2;
                    refState.extState.postUnloadForkLiftDelay = BitConverter.ToInt16(dataArray, offset); offset += 2;
                    refState.extState.preUnloadForkRetractDelay = BitConverter.ToInt16(dataArray, offset); offset += 2;
                    refState.extState.postUnloadForkRetractDelay = BitConverter.ToInt16(dataArray, offset); offset += 2;

                    refState.extState.travMotorTorque = BitConverter.ToInt32(dataArray, offset); offset += 4;
                    refState.extState.liftMotorTorque = BitConverter.ToInt32(dataArray, offset); offset += 4;
                    refState.extState.fork1MotorTorque = BitConverter.ToInt32(dataArray, offset); offset += 4;
                    refState.extState.fork2MotorTorque = BitConverter.ToInt32(dataArray, offset); offset += 4;

                    refState.extState.totalOperationTime = BitConverter.ToInt32(dataArray, offset); offset += 4;
                    refState.extState.travOperationTime = BitConverter.ToInt32(dataArray, offset); offset += 4;
                    refState.extState.liftOperationTime = BitConverter.ToInt32(dataArray, offset); offset += 4;
                    refState.extState.fork1OperationTime = BitConverter.ToInt32(dataArray, offset); offset += 4;
                    refState.extState.fork2OperationTime = BitConverter.ToInt32(dataArray, offset); offset += 4;

                    refState.extState.travBrakeOpenCount = BitConverter.ToInt32(dataArray, offset); offset += 4;
                    refState.extState.liftBrakeOpenCount = BitConverter.ToInt32(dataArray, offset); offset += 4;
                    refState.extState.fork1BrakeOpenCount = BitConverter.ToInt32(dataArray, offset); offset += 4;
                    refState.extState.fork2BrakeOpenCount = BitConverter.ToInt32(dataArray, offset); offset += 4;
                }


                Srm_StateParse();           // SRM 상태정보 수신 후 - TOWCS를 위한 요구상태/동작상태 갱신
                Srm_InOutParse();           // I/O 센서상태 파싱

                //}

                //long utcTimeInSeconds = 1623586500; // Example UTC time in seconds
                //DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(utcTimeInSeconds);
                //DateTime dateTimeUtc = dateTimeOffset.UtcDateTime;
                //Console.WriteLine(dateTimeUtc);  // Output: 2021-06-13 10:55:00 AM
                return true;
            }
            else
            {
                SaveLogFile("PacketError - 0x0030 Receive Data Length Failed", false);             // 리시브 데이터 길이 에러
                return false;
            }
        }

        private bool Rx_ResponseCycleStop()
        {
            if (gClass.str.SrmPacket[srmNum].recvStr.data != null && gClass.str.SrmPacket[srmNum].recvStr.data.Length >= 1)
            {
                if (gClass.str.SrmPacket[srmNum].recvStr.data[0] != 0)
                {
                    SaveLogFile("PacketError - 0x0054 CycleStop Command Failed", false);
                    return false;
                }
                if (gClass.str.SrmPacket[srmNum].wcsCmdCycleStop)
                    gClass.str.SrmPacket[srmNum].wcsCmdCycleStop = false;
                return true;
            }
            SaveLogFile("PacketError - 0x0054 CycleStop Command Length Failed", false);
            return false;
        }

        private bool Rx_ResponseEmStop()
        {
            if (gClass.str.SrmPacket[srmNum].recvStr.data != null && gClass.str.SrmPacket[srmNum].recvStr.data.Length >= 1)
            {
                if (gClass.str.SrmPacket[srmNum].recvStr.data[0] != 0)
                {
                    SaveLogFile("PacketError - 0x0055 Em Stop Command Failed", false);
                    return false;
                }
                if (gClass.str.SrmPacket[srmNum].wcsCmdEmergencyStop)
                    gClass.str.SrmPacket[srmNum].wcsCmdEmergencyStop = false;
                return true;
            }
            SaveLogFile("PacketError - 0x0055 Em Stop Command Length Failed", false);
            return false;
        }

        // SRM 변경상태 갱신 파싱
        public void Srm_StateParse()
        {
            gClass.str.SrmPacket[srmNum].startEnable = gClass.str.SrmState[srmNum].dSt1StartSt == 0 && (gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.SEMI_AUTO].value || gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.AUTO].value) && gClass.str.SrmState[srmNum].autoMode > 0 && !gClass.str.SrmPacket[srmNum].gcpError;


            // 동작 중 X  && 수동 정지 명령 중  -> 명령해제
            if (gClass.str.SrmPacket[srmNum].manStop && gClass.str.SrmState[srmNum].trav.trSt1OperState == 0
                && gClass.str.SrmState[srmNum].lift.liSt1OperState == 0 && gClass.str.SrmState[srmNum].fork1.operState == 0
                && gClass.str.SrmState[srmNum].fork2.operState == 0)
            {
                //gClass.str.SrmPacket[srmNum].manStop = false;
            }

            // WCS 홈 복귀 명령 클리어 조건 - 홈 복귀 동작 중 이거나 홈 위치 일 경우 cmd 해제
            if (gClass.str.SrmPacket[srmNum].wcsCmdHomeReturn && (gClass.str.SrmState[srmNum].trav.homeMove > 0 || gClass.str.SrmState[srmNum].lift.homeMove > 0) || gClass.str.SrmState[srmNum].dSt2homePos > 0)
            {
                gClass.str.SrmPacket[srmNum].wcsCmdHomeReturn = false;
            }

            // 자동 작업삭제 클리어 조건
            if (gClass.str.SrmPacket[srmNum].autoJobDelete && gClass.str.SrmState[srmNum].fork1.jobNo == 0
                && gClass.str.SrmState[srmNum].fork2.jobNo == 0 && gClass.str.SrmState[srmNum].fork1.mvJobNo == 0 && gClass.str.SrmState[srmNum].fork2.mvJobNo == 0)
            {
                gClass.str.SrmPacket[srmNum].autoJobDelete = false;
                cIniAccess.SaveJobLog(srmNum, "GCP == 자동삭제 - 작업삭제 완료");

                cIniAccess.ChangeJobState(srmNum, JOBSTATE.NONE);
            }


            // 수동버튼 작업삭제 클리어 조건
            if (gClass.str.SrmPacket[srmNum].manuFork1JobDelete > 0 && gClass.str.SrmState[srmNum].fork1.jobNo == 0 && gClass.str.SrmState[srmNum].fork1.mvJobNo == 0)
            {
                cIniAccess.Write($"{AppDomain.CurrentDomain.BaseDirectory}\\SRM{srmNum.ToString()}\\JobInfo.ini", "FORK1", "JOB_STEP", "1");
                if (gClass.str.SrmPacket[srmNum].reqJobNoFk1 < 30000)
                {
                    int reqJobNoFk1 = (int)gClass.str.SrmPacket[srmNum].reqJobNoFk1;
                }
                cIniAccess.SaveJobLog(srmNum, "GCP == Fork1 수동작업삭제 완료");
                cIniAccess.ChangeJobState(srmNum, JOBSTATE.NONE, "(Fork1 DELETE MAN)");
                gClass.str.SrmPacket[srmNum].manuFork1JobDelete = 0;
            }
            if (gClass.str.SrmPacket[srmNum].manuFork2JobDelete > 0 && gClass.str.SrmState[srmNum].fork2.jobNo == 0U && gClass.str.SrmState[srmNum].fork2.mvJobNo == 0U)
            {
                cIniAccess.Write($"{AppDomain.CurrentDomain.BaseDirectory}\\SRM{srmNum.ToString()}\\JobInfo.ini", "FORK2", "JOB_STEP", "1");
                cIniAccess.SaveJobLog(srmNum, "GCP == Fork2 수동작업삭제 완료");
                cIniAccess.ChangeJobState(srmNum, JOBSTATE.NONE, "(Fork2 DELETE MAN)");
                gClass.str.SrmPacket[srmNum].manuFork2JobDelete = 0;
            }

            if (gClass.str.SrmPacket[srmNum].manuFork1JobComplete > 0 && gClass.str.SrmState[srmNum].fork1.jobNo == 0U && gClass.str.SrmState[srmNum].fork1.mvJobNo == 0U)
            {
                cIniAccess.SaveJobLog(srmNum, "GCP == Fork1 수동 작업완료");
                cIniAccess.SaveJobLog(srmNum, "GCP == 포크1 작업완료 - " + gClass.str.SrmPacket[srmNum].reqJobNoFk1.ToString());
                cIniAccess.Write($"{AppDomain.CurrentDomain.BaseDirectory}\\SRM{srmNum.ToString()}\\JobInfo.ini", "FORK1", "JOB_STEP", "1");
                cIniAccess.ChangeJobState(srmNum, JOBSTATE.NONE, "(Fork1 COMPLETE MAN)");
                gClass.str.SrmPacket[srmNum].manuFork1JobComplete = 0;
            }
            if (gClass.str.SrmPacket[srmNum].manuFork2JobComplete > 0 && gClass.str.SrmState[srmNum].fork2.jobNo == 0U && gClass.str.SrmState[srmNum].fork2.mvJobNo == 0U)
            {
                cIniAccess.SaveJobLog(srmNum, "GCP == Fork2 수동 작업완료");
                cIniAccess.SaveJobLog(srmNum, "GCP == 포크2 작업완료 - " + gClass.str.SrmPacket[srmNum].reqJobNoFk2.ToString());
                cIniAccess.Write($"{AppDomain.CurrentDomain.BaseDirectory}\\SRM{srmNum.ToString()}\\JobInfo.ini", "FORK2", "JOB_STEP", "1");
                cIniAccess.ChangeJobState(srmNum, JOBSTATE.NONE, "(Fork2 COMPLETE MAN)");
                gClass.str.SrmPacket[srmNum].manuFork2JobComplete = 0;
            }

            // WCS 전체작업삭제 클리어 조건
            // to do 동작 중에 들어오면 무시 - 완료 후 삭제 처리할 지..
            if (gClass.str.SrmPacket[srmNum].wcsCmdDeleteAll && gClass.str.SrmState[srmNum].fork1.jobNo == 0 && gClass.str.SrmState[srmNum].fork2.jobNo == 0 && gClass.str.SrmState[srmNum].fork1.mvJobNo == 0 && gClass.str.SrmState[srmNum].fork2.mvJobNo == 0)
            {
                cIniAccess.ChangeJobState(srmNum, JOBSTATE.CLEARJOB);
                gClass.str.SrmPacket[srmNum].wcsCmdDeleteAll = false;
            }

            if (gClass.str.SrmPacket[srmNum].wcsCmdDeleteFork1 && gClass.str.SrmState[srmNum].fork1.jobNo == 0 && gClass.str.SrmState[srmNum].fork1.mvJobNo == 0)
            {
                cIniAccess.ChangeJobState(srmNum, JOBSTATE.CLEARJOB);
                gClass.str.SrmPacket[srmNum].wcsCmdDeleteFork1 = false;
            }
            if (gClass.str.SrmPacket[srmNum].wcsCmdDeleteFork2 && gClass.str.SrmState[srmNum].fork2.jobNo == 0 && gClass.str.SrmState[srmNum].fork2.mvJobNo == 0)
            {
                cIniAccess.ChangeJobState(srmNum, JOBSTATE.CLEARJOB);
                gClass.str.SrmPacket[srmNum].wcsCmdDeleteFork2 = false;
            }



            // SRM Recv Parse 상태에서 처리 할 지..
            // 장비 동작 상태 갱신-------------------------------현재 장비 구동중인지 확인 / 홈복귀, 명령 전송 가능여부 판단 
            bool tmpState = false;
            if (gClass.str.SrmState[srmNum].trav.trSt1OperState > 0)
            {
                tmpState = true;
            }
            if (gClass.str.SrmState[srmNum].lift.liSt1OperState > 0)
            {
                tmpState = true;
            }
            if (gClass.str.SrmState[srmNum].fork1.operState > 0)
            {
                tmpState = true;
            }
            else
            {
                if (gClass.str.SrmInfo[srmNum].forkCnt == 2)                    // 트윈포크 일 경우
                {
                    if (gClass.str.SrmState[srmNum].fork2.operState > 0)
                    {
                        tmpState = true;
                    }
                }
            }

            gClass.str.SrmPacket[srmNum].operState = tmpState;                          // 장비 동작상태

            //----------------------------------------------------------------------------------------------------------------------------------
            // 작업완료 후 부여작업 삭제 체크 플래그
            //----------------------------------------------------------------------------------------------------------------------------------
            // 작업 시퀀스 정리
            // 작업수행(SRM) -> 작업완료(GCP) -> 요청작업삭제(WCS) -> 작업요구비트ON(GCP) -> 작업요청(WCS) -> 작업확인및전송(GCP)

            // 재가동 후 통신연결 시 미처리 작업 완료검사 --------------------------- to do  WCS 미처리 작업 과 Vexi에서 테스트한 작업번호가 남아있는 경우 처리가 애매함
            if (gClass.str.SrmPacket[srmNum].notPrecessedJob)
            {
                // Vexi 잔여작업 삭제 처리 플로우-----------------------------------------------------------------------------------------------------
                bool autoJobDelete = false;
                if (!gClass.str.SrmPacket[srmNum].autoJobDelete)
                {
                    if (gClass.str.SrmPacket[srmNum].reqJobCodeFk1 == 0x01)     // 이동명령
                    {
                        if (gClass.str.SrmState[srmNum].fork1.mvJobNo > 0 && (gClass.str.SrmState[srmNum].fork1.mvJobNo != gClass.str.SrmPacket[srmNum].reqJobNoFk1))   // 이동작업번호 있는데 미처리 작업번호랑 다른 경우 = 미삭제작업
                        {
                            autoJobDelete = true;
                        }
                    }
                    else
                    {
                        if (gClass.str.SrmState[srmNum].fork1.jobNo > 0 && (gClass.str.SrmState[srmNum].fork1.jobNo != gClass.str.SrmPacket[srmNum].reqJobNoFk1))       // 반송작업번호 있는데 미처리 작업번호랑 다른 경우 = 미삭제작업
                        {
                            autoJobDelete = true;
                        }
                    }

                    if (gClass.str.SrmPacket[srmNum].reqJobCodeFk2 == 0x01)     // 이동명령
                    {
                        if (gClass.str.SrmState[srmNum].fork2.mvJobNo > 0 && (gClass.str.SrmState[srmNum].fork2.mvJobNo != gClass.str.SrmPacket[srmNum].reqJobNoFk2))   // 이동작업번호 있는데 미처리 작업번호랑 다른 경우 = 미삭제작업
                        {
                            autoJobDelete = true;
                        }
                    }
                    else
                    {
                        if (gClass.str.SrmState[srmNum].fork2.jobNo > 0 && (gClass.str.SrmState[srmNum].fork2.jobNo != gClass.str.SrmPacket[srmNum].reqJobNoFk2))       // 반송작업번호 있는데 미처리 작업번호랑 다른 경우 = 미삭제작업
                        {
                            autoJobDelete = true;
                        }
                    }
                }

                if (autoJobDelete)
                {
                    cIniAccess.SaveJobLog(srmNum, "미처리 작업 - 잔여작업삭제 실행");
                    gClass.str.SrmPacket[srmNum].autoJobDelete = true;
                }
                else if (gClass.str.SrmPacket[srmNum].autoJobDelete)
                {
                    // 삭제 수행 중....
                }
                else
                {
                    // 미처리 작업 관련 내용 제거
                }

                if (gClass.str.SrmState[srmNum].dSt1StartSt > 0)
                {
                    if (!gClass.str.SrmInfo[srmNum].initialdSt1StartSt)
                    {
                        gClass.str.SrmState[srmNum].dSt1StartSt = 0;
                        cIniAccess.SaveJobLog(srmNum, "GCP == 자동 OFFLINE");
                        gClass.str.SrmPacket[srmNum].pulseClicked = true;
                        gClass.str.SrmPacket[srmNum].startCmd = 1;
                        gClass.str.SrmPacket[srmNum].startOnOff = 0;
                    }
                }
                else if (!gClass.str.SrmInfo[srmNum].initialdSt1StartSt)
                {
                    gClass.str.SrmInfo[srmNum].initialdSt1StartSt = true;
                    cIniAccess.SaveJobLog(srmNum, "GCP == 자동 OFFLINE 완료");
                }
            }
            else
            {
                // 미처리 작업 처리안할 경우, Vexi / 반자동 테스트 작업 삭제
                // 크레인에 작번이 남아있는 경우
                if (gClass.str.SrmState[srmNum].fork1.jobNo > 0 || gClass.str.SrmState[srmNum].fork2.jobNo > 0
                    || gClass.str.SrmState[srmNum].fork1.mvJobNo > 0 || gClass.str.SrmState[srmNum].fork2.mvJobNo > 0)
                {
                    if (gClass.str.SrmPacket[srmNum].autoJobDelete == false)
                    {
                        if (gClass.str.SrmState[srmNum].fork1.jobNo > 0)
                        {
                            if(gClass.str.SrmState[srmNum].fork1.jobNo >= 30000)
                            {
                                gClass.str.SrmPacket[srmNum].autoJobDelete = true;
                                cIniAccess.SaveJobLog(srmNum, "GCP == 자동삭제준비 - Fork1 반송작업");
                            }
                        }
                        else if (gClass.str.SrmState[srmNum].fork1.mvJobNo > 0)
                        {
                            if (gClass.str.SrmState[srmNum].fork1.mvJobNo >= 30000)
                            {
                                gClass.str.SrmPacket[srmNum].autoJobDelete = true;
                                cIniAccess.SaveJobLog(srmNum, "GCP == 자동삭제준비 - Fork1 이동작업");
                            }
                        }
                        else if (gClass.str.SrmState[srmNum].fork2.jobNo > 0)
                        {
                            if (gClass.str.SrmState[srmNum].fork1.jobNo >= 30000)
                            {
                                gClass.str.SrmPacket[srmNum].autoJobDelete = true;
                                cIniAccess.SaveJobLog(srmNum, "GCP == 자동삭제준비 - Fork2 반송작업");
                            }
                        }
                        else if (gClass.str.SrmState[srmNum].fork2.mvJobNo > 0)
                        {
                            if (gClass.str.SrmState[srmNum].fork1.mvJobNo >= 30000)
                            {
                                gClass.str.SrmPacket[srmNum].autoJobDelete = true;
                                cIniAccess.SaveJobLog(srmNum, "GCP == 자동삭제준비 - Fork2 반송작업");
                            }
                        }
                        else
                        {
                        }
                    }
                }
            }

            // NONE 상태에서 비상/요청정지 시 WCS 홈복귀 요청 플래그 설정 (udp에서 사용)
            // TP2 화재신호 관련 처리 251107 shk
            if (gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.REQ_STOP].value == true && gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.REQ_STOP].pin > 0)
            {
                if (gClass.str.SrmPacket[srmNum].wcsCmdHomeReturn == false)
                {
                    cIniAccess.SaveJobLog(srmNum, "GCP == 비상감지 홈복귀요청 - 송신");
                    gClass.str.SrmPacket[srmNum].wcsCmdHomeReturn = true;           // 장비 상태 홈복귀 중 으로 바뀌면 상태 해제
                }
            }

            // 반송작업 가능 상태 ----------------------------------------------------------
            //tmpState = false;
            switch ((JOBSTATE)gClass.str.SrmPacket[srmNum].jobState)
            {
                case JOBSTATE.NONE:
                    break;
                case JOBSTATE.WAIT:
                    break;
                case JOBSTATE.RECEIVE:
                    break;
                case JOBSTATE.SEND:
                    if (gClass.str.SrmPacket[srmNum].gcpError == true)               // 크레인 이상 발생 시 STOP 스텝으로
                    {
                        cIniAccess.ChangeJobState(srmNum, JOBSTATE.STOP, "- Error " + gClass.str.SrmState[srmNum].errcodeH + "-" + gClass.str.SrmState[srmNum].errcodeM);
                    }
                    else if (gClass.str.SrmState[srmNum].dSt1StartSt == 0)               // 시작 OFF 상태일 때 STOP 스텝으로
                    {
                        cIniAccess.ChangeJobState(srmNum, JOBSTATE.STOP, "(Start OFF)");
                    }
                    break;
                case JOBSTATE.PEND:     // 작업송신 완료 
                    if (gClass.str.SrmPacket[srmNum].reqJobCodeFk1 == 0x01)     // 이동 명령이면
                    {
                        if (gClass.str.SrmPacket[srmNum].operState && (gClass.str.SrmState[srmNum].fork1.mvJobNo == gClass.str.SrmPacket[srmNum].reqJobNoFk1)
                        && (gClass.str.SrmState[srmNum].fork2.mvJobNo == gClass.str.SrmPacket[srmNum].reqJobNoFk2))       // 동작중 && 요청 작업번호 비교
                        {
                            cIniAccess.ChangeJobState(srmNum, JOBSTATE.EXEC);
                        }
                    }
                    else
                    {
                        if (gClass.str.SrmPacket[srmNum].operState && (gClass.str.SrmState[srmNum].fork1.jobNo == gClass.str.SrmPacket[srmNum].reqJobNoFk1)
                        && (gClass.str.SrmState[srmNum].fork2.jobNo == gClass.str.SrmPacket[srmNum].reqJobNoFk2))       // 동작중 && 요청 작업번호 비교
                        {
                            cIniAccess.ChangeJobState(srmNum, JOBSTATE.EXEC);
                        }
                    }
                    if (gClass.str.SrmPacket[srmNum].gcpError == true)               // 크레인 이상 발생 시 STOP 스텝으로
                    {
                        cIniAccess.ChangeJobState(srmNum, JOBSTATE.STOP, "- Error " + gClass.str.SrmState[srmNum].errcodeH + "-" + gClass.str.SrmState[srmNum].errcodeM);
                    }
                    break;
                case JOBSTATE.EXEC:
                    if (gClass.str.SrmPacket[srmNum].gcpError == true)               // 크레인 이상 발생 시 STOP 스텝으로
                    {
                        cIniAccess.ChangeJobState(srmNum, JOBSTATE.STOP, "- Error " + gClass.str.SrmState[srmNum].errcodeH + "-" + gClass.str.SrmState[srmNum].errcodeM);
                    }
                    //작업완료 플래그 확인 후 완료 처리
                    int compChk = 0;        // 작업완료 체크 플래그
                    if (gClass.str.SrmState[srmNum].fork1.jobNo > 0 || gClass.str.SrmState[srmNum].fork1.mvJobNo > 0)      // fork1 지령있으면
                    {
                        if (gClass.str.SrmPacket[srmNum].reqJobCodeFk1 == 0x01)     // 이동 명령이면
                        {
                            if (gClass.str.SrmState[srmNum].fork1.mvProcState == 4)
                            {
                                cIniAccess.SaveJobLog(srmNum, "SRM -> GCP == 포크1 이동작업완료 - " + gClass.str.SrmState[srmNum].fork1.mvJobNo);
                                compChk += 1;
                            }
                        }
                        else
                        {
                            if (gClass.str.SrmState[srmNum].fork1.procState == 4)
                            {
                                cIniAccess.SaveJobLog(srmNum, "SRM -> GCP == 포크1 반송작업완료 - " + gClass.str.SrmState[srmNum].fork1.jobNo);
                                compChk += 1;
                            }
                        }

                    }
                    if (gClass.str.SrmState[srmNum].fork2.jobNo > 0 || gClass.str.SrmState[srmNum].fork2.mvJobNo > 0)      // fork2 지령있으면
                    {
                        if (gClass.str.SrmPacket[srmNum].reqJobCodeFk2 == 0x01)     // 이동 명령이면
                        {
                            if (gClass.str.SrmState[srmNum].fork2.mvProcState == 4)
                            {
                                cIniAccess.SaveJobLog(srmNum, "SRM -> GCP == 포크2 이동작업완료 - " + gClass.str.SrmState[srmNum].fork2.mvJobNo);
                                compChk += 2;
                            }
                        }
                        else
                        {
                            if (gClass.str.SrmState[srmNum].fork2.procState == 4)
                            {
                                cIniAccess.SaveJobLog(srmNum, "SRM -> GCP == 포크2 반송작업완료 - " + gClass.str.SrmState[srmNum].fork2.jobNo);
                                compChk += 2;
                            }
                        }

                    }
                    switch (compChk)
                    {
                        case 1:
                            if (gClass.str.SrmPacket[srmNum].reqJobNoFk1 >= 30000)
                            {
                                gClass.str.SrmPacket[srmNum].fork1JobComplete = 0;
                                cIniAccess.SaveJobLog(srmNum, "GCP == Fork1 작업 자동삭제");
                                gClass.str.SrmPacket[srmNum].autoJobDelete = true;
                            }
                            else if (gClass.str.SrmPacket[srmNum].reqJobNoFk1 > 0)
                            {
                                gClass.str.SrmPacket[srmNum].fork1JobComplete = 1;
                                cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == Fork1 작업완료 : ON");
                                cIniAccess.ChangeJobState(srmNum, JOBSTATE.CLEARJOB);
                            }
                            break;
                        case 2:
                            if (gClass.str.SrmPacket[srmNum].reqJobNoFk2 >= 30000)
                            {
                                gClass.str.SrmPacket[srmNum].fork2JobComplete = 0;
                                cIniAccess.SaveJobLog(srmNum, "GCP == Fork2 작업 자동삭제");
                                gClass.str.SrmPacket[srmNum].autoJobDelete = true;
                            }
                            else if (gClass.str.SrmPacket[srmNum].reqJobNoFk2 > 0)
                            {
                                gClass.str.SrmPacket[srmNum].fork2JobComplete = 1;
                                cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == Fork2 작업완료 : ON");
                                cIniAccess.ChangeJobState(srmNum, JOBSTATE.CLEARJOB);
                                gClass.str.SrmPacket[srmNum].reqJobStepFk2 = gClass.str.SrmPacket[srmNum].fork2JobComplete;
                                cIniAccess.Write($"{AppDomain.CurrentDomain.BaseDirectory}\\SRM{srmNum.ToString()}\\JobInfo.ini", "FORK2", "JOB_STEP", gClass.str.SrmPacket[srmNum].reqJobStepFk2.ToString());
                            }
                            break;
                        case 3:
                            if (gClass.str.SrmPacket[srmNum].reqJobNoFk1 >= 30000 || gClass.str.SrmPacket[srmNum].reqJobNoFk2 >= 30000)
                            {
                                gClass.str.SrmPacket[srmNum].fork1JobComplete = 0;
                                gClass.str.SrmPacket[srmNum].fork2JobComplete = 0;
                                cIniAccess.SaveJobLog(srmNum, "GCP == 작업 자동삭제");
                                gClass.str.SrmPacket[srmNum].autoJobDelete = true;
                            }
                            else
                            {
                                if (gClass.str.SrmPacket[srmNum].reqJobNoFk1 > 0)
                                {
                                    gClass.str.SrmPacket[srmNum].fork1JobComplete = 1;
                                    cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == Fork1 작업완료 : ON");
                                }
                                if (gClass.str.SrmPacket[srmNum].reqJobNoFk2 > 0)
                                {
                                    gClass.str.SrmPacket[srmNum].fork2JobComplete = 1;
                                    cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == Fork2 작업완료 : ON");
                                }
                                cIniAccess.ChangeJobState(srmNum, JOBSTATE.CLEARJOB);
                            }
                            break;
                        default:
                            break;
                    }
                    break;
                case JOBSTATE.CLEARJOB:     // WCS 작업일 때만,
                    if (gClass.str.SrmState[srmNum].fork1.jobNo == 0 && gClass.str.SrmState[srmNum].fork1.mvJobNo == 0)
                    {
                        gClass.str.SrmPacket[srmNum].reqWcsCodeFk1 = 0;
                        gClass.str.SrmPacket[srmNum].reqJobNoFk1 = 0;
                        gClass.str.SrmPacket[srmNum].reqJobCodeFk1 = 0;
                        gClass.str.SrmPacket[srmNum].reqFromStFk1 = 0;
                        gClass.str.SrmPacket[srmNum].reqFromRowFk1 = 0;
                        gClass.str.SrmPacket[srmNum].reqFromBayFk1 = 0;
                        gClass.str.SrmPacket[srmNum].reqFromLevFk1 = 0;

                        gClass.str.SrmPacket[srmNum].reqToStFk1 = 0;
                        gClass.str.SrmPacket[srmNum].reqToRowFk1 = 0;
                        gClass.str.SrmPacket[srmNum].reqToBayFk1 = 0;
                        gClass.str.SrmPacket[srmNum].reqToLevFk1 = 0;
                        if (gClass.str.SrmState[srmNum].fork2.jobNo > 0 || gClass.str.SrmState[srmNum].fork2.mvJobNo > 0)
                        {
                            if (gClass.str.SrmPacket[srmNum].fork2JobComplete == 0)
                            {
                                cIniAccess.ChangeJobState(srmNum, JOBSTATE.EXEC, "(Fork2 완료대기)");
                            }
                        }

                    }
                    if (gClass.str.SrmState[srmNum].fork2.jobNo == 0 && gClass.str.SrmState[srmNum].fork2.mvJobNo == 0)
                    {
                        gClass.str.SrmPacket[srmNum].reqWcsCodeFk2 = 0;
                        gClass.str.SrmPacket[srmNum].reqJobNoFk2 = 0;
                        gClass.str.SrmPacket[srmNum].reqJobCodeFk2 = 0;
                        gClass.str.SrmPacket[srmNum].reqFromStFk2 = 0;
                        gClass.str.SrmPacket[srmNum].reqFromRowFk2 = 0;
                        gClass.str.SrmPacket[srmNum].reqFromBayFk2 = 0;
                        gClass.str.SrmPacket[srmNum].reqFromLevFk2 = 0;

                        gClass.str.SrmPacket[srmNum].reqToStFk2 = 0;
                        gClass.str.SrmPacket[srmNum].reqToRowFk2 = 0;
                        gClass.str.SrmPacket[srmNum].reqToBayFk2 = 0;
                        gClass.str.SrmPacket[srmNum].reqToLevFk2 = 0;
                        if (gClass.str.SrmState[srmNum].fork1.jobNo > 0 || gClass.str.SrmState[srmNum].fork1.mvJobNo > 0)
                        {
                            if (gClass.str.SrmPacket[srmNum].fork1JobComplete == 0)
                            {
                                cIniAccess.ChangeJobState(srmNum, JOBSTATE.EXEC, "(Fork1 완료대기)");
                            }
                        }
                    }

                    if (gClass.str.SrmState[srmNum].fork1.jobNo == 0 && gClass.str.SrmState[srmNum].fork2.jobNo == 0 && gClass.str.SrmState[srmNum].fork1.mvJobNo == 0 && gClass.str.SrmState[srmNum].fork2.mvJobNo == 0)
                    {
                        cIniAccess.ChangeJobState(srmNum, JOBSTATE.COMPLETE);
                    }
                    break;
                case JOBSTATE.COMPLETE:
                    if (gClass.str.WcsPacket[srmNum].WCS_PARSE.dataReportOK == 0)         // DATA ReportOK : OFF → 정상 완료
                    {
                        gClass.str.SrmPacket[srmNum].completeStateDataReportOKWaitTime = DateTime.MinValue;
                        gClass.str.SrmPacket[srmNum].dataReportOKTimeoutError = false;
                        cIniAccess.ChangeJobState(srmNum, JOBSTATE.NONE);
                    }
                    else
                    {
                        // dataReportOK가 0으로 바뀌지 않고 특정 시간 이상 대기 시 에러
                        if (gClass.str.SrmPacket[srmNum].completeStateDataReportOKWaitTime != DateTime.MinValue &&
                            (DateTime.Now - gClass.str.SrmPacket[srmNum].completeStateDataReportOKWaitTime).TotalSeconds >= DATA_REPORT_OK_WAIT_TIMEOUT_SEC &&
                            !gClass.str.SrmPacket[srmNum].dataReportOKTimeoutError)
                        {
                            gClass.str.SrmPacket[srmNum].jobError = true;
                            gClass.str.SrmPacket[srmNum].gcpErrorCode = 66;  // DATA ReportOK 대기 타임아웃
                            gClass.str.SrmPacket[srmNum].gcpSubCode = 08;
                            // FLT 66-08 : GCP에서 WCS에서 DATA ReportOK 신호가 OFF 되지 않고 대기하는 경우 타임아웃 처리
                            cIniAccess.SaveJobLog(srmNum, $"GCP ERROR == COMPLETE 상태 DATA ReportOK OFF 대기 타임아웃 ({DATA_REPORT_OK_WAIT_TIMEOUT_SEC}초 초과)");
                            gClass.str.SrmPacket[srmNum].dataReportOKTimeoutError = true;
                        }
                    }
                    break;
                case JOBSTATE.STOP:
                    break;
            }


            // to do 주행/승강 홈복귀 비트가 홈복귀 중을 표시하는 것인지 확인필요 아닐 경우 제거하거나 변경필요함
            // 크레인자동 && 시작상태 ON && WCS작업X && fork1/2작업지령X && fork1/2이동지령X && 모터동작중아님 && 홈복귀아님 && 지상반 자동모드 (1:수동 2:반자동 3:자동)
            //if ((gClass.str.SrmState[srmNum].autoMode > 0) && (gClass.str.SrmState[srmNum].dSt1StartSt > 0) && (gClass.str.SrmState[srmNum].fork1.mvProcState == 0) &&
            //    (gClass.str.SrmState[srmNum].fork2.mvProcState == 0) && !gClass.str.SrmPacket[srmNum].operState && (gClass.str.SrmState[srmNum].trav.homeMove > 0) &&
            //    (gClass.str.SrmState[srmNum].trav.homeMove > 0) && (gClass.str.SrmState[srmNum].gcpState.gcpTxMode > 2))                       // 반송작업 가능상태 공통조건
            //{
            //    if((gClass.str.SrmState[srmNum].fork1.cmdCode == 0) && (gClass.str.SrmState[srmNum].fork2.cmdCode == 0) && !gClass.str.SrmPacket[srmNum].wcsJobExist)
            //    {
            //        gClass.str.SrmPacket[srmNum].SRrequestCmd = true;           // =SET= 작업요구 비트 ON 조건
            //    }
            //    else if (gClass.str.SrmPacket[srmNum].SRrequestCmd)
            //    {
            //        if (gClass.str.SrmPacket[srmNum].wcsJobExist)     // WCS 작업 수신
            //        {
            //            if((gClass.str.SrmState[srmNum].fork1.cmdCode == 0) && (gClass.str.SrmState[srmNum].fork2.cmdCode == 0))        // SRM 작업 송신 중
            //            {
            //                gClass.str.SrmPacket[srmNum].srmJobSend = true;
            //            }
            //            else
            //            {           // SRM 작업 송신 완료
            //                gClass.str.SrmPacket[srmNum].srmJobSend = false;
            //                gClass.str.SrmPacket[srmNum].SRrequestCmd = false;           // =SET= 작업요구 비트 OFF 조건
            //            }
            //        }

            //    }
            //}
            //if (gClass.str.SrmPacket[srmNum].SRrequestCmd && (gClass.str.SrmState[srmNum].autoMode > 0) && (gClass.str.SrmState[srmNum].dSt1StartSt > 0))
            //{
            //    if ((gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1JobNo == 0) && (gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2JobNo == 0) && (gClass.str.WcsPacket[srmNum].WCS_PARSE.cmdJobCmd == 0))                       // WCS 요청 삭제 상태 체크
            //    {
            //        tmpState = true;
            //    }
            //}

        }

        // ── 리미트 스위치 안전감시 상태(호기별) ──
        //   주행(TST)/승강(LST) 리미트가 ★시작 시점(baseline) 상태에서 바뀌면★ 0x50(시작 OFF) 정지를 강제한다.
        //   극성 무관 — 최초 정상상태를 기준으로 잡고 그와 달라지면 리미트 작동으로 간주(사용자 지정 방식).
        private bool limitBaseCaptured = false;
        private bool limitBaseLST = false, limitBaseTST = false;
        private bool limitStopLatched = false;

        /// <summary>
        /// 주행/승강 리미트가 시작 시점 상태에서 바뀌면 0x50 정지를 강제(전 호기 상시).
        /// ★ 전제: 최초 상태수신 시점에 크레인이 리미트에 걸려있지 않아야 함(그때 값이 기준이 됨).
        /// ★ 하드웨어/MCU 1차 정지를 대체하지 않는 보조 SW 레이어(상태수신 주기·UDP 의존).
        /// </summary>
        private void CheckLimitSwitchStop(bool curLST, bool curTST)
        {
            if (!limitBaseCaptured)
            {
                limitBaseLST = curLST; limitBaseTST = curTST; limitBaseCaptured = true;
                return;
            }

            bool changed = (curLST != limitBaseLST) || (curTST != limitBaseTST);
            if (changed)
            {
                // 변화가 지속되는 동안 매 상태수신마다 0x50 정지 강제(무조건 멈춤). 로그/예외기록은 진입 엣지 1회.
                gClass.str.SrmPacket[srmNum].startCmd = 1;
                gClass.str.SrmPacket[srmNum].startOnOff = 0;
                if (!limitStopLatched)
                {
                    limitStopLatched = true;
                    SaveLogFile($"[LIMIT][STOP] 리미트 감지 정지 — 주행TST={curTST}(기준{limitBaseTST}) 승강LST={curLST}(기준{limitBaseLST}) → 0x50 시작OFF", false);
                    cIniAccess.SaveExLog(srmNum, $"LIMIT STOP TST={curTST}(base{limitBaseTST}) LST={curLST}(base{limitBaseLST})");
                }
            }
            else
            {
                limitStopLatched = false;   // 기준 상태로 복귀(리미트 해제) → 재무장
            }
        }

        // SRM I/O 센서상태 파싱
        unsafe private void Srm_InOutParse()
        {
            ref Srm_State refState = ref gClass.str.SrmState[srmNum];
            ref SRM_IO refIO = ref gClass.str.SRMIO[srmNum];


            //----------------------------Digital Input Parse---------------------------
            // DI 0
            refIO.EM = (refState.dInput[0] & (1 << 0)) != 0;
            refIO.AUTO = (refState.dInput[0] & (1 << 1)) != 0;
            refIO.MAN = (refState.dInput[0] & (1 << 2)) != 0;
            refIO.RDF = (refState.dInput[0] & (1 << 3)) != 0;
            refIO.LST = (refState.dInput[0] & (1 << 4)) != 0;
            refIO.TST = (refState.dInput[0] & (1 << 5)) != 0;
            refIO.MFLT = (refState.dInput[0] & (1 << 6)) != 0;
            refIO.GOV = (refState.dInput[0] & (1 << 7)) != 0;

            // ★ 리미트 스위치 안전감시 — 주행(TST)/승강(LST)이 시작 시점 상태에서 바뀌면 0x50 정지(전 호기 상시).
            CheckLimitSwitchStop(refIO.LST, refIO.TST);
            // DI 1
            refIO.MCF = (refState.dInput[1] & (1 << 0)) != 0;
            refIO.MC1F = (refState.dInput[1] & (1 << 1)) != 0;
            refIO.PDR = (refState.dInput[1] & (1 << 2)) != 0;
            refIO.PTH = (refState.dInput[1] & (1 << 3)) != 0;
            refIO.MCTMF = (refState.dInput[1] & (1 << 4)) != 0;
            refIO.MCFMF = (refState.dInput[1] & (1 << 5)) != 0;
            refIO.T1PSF = (refState.dInput[1] & (1 << 6)) != 0;
            refIO.T1OSO = (refState.dInput[1] & (1 << 7)) != 0;
            // DI 2
            refIO.LBMMSF1 = (refState.dInput[2] & (1 << 0)) != 0;
            refIO.TBMMSF1 = (refState.dInput[2] & (1 << 1)) != 0;
            refIO.FBMMSF1 = (refState.dInput[2] & (1 << 2)) != 0;
            refIO.CPTF = (refState.dInput[2] & (1 << 3)) != 0;
            refIO.TDF = (refState.dInput[2] & (1 << 4)) != 0;
            refIO.TDR = (refState.dInput[2] & (1 << 5)) != 0;
            refIO.THP = (refState.dInput[2] & (1 << 6)) != 0;
            refIO.TSP = (refState.dInput[2] & (1 << 7)) != 0;
            // DI 3
            refIO.CFLT = (refState.dInput[3] & (1 << 0)) != 0;
            refIO.CRD = (refState.dInput[3] & (1 << 1)) != 0;
            refIO.MC2F = (refState.dInput[3] & (1 << 2)) != 0;
            refIO.MCLMF = (refState.dInput[3] & (1 << 3)) != 0;
            refIO.MCFM2F = (refState.dInput[3] & (1 << 4)) != 0;
            refIO.L1PSF = (refState.dInput[3] & (1 << 5)) != 0;
            refIO.L1OSO = (refState.dInput[3] & (1 << 6)) != 0;
            refIO.FBMMSF2 = (refState.dInput[3] & (1 << 7)) != 0;
            // DI 4
            refIO.CVOK1 = (refState.dInput[4] & (1 << 0)) != 0;
            refIO.CVOK2 = (refState.dInput[4] & (1 << 1)) != 0;
            refIO.CVOK3 = (refState.dInput[4] & (1 << 2)) != 0;
            refIO.CVOK4 = (refState.dInput[4] & (1 << 3)) != 0;
            refIO.CVOK5 = (refState.dInput[4] & (1 << 4)) != 0;
            refIO.CVOK6 = (refState.dInput[4] & (1 << 5)) != 0;
            refIO.CVOK7 = (refState.dInput[4] & (1 << 6)) != 0;
            refIO.CVOK8 = (refState.dInput[4] & (1 << 7)) != 0;
            // DI 5
            refIO.GRA = (refState.dInput[5] & (1 << 0)) != 0;           // FAN_FAULT
            refIO.DEVICE_FLT = (refState.dInput[5] & (1 << 1)) != 0;    // TS1-ENB
            refIO.TS1_ENB = (refState.dInput[5] & (1 << 2)) != 0;
            refIO.TS2_ENB = (refState.dInput[5] & (1 << 3)) != 0;
            refIO.M_EST = (refState.dInput[5] & (1 << 4)) != 0;
            refIO.M_KEYSW = (refState.dInput[5] & (1 << 5)) != 0;
            refIO.M_FLT = (refState.dInput[5] & (1 << 6)) != 0;
            refIO.LBMMSF2 = (refState.dInput[5] & (1 << 7)) != 0;
            // DI 6
            refIO.TBMMSF2 = (refState.dInput[6] & (1 << 0)) != 0;
            refIO.F1ENC = (refState.dInput[6] & (1 << 1)) != 0;
            refIO.LDU = (refState.dInput[6] & (1 << 2)) != 0;
            refIO.LDD = (refState.dInput[6] & (1 << 3)) != 0;
            refIO.LHP = (refState.dInput[6] & (1 << 4)) != 0;
            refIO.LSP = (refState.dInput[6] & (1 << 5)) != 0;
            refIO.GOX1 = (refState.dInput[6] & (1 << 6)) != 0;
            refIO.GOXH1 = (refState.dInput[6] & (1 << 7)) != 0;
            // DI 7
            refIO.GOXM1 = (refState.dInput[7] & (1 << 0)) != 0;
            refIO.GOXS1 = (refState.dInput[7] & (1 << 1)) != 0;
            refIO.GWL1 = (refState.dInput[7] & (1 << 2)) != 0;
            refIO.GWR1 = (refState.dInput[7] & (1 << 3)) != 0;
            refIO.GWLe1 = (refState.dInput[7] & (1 << 4)) != 0;
            refIO.GWRe1 = (refState.dInput[7] & (1 << 5)) != 0;
            refIO.GDFL1 = (refState.dInput[7] & (1 << 6)) != 0;
            refIO.GDFR1 = (refState.dInput[7] & (1 << 7)) != 0;
            // DI 8
            refIO.GDRL1 = (refState.dInput[8] & (1 << 0)) != 0;
            refIO.GDRR1 = (refState.dInput[8] & (1 << 1)) != 0;
            refIO.GHL1 = (refState.dInput[8] & (1 << 2)) != 0;
            refIO.GHR1 = (refState.dInput[8] & (1 << 3)) != 0;
            refIO.FOKL1 = (refState.dInput[8] & (1 << 4)) != 0;
            refIO.FOKR1 = (refState.dInput[8] & (1 << 5)) != 0;
            refIO.FEL1 = (refState.dInput[8] & (1 << 6)) != 0;
            refIO.FER1 = (refState.dInput[8] & (1 << 7)) != 0;
            // DI 9
            refIO.FCL1 = (refState.dInput[9] & (1 << 0)) != 0;
            refIO.FCR1 = (refState.dInput[9] & (1 << 1)) != 0;
            refIO.DSTL1 = (refState.dInput[9] & (1 << 2)) != 0;
            refIO.DSTR1 = (refState.dInput[9] & (1 << 3)) != 0;
            refIO.DSTLe1 = (refState.dInput[9] & (1 << 4)) != 0;
            refIO.DSTRe1 = (refState.dInput[9] & (1 << 5)) != 0;
            refIO.RTF = (refState.dInput[9] & (1 << 6)) != 0;
            refIO.RTR = (refState.dInput[9] & (1 << 7)) != 0;
            // DI 10
            refIO.RTF2 = (refState.dInput[10] & (1 << 0)) != 0;
            refIO.RTR2 = (refState.dInput[10] & (1 << 1)) != 0;
            refIO.GOX2 = (refState.dInput[10] & (1 << 2)) != 0;
            refIO.GOXH2 = (refState.dInput[10] & (1 << 3)) != 0;
            refIO.GOXM2 = (refState.dInput[10] & (1 << 4)) != 0;
            refIO.GOXS2 = (refState.dInput[10] & (1 << 5)) != 0;
            refIO.GWL2 = (refState.dInput[10] & (1 << 6)) != 0;
            refIO.GWR2 = (refState.dInput[10] & (1 << 7)) != 0;
            // DI 11
            refIO.GWLe2 = (refState.dInput[11] & (1 << 0)) != 0;
            refIO.GWRe2 = (refState.dInput[11] & (1 << 1)) != 0;
            refIO.GDFL2 = (refState.dInput[11] & (1 << 2)) != 0;
            refIO.GDFR2 = (refState.dInput[11] & (1 << 3)) != 0;
            refIO.GDRL2 = (refState.dInput[11] & (1 << 4)) != 0;
            refIO.GDRR2 = (refState.dInput[11] & (1 << 5)) != 0;
            refIO.GHL2 = (refState.dInput[11] & (1 << 6)) != 0;
            refIO.GHR2 = (refState.dInput[11] & (1 << 7)) != 0;
            // DI 12
            refIO.FOKL2 = (refState.dInput[12] & (1 << 0)) != 0;
            refIO.FOKR2 = (refState.dInput[12] & (1 << 1)) != 0;
            refIO.FEL2 = (refState.dInput[12] & (1 << 2)) != 0;
            refIO.FER2 = (refState.dInput[12] & (1 << 3)) != 0;
            refIO.FCL2 = (refState.dInput[12] & (1 << 4)) != 0;
            refIO.FCR2 = (refState.dInput[12] & (1 << 5)) != 0;
            refIO.DSTL2 = (refState.dInput[12] & (1 << 6)) != 0;
            refIO.DSTR2 = (refState.dInput[12] & (1 << 7)) != 0;
            // DI 13
            refIO.DSTLe2 = (refState.dInput[13] & (1 << 0)) != 0;
            refIO.DSTRe2 = (refState.dInput[13] & (1 << 1)) != 0;
            refIO.ODSTL1 = (refState.dInput[13] & (1 << 2)) != 0;
            refIO.ODSTR1 = (refState.dInput[13] & (1 << 3)) != 0;
            refIO.DSTLR1 = (refState.dInput[13] & (1 << 4)) != 0;
            refIO.DSTRR1 = (refState.dInput[13] & (1 << 5)) != 0;
            refIO.ODSTL2 = (refState.dInput[13] & (1 << 6)) != 0;
            refIO.ODSTR2 = (refState.dInput[13] & (1 << 7)) != 0;
            // DI 14
            refIO.DSTLR2 = (refState.dInput[14] & (1 << 0)) != 0;
            refIO.DSTRR2 = (refState.dInput[14] & (1 << 1)) != 0;
            refIO.FML1 = (refState.dInput[14] & (1 << 2)) != 0;
            refIO.FMR1 = (refState.dInput[14] & (1 << 3)) != 0;
            refIO.FHL1 = (refState.dInput[14] & (1 << 4)) != 0;
            refIO.FHR1 = (refState.dInput[14] & (1 << 5)) != 0;
            refIO.FML2 = (refState.dInput[14] & (1 << 6)) != 0;
            refIO.FMR2 = (refState.dInput[14] & (1 << 7)) != 0;
            // DI 15
            refIO.FHL2 = (refState.dInput[15] & (1 << 0)) != 0;
            refIO.FHR2 = (refState.dInput[15] & (1 << 1)) != 0;

            /* 260714 PCE LiDAR 파싱로직 추가 향후 사용예정*/

            refIO.LiDAR1_Observe_Signal = (refState.dInput[9] & (1 << 2)) != 0; //DSTL1
            refIO.LiDAR1_Alert_Signal = (refState.dInput[9] & (1 << 4)) != 0; //DSTLe1
            refIO.LiDAR1_Alarm_Signal = (refState.dInput[13] & (1 << 2)) != 0; //ODSTL1
            //refIO.LiDAR1_System_Alarm = (refState.dInput[13] & (1 << 0)) != 0;
            refIO.LiDAR2_Observe_Signal = (refState.dInput[9] & (1 << 3)) != 0; //DSTR1
            refIO.LiDAR2_Alert_Signal = (refState.dInput[9] & (1 << 5)) != 0; //DSTRe1
            refIO.LiDAR2_Alarm_Signal = (refState.dInput[13] & (1 << 3)) != 0; //ODSTR1
            //refIO.LiDAR2_System_Alarm = (refState.dInput[13] & (1 << 0)) != 0;

            //----------------------------Digital Output Parse---------------------------
            // DO 0
            refIO.IINH = (refState.dOutput[0] & (1 << 0)) != 0;
            refIO.FCD = (refState.dOutput[0] & (1 << 1)) != 0;
            refIO.RDE = (refState.dOutput[0] & (1 << 2)) != 0;
            refIO.RED = (refState.dOutput[0] & (1 << 3)) != 0;
            refIO.YEL = (refState.dOutput[0] & (1 << 4)) != 0;
            refIO.GRN = (refState.dOutput[0] & (1 << 5)) != 0;
            refIO.SUD = (refState.dOutput[0] & (1 << 6)) != 0;
            refIO.MCE = (refState.dOutput[0] & (1 << 7)) != 0;
            // DO 1
            refIO.MCUB = (refState.dOutput[1] & (1 << 0)) != 0;
            refIO.PLAMP = (refState.dOutput[1] & (1 << 1)) != 0;
            refIO.PFAN = (refState.dOutput[1] & (1 << 2)) != 0;
            refIO.MCTM = (refState.dOutput[1] & (1 << 3)) != 0;
            refIO.MCFM1 = (refState.dOutput[1] & (1 << 4)) != 0;
            refIO.T1FSPC = (refState.dOutput[1] & (1 << 5)) != 0;
            refIO.T1SPO = (refState.dOutput[1] & (1 << 6)) != 0;
            refIO.MCFB1 = (refState.dOutput[1] & (1 << 7)) != 0;
            // DO 2
            refIO.COSE = (refState.dOutput[2] & (1 << 0)) != 0;
            refIO.CENB = (refState.dOutput[2] & (1 << 1)) != 0;
            refIO.CRST = (refState.dOutput[2] & (1 << 2)) != 0;
            refIO.MCLM = (refState.dOutput[2] & (1 << 3)) != 0;
            refIO.MCFM2 = (refState.dOutput[2] & (1 << 4)) != 0;
            refIO.LFSPC = (refState.dOutput[2] & (1 << 5)) != 0;
            refIO.LSPO = (refState.dOutput[2] & (1 << 6)) != 0;
            refIO.MCFB2 = (refState.dOutput[2] & (1 << 7)) != 0;
            // DO 3
            refIO.CVNO1 = (refState.dOutput[3] & (1 << 0)) != 0;
            refIO.CVNO2 = (refState.dOutput[3] & (1 << 1)) != 0;
            refIO.CVNO3 = (refState.dOutput[3] & (1 << 2)) != 0;
            refIO.CVNO4 = (refState.dOutput[3] & (1 << 3)) != 0;
            refIO.CVNO5 = (refState.dOutput[3] & (1 << 4)) != 0;
            refIO.CVNO6 = (refState.dOutput[3] & (1 << 5)) != 0;
            refIO.CVNO7 = (refState.dOutput[3] & (1 << 6)) != 0;
            refIO.CVNO8 = (refState.dOutput[3] & (1 << 7)) != 0;
            // DO 4
            refIO.GRA_RST = (refState.dOutput[4] & (1 << 0)) != 0;
            refIO.DEVICE_RST = (refState.dOutput[4] & (1 << 1)) != 0;
            refIO.LED_RD = (refState.dOutput[4] & (1 << 2)) != 0;
            refIO.LED_GR = (refState.dOutput[4] & (1 << 3)) != 0;
            refIO.LED_BU = (refState.dOutput[4] & (1 << 4)) != 0;
        }




        //------------------------------------------------SEND COMMAND FUNC--------------------------------------------------------------------------------------------------------
        //-------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        // Send Command Main Func
        private void Send_Command()
        {
            //-1-이전 응답 시퀀스 번호 비정상
            if (seqNum == gClass.str.SrmPacket[srmNum].recvStr.seqNum)           // 시퀀스 번호 동일할 경우 작업커맨드 완료처리 NO
            {
                // 시퀀스 번호 미 변경 시 최근 명령 재전송 할지  to do 
                Tx_SendCmd(0x00, 0x30);
                //Console.WriteLine("Tx Retry Command - " + ipAddress + " " + port);
            }
            //-1- 이전 응답 시퀀스 번호 정상
            else
            {
                //-2- 이전 송신이 상태조회가 아닐 경우 상태조회 전송
                if (gClass.str.SrmPacket[srmNum].sendStr.cmd2 != 0x30)           // 상태조회 명령이 아닐경우 응답코드 확인 후 완료 처리
                {
                    // 상태조회 명령 전송
                    Tx_SendCmd(0x00, 0x30);
                }
                //-2- 이전 송신이 상태조회 명령일 경우 조건 검사 후 조건 전송
                else
                {
                    // 상위관련 통신 시퀀스 로직은 해당 함수에서 결정
                    /* 송신 명령은 명령 / 상태전송을 반복
                     * 조건에 따라 송신 우선순위를 생각하며 배치할 것 */


                    //if (gClass.str.SrmPacket[srmNum].stdInfoRequest)       // 기본정보 요청
                    //{
                    //    gClass.str.SrmPacket[srmNum].stdInfoRequest = false;
                    //    Tx_SendCmd(0x01, 0x10);
                    //    return;
                    //}
                    // to do 펄스클릭 명령 (지상반 UI 클릭 명령은 한번만 보내고 처리 - 처리 확인 안함
                    if (gClass.str.SrmPacket[srmNum].pulseClicked)     // 펄스 클릭명령 있을 경우 ------ 시작 정지 리셋 작업삭제 등.
                    {
                        if (gClass.str.SrmPacket[srmNum].resetCmd > 0)        // 이상리셋 명령 일 경우
                        {
                            // 보내고 초기화를 해버리는 걸로 처리해도 될 듯
                            gClass.str.SrmPacket[srmNum].pulseClicked = false;
                            gClass.str.SrmPacket[srmNum].resetCmd = 0;
                            // 이상리셋 명령 전송 
                            Tx_SendCmd(0x00, 0x52);
                            return;
                        }
                    }

                    if (gClass.str.SrmPacket[srmNum].startCmd > 0 && gClass.str.SrmPacket[srmNum].startOnOff == 0)          // 시작 OFF 명령 일 경우  - 정지 명령일 경우
                    {
                        gClass.str.SrmPacket[srmNum].pulseClicked = false;
                        gClass.str.SrmPacket[srmNum].startCmd = 0;
                        // 시작 OFF 명령 전송 
                        Tx_SendCmd(0x00, 0x50);
                        return;
                    }

                    if (gClass.str.SrmPacket[srmNum].wcsCmdReset)
                    {
                        gClass.str.SrmPacket[srmNum].wcsCmdReset = false;
                        Tx_SendCmd(0x00, 0x52);
                        return;
                    }

                    if (gClass.str.SrmPacket[srmNum].stdInfoControl)      // 시간동기화 요청
                    {
                        gClass.str.SrmPacket[srmNum].stdInfoControl = false;
                        Tx_SendCmd(0x01, 0x11);
                        return;
                    }

                    if (gClass.str.SrmPacket[srmNum].craneSetRequest)      // 크레인 정보 요청
                    {
                        gClass.str.SrmPacket[srmNum].craneSetRequest = false;
                        Tx_SendCmd(0x01, 0x25);
                        return;
                    }

                    if (gClass.str.SrmPacket[srmNum].rackRequest)           // 렉 설정 정보 요청
                    {
                        gClass.str.SrmPacket[srmNum].rackRequest = false;
                        Tx_SendCmd(0x00, 0x94);
                        return;
                    }

                    if (gClass.str.SrmPacket[srmNum].cellPosWriteReq)           // 셀 위치 설정 (Write) 요청
                    {
                        gClass.str.SrmPacket[srmNum].cellPosWriteReq = false;
                        gClass.str.SrmPacket[srmNum].cellPosWriteDone = false;
                        gClass.str.SrmPacket[srmNum].cellPosWriteNack = false;
                        Tx_SendCmd(0x00, 0x95);
                        return;
                    }

                    if (gClass.str.SrmPacket[srmNum].stationRequest)           // 스테이션 설정 정보 요청
                    {
                        gClass.str.SrmPacket[srmNum].stationRequest = false;
                        Tx_SendCmd(0x00, 0x98);
                        return;
                    }

                    if (gClass.str.SrmPacket[srmNum].prohRackRequest)           // 금지렉 설정 정보 요청
                    {
                        gClass.str.SrmPacket[srmNum].prohRackRequest = false;
                        Tx_SendCmd(0x00, 0x9C);
                        return;
                    }

                    if (gClass.str.SrmPacket[srmNum].forkRequest)           // 포크 설정 정보 요청
                    {
                        gClass.str.SrmPacket[srmNum].forkRequest = false;
                        Tx_SendCmd(0x00, 0xA7);
                        return;
                    }


                    if (gClass.str.SrmPacket[srmNum].modeSetReq && gClass.str.SrmState[srmNum].dSt1Abnormal == 0)           // 모드 설정 정보 요청
                    {
                        gClass.str.SrmPacket[srmNum].modeSetReq = false;
                        Tx_SendCmd(0x00, 0x58);
                        return;
                    }

                    // 0x59 프로토콜 탐색 (Probe)
                    if (gClass.str.SrmPacket[srmNum].probeReq)
                    {
                        gClass.str.SrmPacket[srmNum].probeReq = false;
                        Tx_SendCmd(0x00, 0x59);
                        return;
                    }

                    // 0xA3 Drive 파라미터 읽기
                    if (gClass.str.SrmPacket[srmNum].driveParamReadReq)
                    {
                        gClass.str.SrmPacket[srmNum].driveParamReadReq = false;
                        Tx_SendCmd(0x00, 0xA3);
                        return;
                    }
                    // 0xA4 Drive 파라미터 쓰기
                    if (gClass.str.SrmPacket[srmNum].driveParamWriteReq)
                    {
                        gClass.str.SrmPacket[srmNum].driveParamWriteReq = false;
                        Tx_SendCmd(0x00, 0xA4);
                        return;
                    }
                    // 0xA5 Lift 파라미터 읽기
                    if (gClass.str.SrmPacket[srmNum].liftParamReadReq)
                    {
                        gClass.str.SrmPacket[srmNum].liftParamReadReq = false;
                        Tx_SendCmd(0x00, 0xA5);
                        return;
                    }
                    // 0xA6 Lift 파라미터 쓰기
                    if (gClass.str.SrmPacket[srmNum].liftParamWriteReq)
                    {
                        gClass.str.SrmPacket[srmNum].liftParamWriteReq = false;
                        Tx_SendCmd(0x00, 0xA6);
                        return;
                    }
                    // 0x59 보수위치 이동
                    if (gClass.str.SrmPacket[srmNum].maintMoveReq)
                    {
                        gClass.str.SrmPacket[srmNum].maintMoveReq = false;
                        Tx_SendCmd(0x00, 0x59);
                        return;
                    }

                    if (gClass.str.SrmPacket[srmNum].wcsCmdDeleteAll)
                    {
                        gClass.str.SrmPacket[srmNum].flagJobDelete = 3;         // Fork 1/2 작업 전체삭제
                        Tx_SendCmd(0x00, 0x53);
                        return;
                    }
                    if (gClass.str.SrmPacket[srmNum].wcsCmdDeleteFork1)
                    {
                        gClass.str.SrmPacket[srmNum].flagJobDelete = 1;         // Fork 1 작업삭제
                        Tx_SendCmd(0x00, 0x53);
                        return;
                    }
                    if (gClass.str.SrmPacket[srmNum].wcsCmdDeleteFork2)
                    {
                        gClass.str.SrmPacket[srmNum].flagJobDelete = 2;         // Fork 2 작업삭제
                        Tx_SendCmd(0x00, 0x53);
                        return;
                    }


                    //-3-
                    else if (gClass.str.SrmPacket[srmNum].manuFork1JobComplete > 0)
                    {
                        gClass.str.SrmPacket[srmNum].flagJobDelete = 1;
                        Tx_SendCmd(0, 83);
                        return;
                    }
                    else if (gClass.str.SrmPacket[srmNum].manuFork2JobComplete > 0)
                    {
                        gClass.str.SrmPacket[srmNum].flagJobDelete = 2;
                        Tx_SendCmd(0, 83);
                        return;
                    }
                    else if (gClass.str.SrmPacket[srmNum].manuFork1JobDelete > 0)
                    {
                        gClass.str.SrmPacket[srmNum].flagJobDelete = 1;
                        Tx_SendCmd(0, 83);
                        return;
                    }
                    else if (gClass.str.SrmPacket[srmNum].manuFork2JobDelete > 0)
                    {
                        gClass.str.SrmPacket[srmNum].flagJobDelete = 2;
                        Tx_SendCmd(0, 83);
                        return;
                    }

                    if (gClass.str.SrmPacket[srmNum].autoJobDelete)       // 작업 자동삭제 명령
                    {
                        // 보내고 초기화를 해버리는 걸로 처리해도 될 듯
                        gClass.str.SrmPacket[srmNum].flagJobDelete = 3;         // Fork 1/2 작업 전체삭제
                        Tx_SendCmd(0x00, 0x53);
                        return;
                    }

                    //-3-
                    if (gClass.str.SrmState[srmNum].autoMode > 0)                       // 크레인(차상반) 자동상태
                    {
                        if (gClass.str.SrmPacket[srmNum].wcsCmdCycleStop)
                        {
                            Tx_SendCmd((byte)0, (byte)84);
                            return;
                        }
                        if (gClass.str.SrmPacket[srmNum].wcsCmdEmergencyStop)
                        {
                            Tx_SendCmd((byte)0, (byte)85);
                            return;
                        }

                        if ((JOBSTATE)gClass.str.SrmPacket[srmNum].jobState == JOBSTATE.SEND)
                        {
                            if (gClass.str.SrmState[srmNum].dSt1StartSt > 0)
                            {
                                Tx_SendCmd(0x00, 0x41);
                                return;
                            }
                            else
                            {
                                // 미처리 작업/ 목적지 변경인지 확인 후 전송시작
                                if (gClass.str.SrmPacket[srmNum].notPrecessedJob)  // 반송작업 송신 성공 시 미처리 작업 완료 된걸로 처리 (목적지 변경 등)
                                {
                                    // 미처리작업 목적지변경 명령 StartOFF 상태에서 STOP에서 SEND로 바뀐경우 전송처리
                                    Tx_SendCmd(0x00, 0x41);
                                    return;
                                }
                                else
                                {
                                    // to do 작업삭제 후 목적지변경 처리할 거라서 삭제할지 확인 필요
                                    if (gClass.str.SrmPacket[srmNum].reqJobCodeFk1 == 0x16 || gClass.str.SrmPacket[srmNum].reqJobCodeFk2 == 0x17) // 목적지변경
                                    {
                                        Tx_SendCmd(0x00, 0x41);
                                        return;
                                    }
                                }

                                //if(gClass.str.SrmPacket[srmNum].reqJobCode == 0x16 || gClass.str.SrmPacket[srmNum].reqJobCode == 0x17)
                                //{
                                //    Tx_SendCmd(0x00, 0x41);
                                //    return;
                                //}
                            }
                            //return;
                        }
                        else
                        {
                            // ---장비 시작상태
                            if (gClass.str.SrmState[srmNum].dSt1StartSt > 0)
                            {
                                // 지상반 조작
                                if (gClass.str.SrmPacket[srmNum].pulseClicked)              // 펄스 클릭명령 있을 경우 ------ 홈복귀
                                {
                                    if (gClass.str.SrmPacket[srmNum].homeCmd > 0)           // 홈복귀 명령 일 경우
                                    {
                                        // 보내고 초기화를 해버리는 걸로 처리해도 될 듯
                                        gClass.str.SrmPacket[srmNum].pulseClicked = false;
                                        gClass.str.SrmPacket[srmNum].homeCmd = 0;
                                        // to do 홈 복귀 가능 조건 검사
                                        // 홈복귀 명령 전송 
                                        Tx_SendCmd(0x00, 0x51);
                                        return;
                                    }
                                }

                                if (gClass.str.SrmState[srmNum].gcpState.gcpTxMode == 2)             // 지상반 반자동상태   1:수동 2:반자동 3:자동
                                {
                                    // to do
                                    if (gClass.str.SrmPacket[srmNum].semiJobClicked)                // 반자동 동작 명령 있을 경우
                                    {
                                        Tx_SendCmd(0x00, 0x41);                     // to do 반자동 / 자동 구분 없앨까 고민중
                                        gClass.str.SrmPacket[srmNum].semiJobClicked = false;        // 전송 후 제거
                                        return;
                                    }
                                }
                                else if (gClass.str.SrmState[srmNum].gcpState.gcpTxMode == 3)             // 지상반 자동상태   1:수동 2:반자동 3:자동
                                {
                                    if (gClass.str.SrmPacket[srmNum].wcsCmdHomeReturn)
                                    {
                                        Tx_SendCmd(0x00, 0x51);
                                        return;
                                    }
                                }

                                if (gClass.str.SrmPacket[srmNum].wcsCmdSrmManual)
                                {
                                    //gClass.str.SrmPacket[srmNum].wcsCmdSrmManual = false;
                                    Tx_SendCmd(0x00, 0x50 /*0x50*/);
                                    return;
                                }
                                // 온라인 상태일때 작업 전송 - 작업번호 비교

                            }
                            // ---장비 시작 상태가 아닐 경우
                            else
                            {
                                if (gClass.str.SrmPacket[srmNum].pulseClicked)              // 펄스 클릭명령 있을 경우 ------ 시작
                                {
                                    if (gClass.str.SrmPacket[srmNum].homeCmd > 0)           // 홈복귀 명령 일 경우 무시
                                    {
                                        gClass.str.SrmPacket[srmNum].homeCmd = 0;
                                    }

                                    if (gClass.str.SrmPacket[srmNum].startCmd > 0)          // 시작 명령 일 경우
                                    {
                                        // 보내고 초기화를 해버리는 걸로 처리해도 될 듯
                                        gClass.str.SrmPacket[srmNum].pulseClicked = false;
                                        gClass.str.SrmPacket[srmNum].startCmd = 0;
                                        // to do 홈 복귀 가능 조건 검사
                                        // 시작 명령 전송 
                                        Tx_SendCmd(0x00, 0x50);
                                        return;
                                    }
                                }
                                else
                                {
                                    if (gClass.str.SrmPacket[srmNum].wcsCmdSrmManual)
                                    {
                                        gClass.str.SrmPacket[srmNum].wcsCmdSrmManual = false;
                                        Tx_SendCmd((byte)0, (byte)80 /*0x50*/);
                                        return;
                                    }
                                    if (gClass.str.SrmPacket[srmNum].wcsCmdSrmOnline)
                                    {
                                        gClass.str.SrmPacket[srmNum].wcsCmdSrmOnline = false;
                                        Tx_SendCmd(0x00, 0x50 /*0x50*/);
                                        return;
                                    }
                                }
                            }
                        }
                    }
                    //-3-
                    else if (gClass.str.SrmState[srmNum].manualMode > 0 || gClass.str.SrmState[srmNum].setupMode > 0)    // 크레인 수동 또는 셋업
                    {
                        // CMD2_80 송신 조건: 수동모드(셀단위 jog) 또는 셋업모드(mm단위 자유 jog) AND 지상반 수동 송신
                        //   엑셀 알람 1-8 정의:
                        //     (수동모드) 스테이션/랙 시작 위치까지 도달 — 셀 단위 jog 제약
                        //     (셋업모드) [주행드라이브 설정] 수동운전 - ManualOp_Start까지 mm 자유 jog
                        if (gClass.str.SrmState[srmNum].gcpState.gcpTxMode == 1)
                        {
                            if (gClass.str.SrmPacket[srmNum].manClicked)                // 수동 동작 명령 있을 경우
                            {
                                Tx_SendCmd(0x00, 0x80);
                                return;
                            }
                            if (gClass.str.SrmPacket[srmNum].manStop)                // 수동 버튼 해제 시
                            {
                                Tx_SendCmd(0x00, 0x80);
                                return;
                            }
                        }

                        if (gClass.str.SrmPacket[srmNum].homeCmd > 0)           // 홈복귀 명령 일 경우 무시
                        {
                            gClass.str.SrmPacket[srmNum].homeCmd = 0;
                        }
                    }
                    //switch (gClass.str.SrmState[srmNum].devMode)
                    //{
                    //    case 64:    //  기상반 수동
                    //        break;
                    //    case 128:   //  기상반 자동
                    //        break;
                    //    case 32:    //  기상반 강제모드
                    //        break;
                    //    case 16:    //  기상반 셋업모드
                    //        break;
                    //    default:
                    //        break;
                    //}
                    //-3-
                    else
                    {
                    }

                    // 중간 리턴이 아니면 상태정보 전송
                    // 수동 - 작업 명령 등 기타 커맨드 요청 상태에 따라 없으면 상태조회
                    Tx_SendCmd(0x00, 0x30);
                }
            }
        }


        private byte[] Tx_SetHeader(byte cmd1)
        {
            byte[] test = new byte[12];
            test[0] = 0x16;
            test[1] = 0x16;
            test[2] = 0x16;
            test[3] = 0x16;

            test[4] = 0x00;     // SRC ID - TYPE    00:지상반
            test[5] = 0x00;     // SRC ID - INDEX
            test[6] = 0x60;     // DST ID - TYPE    60:SRM    
            test[7] = Convert.ToByte(gClass.str.SrmInfo[srmNum].srmID);     // DST ID - INDEX   00:SRM 내 INDEX ex)호기번호

            test[8] = Convert.ToByte(seqNum);     // Sequence Num 

            seqNum++;
            if (seqNum > 255) seqNum = 0;   // 수신 데이터 파싱 정상 시 seqNum 증가
            //test[8] = 78;     // Sequence Num 

            test[9] = 0x00;     // Bypass1
            test[10] = 0x00;    // Bypass2
            test[11] = cmd1;    // CMD1

            return test;
        }

        private void Tx_SendCmd(byte cmd1, byte cmd2)      // 명령 전송
        {
            List<byte[]> txData = new List<byte[]>();
            txData.Clear();
            txData.Add(Tx_SetHeader(cmd1));                                         // Header 설정
            if (Tx_SetData(ref txData, cmd2))                                       // Data 설정
            {
                byte[] byteArray = txData.SelectMany(bytes => bytes).ToArray();     // Byte Lists Merge 1 Byte List
                //Console.WriteLine("Print TxData : " + ipAddress);

                // Append CRC ------------------------------------------------------
                byte[] packetData = new byte[byteArray.Length - 4];                        // Total Length - SYN
                Array.Copy(byteArray, 4, packetData, 0, packetData.Length);             // SYN CRC ETX 를 제외한 Recv 총 길이 = 계산길이 
                ushort calcCrc = crc16_ccitt(packetData);                               // 송신데이터 crc16 계산

                Array.Resize(ref byteArray, byteArray.Length + 3);                      // Append CRC + ETX
                // CRC BigEndian
                byteArray[byteArray.Length - 3] = (byte)(calcCrc >> 8);
                byteArray[byteArray.Length - 2] = (byte)calcCrc;
                byteArray[byteArray.Length - 1] = 0xF5;

                // Send To SRM
                Send(byteArray);
            }

            txData.Clear();
        }

        private bool Tx_SetData(ref List<byte[]> txData, byte cmd2)
        {
            ushort length;
            byte[] data = { };
            byte[] len = new byte[3];

            switch (cmd2)
            {
                case 0x10:
                    CMD_Req_SrmStruct(ref data);     //  0x0110  기본정보 조회            // req Reserve 동일 - 프로토콜 변경 시 별도 함수추가 필요
                    break;
                case 0x11:
                    CMD_Req_StdInfoControl(ref data);     //  0x0110  기본정보 제어            // req Reserve 동일 - 프로토콜 변경 시 별도 함수추가 필요
                    break;
                case 0x25:
                    CMD_Req_SrmStruct(ref data);     //  0x0125  SRM 장치구조 조회        // req Reserve 동일 - 프로토콜 변경 시 별도 함수추가 필요
                    break;
                case 0x30:
                    CMD_Req_State(ref data);        //  0x0030  SRM 상태조회
                    break;
                case 0x31:
                    CMD_Req_DriveInfo(ref data);    //  0x0031  운행정보 요구
                    break;
                case 0x32:
                    CMD_Req_InvStateInfo(ref data); //  0x0032  인버터정보 요구
                    break;
                case 0x34:
                    CMD_Req_AlarmLog(ref data);     //  0x0034  알람로그
                    break;
                case 0x41:
                    CMD_Req_Operation(ref data);     //  0x0041  반송명령
                    break;
                case 0x50:                          //  0x0050  시작
                    Array.Resize(ref data, 1);
                    data[0] = gClass.str.SrmPacket[srmNum].startOnOff;     //  시작 On/Off   0:Off, 1:On
                    break;
                case 0x53:                          //  작업삭제
                    Array.Resize(ref data, 1);
                    data[0] = (byte)gClass.str.SrmPacket[srmNum].flagJobDelete;     //  삭제Flag  Bit2~0 대기작업모두삭제,Fork2작업삭제, Fork1작업삭제
                    break;
                case 0x51:                          //  0x0051  홈복귀
                case 0x52:                          //  0x0052  이상리셋
                case 0x54:                          //  0x0054  정지
                case 0x55:                          //  0x0055  비상정지
                case 0x56:                          //  0x0056  일시정지
                case 0x57:                          //  0x0057  복구
                    break;
                case 0x59:                          //  0x0059  지정위치 이동 (Probe / 보수위치)
                    {
                        byte[] pd = gClass.str.SrmPacket[srmNum].probeData;
                        if (pd != null && pd.Length > 0)
                        {
                            Array.Resize(ref data, pd.Length);
                            Array.Copy(pd, data, pd.Length);
                        }
                        // probeData == null 이면 빈 패킷 전송 (보수위치 이동)
                    }
                    break;
                case 0x58:                          //  0x0058  모드설정 요청
                    Array.Resize(ref data, 2);
                    data[0] = (byte)gClass.str.SrmPacket[srmNum].modeSetCmd;      // 0: 수동모드, 1: 셋업모드, 2: 자동모드
                    data[1] = (byte)gClass.str.SrmPacket[srmNum].modeSetOpt;      // 1: 강제모드
                    break;
                case 0x92:                          //  0x0092  렉 기본 설정 조회
                    Array.Resize(ref data, 1);
                    data[0] = 0x01;                 //  Rack Type  1: SRM, 2: RTV
                    break;
                case 0x94:                          //  0x0094  셀 설정 조회
                    Array.Resize(ref data, 4);
                    data[0] = 0x01;                 //  Rack Type  1: SRM, 2: RTV
                    data[1] = (byte)gClass.str.SrmPacket[srmNum].rackReqType;       //  Data Type  1: Bay 좌측기준   2: Lev 좌측기준
                    data[2] = 0x00;                 //  START
                    data[3] = (byte)gClass.str.SrmPacket[srmNum].rackReqCount;      //  END
                    break;
                case 0x95:                          //  0x0095  셀 위치 설정 (Write)
                    {
                        int start = gClass.str.SrmPacket[srmNum].cellPosWriteStart;
                        int end = gClass.str.SrmPacket[srmNum].cellPosWriteEnd;
                        int count = end - start + 1;
                        int bayCount = gClass.str.SrmInfo[srmNum].bay;
                        int levCount = gClass.str.SrmInfo[srmNum].lev;
                        // 프로토콜: RackType(1) + BayCount(2) + LevCount(2) + DataType(1) + StartNo(1) + EndNo(1) = 8
                        int headerSize = 8;
                        Array.Resize(ref data, headerSize + count * 4);
                        data[0] = 0x01;         //  Rack Type  1: SRM
                        Buffer.BlockCopy(BitConverter.GetBytes((ushort)bayCount), 0, data, 1, 2);    // Bay Count (2 bytes LE)
                        Buffer.BlockCopy(BitConverter.GetBytes((ushort)levCount), 0, data, 3, 2);    // Level Count (2 bytes LE)
                        data[5] = (byte)gClass.str.SrmPacket[srmNum].cellPosWriteType;   // 1: Bay좌, 2: Level좌
                        data[6] = (byte)start;
                        data[7] = (byte)end;
                        for (int i = 0; i < count; i++)
                        {
                            Buffer.BlockCopy(BitConverter.GetBytes(gClass.str.SrmPacket[srmNum].cellPosWriteData[i]), 0, data, headerSize + i * 4, 4);
                        }
                        // 디버깅: 송신 데이터 로그
                        string txHex = "";
                        for (int i = 0; i < data.Length && i < 20; i++)
                            txHex += $"{data[i]:X2} ";
                        SaveLogFile($"0x0095 TX: Type={data[5]} BayCnt={bayCount} LevCnt={levCount} Start={start} End={end} Val={gClass.str.SrmPacket[srmNum].cellPosWriteData[0]} raw=[{txHex.TrimEnd()}]", false);
                    }
                    break;
                case 0x98:                          //  0x0098  스테이션 설정 조회
                    break;
                case 0x9C:                          //  0x009C  금지랙 설정 조회
                    break;
                case 0x80:                          //  0x0080  수동 조작명령
                    CMD_Req_ManualOperation(ref data);
                    break;
                case 0xA3:                          //  0x00A3  Drive 파라미터 읽기
                case 0xA5:                          //  0x00A5  Lift 파라미터 읽기
                    Array.Resize(ref data, 20);     //  STC_REC_PARAMReq (20B 빈 데이터)
                    break;
                case 0xA4:                          //  0x00A4  Drive 파라미터 쓰기
                    {
                        byte[] wd = gClass.str.SrmPacket[srmNum].driveParamWriteData;
                        if (wd != null && wd.Length > 0)
                        {
                            Array.Resize(ref data, wd.Length);
                            Array.Copy(wd, data, wd.Length);
                        }
                        SaveLogFile($"0x00A4 Drive Param Write TX len={data.Length}", false);
                    }
                    break;
                case 0xA6:                          //  0x00A6  Lift 파라미터 쓰기
                    {
                        byte[] wd = gClass.str.SrmPacket[srmNum].liftParamWriteData;
                        if (wd != null && wd.Length > 0)
                        {
                            Array.Resize(ref data, wd.Length);
                            Array.Copy(wd, data, wd.Length);
                        }
                        SaveLogFile($"0x00A6 Lift Param Write TX len={data.Length}", false);
                    }
                    break;
            }

            //Console.WriteLine("Tx_SetData data byteSize : " + data.Length);
            length = (ushort)data.Length;
            length += 1;    // CMD2 Length Add
            Buffer.BlockCopy(BitConverter.GetBytes(length), 0, len, 0, sizeof(ushort));     // Length - Command2 ~ Data
            len[2] = cmd2;

            txData.Add(len);
            txData.Add(data);

            return true;
        }

        #region 0x30~0x34 상태정보 ~ 로그정보 요구

        private void CMD_Req_State(ref byte[] data)      // 0x0030  상태조회
        {
            Array.Resize(ref data, data.Length + 33);
            data[0] = 0x03;     //  지상반 정보 유효/무효 - 0x01 : 유효
            //----------------------UTC Time Setup------------------------------
            DateTimeOffset utcTime = DateTimeOffset.UtcNow; // get the current UTC time offset
            int utcSeconds = (int)utcTime.ToUnixTimeSeconds(); // convert to seconds since Unix epoch
            byte[] bytes = BitConverter.GetBytes(utcSeconds);
            Buffer.BlockCopy(bytes, 0, data, 6, bytes.Length);     // 지상반 PC 시간 = UTC Time 4Byte

            data[10] = gClass.str.SrmState[srmNum].gcpState.gcpTxMode;     //  지상반 모드  1:수동, 2:반자동, 3:자동
            if (gClass.str.SrmState[srmNum].gcpState.heartBeat > 0)
            {
                gClass.str.SrmState[srmNum].gcpState.heartBeat = 0;
            }
            else
            {
                gClass.str.SrmState[srmNum].gcpState.heartBeat = 8;
            }
            data[11] =
                (byte)(((gClass.str.SrmPacket[srmNum].stWcsComm? 1 : 0) << 4) +                 // WCS COMM 상태전송
                gClass.str.SrmState[srmNum].gcpState.heartBeat + 
                ((gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.RESET].value ? 1 : 0) << 2) + 
                ((gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.EM_SW].value ? 0 : 1) << 1) + 
                (gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.SF_PLUG].value ? 0 : 1));     //  지상반 상태  8Bit to do

            data[12] = 0x00;     //  인터록(CV->장비) to do
            data[13] = 0x00;
            data[14] = 0x00;
            data[15] = 0x00;
            data[16] = 0x00;
            data[17] = 0x00;
            data[18] = 0x00;
            data[19] = 0x00;

            data[20] = 0x00;     //  인터록(장비->CV) to do
            data[21] = 0x00;
            data[22] = 0x00;
            data[23] = 0x00;
            data[24] = 0x00;
            data[25] = 0x00;
            data[26] = 0x00;
            data[27] = 0x00;

            data[28] = 0x00;     //  reserved
            data[29] = 0x00;
            data[30] = 0x00;
            data[31] = 0x00;
            data[32] = 0x00;

            // if (BitConverter.IsLittleEndian)
            //    Array.Reverse(byteArray);
        }

        private void CMD_Req_DriveInfo(ref byte[] data)      // 0x0031  운행정보 요구
        {
            Array.Resize(ref data, data.Length + 20);
            data[0] = 0x00;     //  reserved
            data[1] = 0x00;
            data[2] = 0x00;
            data[3] = 0x00;
            data[4] = 0x00;
            data[5] = 0x00;
            data[6] = 0x00;
            data[7] = 0x00;
            data[8] = 0x00;
            data[9] = 0x00;
            data[10] = 0x00;
            data[11] = 0x00;
            data[12] = 0x00;
            data[13] = 0x00;
            data[14] = 0x00;
            data[15] = 0x00;
            data[16] = 0x00;
            data[17] = 0x00;
            data[18] = 0x00;
            data[19] = 0x00;
        }

        private void CMD_Req_InvStateInfo(ref byte[] data)      // 0x0032  인버터 상태 요구
        {
            Array.Resize(ref data, data.Length + 20);
            data[0] = 0x00;     //  reserved
            data[1] = 0x00;
            data[2] = 0x00;
            data[3] = 0x00;
            data[4] = 0x00;
            data[5] = 0x00;
            data[6] = 0x00;
            data[7] = 0x00;
            data[8] = 0x00;
            data[9] = 0x00;
            data[10] = 0x00;
            data[11] = 0x00;
            data[12] = 0x00;
            data[13] = 0x00;
            data[14] = 0x00;
            data[15] = 0x00;
            data[16] = 0x00;
            data[17] = 0x00;
            data[18] = 0x00;
            data[19] = 0x00;
        }

        private void CMD_Req_AlarmLog(ref byte[] data)      // 0x0034  알람로그 조회
        {
            Array.Resize(ref data, data.Length + 12);
            data[0] = 0x00;     //  로그종류    0:알람로그, 1:이벤트로그
            data[1] = 0x00;     //  로그요청 Type

            ushort logCount = 0;
            byte[] bytes = BitConverter.GetBytes(logCount);
            Buffer.BlockCopy(bytes, 0, data, 2, bytes.Length);     // 지상반 PC 시간 = UTC Time 4Byte


            data[4] = 0x00;     //  로그요청 시작시간
            data[5] = 0x00;     //  로그요청 종료시간
            data[6] = 0x00;
            data[7] = 0x00;
            data[8] = 0x00;
            data[9] = 0x00;
            data[10] = 0x00;
            data[11] = 0x00;
        }

        #endregion

        #region 0x41 반송 명령
        private void CMD_Req_Operation(ref byte[] data)      // 0x0041  반송명령
        {
            Array.Resize(ref data, data.Length + 46);
            data[0] = (byte)gClass.str.SrmPacket[srmNum].reqJobCodeFk1;     //  작업 코드

            byte tmp = 0;
            if (gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.SEMI_AUTO].value)
            {
                if (gClass.str.SrmInfo[gClass.srmNum].ignoreCV)
                {
                    tmp += 2;
                }
                if (gClass.str.SrmInfo[gClass.srmNum].ignoreGoods)
                {
                    tmp += 1;
                }
            }

            data[1] = tmp;     //  Ignore CV Bit1 , IgnoreGoods :Bit0
            data[2] = 0x00;     //  reserved 2
            data[3] = 0x00;     //  reserved 3
            data[4] = 0x00;     //  reserved 4
            data[5] = 0x00;     //  reserved 5

            // Fork 1 Command-----------------------------------------------------------------------------------
            Buffer.BlockCopy(BitConverter.GetBytes(gClass.str.SrmPacket[srmNum].reqJobNoFk1), 0, data, 6, 4);    //  작업번호 4Byte 카피

            if (gClass.str.SrmPacket[srmNum].reqJobCodeFk1 == 0x01)  // 이동 명령만   Dest 버퍼를 To 영역으로
            {
                data[10] = 0;     //  From Station
                data[11] = 0;     //  From Row
                data[12] = 0;     //  From Bay 1
                data[13] = 0;     //  From Bay 2
                data[14] = 0;     //  From Lev

                data[15] = (byte)gClass.str.SrmPacket[srmNum].semiDestSt;     //  To Station
                data[16] = (byte)gClass.str.SrmPacket[srmNum].semiDestRow;     //  To Row
                Buffer.BlockCopy(BitConverter.GetBytes(gClass.str.SrmPacket[srmNum].semiDestBay), 0, data, 17, 2);    //  To Bay 2Byte 카피
                data[19] = (byte)gClass.str.SrmPacket[srmNum].semiDestLev;     //  To Lev
            }
            else
            {
                // Fork 1 Command-----------------------------------------------------------------------------------
                Buffer.BlockCopy(BitConverter.GetBytes(gClass.str.SrmPacket[srmNum].reqJobNoFk1), 0, data, 6, 4);    //  작업번호 4Byte 카피
                data[10] = (byte)gClass.str.SrmPacket[srmNum].reqFromStFk1;     //  From Station
                data[11] = (byte)gClass.str.SrmPacket[srmNum].reqFromRowFk1;     //  From Row
                Buffer.BlockCopy(BitConverter.GetBytes(gClass.str.SrmPacket[srmNum].reqFromBayFk1), 0, data, 12, 2);    //  From Bay 2Byte 카피
                data[14] = (byte)gClass.str.SrmPacket[srmNum].reqFromLevFk1;     //  From Lev

                data[15] = (byte)gClass.str.SrmPacket[srmNum].reqToStFk1;     //  To Station
                data[16] = (byte)gClass.str.SrmPacket[srmNum].reqToRowFk1;     //  To Row
                Buffer.BlockCopy(BitConverter.GetBytes(gClass.str.SrmPacket[srmNum].reqToBayFk1), 0, data, 17, 2);    //  To Bay 2Byte 카피
                data[19] = (byte)gClass.str.SrmPacket[srmNum].reqToLevFk1;     //  To Lev
            }
            data[20] = (byte)gClass.str.SrmPacket[srmNum].reqGoodsTypeFk1;     //  Goods Type

            data[21] = 0x00;     //  reserved 1
            data[22] = 0x00;     //  reserved 2
            data[23] = 0x00;     //  reserved 3
            data[24] = 0x00;     //  reserved 4
            data[25] = 0x00;     //  reserved 5
            // Fork 2 Command-----------------------------------------------------------------------------------
            Buffer.BlockCopy(BitConverter.GetBytes(gClass.str.SrmPacket[srmNum].reqJobNoFk2), 0, data, 26, 4);    //  작업번호 4Byte 카피
            data[30] = (byte)gClass.str.SrmPacket[srmNum].reqFromStFk2;     //  From Station
            data[31] = (byte)gClass.str.SrmPacket[srmNum].reqFromRowFk2;     //  From Row
            Buffer.BlockCopy(BitConverter.GetBytes(gClass.str.SrmPacket[srmNum].reqFromBayFk2), 0, data, 32, 2);    //  From Bay 2Byte 카피
            data[34] = (byte)gClass.str.SrmPacket[srmNum].reqFromLevFk2;     //  From Lev

            data[35] = (byte)gClass.str.SrmPacket[srmNum].reqToStFk2;     //  To Station
            data[36] = (byte)gClass.str.SrmPacket[srmNum].reqToRowFk2;     //  To Row
            Buffer.BlockCopy(BitConverter.GetBytes(gClass.str.SrmPacket[srmNum].reqToBayFk2), 0, data, 37, 2);    //  To Bay 2Byte 카피
            data[39] = (byte)gClass.str.SrmPacket[srmNum].reqToLevFk2;     //  To Lev

            data[40] = (byte)gClass.str.SrmPacket[srmNum].reqGoodsTypeFk2;     //  Goods Type

            data[41] = 0x00;     //  reserved 1
            data[42] = 0x00;     //  reserved 2
            data[43] = 0x00;     //  reserved 3
            data[44] = 0x00;     //  reserved 4
            data[45] = 0x00;     //  reserved 5
        }
        #endregion

        #region 0x80 수동 조작
        private void CMD_Req_ManualOperation(ref byte[] data)      //
        {
            Array.Resize(ref data, data.Length + 18);

            data[0] = (byte)gClass.str.SrmPacket[srmNum].manAxis;     //  제어 flag 2
            data[1] = 0x00;     //  제어 flag 2
            data[2] = gClass.str.SrmPacket[srmNum].manTrav;     //  주행
            data[3] = gClass.str.SrmPacket[srmNum].manLift;     //  승강
            data[4] = gClass.str.SrmPacket[srmNum].manFork1;     //  포크1
            data[5] = gClass.str.SrmPacket[srmNum].manFork2;     //  포크2
            data[6] = 0x00;     //  reserved 1
            data[7] = 0x00;     //  reserved 2
            data[8] = 0x00;     //  reserved 3
            data[9] = 0x00;     //  reserved 4
            data[10] = 0x00;    //  reserved 5
            data[11] = 0x00;    //  reserved 6
            data[12] = 0x00;    //  reserved 7
            data[13] = 0x00;    //  reserved 8
            data[14] = 0x00;    //  reserved 9
            data[15] = 0x00;    //  reserved 10
            data[16] = (byte)gClass.str.SrmInfo[srmNum].forkType;    //  포크 정위치 기준   
            data[17] = gClass.str.SrmPacket[srmNum].manPosStd;    //  저속 정위치 기준     1:포크1좌  2:포크1우  3:포크2좌  4:포크2우 
        }

        #endregion

        #region 0x0125 장치구조 조회
        private void CMD_Req_SrmStruct(ref byte[] data)      // 
        {
            Array.Resize(ref data, data.Length + 20);
            data[0] = 0x00;     //  reserved
            data[1] = 0x00;
            data[2] = 0x00;
            data[3] = 0x00;
            data[4] = 0x00;
            data[5] = 0x00;
            data[6] = 0x00;
            data[7] = 0x00;
            data[8] = 0x00;
            data[9] = 0x00;
            data[10] = 0x00;
            data[11] = 0x00;
            data[12] = 0x00;
            data[13] = 0x00;
            data[14] = 0x00;
            data[15] = 0x00;
            data[16] = 0x00;
            data[17] = 0x00;
            data[18] = 0x00;
            data[19] = 0x00;
        }
        #endregion

        #region 0x0111 기본정보 제어
        private void CMD_Req_StdInfoControl(ref byte[] data)      // 0x0111  기본정보 제어
        {
            Array.Resize(ref data, data.Length + 101);
            data[0] = 0x00;     //  reserved
            data[1] = 0x01;     //  System DateTime 설정
            data[2] = 0x00;
            data[3] = 0x00;
            //----------------------UTC Time Setup------------------------------
            DateTimeOffset utcTime = DateTimeOffset.UtcNow; // get the current UTC time offset
            int utcSeconds = (int)utcTime.ToUnixTimeSeconds(); // convert to seconds since Unix epoch
            byte[] bytes = BitConverter.GetBytes(utcSeconds);
            Buffer.BlockCopy(bytes, 0, data, 4, bytes.Length);     // 지상반 PC 시간 = UTC Time 4Byte
            for (int i = 8; i < data.Length; i++)
                data[i] = 0x00;
        }
        #endregion
    }
}
