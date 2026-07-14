using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace gcp_Wpf.commClass
{
    internal class tcpServerClass
    {
        private TcpListener tcpListener;
        private CancellationTokenSource cts;
        private TcpClient tcpClient;

        private CancellationTokenSource cReads;
        private Thread clientThread = null;
        private Thread listenThread = null;
        private int port;
        private int srmNum;
        public bool isListening = false;
        public bool isRunning = false;
        private int connectCnt = 0;
        static System.Threading.Mutex mutex = new System.Threading.Mutex();
        string pathString;
        private string rxMessage;
        private int rxCompCount;
        private string txMessage;
        // 이전 메시지 비교 저장용
        string curCmdStr;
        string preCmdStr;

        string preRejectStr;

        // 반송요청 응답 플래그
        int reqJobResponse;
        int preJobResponse;
        // 반송요청 검사 플래그
        int reqJobCheckResult;
        string reqJobCheckStr;

        //Singletone
        singletonClass gClass;
        MainWindow pMain;
        public static readonly Dictionary<int, Dictionary<int, int>> PositionValues = new Dictionary<int, Dictionary<int, int>>()
  {
            { 1, new Dictionary<int, int> { { -1, 1 }, { 0, 0 }, { 1, 2 } } },                      // SINGLE
            { 2, new Dictionary<int, int> { { -2, 1 }, { -1, 2 }, { 0, 0 }, { 1, 3 }, { 2, 4 } } }, // 2POS
            { 3, new Dictionary<int, int> { { -3, 1 }, { -2, 2 }, { -1, 3 }, { 0, 0 }, { 1, 4 }, { 2, 5 }, { 3, 6 } } } // 3POS
  };

        public tcpServerClass(MainWindow parent, int srmNum, int port)
        {
            pMain = parent;
            gClass = singletonClass.Instance;
            pathString = System.IO.Path.Combine(Environment.CurrentDirectory, "SRM" + srmNum, cConstDefine.PATH_LOG, cConstDefine.PATH_HOSTLOG);
            this.srmNum = srmNum;
            this.port = port;
            //StartServer(port);
        }
        public void StartServer(int port)
        {
            try
            {
                tcpListener = new TcpListener(IPAddress.Any, port);
                listenThread = new Thread(new ThreadStart(ListenForClients));
                isListening = true;
                listenThread.Start();
                Console.WriteLine("Server started.");
            }
            catch (Exception ex)
            {
                cIniAccess.SaveExLog(srmNum, "EXCEPTION - Tcp ServerStart : " + ex.Message);
            }
        }

        private async void ListenForClients()
        {
            Console.WriteLine("Listening for clients...");
            cts = new CancellationTokenSource(5000);
            // Check Main Process
            int watchDogCnt = 0;
            int watchCnt = 10;

            try
            {
                tcpListener.Start();
                Console.WriteLine($"Listening on port {port}...");

                while (isListening)
                {
                    //Console.WriteLine("Listening for Wait...");

                    if (watchCnt < 0)
                    {
                        if (watchDogCnt < cIniAccess.watchDogCnt)       // 변화가 없으면 메인종료
                        {
                            if (cIniAccess.watchDogCnt > 1)
                            {
                                watchDogCnt = cIniAccess.watchDogCnt;       // 메인타이머 시작 확인 후
                            }
                            watchCnt = 10;
                        }
                        else
                        {
                            SaveLogFile("Client Thread WATCHDOG Exit: ");
                            Console.WriteLine("WATCHDOG Exception - ListenForClients " + watchDogCnt + " " + cIniAccess.watchDogCnt);
                            isListening = false;
                            //continue;
                            break;
                        }
                    }
                    else
                    {
                        watchCnt -= 1;
                    }

                    try
                    {
                        Console.WriteLine("Client Listening Start.");
                        cts = new CancellationTokenSource(5000);
                        //var client = await tcpListener.AcceptTcpClientAsync().WithCancellation(cts.Token);
                        TcpClient client = tcpListener.AcceptTcpClient();
                        IPEndPoint clientEndpoint = client.Client.RemoteEndPoint as IPEndPoint;



                        Console.WriteLine("Client Connect Request.");

                        if (clientThread == null)           // 스레드 캔슬이 아닌이상 TCP스레드는 살아있음,
                        {
                            isRunning = true;
                            gClass.str.SrmPacket[srmNum].stWcsComm = true;           // WCS CONNECTED STATE
                            gClass.str.SrmPacket[srmNum].lastHeartBeatTime = DateTime.Now; // WCS Connection Init - HeartBeat Time Reset
                            SaveLogFile("Client Connected: " + clientEndpoint.Port);
                            clientThread = new Thread(new ParameterizedThreadStart(HandleClientComm));
                            clientThread.Start(client);
                        }
                        else
                        {
                            // 현재 접속중인 요청 외 접근은 접속해제
                            client.Close();
                            Thread.Sleep(1000);
                            SaveLogFile("Client Already Connected: ");
                            Console.WriteLine("Client Already Connected.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Listening Exception");
                        //cIniAccess.SaveExLog(0, "EXCEPTION - TCP ListenForClients : " + ex.Message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Listening canceled." + srmNum);
            }
            finally
            {
                Console.WriteLine("Listening finally." + srmNum);
                tcpListener.Stop();
            }
        }

        public bool getClientConnectedState()
        {
            bool result = false;
            if (tcpClient != null)
            {
                result = tcpClient.Connected;
            }
            return result;
        }

        private async void HandleClientComm(object client)
        {
            tcpClient = (TcpClient)client;

            // Socket 레벨에서 설정
            tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            // 더 세밀한 제어 (Windows 전용)
            uint keepAliveInterval = 1000; // 1초
            if (gClass.str.SrmInfo[srmNum].hostTimeout < 1)
            {
                gClass.str.SrmInfo[srmNum].hostTimeout = 2;
            }
            uint keepAliveTime = gClass.str.SrmInfo[srmNum].hostTimeout * 1000;    // 사용자 설정 타임아웃 주기
            byte[] inValue = new byte[12];
            BitConverter.GetBytes((uint)1).CopyTo(inValue, 0); // KeepAlive 활성화
            BitConverter.GetBytes(keepAliveTime).CopyTo(inValue, 4);
            BitConverter.GetBytes(keepAliveInterval).CopyTo(inValue, 8);
            tcpClient.Client.IOControl(IOControlCode.KeepAliveValues, inValue, null);


            NetworkStream clientStream = tcpClient.GetStream();
            IPEndPoint clientEndpoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;
            clientStream.ReadTimeout = 2000;
            cReads = new CancellationTokenSource();
            byte[] data = new byte[500];        // to do  byte 제한 오버플로우 시 처리 필요
            byte[] txData = { };
            int bytesRead = 0;

            int txlogCount = 0;
            int rxlogCount = 0;
            // Check Main Process
            int watchDogCnt = 0;
            int watchCnt = 5;

            try
            {
                while (isRunning)
                {
                    bytesRead = 0;
                    // Blocks until a client sends a message
                    if (watchCnt < 0)
                    {
                        if (watchDogCnt <= cIniAccess.watchDogCnt)
                        {
                            if (cIniAccess.watchDogCnt > 1)
                            {
                                watchDogCnt = cIniAccess.watchDogCnt;       // 메인타이머 시작 확인 후
                            }
                            watchCnt = 5;
                        }
                        else
                        {
                            SaveLogFile("TCP Thread WatcDog Exit: " + clientEndpoint.Port);
                            Console.WriteLine("TCP Thread WatchDog Exit");
                            isRunning = false;
                            continue;
                        }
                    }
                    else
                    {
                        watchCnt -= 1;
                    }

                    if (tcpClient.Connected)
                    {
                        try
                        {
                            //if (gClass.str.SrmInfo[srmNum].hostTimeout > 0)      // 타임아웃 시간 설정 시 타임아웃 스레드 종료
                            //{
                            //    clientStream.ReadTimeout = (int)gClass.str.SrmInfo[srmNum].hostTimeout * 1000;
                            //    bytesRead = clientStream.Read(data, 0, 255);
                            //}
                            //else
                            //{
                            //Console.WriteLine("Receive Wait TCP data: " + port);
                            bytesRead = await clientStream.ReadAsync(data, cReads.Token);
                            //Console.WriteLine("Receive Info : ");
                            //}

                            //bytesRead = await clientStream.ReadAsync(data, 0, 255);
                            //Console.WriteLine("ReadAsync Check");
                        }
                        catch (IOException ex)
                        {
                            // 소켓이 닫혔거나 연결이 끊긴 경우
                            Console.WriteLine("EXCEPTION - clientStream.Read (IOException): " + ex.Message);
                            SaveLogFile("EXCEPTION - TcpServer Recv IOException " + clientEndpoint.Port);
                            if (!isRunning || tcpClient == null || !tcpClient.Connected)
                            {
                                // 프로그램 종료 또는 소켓이 닫힌 경우 즉시 종료
                                Console.WriteLine("TcpServer Socket Closed, exiting thread");
                            }
                            // 타임아웃 등 기타 예외는 계속 진행
                            break;
                            //continue;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("EXCEPTION - clientStream.Read: " + ex.Message);
                            SaveLogFile("EXCEPTION - TcpServer Recv Exception " + clientEndpoint.Port);
                            if (!isRunning || tcpClient == null || !tcpClient.Connected)
                            {
                                // 프로그램 종료 또는 소켓이 닫힌 경우 즉시 종료
                                Console.WriteLine("TcpServer Socket Closed, exiting thread");
                                break;
                            }
                            continue;
                        }

                        //Console.WriteLine("tcpClient State - " + tcpClient.Connected);

                    }
                    else
                    {
                        Console.WriteLine("TcpClient Disconnected ");
                        break;
                    }
                    //bytesRead = await clientStream.ReadAsync(data, 0, 4096).WithCancellation(cts.Token);

                    if (bytesRead == 0)
                    {
                        isRunning = false;
                        Console.WriteLine("EXCEPTION - bytesRead is 0");
                        // The client has disconnected from the server
                        break;
                    }

                    gClass.str.SrmPacket[srmNum].rxWcsComm = true;                // WCS RX STATE ON

                    // Convert the message bytes to a string and display it.
                    //string message = System.Text.Encoding.ASCII.GetString(data, 0, bytesRead);            // Encoding ASCII Char
                    byte[] buffer = data.Take(bytesRead).ToArray();
                    string message = BitConverter.ToString(buffer).Replace("-", string.Empty);                   // Encoding Raw Byte
                    string truncatedMessage = message.Length > 12 ? message.Substring(12) : string.Empty;        // 앞에 ReqType 00/01 차이 무시하도록
                    //Console.WriteLine(string.Format("Received: {0} - {1}", data, port));
                    if (truncatedMessage != rxMessage)
                    {
                        SaveLogFile("WCS: (" + clientEndpoint.Port + ")" + message);
                        rxMessage = truncatedMessage;
                    }
                    else
                    {
                        ++rxlogCount;
                        if (rxlogCount > 50)
                        {
                            SaveLogFile("WCS Connection is Alive...rx");
                            rxlogCount = 0;
                        }
                    }
                    bool result = Rx_DataCheck(buffer);                   // 수신 정보 정합성 확인
                    ushort tmpBuf;

                    if (result)         // 리시브 헤더, CRC 정상확인
                    {
                        // Receive Data Parsing----------------------------------------------------
                        for (int i = 0; i < cConstDefine.WCSFROM; i++)
                        {
                            tmpBuf = BitConverter.ToUInt16(buffer, 6 + (i * 2));          // 수신 데이터 공용구조체 파싱
                            gClass.str.WcsPacket[srmNum].WCSFROM[i] = tmpBuf;
#if DONGWON
                            gClass.str.WcsPacket[srmNum].WCSFROM[i] = (ushort)(((tmpBuf & 0x00FF) << 8) | ((tmpBuf & 0xFF00) >> 8)); 
#endif
                        }
                        Rx_DataParse();         // 수신 데이터               // 250514 to do ing..

                    }
                    else
                    {
                        // to do 수신 데이터 비 정상 일 경우 처리
                        SaveLogFile("Host Data Parse Failed: " + port);
                    }


                    // Echo the message back to the client.
                    //byte[] buffer = System.Text.Encoding.ASCII.GetBytes(data);                              // ASCII To Byte

                    // TCP 전달용 jobRequest 비트 ON/OFF 조건 정리 (단일 메소드에서만 갱신)
                    UpdateJobRequestBitForTcp();

                    Tx_DataParse();


                    Tx_SetData(ref txData);
                    clientStream.Write(txData, 0, txData.Length);
                    clientStream.Flush();


                    gClass.str.SrmPacket[srmNum].txWcsComm = true;              // WCS TX STATE ON

                    message = BitConverter.ToString(txData).Replace("-", string.Empty);                   // Encoding Raw Byte
                    if (message != txMessage)
                    {
                        if (gClass.str.WcsPacket[srmNum].WCS_PARSE.reqType == 1)        // REQ TYPE 01 인건 무시
                        {
                            ++txlogCount;
                        }
                        else
                        {
                            SaveLogFile("GCP: " + message);
                            txMessage = message;
                        }
                    }
                    else
                    {
                        ++txlogCount;
                        if (txlogCount > 50)
                        {
                            SaveLogFile("WCS Connection is Alive...tx");
                            txlogCount = 0;
                        }
                    }
                }

                SaveLogFile("Client Disconnected: " + clientEndpoint.Port);
                Console.WriteLine("Client Disconnected." + clientEndpoint.Port);

                gClass.str.SrmPacket[srmNum].stWcsComm = false;           // WCS DISCONNECTED STATE

            }
            catch (OperationCanceledException)
            {
                SaveLogFile("Client Handle Comm Cancelation Error: " + port);
                Console.WriteLine("Read Waitfor canceled." + port);
            }
            catch (Exception ex)
            {
                cIniAccess.SaveExLog(0, "EXCEPTION - TcpServer HandleClientComm : " + ex.Message);
                //Console.WriteLine("Modbus Comm Error: " + ex.Message);
                // Handle the exception, e.g. display an error message
                SaveLogFile("Client Exception Error: " + ex.Message);
            }
            finally
            {
                SaveLogFile("Client Thread Finally: " + clientEndpoint.Port);
                Console.WriteLine("Read finally." + srmNum);
                try
                {
                    if (tcpClient != null)
                    {
                        tcpClient.Close();
                        tcpClient = null;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("TcpServer Finally Close Exception: " + ex.Message);
                }
                clientThread = null;
                gClass.str.SrmPacket[srmNum].stWcsComm = false;  // WCS DISCONNECTED STATE
                //tcpListener.Stop();
                //cReads.Dispose();
            }
        }


        // WCSTO DATA ARRAY SETUP
        private void Tx_SetData(ref byte[] txData)
        {
            List<byte[]> txList = new List<byte[]>();
            txList.Clear();
            byte[] dataBuf = { };

            int startIdx = 0;
            int endIdx = 0;
            if (gClass.str.WcsPacket[srmNum].WCS_PARSE.reqType == 0)
            {
                startIdx = 0;
                endIdx = 200;
            }
            else
            {
                startIdx = 200;
                endIdx = 300;
            }

            // Send Data Parsing----------------------------------------------------
            for (int i = startIdx; i < endIdx; i++) //250513 테스트 위해 수정
            {
                //gClass.str.WcsPacket[srmNum].WCSTO[i] = 8;
                byte[] wordArray = BitConverter.GetBytes(gClass.str.WcsPacket[srmNum].WCSTO[i]);
#if DONGWON
                if (BitConverter.IsLittleEndian) Array.Reverse(wordArray);
#endif
                txList.Add(wordArray);
            }

            dataBuf = txList.SelectMany(bytes => bytes).ToArray();
            // ---------------------------------------------------------------------
            ushort calcCrc = crc16_ccitt(dataBuf);                               // 송신데이터 crc16 계산


            // Tx DataBuf Setup-----------------------------------------------------
            Array.Resize(ref txData, dataBuf.Length + 9);                      // Resize SYN + + EQID + REQTYPE + CRC + ETX
            txData[0] = 0x16;                                                  // Append SYN
            txData[1] = 0x16;
            txData[2] = 0x16;
            txData[3] = 0x16;
            txData[4] = (byte)(gClass.str.SrmInfo[srmNum].srmID);                            // EQID
            txData[5] = (byte)(gClass.str.WcsPacket[srmNum].WCS_PARSE.reqType + 0x80);       // TYPE
            Array.Copy(dataBuf, 0, txData, 6, dataBuf.Length);             // Append SendBuf 
            txData[txData.Length - 3] = (byte)(calcCrc >> 8);                // Append CRC BigEndian
            txData[txData.Length - 2] = (byte)calcCrc;
            txData[txData.Length - 1] = 0xF5;
        }

        private bool Rx_DataCheck(byte[] recv)
        {
            // to do 송신데이터 주기적 업데이트 타이밍 또는 위치 고민
            // 수신 데이터 최소한의 구분을 위해 STX(0x16 * 4) / ETX(0xF5) 추가

            bool result = true;
            if (recv.Length < 209)        // SYN + DATA(52BYTE) + CRC + ETX
            {
                SaveLogFile("PacketError - Receive Length Failed (Received " + recv.Length + "Bytes )");           // 패킷 길이가 맞지않음
                return false;
            }
            // Check Header----SYN---0x16 X 4 Check-------------------
            for (int i = 0; i < 4; i++)
            {
                if (recv[i] != 0x16)
                {
                    result = false;
                    break;
                }
            }

            if (!result)
            {
                SaveLogFile("PacketError - Start Header Failed");  // SYN 헤더 형식이 맞지 않음
                return false;
            }


            // Check ETX ------------------------------------------------------
            if (recv[recv.Length - 1] != 0xF5)
            {
                SaveLogFile("PacketError - ETX Failed ");                        // ETX 를 찾지못함
                return false;
            }

            // Check EQID -----------------------------------------------------
            if (recv[4] != gClass.str.SrmInfo[srmNum].srmID)
            {
                SaveLogFile("PacketError - EQID Failed (Received " + recv[4] + " )");  // 요청 호기번호가 다름
                return false;
            }

            // Check REQ Type -------------------------------------------------
            if (recv[5] > 1)        // 00 / 01 외의 타입 요청 제외
            {
                SaveLogFile("PacketError - ReqType Failed (Received " + recv[5] + " )");  // 요청 타입번호가 다름
                return false;
            }

            // Check CRC ------------------------------------------------------
            byte[] packetData = new byte[recv.Length - 9];                        // Receive Length - (SYN, EQID, REQTYPE, CRC, ETX)
            Array.Copy(recv, 6, packetData, 0, packetData.Length);              // SYN EQID REQTYPE CRC ETX 를 제외한 Recv 총 길이 = 계산길이
            ushort recvCrc = (ushort)((recv[recv.Length - 3] << 8) | recv[recv.Length - 2]);      // CRC BigEndian   
            ushort calcCrc = crc16_ccitt(packetData);         // 수신데이터 작성데이터 crc16 계산

            if (recvCrc != calcCrc)
            {
                SaveLogFile("PacketError - CRC Data Failed");             // CRC 체크 에러
                return false;
            }

            // 수신 된 EQID / TYPE 저장
            gClass.str.WcsPacket[srmNum].WCS_PARSE.reqEqid = recv[4];       // EQID
            gClass.str.WcsPacket[srmNum].WCS_PARSE.reqType = recv[5];       // TYPE
            return true;
        }

        private void CopyReqBufferFromWcsParse()
        {
            // SRM 요청 버퍼 초기화 (작업 데이터 저장) - WCS에서 데이터 검사 후 DataOK를 주기 위해 req 변수에 복사
            gClass.str.SrmPacket[srmNum].reqWcsCodeFk1 = gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1JobCmd;
            gClass.str.SrmPacket[srmNum].reqWcsCodeFk2 = gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2JobCmd;

            // Fork1
            gClass.str.SrmPacket[srmNum].reqJobNoFk1 = gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1JobNo;

            gClass.str.SrmPacket[srmNum].reqFromStFk1 = (byte)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1FromSt;
            gClass.str.SrmPacket[srmNum].reqFromRowFk1 = (byte)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1FromRow;
            gClass.str.SrmPacket[srmNum].reqFromBayFk1 = gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1FromBay;
            gClass.str.SrmPacket[srmNum].reqFromLevFk1 = (byte)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1FromLev;

            gClass.str.SrmPacket[srmNum].reqToStFk1 = (byte)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1ToSt;
            gClass.str.SrmPacket[srmNum].reqToRowFk1 = (byte)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1ToRow;
            gClass.str.SrmPacket[srmNum].reqToBayFk1 = gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1ToBay;
            gClass.str.SrmPacket[srmNum].reqToLevFk1 = (byte)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1ToLev;

            // Fork2
            gClass.str.SrmPacket[srmNum].reqJobNoFk2 = gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2JobNo;

            gClass.str.SrmPacket[srmNum].reqFromStFk2 = (byte)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2FromSt;
            gClass.str.SrmPacket[srmNum].reqFromRowFk2 = (byte)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2FromRow;
            gClass.str.SrmPacket[srmNum].reqFromBayFk2 = gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2FromBay;
            gClass.str.SrmPacket[srmNum].reqFromLevFk2 = (byte)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2FromLev;

            gClass.str.SrmPacket[srmNum].reqToStFk2 = (byte)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2ToSt;
            gClass.str.SrmPacket[srmNum].reqToRowFk2 = (byte)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2ToRow;
            gClass.str.SrmPacket[srmNum].reqToBayFk2 = gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2ToBay;
            gClass.str.SrmPacket[srmNum].reqToLevFk2 = (byte)gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2ToLev;
        }

        private bool NeedRecoverReqBufferFromWcs(out string recoverTarget)
        {
            recoverTarget = string.Empty;

            bool fork1HasWcsJob = gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1JobNo > 0;
            bool fork2HasWcsJob = gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2JobNo > 0;

            bool fork1ReqInvalid =
                fork1HasWcsJob
                && (gClass.str.SrmPacket[srmNum].reqJobNoFk1 != gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1JobNo
                    || gClass.str.SrmPacket[srmNum].reqWcsCodeFk1 != gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1JobCmd);

            bool fork2ReqInvalid =
                fork2HasWcsJob
                && (gClass.str.SrmPacket[srmNum].reqJobNoFk2 != gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2JobNo
                    || gClass.str.SrmPacket[srmNum].reqWcsCodeFk2 != gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2JobCmd);

            if (fork1ReqInvalid && fork2ReqInvalid)
            {
                recoverTarget = "Fork1/Fork2";
                return true;
            }

            if (fork1ReqInvalid)
            {
                recoverTarget = "Fork1";
                return true;
            }

            if (fork2ReqInvalid)
            {
                recoverTarget = "Fork2";
                return true;
            }

            return false;
        }

        private void Rx_DataParse()
        {

            // Receive Data Parsing----------------------------------------------------
            gClass.str.WcsPacket[srmNum].WCS_PARSE.cmdPriority = (byte)gClass.str.WcsPacket[srmNum].WCSFROM[0];                         // D7000 Fork2 우선수행을 위한 우선순위 비트

            //---------------------------------------FORK1-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1JobNo = gClass.str.WcsPacket[srmNum].WCSFROM[1];                                // D7001
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1JobCmd = (byte)(gClass.str.WcsPacket[srmNum].WCSFROM[2] & 0xFF);                // D7002

            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1Move = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[2] & 0x01) >> 0);
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1Storage = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[2] & 0x02) >> 1);
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1Retrieval = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[2] & 0x04) >> 2);
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1RackToRack = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[2] & 0x08) >> 3);
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1StToSt = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[2] & 0x10) >> 4);
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1ChangeRack = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[2] & 0x20) >> 5);
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1ChangeSt = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[2] & 0x40) >> 6);
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1Sticky = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[2] & 0x80) >> 7);

            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1FromSt = gClass.str.WcsPacket[srmNum].WCSFROM[3];                               // D7003
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1FromRow = gClass.str.WcsPacket[srmNum].WCSFROM[4];                              // D7004
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1FromBay = gClass.str.WcsPacket[srmNum].WCSFROM[5];                              // D7005
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1FromLev = gClass.str.WcsPacket[srmNum].WCSFROM[6];                              // D7006

            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1ToSt = gClass.str.WcsPacket[srmNum].WCSFROM[7];                                 // D7007
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1ToRow = gClass.str.WcsPacket[srmNum].WCSFROM[8];                                // D7008
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1ToBay = gClass.str.WcsPacket[srmNum].WCSFROM[9];                                // D7009
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1ToLev = gClass.str.WcsPacket[srmNum].WCSFROM[10];                               // D7010

            //---------------------------------------FORK2-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2JobNo = gClass.str.WcsPacket[srmNum].WCSFROM[11];                                // D7011
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2JobCmd = (byte)(gClass.str.WcsPacket[srmNum].WCSFROM[12] & 0xFF);                // D7012

            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2Move = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[12] & 0x01) >> 0);
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2Storage = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[12] & 0x02) >> 1);
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2Retrieval = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[12] & 0x04) >> 2);
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2RackToRack = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[12] & 0x08) >> 3);
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2StToSt = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[12] & 0x10) >> 4);
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2ChangeRack = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[12] & 0x20) >> 5);
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2ChangeSt = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[12] & 0x40) >> 6);
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2Sticky = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[12] & 0x80) >> 7);

            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2FromSt = gClass.str.WcsPacket[srmNum].WCSFROM[13];                              // D7013
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2FromRow = gClass.str.WcsPacket[srmNum].WCSFROM[14];                             // D7014
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2FromBay = gClass.str.WcsPacket[srmNum].WCSFROM[15];                             // D7015
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2FromLev = gClass.str.WcsPacket[srmNum].WCSFROM[16];                             // D7016

            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2ToSt = gClass.str.WcsPacket[srmNum].WCSFROM[17];                                // D7017
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2ToRow = gClass.str.WcsPacket[srmNum].WCSFROM[18];                               // D7018
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2ToBay = gClass.str.WcsPacket[srmNum].WCSFROM[19];                               // D7019
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2ToLev = gClass.str.WcsPacket[srmNum].WCSFROM[20];                               // D7020

            byte newHeartBeat = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[25] & 0x01) >> 0);
            if (gClass.str.SrmPacket[srmNum].lastHeartBeat != newHeartBeat || gClass.str.SrmPacket[srmNum].lastHeartBeatTime == DateTime.MinValue)
            {
                gClass.str.SrmPacket[srmNum].lastHeartBeat = newHeartBeat;
                gClass.str.SrmPacket[srmNum].lastHeartBeatTime = DateTime.Now;
            }
            gClass.str.WcsPacket[srmNum].WCS_PARSE.heartBeat = newHeartBeat;          // D7025
            gClass.str.WcsPacket[srmNum].WCS_PARSE.homeReturn = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[25] & 0x02) >> 1);
            gClass.str.WcsPacket[srmNum].WCS_PARSE.errorReset = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[25] & 0x04) >> 2);
            gClass.str.WcsPacket[srmNum].WCS_PARSE.jobDelete = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[25] & 0x08) >> 3);
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1Delete = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[25] & 0x10) >> 4);
            gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2Delete = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[25] & 0x20) >> 5);
            gClass.str.WcsPacket[srmNum].WCS_PARSE.timeSynchro = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[25] & 0x40) >> 6);
            gClass.str.WcsPacket[srmNum].WCS_PARSE.dataReportOK = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[25] & 0x100) >> 8);
            gClass.str.WcsPacket[srmNum].WCS_PARSE.srmOnline = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[25] & 0x400) >> 10);
            gClass.str.WcsPacket[srmNum].WCS_PARSE.srmManual = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[25] & 0x800) >> 11);
            gClass.str.WcsPacket[srmNum].WCS_PARSE.srmCycleStop = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[25] & 0x4000) >> 14);
            gClass.str.WcsPacket[srmNum].WCS_PARSE.srmEmStop = (byte)((gClass.str.WcsPacket[srmNum].WCSFROM[25] & 0x8000) >> 15);


            // to do Request 비트 체크하여 작업 수신 가능여부 일 때 버퍼에 작업저장
            //gClass.str.SrmPacket[srmNum].reqFromStFk1;
            // SRrequestCmd

            gClass.str.SrmPacket[srmNum].resMainCode = 00;
            gClass.str.SrmPacket[srmNum].resSubCode = 00;

            // to do 시작요청 ack 줄건지
            //if (gClass.str.WcsPacket[srmNum].WCS_PARSE.srmOnline == 0 && gClass.str.SrmPacket[srmNum].wcsAckSrmOnline)      // 시작요청 OFF
            //{
            //    gClass.str.SrmPacket[srmNum].wcsAckSrmOnline = false;
            //    cIniAccess.SaveJobLog(srmNum, "WCS 시작요청 Ack : False");
            //}
            //if (gClass.str.WcsPacket[srmNum].WCS_PARSE.srmOnline == 0 && gClass.str.SrmPacket[srmNum].wcsAckSrmOnline)      // 시작요청 OFF
            //{
            //    gClass.str.SrmPacket[srmNum].wcsAckSrmOnline = false;
            //    cIniAccess.SaveJobLog(srmNum, "WCS 시작요청 Ack : False");
            //}

            // 이상리셋
            if ((gClass.str.WcsPacket[srmNum].WCS_PARSE.errorReset != gClass.str.WcsPacket[srmNum].WCS_BUF.errorReset))
            {
                if (gClass.str.WcsPacket[srmNum].WCS_PARSE.errorReset > 0)
                {
                    curCmdStr = "이상리셋";
                    gClass.str.SrmPacket[srmNum].wcsJobReceive = true;
                    cIniAccess.SaveJobLog(srmNum, "WCS -> GCP == WCS 이상리셋요청");
                    // D7625  리셋 수신 Ack: ON  펄스명령 이므로 바로 Ack
                    gClass.str.SrmPacket[srmNum].wcsAckReset = true;
                    cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 이상리셋요청 Ack : True");
                    if (gClass.str.SrmState[srmNum].dSt1Abnormal == 0 && gClass.str.SrmState[srmNum].dSt1Warning == 0 && !gClass.str.SrmPacket[srmNum].gcpError)
                    {
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 이상리셋요청 - 수신성공:이상없음");

                        gClass.str.SrmPacket[srmNum].resMainCode = 00;          // 수신성공 
                    }
                    else
                    {
                        gClass.str.SrmPacket[srmNum].gcpError = false;
                        gClass.str.SrmPacket[srmNum].gcpWarning = false;
                        gClass.str.SrmPacket[srmNum].recovError = false;
                        gClass.str.SrmPacket[srmNum].jobError = false;
                        gClass.str.DioPacket[srmNum].DOSET[(int)DOSTATE.BUZZER].value = false;        // BUZZER OFF 이상리셋 시 부저정지
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 이상리셋요청 - 수신성공");
                        gClass.str.SrmPacket[srmNum].resMainCode = 00;          // 수신불가 상태 
                        gClass.str.SrmPacket[srmNum].wcsCmdReset = true;
                        gClass.str.SrmPacket[srmNum].gcpModemFlt = false;
                    }
                }
                else
                {
                    cIniAccess.SaveJobLog(srmNum, "WCS -> GCP == WCS 이상리셋요청 해제");
                    gClass.str.SrmPacket[srmNum].wcsAckReset = false;
                    cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 에러리셋 Ack : False");
                }
            }

            // 전체작업 삭제
            if ((gClass.str.WcsPacket[srmNum].WCS_PARSE.jobDelete != gClass.str.WcsPacket[srmNum].WCS_BUF.jobDelete))
            {
                if (gClass.str.WcsPacket[srmNum].WCS_PARSE.jobDelete > 0)
                {
                    curCmdStr = "Fork1/2 작업삭제";
                    gClass.str.SrmPacket[srmNum].wcsJobReceive = true;
                    cIniAccess.SaveJobLog(srmNum, "WCS -> GCP == WCS 전체작업삭제");
                    // D7625  전체작업삭제 수신 Ack: ON  펄스명령 이므로 바로 Ack
                    if (gClass.str.SrmPacket[srmNum].wcsReqCompleteFork1 > 0 || gClass.str.SrmPacket[srmNum].wcsReqCompleteFork2 > 0) // GCP -> WCS 수동삭제요청에 대한 피드백
                    {
                        gClass.str.SrmPacket[srmNum].wcsReqCompleteFork1 = 0;
                        gClass.str.SrmPacket[srmNum].wcsReqCompleteFork2 = 0;
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 전체작업삭제 - 수동완료요청 피드백 OFF");
                    }
                    if (gClass.str.SrmPacket[srmNum].wcsReqDeleteFork1 > 0 || gClass.str.SrmPacket[srmNum].wcsReqDeleteFork2 > 0)
                    {
                        gClass.str.SrmPacket[srmNum].wcsReqDeleteFork1 = 0;
                        gClass.str.SrmPacket[srmNum].wcsReqDeleteFork2 = 0;
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 전체작업삭제 - 수동삭제요청 피드백 OFF");
                    }

                    //if (gClass.str.SrmPacket[srmNum].operState &&
                    //    (gClass.str.SrmState[srmNum].fork1.jobNo > 0 && (gClass.str.SrmState[srmNum].fork1.jobNo == gClass.str.SrmPacket[srmNum].reqJobNoFk1))
                    //    && (gClass.str.SrmState[srmNum].fork2.jobNo > 0 && (gClass.str.SrmState[srmNum].fork2.jobNo == gClass.str.SrmPacket[srmNum].reqJobNoFk2)))
                    //{
                    //    // 동작 중 && 현재 SRM 수행중인 작업이 버퍼작업과 동일하면
                    //    gClass.str.SrmPacket[srmNum].resMainCode = 02;          // 실행 중
                    //    cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 전체 반송작업삭제 - 실패:동작 중");
                    //}
                    //else if (gClass.str.SrmPacket[srmNum].operState &&
                    //    (gClass.str.SrmState[srmNum].fork1.mvJobNo > 0 && (gClass.str.SrmState[srmNum].fork1.mvJobNo == gClass.str.SrmPacket[srmNum].reqJobNoFk1))
                    //    && (gClass.str.SrmState[srmNum].fork2.mvJobNo > 0 && (gClass.str.SrmState[srmNum].fork2.mvJobNo == gClass.str.SrmPacket[srmNum].reqJobNoFk2)))
                    //{
                    //    // 동작 중 && 현재 SRM 수행중인 작업이 버퍼작업과 동일하면
                    //    gClass.str.SrmPacket[srmNum].resMainCode = 02;          // 실행 중
                    //    cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 전체 이동작업삭제 - 실패:동작 중");
                    //}
                    if ((gClass.str.SrmState[srmNum].fork1.jobNo == 0) && (gClass.str.SrmState[srmNum].fork2.jobNo == 0)
                        && (gClass.str.SrmState[srmNum].fork1.mvJobNo == 0) && (gClass.str.SrmState[srmNum].fork2.mvJobNo == 0))
                    {
                        // 이미 삭제 된 작업
                        gClass.str.SrmPacket[srmNum].resMainCode = 00;          // 실행 중
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 전체작업삭제 - 수신성공-이미삭제됨");
                        gClass.str.SrmPacket[srmNum].wcsCmdDeleteAll = true;
                        _ = Task.Run(async () => await MonitorStatus_AllFork());
                    }
                    else
                    {
                        gClass.str.SrmPacket[srmNum].resMainCode = 0;
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 전체작업삭제 - 수신성공");
                        gClass.str.SrmPacket[srmNum].wcsCmdDeleteAll = true;
                        _ = Task.Run(async () => await MonitorStatus_AllFork());
                    }
                }
                else
                {
                    cIniAccess.SaveJobLog(srmNum, "WCS -> GCP == WCS 전체작업삭제 해제");
                    if (gClass.str.SrmPacket[srmNum].wcsAckDeleteAll)      // ACK 상태일 경우 ACK / 완료비트 해제
                    {
                        gClass.str.SrmPacket[srmNum].wcsAckDeleteAll = false;
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 전체작업삭제 Ack : False");
                        if (gClass.str.SrmPacket[srmNum].fork1JobComplete > 0)
                        {
                            gClass.str.SrmPacket[srmNum].fork1JobComplete = 0;
                            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == Fork1 작업완료 : OFF");
                        }
                        if (gClass.str.SrmPacket[srmNum].fork2JobComplete > 0)
                        {
                            gClass.str.SrmPacket[srmNum].fork2JobComplete = 0;
                            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == Fork2 작업완료 : OFF");
                        }
                    }
                }
            }

            // Fork1 작업삭제 명령 수신
            if ((gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1Delete != gClass.str.WcsPacket[srmNum].WCS_BUF.fork1Delete))
            {
                if (gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1Delete > 0)
                {
                    curCmdStr = "Fork1 작업삭제";
                    gClass.str.SrmPacket[srmNum].wcsJobReceive = true;
                    cIniAccess.SaveJobLog(srmNum, "WCS -> GCP == WCS Fork1 작업삭제");

                    if (gClass.str.SrmPacket[srmNum].wcsReqCompleteFork1 > 0)
                    {
                        gClass.str.SrmPacket[srmNum].wcsReqCompleteFork1 = 0;
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS Fork1 작업삭제 - 수동완료요청 피드백 OFF");
                    }
                    if (gClass.str.SrmPacket[srmNum].wcsReqDeleteFork1 > 0)
                    {
                        gClass.str.SrmPacket[srmNum].wcsReqDeleteFork1 = 0;
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS Fork1 작업삭제 - 수동삭제요청 피드백 OFF");
                    }

                    //}
                    if (gClass.str.SrmState[srmNum].fork1.jobNo == 0 && gClass.str.SrmState[srmNum].fork1.mvJobNo == 0)
                    {
                        gClass.str.SrmPacket[srmNum].resMainCode = 0;
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS Fork1 작업삭제 - 수신성공-이미삭제됨");
                        gClass.str.SrmPacket[srmNum].wcsCmdDeleteFork1 = true;
                        _ = Task.Run(async () => await MonitorStatus_Fork1());
                    }
                    else
                    {
                        gClass.str.SrmPacket[srmNum].resMainCode = 0;
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS Fork1 작업삭제 - 수신성공");
                        gClass.str.SrmPacket[srmNum].wcsCmdDeleteFork1 = true;
                        _ = Task.Run(async () => await MonitorStatus_Fork1());
                    }
                }
                else
                {
                    cIniAccess.SaveJobLog(srmNum, "WCS -> GCP == WCS Fork1 작업삭제 해제");
                    if (gClass.str.SrmPacket[srmNum].wcsAckDeleteFork1)      // Fork1 작업삭제 OFF
                    {
                        gClass.str.SrmPacket[srmNum].wcsAckDeleteFork1 = false;
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS Fork1 작업삭제 Ack : False");
                        if (gClass.str.SrmPacket[srmNum].fork1JobComplete > 0)
                        {
                            gClass.str.SrmPacket[srmNum].fork1JobComplete = 0;
                            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == Fork1 작업완료 : OFF");
                        }
                    }
                }
            }
            // Fork2 작업삭제 명령 수신
            if ((gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2Delete != gClass.str.WcsPacket[srmNum].WCS_BUF.fork2Delete))
            {
                if (gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2Delete > 0)
                {
                    curCmdStr = "Fork2 작업삭제";
                    gClass.str.SrmPacket[srmNum].wcsJobReceive = true;
                    cIniAccess.SaveJobLog(srmNum, "WCS -> GCP == WCS Fork2 작업삭제");
                    if (gClass.str.SrmPacket[srmNum].wcsReqCompleteFork2 > 0)
                    {
                        gClass.str.SrmPacket[srmNum].wcsReqCompleteFork2 = 0;
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS Fork2 작업삭제 - 수동완료요청 피드백 OFF");
                    }
                    if (gClass.str.SrmPacket[srmNum].wcsReqDeleteFork2 > 0)
                    {
                        gClass.str.SrmPacket[srmNum].wcsReqDeleteFork2 = 0;
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS Fork2 작업삭제 - 수동삭제요청 피드백 OFF");
                    }

                    //if (gClass.str.SrmPacket[srmNum].operState &&
                    //    (gClass.str.SrmState[srmNum].fork2.jobNo > 0 && (gClass.str.SrmState[srmNum].fork2.jobNo == gClass.str.SrmPacket[srmNum].reqJobNoFk2)))
                    //{
                    //    // 동작 중 && 현재 SRM 수행중인 작업이 버퍼작업과 동일하면
                    //    gClass.str.SrmPacket[srmNum].resMainCode = 02;          // 실행 중
                    //    cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS Fork2 반송작업삭제 - 실패:실행 중");
                    //}
                    //else if (gClass.str.SrmPacket[srmNum].operState &&
                    //    (gClass.str.SrmState[srmNum].fork2.mvJobNo > 0 && (gClass.str.SrmState[srmNum].fork2.mvJobNo == gClass.str.SrmPacket[srmNum].reqJobNoFk2)))
                    //{
                    //    // 동작 중 && 현재 SRM 수행중인 작업이 버퍼작업과 동일하면
                    //    gClass.str.SrmPacket[srmNum].resMainCode = 02;          // 실행 중
                    //    cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS Fork2 이동작업삭제 - 실패:실행 중");
                    //}
                    if (gClass.str.SrmState[srmNum].fork2.jobNo == 0 && gClass.str.SrmState[srmNum].fork2.mvJobNo == 0)
                    {
                        gClass.str.SrmPacket[srmNum].resMainCode = 0;
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS Fork2 작업삭제 - 수신성공-이미삭제됨");
                        gClass.str.SrmPacket[srmNum].wcsCmdDeleteFork2 = true;
                        _ = Task.Run(async () => await MonitorStatus_Fork2());
                    }
                    else
                    {
                        gClass.str.SrmPacket[srmNum].resMainCode = 0;
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS Fork2 작업삭제 - 수신성공");
                        gClass.str.SrmPacket[srmNum].wcsCmdDeleteFork2 = true;
                        _ = Task.Run(async () => await MonitorStatus_Fork2());
                    }
                }
                else
                {
                    cIniAccess.SaveJobLog(srmNum, "WCS -> GCP == WCS Fork2 작업삭제 해제");
                    if (gClass.str.SrmPacket[srmNum].wcsAckDeleteFork2)      // Fork2 작업삭제 OFF
                    {
                        gClass.str.SrmPacket[srmNum].wcsAckDeleteFork2 = false;
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS Fork2 작업삭제 Ack : False");
                        if (gClass.str.SrmPacket[srmNum].fork2JobComplete > 0)
                        {
                            gClass.str.SrmPacket[srmNum].fork2JobComplete = 0;
                            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == Fork2 작업완료 : OFF");
                        }
                    }
                }
            }

            //----------------------------------------------------------------------------키 자동 상태 수신조건---------------------------------------------------------------------------------------------------

            // 홈복귀 명령 수신
            if (gClass.str.WcsPacket[srmNum].WCS_PARSE.srmEmStop != gClass.str.WcsPacket[srmNum].WCS_BUF.srmEmStop)
            {
                if (gClass.str.WcsPacket[srmNum].WCS_PARSE.srmEmStop > 0)
                {
                    cIniAccess.SaveJobLog(srmNum, "WCS -> GCP == WCS EmStop 요청");
                    gClass.str.SrmPacket[srmNum].wcsAckEmergencyStop = true;
                    cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS EmStop Ack : True");
                    if (gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.AUTO].value == false)            // 지상반이 자동 상태 일때만 요청작업 수신 가능
                    {
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS EmStop - 실패:수신불가상태(자동모드아님)");
                    }
                    else
                    {
                        gClass.str.SrmPacket[srmNum].wcsCmdEmergencyStop = true;
                    }
                }
                else
                {
                    cIniAccess.SaveJobLog(srmNum, "WCS -> GCP == WCS EmStop 요청 해제");
                    gClass.str.SrmPacket[srmNum].wcsCmdEmergencyStop = false;
                    if (gClass.str.SrmPacket[srmNum].wcsAckEmergencyStop)
                    {
                        gClass.str.SrmPacket[srmNum].wcsAckEmergencyStop = false;
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS EmStop Ack : False");
                    }
                }
            }

            if (gClass.str.WcsPacket[srmNum].WCS_PARSE.srmCycleStop != gClass.str.WcsPacket[srmNum].WCS_BUF.srmCycleStop)
            {
                if (gClass.str.WcsPacket[srmNum].WCS_PARSE.srmCycleStop > 0)
                {
                    cIniAccess.SaveJobLog(srmNum, "WCS -> GCP == WCS CycleStop 요청");
                    gClass.str.SrmPacket[srmNum].wcsAckCycleStop = true;
                    cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS CycleStop Ack : True");
                    if (gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.AUTO].value == false)            // 지상반이 자동 상태 일때만 요청작업 수신 가능
                    {
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS CycleStop - 실패:수신불가상태(자동모드아님)");
                    }
                    else
                    {
                        gClass.str.SrmPacket[srmNum].wcsCmdCycleStop = true;
                    }
                }
                else
                {
                    cIniAccess.SaveJobLog(srmNum, "WCS -> GCP == WCS CycleStop 요청 해제");
                    gClass.str.SrmPacket[srmNum].wcsCmdCycleStop = false;
                    if (gClass.str.SrmPacket[srmNum].wcsAckCycleStop)
                    {
                        gClass.str.SrmPacket[srmNum].wcsAckCycleStop = false;
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS CycleStop Ack : False");
                    }
                }
            }

            if ((gClass.str.WcsPacket[srmNum].WCS_PARSE.homeReturn != gClass.str.WcsPacket[srmNum].WCS_BUF.homeReturn))
            {
                if (gClass.str.WcsPacket[srmNum].WCS_PARSE.homeReturn > 0)
                {
                    curCmdStr = "홈복귀";
                    gClass.str.SrmPacket[srmNum].wcsJobReceive = true;

                    cIniAccess.SaveJobLog(srmNum, "WCS -> GCP == WCS 홈복귀요청");
                    // D7625  홈복귀 수신 Ack: ON  펄스명령 이므로 바로 Ack
                    gClass.str.SrmPacket[srmNum].wcsAckHomeReturn = true;
                    cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 홈복귀요청 Ack : True");

                    if (gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.AUTO].value == false)            // 지상반이 자동 상태 일때만 요청작업 수신 가능
                    {
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 홈복귀요청 - 실패:수신불가상태(자동모드아님)");
                    }
                    else if (gClass.str.SrmState[srmNum].trav.homeMove > 0 || gClass.str.SrmState[srmNum].lift.homeMove > 0)
                    {
                        gClass.str.SrmPacket[srmNum].resMainCode = 02;          // 홈복귀 실행 중
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 홈복귀요청 - 실패:실행 중");
                    }
                    else if (gClass.str.SrmState[srmNum].dSt1StartSt == 0)       // 장치 시작 상태가 아님
                    {
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 홈복귀요청 - 실패:수신불가상태");
                        gClass.str.SrmPacket[srmNum].resMainCode = 03;          // 수신불가 상태 
                    }
                    else if (gClass.str.SrmState[srmNum].dSt2homePos > 0)
                    {
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 홈복귀요청 - 수신성공:홈위치");
                        gClass.str.SrmPacket[srmNum].resMainCode = 00;          // 홈 위치 상태 
                    }
                    else
                    {
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 홈복귀요청 - 수신성공");
                        // to do 홈복귀 시도 특정 조건이면 해제 되도록 하는 부분 추가필요 함 에러발생 이라던지
                        gClass.str.SrmPacket[srmNum].wcsCmdHomeReturn = true;           // 장비 상태 홈복귀 중 으로 바뀌면 상태 해제
                        gClass.str.SrmPacket[srmNum].resMainCode = 00;          // 수신불가 상태 
                    }
                }
                else
                {
                    cIniAccess.SaveJobLog(srmNum, "WCS -> GCP == WCS 홈복귀요청 해제");
                    if (gClass.str.SrmPacket[srmNum].wcsAckHomeReturn)      // 홈복귀 OFF
                    {
                        gClass.str.SrmPacket[srmNum].wcsAckHomeReturn = false;
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 홈복귀요청 Ack : False");
                    }
                }
            }

            // 온라인 명령 수신
            if ((gClass.str.WcsPacket[srmNum].WCS_PARSE.srmOnline != gClass.str.WcsPacket[srmNum].WCS_BUF.srmOnline))
            {
                if (gClass.str.WcsPacket[srmNum].WCS_PARSE.srmOnline > 0)
                {
                    curCmdStr = "시작요청";
                    gClass.str.SrmPacket[srmNum].wcsJobReceive = true;
                    cIniAccess.SaveJobLog(srmNum, "WCS -> GCP == WCS 시작요청 - SRM Auto Request ON");


                    if (gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.AUTO].value == false)            // 지상반이 자동 상태 일때만 요청작업 수신 가능
                    {
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 시작요청 - 실패:수신불가상태(자동모드아님)");
                    }
                    else if (gClass.str.SrmPacket[srmNum].startEnable)
                    {
                        if (gClass.str.SrmPacket[srmNum].gcpError)
                        {
                            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 시작요청 - 수신실패:크레인이상(01)");
                            gClass.str.SrmPacket[srmNum].resMainCode = 01;          // 실패, 크레인 이상상태 (리셋필요)
                        }
                        else if (gClass.str.SrmState[srmNum].dSt1StartSt > 0)
                        {
                            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 시작요청 - 수신성공:이미시작상태");
                            gClass.str.SrmPacket[srmNum].resMainCode = 00;          // 이미 처리 됨
                        }
                        else
                        {
                            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 시작요청 - 수신성공");
                            gClass.str.SrmPacket[srmNum].resMainCode = 00;          // 수신불가 상태 
                            gClass.str.SrmPacket[srmNum].startOnOff = 1;        // TX SubItem = START ON
                            gClass.str.SrmPacket[srmNum].wcsCmdSrmOnline = true;
                        }
                    }
                    else
                    {
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 시작요청 - 수신실패:Ready상태 아님");
                        gClass.str.SrmPacket[srmNum].resMainCode = 01;          // 실패, 크레인 이상상태 (리셋필요)
                    }
                }
                else
                {
                    cIniAccess.SaveJobLog(srmNum, "WCS -> GCP == WCS 시작요청 해제");
                }
            }

            if (gClass.str.WcsPacket[srmNum].WCS_PARSE.srmManual != gClass.str.WcsPacket[srmNum].WCS_BUF.srmManual)
            {
                if (gClass.str.WcsPacket[srmNum].WCS_PARSE.srmManual > 0)
                {
                    curCmdStr = "수동요청";
                    gClass.str.SrmPacket[srmNum].wcsJobReceive = true;
                    cIniAccess.SaveJobLog(srmNum, "WCS -> GCP == WCS 수동요청 - SRM Manual Request ON");
                    if (gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.AUTO].value == false)            // 지상반이 자동 상태 일때만 요청작업 수신 가능
                    {
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 수동요청 - 실패:수신불가상태(자동모드아님)");
                    }
                    else if (gClass.str.SrmState[srmNum].dSt1StartSt < 0)
                    {
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 수동요청 - 수신성공:이미수동상태");
                        gClass.str.SrmPacket[srmNum].resMainCode = 0;
                    }
                    else
                    {
                        cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 수동요청 - 수신성공");
                        gClass.str.SrmPacket[srmNum].resMainCode = 0;
                        gClass.str.SrmPacket[srmNum].startOnOff = 0;
                        gClass.str.SrmPacket[srmNum].wcsCmdSrmManual = true;
                    }
                }
                else
                {
                    cIniAccess.SaveJobLog(srmNum, "WCS -> GCP == WCS 수동요청 해제");
                }
            }

            // WCS 수신 값 중 한개라도 변경되면 로깅 하도록
            if (Enumerable.Range(0, 21).Any(index => gClass.str.WcsPacket[srmNum].WCSFROMBUF[index] != gClass.str.WcsPacket[srmNum].WCSFROM[index]))
            {
                ref Wcs_From refState = ref gClass.str.WcsPacket[srmNum].WCS_PARSE;

                curCmdStr = "작업영역변경";
                string fork1String = "";
                string fork2String = "";

                // 작업 데이터 변경 수신 시 작업수신 로깅
                fork1String = string.Format("Fork1-{0} 작업번호:{1} Loading = Stn:{2} Row:{3} Bay:{4} Lev:{5} Unloading = Stn:{6} Row:{7} Bay:{8} Lev:{9} \r\n", cIniAccess.getJobString(refState.fork1JobCmd),
                    refState.fork1JobNo, refState.fork1FromSt, refState.fork1FromRow, refState.fork1FromBay, refState.fork1FromLev, refState.fork1ToSt, refState.fork1ToRow, refState.fork1ToBay, refState.fork1ToLev);
                fork2String = string.Format("Fork2-{0} 작업번호:{1} Loading = Stn:{2} Row:{3} Bay:{4} Lev:{5} Unloading = Stn:{6} Row:{7} Bay:{8} Lev:{9} \r\n", cIniAccess.getJobString(refState.fork2JobCmd),
                    refState.fork2JobNo, refState.fork2FromSt, refState.fork2FromRow, refState.fork2FromBay, refState.fork2FromLev, refState.fork2ToSt, refState.fork2ToRow, refState.fork2ToBay, refState.fork2ToLev);

                // 로그표시 포크갯수 구분할지 고민 중 gClass.str.SrmInfo[srmNum].forkCnt

                SaveLogFile("WCS ==" + fork1String + fork2String);
                cIniAccess.SaveJobLog(srmNum, "WCS -> GCP == \r\n" + fork1String + fork2String);
            }

            // 작번이 써있는 경우 ByPass로 처리 - 모든 가능 조건일 때 (재 트리거 방지 및 로깅 때문)
            if (gClass.str.SrmPacket[srmNum].jobRequest)
            {
                CopyReqBufferFromWcsParse();
            }
            else
            {
            }

            if (gClass.str.WcsPacket[srmNum].WCS_PARSE.dataReportOK != gClass.str.WcsPacket[srmNum].WCS_BUF.dataReportOK)         // DATA ReportOK : OFF
            {
                if (gClass.str.WcsPacket[srmNum].WCS_PARSE.dataReportOK > 0)         // DATA ReportOK : ON
                {
                    cIniAccess.SaveJobLog(srmNum, "WCS -> GCP == DATA REPORT OK : ON");
                }
                else
                {
                    cIniAccess.SaveJobLog(srmNum, "WCS -> GCP == DATA REPORT OK : OFF");
                }
            }

            // 스텝은 SRM기준 자동모드 & Online 상태에서만 진행하도록 조건 추가
            if (gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.AUTO].value)
            {
                if (gClass.str.SrmState[srmNum].autoMode > 0 && gClass.str.SrmState[srmNum].dSt1StartSt > 0)
                {
                    // 작업스텝 결정 조건
                    if ((int)gClass.str.SrmPacket[srmNum].jobState < 4) // SRM 전송 전 상태 스텝에서만 스텝결정
                    {
                        JOBSTATE definedStep = JOBSTATE.NONE;

                        // WCS 작업 있는 상태
                        if (gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1JobNo > 0 || gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2JobNo > 0)
                        {
                            // SRM 작업 없는 상태 
                            if (gClass.str.SrmState[srmNum].fork1.jobNo == 0 && gClass.str.SrmState[srmNum].fork2.jobNo == 0 && gClass.str.SrmState[srmNum].fork1.mvJobNo == 0 && gClass.str.SrmState[srmNum].fork2.mvJobNo == 0)
                            {
                                // DataReport OK ON 상태인 경우
                                if (gClass.str.WcsPacket[srmNum].WCS_PARSE.dataReportOK > 0)
                                {
                                    if (NeedRecoverReqBufferFromWcs(out string recoverTarget))
                                    {
                                        CopyReqBufferFromWcsParse();
                                        cIniAccess.SaveJobLog(srmNum, "WCS -> GCP == dataReportOK : ON -> 버퍼 동기화 (" + recoverTarget + ")");
                                    }

                                    // 작업 영역 데이터 변경 시 유효성 체크 수행 및 req 변수에 데이터 복사 (조건과 관계없이)
                                    gClass.str.SrmPacket[srmNum].resMainCode = pMain.Srm_JobEnableParse(ref reqJobCheckStr, srmNum, true);       // true : WCS / SEMI 잡인지 구분
                                    if (gClass.str.SrmPacket[srmNum].resMainCode == 0)       // 작업 유효성 체크 확인 시
                                    {
                                        if (reqJobCheckResult == 0)
                                        {
                                            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == 작업실행요청 - 유효성 체크 완료");
                                            reqJobCheckResult = 1;      // 1차 수신
                                        }
                                        // Fork1
                                        gClass.str.SrmPacket[srmNum].reqJobCodeFk1 = gClass.str.SrmPacket[srmNum].semiSendCodeFk1;
                                        // Fork2
                                        gClass.str.SrmPacket[srmNum].reqJobCodeFk2 = gClass.str.SrmPacket[srmNum].semiSendCodeFk2;

                                        definedStep = JOBSTATE.SEND;        // WCS작업 없는 상태
                                    }
                                    else
                                    {
                                        reqJobCheckResult = 0;
                                        gClass.str.SrmPacket[srmNum].jobError = true;

                                        definedStep = JOBSTATE.STOP;        // WCS작업 없는 상태
                                                                            // resMainCode 값에 따른 에러 메시지 처리
                                        switch (gClass.str.SrmPacket[srmNum].resMainCode)
                                        {
                                            case 1:
                                                cIniAccess.SaveJobLog(srmNum, $"GCP -> WCS == 반송작업 - 유효성 체크 실패:금지렉 에러({reqJobCheckStr})");
                                                gClass.str.SrmPacket[srmNum].gcpErrorCode = 66;
                                                gClass.str.SrmPacket[srmNum].gcpSubCode = 01;
                                                // FLT 66-01 금지랙 에러 추가
                                                break;
                                            case 2:
                                                cIniAccess.SaveJobLog(srmNum, $"GCP -> WCS == 반송작업 - 유효성 체크 실패:Lev 에러({reqJobCheckStr})");
                                                gClass.str.SrmPacket[srmNum].gcpErrorCode = 66;
                                                gClass.str.SrmPacket[srmNum].gcpSubCode = 02;
                                                // FLT 66-02 레벨 에러 추가
                                                break;
                                            case 3:
                                                cIniAccess.SaveJobLog(srmNum, $"GCP -> WCS == 반송작업 - 유효성 체크 실패:Bay 에러({reqJobCheckStr})");
                                                gClass.str.SrmPacket[srmNum].gcpErrorCode = 66;
                                                gClass.str.SrmPacket[srmNum].gcpSubCode = 04;
                                                // FLT 66-04 베이 에러 추가
                                                break;
                                            case 4:
                                                cIniAccess.SaveJobLog(srmNum, $"GCP -> WCS == 반송작업 - 유효성 체크 실패:Bay 값 초과({reqJobCheckStr})");
                                                gClass.str.SrmPacket[srmNum].gcpErrorCode = 66;
                                                gClass.str.SrmPacket[srmNum].gcpSubCode = 04;
                                                // FLT 66-04 베이 에러 추가
                                                break;
                                            case 5:
                                                cIniAccess.SaveJobLog(srmNum, $"GCP -> WCS == 반송작업 - 유효성 체크 실패:Station 에러({reqJobCheckStr})");
                                                gClass.str.SrmPacket[srmNum].gcpErrorCode = 66;
                                                gClass.str.SrmPacket[srmNum].gcpSubCode = 05;
                                                // FLT 66-05 스테이션 에러 추가
                                                break;
                                            case 6:
                                                if (reqJobCheckStr.Contains("중복"))
                                                {
                                                    cIniAccess.SaveJobLog(srmNum, $"GCP -> WCS == 반송작업 - 유효성 체크 실패:중복 값 입력({reqJobCheckStr})");
                                                    gClass.str.SrmPacket[srmNum].gcpErrorCode = 66;
                                                    gClass.str.SrmPacket[srmNum].gcpSubCode = 05;
                                                    // FLT 66-05 스테이션 에러 추가
                                                }
                                                else
                                                {
                                                    cIniAccess.SaveJobLog(srmNum, $"GCP -> WCS == 반송작업 - 유효성 체크 실패:Station Type 에러({reqJobCheckStr})");
                                                    gClass.str.SrmPacket[srmNum].gcpErrorCode = 66;
                                                    gClass.str.SrmPacket[srmNum].gcpSubCode = 05;
                                                    // FLT 66-05 스테이션 에러 추가
                                                }
                                                break;
                                            case 8:
                                                cIniAccess.SaveJobLog(srmNum, $"GCP -> WCS == 반송작업 - 유효성 체크 실패:Row 에러({reqJobCheckStr})");
                                                gClass.str.SrmPacket[srmNum].gcpErrorCode = 66;
                                                gClass.str.SrmPacket[srmNum].gcpSubCode = 03;
                                                // FLT 66-03 로우 에러 추가
                                                break;
                                            case 99:
                                                cIniAccess.SaveJobLog(srmNum, $"GCP -> WCS == 반송작업 - 유효성 체크 실패:예외 발생({reqJobCheckStr})");
                                                gClass.str.SrmPacket[srmNum].gcpErrorCode = 66;
                                                gClass.str.SrmPacket[srmNum].gcpSubCode = 99;
                                                // FLT 66-99 기타 예외 에러 추가
                                                break;
                                            default:
                                                cIniAccess.SaveJobLog(srmNum, $"GCP -> WCS == 반송작업 - 유효성 체크 실패:데이터 이상({reqJobCheckStr})");
                                                gClass.str.SrmPacket[srmNum].gcpErrorCode = 66;
                                                gClass.str.SrmPacket[srmNum].gcpSubCode = 99;
                                                // FLT 66-99 기타 예외 에러 추가
                                                break;
                                        }
                                    }
                                }
                                // DataReport OK OFF 상태인 경우
                                else
                                {
                                    reqJobCheckResult = 0;
                                    definedStep = JOBSTATE.RECEIVE;        // WCS작업 없는 상태
                                }
                            }
                            // SRM 작업 있는 상태
                            else
                            {
                                // SRM에 작업이 이미 있는 경우
                                if (NeedRecoverReqBufferFromWcs(out string recoverTarget))
                                {
                                    CopyReqBufferFromWcsParse();
                                    cIniAccess.SaveJobLog(srmNum, "WCS -> GCP == SRM JOB Exist -> 버퍼 동기화 (" + recoverTarget + ")");
                                }

                                if ((gClass.str.SrmState[srmNum].fork1.jobNo > 0 && (gClass.str.SrmPacket[srmNum].reqJobNoFk1 != gClass.str.SrmState[srmNum].fork1.jobNo)) ||
                                    (gClass.str.SrmState[srmNum].fork2.jobNo > 0 && (gClass.str.SrmPacket[srmNum].reqJobNoFk2 != gClass.str.SrmState[srmNum].fork2.jobNo)))
                                {
                                    //SRM 작업과 WCS 버퍼 작업이 다른 경우 (수동으로 작업이 변경된 경우 등)
                                    cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == 반송작업 - 수신작업과 SRM 보유작업 상이");
                                    gClass.str.SrmPacket[srmNum].gcpErrorCode = 66;
                                    gClass.str.SrmPacket[srmNum].gcpSubCode = 99;
                                    // FLT 66-99 기타 예외 에러 추가

                                    reqJobCheckResult = 0;
                                    gClass.str.SrmPacket[srmNum].jobError = true;

                                    definedStep = JOBSTATE.STOP;        // WCS작업 없는 상태
                                }
                                else
                                {
                                    // 작업이 수행 후 상태인 경우 Exec으로 처리
                                    if ((gClass.str.SrmState[srmNum].fork1.jobNo > 0 && (gClass.str.SrmState[srmNum].fork1.mvProcState > 0 || gClass.str.SrmState[srmNum].fork1.procState > 0)) ||
                                        (gClass.str.SrmState[srmNum].fork2.jobNo > 0 && (gClass.str.SrmState[srmNum].fork2.mvProcState > 0 || gClass.str.SrmState[srmNum].fork2.procState > 0)))
                                    {
                                        definedStep = JOBSTATE.EXEC;        // 수신- 실행 중 또는 완료
                                    }
                                    else
                                    {
                                        definedStep = JOBSTATE.PEND;        // 수신- 실행 전
                                    }
                                }
                            }
                        }
                        else
                        {
                            definedStep = JOBSTATE.WAIT;        // WCS작업 없는 상태
                        }

                        cIniAccess.ChangeJobState(srmNum, definedStep, "GCP 작업스텝 자동처리");
                    }
                    else
                    {
                        // 전송 이후 상태 에 대한 자동 스텝처리

                        // 이전작업 비교 및 완료상태 처리 통신 재연결 상황도 포함
                    }
                    // 작업스텝 
                    switch ((JOBSTATE)gClass.str.SrmPacket[srmNum].jobState)            // 작업 요청 스탭
                    {
                        case JOBSTATE.NONE:
                            break;
                        case JOBSTATE.WAIT:
                            break;
                        case JOBSTATE.RECEIVE:
                            break;
                        case JOBSTATE.SEND:
                            // UDP SEND 응답에서 PEND 전환
                            break;
                        case JOBSTATE.PEND:
                            // UDP 스텝에서 EXEC 전환
                            break;
                        case JOBSTATE.EXEC:
                            // UDP 스텝에서 COMPLETE 전환
                            break;
                        case JOBSTATE.COMPLETE:
                            // UDP 스텝에서 NONE 전환
                            break;
                        case JOBSTATE.STOP:
                            if (gClass.str.SrmPacket[srmNum].gcpError == false)    // 크레인 이상 해제 시 NONE 스텝으로 - 스텝 재확인
                            {
                                //cIniAccess.ChangeJobState(srmNum, (JOBSTATE)gClass.str.SrmPacket[srmNum].oldJobState);
                                cIniAccess.ChangeJobState(srmNum, JOBSTATE.NONE, "에러상태 해제");
                            }
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    if (gClass.str.SrmState[srmNum].dSt1StartSt > 0 && gClass.str.SrmPacket[srmNum].startCmd == 0)        // 시작상태 && 시작OFF요청 X
                    {
                        cIniAccess.SaveJobLog(srmNum, "GCP -> SRM == 동작모드 변경 - OFFLINE전환");
                        gClass.str.SrmPacket[srmNum].pulseClicked = true;
                        gClass.str.SrmPacket[srmNum].startCmd = 1;
                        gClass.str.SrmPacket[srmNum].startOnOff = 0;
                    }
                }

            }


            // 작업 수신 파싱 시 버퍼처리
            Array.Copy(gClass.str.WcsPacket[srmNum].WCSFROM, gClass.str.WcsPacket[srmNum].WCSFROMBUF, gClass.str.WcsPacket[srmNum].WCSFROM.Length);

            gClass.str.WcsPacket[srmNum].WCS_BUF = gClass.str.WcsPacket[srmNum].WCS_PARSE;              // 현재 수신 작업 조건 버프를 저장
            // UI PAGE 모니터링용-------------------------------------------
            //--------------------------------------------------------------
        }

        /// <summary>
        /// TCP로 전달하는 jobRequest 비트의 ON/OFF 조건을 한 곳에서만 정리.
        /// Tx_DataParse() 직전에 호출하여 WCS 전송용 비트를 갱신한다.
        /// </summary>
        private void UpdateJobRequestBitForTcp()
        {
            var jobState = (JOBSTATE)gClass.str.SrmPacket[srmNum].jobState;
            bool autoOn = gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.AUTO].value;
            bool reqStopOn = gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.REQ_STOP].value && gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.REQ_STOP].pin > 0;


            // ----- OFF 조건 (하나라도 해당하면 jobRequest = false) -----
            if (gClass.str.SrmPacket[srmNum].jobRequest)
            {
                if (gClass.str.SrmPacket[srmNum].gcpError)                                                    // 에러 시 요구비트 해제 (메인 취소 처리 포함)
                {
                    gClass.str.SrmPacket[srmNum].jobRequest = false;
                    cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == 작업요구비트 OFF (사유: GCP 에러 발생)");
                    return;
                }
                if (gClass.str.SrmState[srmNum].dSt1ReqCmd == 0)                                            // SRM 요청 OFF
                {
                    gClass.str.SrmPacket[srmNum].jobRequest = false;
                    cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == 작업요구비트 OFF (사유: SRM 요청 OFF, dSt1ReqCmd=0)");
                    return;
                }
                if (!autoOn)                                                                                 // 지상반 자동 아님
                {
                    gClass.str.SrmPacket[srmNum].jobRequest = false;
                    cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == 작업요구비트 OFF (사유: 지상반 자동 아님, DISTATE.AUTO OFF)");
                    return;
                }
                if (gClass.str.SrmState[srmNum].autoMode == 0)                                  // SRM 자동모드 아님
                {
                    gClass.str.SrmPacket[srmNum].jobRequest = false;
                    cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == 작업요구비트 OFF (사유: SRM 자동모드아님)");
                    return;
                }
                if (gClass.str.SrmState[srmNum].dSt1StartSt == 0)                                  // SRM 시작상태 아님
                {
                    gClass.str.SrmPacket[srmNum].jobRequest = false;
                    cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == 작업요구비트 OFF (사유: SRM 시작상태아님)");
                    return;
                }
                if (reqStopOn)                                                                               // 비상/요청정지(홈복귀 요청 등)
                {
                    gClass.str.SrmPacket[srmNum].jobRequest = false;
                    cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == 작업요구비트 OFF (사유: 비상/요청정지, REQ_STOP ON)");
                    return;
                }
                if ((int)jobState >= (int)JOBSTATE.SEND)                                  // 작업 수신 완료 및 전송단계 일 경우
                {
                    gClass.str.SrmPacket[srmNum].jobRequest = false;
                    cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == 작업요구비트 OFF (사유: 작업 수신/실행 중, jobState=" + jobState + ")");
                    return;
                }
            }
            else
            {
                // ----- ON 조건 (NONE 상태에서 모든 조건 만족 시만 jobRequest = true) -----
                if (gClass.str.SrmState[srmNum].dSt1ReqCmd > 0
                    && gClass.str.SrmPacket[srmNum].gcpError == false
                    && autoOn
                    && gClass.str.SrmState[srmNum].dSt1StartSt > 0
                    && gClass.str.SrmState[srmNum].autoMode > 0
                    && (int)jobState < (int)JOBSTATE.SEND
                    && ((gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.REQ_STOP].value == false && gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.REQ_STOP].pin > 0) || gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.REQ_STOP].pin == 0)
                    && gClass.str.WcsPacket[srmNum].WCS_PARSE.srmCycleStop == 0
                    && gClass.str.WcsPacket[srmNum].WCS_PARSE.srmEmStop == 0
                    && gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1Delete == 0
                    && gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2Delete == 0
                    && gClass.str.SrmPacket[srmNum].fork1JobComplete == 0
                    && gClass.str.SrmPacket[srmNum].fork2JobComplete == 0
                    && gClass.str.WcsPacket[srmNum].WCS_PARSE.dataReportOK == 0)
                {
                    gClass.str.SrmPacket[srmNum].jobRequest = true;
                    cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == 작업요구비트 ON (사유: 조건 만족 - SRM요청/자동/정지해제/WCS정상/dataReportOK=0)");
                }

            }
        }

        // tp 2 3.05 250422 버전파일로 파싱 됨
        private void Tx_DataParse()
        {
            // Send Data Parsing----------------------------------------------------
            ushort tmpValue = 0;
            int flags = 0;

            // to do 버퍼 0으로 초기화 하는거 넣어야 됨
            for (int i = 0; i < 300; i++)
            {
                gClass.str.WcsPacket[srmNum].WCSTOBUF[i] = 0;
            }
            //--------------------------------------------------D7500:0-------------------------------------------
            // SRM Job Request : 작업요구 비트 (조건은 UpdateJobRequestBitForTcp()에서 일괄 적용)
            gClass.str.WcsPacket[srmNum].WCSTOBUF[0] |= (ushort)(gClass.str.SrmPacket[srmNum].jobRequest ? 1 : 0);
            //--------------------------------------------------D7500:1-------------------------------------------
            // 포크1 작업유무 - 작업코드 또는 처리상태가 0인경우 작업없음으로 처리
            gClass.str.WcsPacket[srmNum].WCSTOBUF[0] |= (ushort)(((gClass.str.SrmState[srmNum].fork1.cmdCode != 0 && gClass.str.SrmState[srmNum].fork1.procState != 0) ? 1 : 0) << 1);
            //--------------------------------------------------D7500:2-------------------------------------------
            // 포크2 작업유무 - 작업코드 또는 처리상태가 0인경우 작업없음으로 처리
            gClass.str.WcsPacket[srmNum].WCSTOBUF[0] |= (ushort)(((gClass.str.SrmState[srmNum].fork2.cmdCode != 0 && gClass.str.SrmState[srmNum].fork2.procState != 0) ? 1 : 0) << 2);
            //--------------------------------------------------D7500:3-------------------------------------------
            // 크레인 운전중 - 동작 중 상태를 체크하여 Busy로 처리
            gClass.str.WcsPacket[srmNum].WCSTOBUF[0] |= (ushort)((gClass.str.SrmPacket[srmNum].operState ? 1 : 0) << 3);
            //--------------------------------------------------D7500:4,5-------------------------------------------
            // 포크1/2 화물유무
            gClass.str.WcsPacket[srmNum].WCSTOBUF[0] |= (ushort)(((gClass.str.SrmState[srmNum].fork1.loadState != 0 ? 1 : 0) << 4) | ((gClass.str.SrmState[srmNum].fork2.loadState != 0 ? 1 : 0) << 5));
            //--------------------------------------------------D7500:6,7-------------------------------------------
            // 포크1/2 작업완료
            gClass.str.WcsPacket[srmNum].WCSTOBUF[0] |= (ushort)((gClass.str.SrmPacket[srmNum].fork1JobComplete << 6) | (gClass.str.SrmPacket[srmNum].fork2JobComplete << 7));
            //--------------------------------------------------D7500:8,9-------------------------------------------
            // Bit 8,9 Spare
            //--------------------------------------------------D7500:A-------------------------------------------
            // SRM 에러비트
            gClass.str.WcsPacket[srmNum].WCSTOBUF[0] |= (ushort)((gClass.str.SrmPacket[srmNum].gcpError && gClass.str.SrmPacket[srmNum].gcpErrorCode > 0 ? 1 : 0) << 10);
            //--------------------------------------------------D7500:B-------------------------------------------
            // SRM 복구가능에러
            gClass.str.WcsPacket[srmNum].WCSTOBUF[0] |= (ushort)((gClass.str.SrmPacket[srmNum].recovError ? 1 : 0) << 11);
            //--------------------------------------------------D7500:C-------------------------------------------
            // SRM 홈 위치
            gClass.str.WcsPacket[srmNum].WCSTOBUF[0] |= (ushort)(gClass.str.SrmState[srmNum].dSt2homePos << 12);           // Crane H.P
            //--------------------------------------------------D7500:D,E,F-------------------------------------------
            // Bit D,E,F Spare
            //--------------------------------------------------D7501,2-------------------------------------------
            // 메인에러/서브에러코드
            // 지상반 에러코드 gcpError ON 일경우 에러코드 전송
            gClass.str.WcsPacket[srmNum].WCSTOBUF[1] = (ushort)(gClass.str.SrmPacket[srmNum].gcpError ? gClass.str.SrmPacket[srmNum].gcpErrorCode : 0);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[2] = (ushort)(gClass.str.SrmPacket[srmNum].gcpError ? gClass.str.SrmPacket[srmNum].gcpSubCode : 0);
            //--------------------------------------------------D7503-------------------------------------------
            // 포크1 기준 현재 스테이션
            gClass.str.WcsPacket[srmNum].WCSTOBUF[3] = gClass.str.SrmState[srmNum].fork1.curStation;
            //--------------------------------------------------D7504-------------------------------------------
            // 포크1 기준 Row 표현하기 위해 포크 포지션 데이터 사용 - ROW 계산 PositionValues로 정의
            if (PositionValues.ContainsKey(gClass.str.SrmInfo[srmNum].forkType) &&
                PositionValues[gClass.str.SrmInfo[srmNum].forkType].ContainsKey(gClass.str.SrmState[srmNum].fork1.curPosNum))
            {
                tmpValue = (ushort)PositionValues[gClass.str.SrmInfo[srmNum].forkType][gClass.str.SrmState[srmNum].fork1.curPosNum];
            }
            gClass.str.WcsPacket[srmNum].WCSTOBUF[4] = (ushort)tmpValue;
            //--------------------------------------------------D7505-------------------------------------------
            // 포크1 기준 Bay
            gClass.str.WcsPacket[srmNum].WCSTOBUF[5] = gClass.str.SrmState[srmNum].fork1.curBay;
            //--------------------------------------------------D7506-------------------------------------------
            // 포크1 기준 Lev
            gClass.str.WcsPacket[srmNum].WCSTOBUF[6] = gClass.str.SrmState[srmNum].fork1.curLev;
            //--------------------------------------------------D7507-------------------------------------------
            // Spare
            gClass.str.WcsPacket[srmNum].WCSTOBUF[7] = 0;
            //--------------------------------------------------D7508:0-------------------------------------------
            // 포크1 좌 정위치
            gClass.str.WcsPacket[srmNum].WCSTOBUF[8] |= (ushort)(
            (gClass.str.SrmState[srmNum].fork1.posLeftExac3 > 0 ||
             gClass.str.SrmState[srmNum].fork1.posLeftExac2 > 0 ||
             gClass.str.SrmState[srmNum].fork1.posLeftExac1 > 0) ? 1 : 0);
            //--------------------------------------------------D7508:1-------------------------------------------
            // 포크 좌측 이동 중
            gClass.str.WcsPacket[srmNum].WCSTOBUF[8] |= (ushort)(
            ((gClass.str.SrmState[srmNum].fork1.operState > 0 &&
             gClass.str.SrmState[srmNum].fork1.moveDirec == 0) ? 1 : 0) << 1);
            //--------------------------------------------------D7508:2-------------------------------------------
            // 포크 센터 정위치
            gClass.str.WcsPacket[srmNum].WCSTOBUF[8] |= (ushort)(
            (gClass.str.SrmState[srmNum].fork1.posCenterExac > 0 ? 1 : 0) << 2);
            //--------------------------------------------------D7508:3-------------------------------------------
            // 포크 센터 이동 중
            // 좌측에서 우측이동 / 우측에서 좌측이동 으로 판단
            gClass.str.WcsPacket[srmNum].WCSTOBUF[8] |= (ushort)((
            (
                (gClass.str.SrmState[srmNum].fork1.curPosNum > 0 &&
                 gClass.str.SrmState[srmNum].fork1.operState > 0 &&
                 gClass.str.SrmState[srmNum].fork1.moveDirec == 0) ||  // 우측 → 센터
                (gClass.str.SrmState[srmNum].fork1.curPosNum < 0 &&
                 gClass.str.SrmState[srmNum].fork1.operState > 0 &&
                 gClass.str.SrmState[srmNum].fork1.moveDirec == 1)     // 좌측 → 센터
            ) ? 1 : 0) << 3);
            //--------------------------------------------------D7508:4-------------------------------------------
            // 포크 우측 정위치
            gClass.str.WcsPacket[srmNum].WCSTOBUF[8] |= (ushort)(
                ((gClass.str.SrmState[srmNum].fork1.posRightExac3 > 0 ||
                  gClass.str.SrmState[srmNum].fork1.posRightExac2 > 0 ||
                  gClass.str.SrmState[srmNum].fork1.posRightExac1 > 0) ? 1 : 0) << 4);
            //--------------------------------------------------D7508:5-------------------------------------------
            // 포크 우측 이동 중
            gClass.str.WcsPacket[srmNum].WCSTOBUF[8] |= (ushort)(
            ((gClass.str.SrmState[srmNum].fork1.operState > 0 &&
              gClass.str.SrmState[srmNum].fork1.moveDirec == 1) ? 1 : 0) << 5);
            //--------------------------------------------------D7508:6,7-------------------------------------------
            // Bit 6,7 Spare
            //--------------------------------------------------D7508.8-------------------------------------------
            // 포크2 좌 정위치
            gClass.str.WcsPacket[srmNum].WCSTOBUF[8] |= (ushort)(
            ((gClass.str.SrmState[srmNum].fork2.posLeftExac3 > 0 ||
              gClass.str.SrmState[srmNum].fork2.posLeftExac2 > 0 ||
              gClass.str.SrmState[srmNum].fork2.posLeftExac1 > 0) ? 1 : 0) << 8);
            //--------------------------------------------------D7508:9-------------------------------------------
            // 포크2 좌 이동 중
            gClass.str.WcsPacket[srmNum].WCSTOBUF[8] |= (ushort)(
            ((gClass.str.SrmState[srmNum].fork2.operState > 0 && gClass.str.SrmState[srmNum].fork2.moveDirec == 0) ? 1 : 0) << 9);
            //--------------------------------------------------D7508:A-------------------------------------------
            // 포크2 센터 정위치
            gClass.str.WcsPacket[srmNum].WCSTOBUF[8] |= (ushort)(
            ((gClass.str.SrmState[srmNum].fork2.posCenterExac > 0) ? 1 : 0) << 10);
            //--------------------------------------------------D7508:B-------------------------------------------
            // 포크2 센터 이동 중
            // 좌측에서 우측이동 / 우측에서 좌측이동 으로 판단
            gClass.str.WcsPacket[srmNum].WCSTOBUF[8] |= (ushort)((
            (
                (gClass.str.SrmState[srmNum].fork2.curPosNum > 0 &&
                 gClass.str.SrmState[srmNum].fork2.operState > 0 &&
                 gClass.str.SrmState[srmNum].fork2.moveDirec == 0) ||  // 우측 → 센터
                (gClass.str.SrmState[srmNum].fork2.curPosNum < 0 &&
                 gClass.str.SrmState[srmNum].fork2.operState > 0 &&
                 gClass.str.SrmState[srmNum].fork2.moveDirec == 1)     // 좌측 → 센터
            ) ? 1 : 0) << 11);
            //--------------------------------------------------D7508:C-------------------------------------------
            // 포크2 우 정위치
            gClass.str.WcsPacket[srmNum].WCSTOBUF[8] |= (ushort)(
            ((gClass.str.SrmState[srmNum].fork2.posRightExac3 > 0 ||
              gClass.str.SrmState[srmNum].fork2.posRightExac2 > 0 ||
              gClass.str.SrmState[srmNum].fork2.posRightExac1 > 0) ? 1 : 0) << 12);
            //--------------------------------------------------D7508.D-------------------------------------------
            // 포크2 우 이동 중
            gClass.str.WcsPacket[srmNum].WCSTOBUF[8] |= (ushort)(
            ((gClass.str.SrmState[srmNum].fork2.operState > 0 && gClass.str.SrmState[srmNum].fork2.moveDirec == 1) ? 1 : 0) << 13);
            //--------------------------------------------------D7508:E,F-------------------------------------------
            // Bit E,F Spare
            //--------------------------------------------------D7509:0-------------------------------------------
            // 주행 동작 중
            gClass.str.WcsPacket[srmNum].WCSTOBUF[9] |= (ushort)((gClass.str.SrmState[srmNum].trav.trSt1OperState > 0 ? 1 : 0) << 0);
            //--------------------------------------------------D7509:1-------------------------------------------
            // 승강 동작 중
            gClass.str.WcsPacket[srmNum].WCSTOBUF[9] |= (ushort)((gClass.str.SrmState[srmNum].lift.liSt1OperState > 0 ? 1 : 0) << 1);
            //--------------------------------------------------D7509:2-------------------------------------------
            // 포크1 동작 중
            gClass.str.WcsPacket[srmNum].WCSTOBUF[9] |= (ushort)((gClass.str.SrmState[srmNum].fork1.operState > 0 ? 1 : 0) << 2);
            //--------------------------------------------------D7509:3-------------------------------------------
            // 포크2 동작 중
            gClass.str.WcsPacket[srmNum].WCSTOBUF[9] |= (ushort)((gClass.str.SrmState[srmNum].fork2.operState > 0 ? 1 : 0) << 3);
            //--------------------------------------------------D7509:4~F-------------------------------------------
            // Bit 4~F Spare
            //--------------------------------------------------D7510-------------------------------------------
            // 응답코드 (to do 사용여부 확인)
            gClass.str.WcsPacket[srmNum].WCSTOBUF[10] = (ushort)gClass.str.SrmPacket[srmNum].resMainCode;
            //--------------------------------------------------D7511~14-------------------------------------------
            // D75011~D75014 Spare
            //--------------------------------------------------D7515:0~3-------------------------------------------
            // SRM 홈 위치 - 개별 홈 위치 비트 없음
            gClass.str.WcsPacket[srmNum].WCSTOBUF[15] |= (ushort)(gClass.str.SrmState[srmNum].dSt2homePos);           // Crane H.P
            gClass.str.WcsPacket[srmNum].WCSTOBUF[15] |= (ushort)(gClass.str.SrmState[srmNum].dSt2homePos << 1);           // Crane H.P
            gClass.str.WcsPacket[srmNum].WCSTOBUF[15] |= (ushort)(gClass.str.SrmState[srmNum].dSt2homePos << 2);           // Crane H.P
            gClass.str.WcsPacket[srmNum].WCSTOBUF[15] |= (ushort)(gClass.str.SrmState[srmNum].dSt2homePos << 3);           // Crane H.P
            //--------------------------------------------------D7515:4-------------------------------------------
            // 주행 정위치
            gClass.str.WcsPacket[srmNum].WCSTOBUF[15] |= (ushort)((gClass.str.SrmState[srmNum].trav.trSt1OriginPos > 0 ? 1 : 0) << 4);
            //--------------------------------------------------D7515:5-------------------------------------------
            // 승강 정위치
            gClass.str.WcsPacket[srmNum].WCSTOBUF[15] |= (ushort)((gClass.str.SrmState[srmNum].lift.liSt1OriginPos > 0 ? 1 : 0) << 5);
            //--------------------------------------------------D7515:6-------------------------------------------
            // 포크1 정위치
            gClass.str.WcsPacket[srmNum].WCSTOBUF[15] |= (ushort)((gClass.str.SrmState[srmNum].fork1.originPos > 0 ? 1 : 0) << 6);
            //--------------------------------------------------D7515:7-------------------------------------------
            // 포크2 정위치
            gClass.str.WcsPacket[srmNum].WCSTOBUF[15] |= (ushort)((gClass.str.SrmState[srmNum].fork2.originPos > 0 ? 1 : 0) << 7);
            //--------------------------------------------------D7515:8~F-------------------------------------------
            // Bit 8~F Spare
            //--------------------------------------------------D7516~19-------------------------------------------
            // D75016~D75019 Spare
            //--------------------------------------------------D7520~21-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[20] = (ushort)(gClass.str.SrmState[srmNum].trav.curPos >> 16);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[21] = (ushort)(gClass.str.SrmState[srmNum].trav.curPos);
            //--------------------------------------------------D7522~23-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[22] = (ushort)(gClass.str.SrmState[srmNum].lift.curPos >> 16);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[23] = (ushort)(gClass.str.SrmState[srmNum].lift.curPos);
            //--------------------------------------------------D7524~25-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[24] = (ushort)(gClass.str.SrmState[srmNum].fork1.curPos >> 16);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[25] = (ushort)(gClass.str.SrmState[srmNum].fork1.curPos);
            //--------------------------------------------------D7526~27-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[26] = (ushort)(gClass.str.SrmState[srmNum].fork2.curPos >> 16);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[27] = (ushort)(gClass.str.SrmState[srmNum].fork2.curPos);
            //--------------------------------------------------D7528~29-------------------------------------------
            // D75028~D75029 Spare
            //--------------------------------------------------D7530~31-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[30] = (ushort)(gClass.str.SrmState[srmNum].trav.targetPos >> 16);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[31] = (ushort)(gClass.str.SrmState[srmNum].trav.targetPos);
            //--------------------------------------------------D7532~33-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[32] = (ushort)(gClass.str.SrmState[srmNum].lift.targetPos >> 16);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[33] = (ushort)(gClass.str.SrmState[srmNum].lift.targetPos);
            //--------------------------------------------------D7534~35-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[34] = (ushort)(gClass.str.SrmState[srmNum].fork1.targetPos >> 16);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[35] = (ushort)(gClass.str.SrmState[srmNum].fork1.targetPos);
            //--------------------------------------------------D7536~37-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[36] = (ushort)(gClass.str.SrmState[srmNum].fork2.targetPos >> 16);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[37] = (ushort)(gClass.str.SrmState[srmNum].fork2.targetPos);
            //--------------------------------------------------D7538~39-------------------------------------------
            // D75038~D75039 Spare
            //--------------------------------------------------D7540~41-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[40] = (ushort)(gClass.str.SrmState[srmNum].trav.curSpd >> 16);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[41] = (ushort)(gClass.str.SrmState[srmNum].trav.curSpd);
            //--------------------------------------------------D7542~43-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[42] = (ushort)(gClass.str.SrmState[srmNum].lift.curSpd >> 16);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[43] = (ushort)(gClass.str.SrmState[srmNum].lift.curSpd);
            //--------------------------------------------------D7544~45-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[44] = (ushort)(gClass.str.SrmState[srmNum].fork1.curSpd >> 16);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[45] = (ushort)(gClass.str.SrmState[srmNum].fork1.curSpd);
            //--------------------------------------------------D7546~47-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[46] = (ushort)(gClass.str.SrmState[srmNum].fork2.curSpd >> 16);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[47] = (ushort)(gClass.str.SrmState[srmNum].fork2.curSpd);
            //--------------------------------------------------D7548~99-------------------------------------------
            // D7548~D7599 Spare
            //--------------------------------------------------D7600:0~1------------------------------------------------------작업데이터 영역----------------------------------------------------------------------------
            // Fork2 우선작업 비트 to do (포크2 없어서 현재 미사용)
            gClass.str.WcsPacket[srmNum].WCSTOBUF[100] = gClass.str.WcsPacket[srmNum].WCS_PARSE.cmdPriority;
            //--------------------------------------------------D7601---------------------------------------------
            // Fork 1 작업번호가 아닌 버퍼 작업번호로 처리 -> 지상반 수동 작업 삭제 시, SRM 작업 삭제된 것 확인 후 (삭제실패에 대한 예외) 상위에서 보는 SRM 작업번호를 유지하기 위함
            gClass.str.WcsPacket[srmNum].WCSTOBUF[101] = (ushort)gClass.str.SrmPacket[srmNum].reqJobNoFk1;
            ////--------------------------------------------------D7602-------------------------------------------
            //gClass.str.WcsPacket[srmNum].WCSTOBUF[102] = gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1JobCmd;
            ////--------------------------------------------------D7603-------------------------------------------
            //gClass.str.WcsPacket[srmNum].WCSTOBUF[103] = (ushort)(gClass.str.SrmState[srmNum].fork1.fromStation);
            ////--------------------------------------------------D7604-------------------------------------------
            //gClass.str.WcsPacket[srmNum].WCSTOBUF[104] = (ushort)(gClass.str.SrmState[srmNum].fork1.fromRow);
            ////--------------------------------------------------D7605-------------------------------------------
            //gClass.str.WcsPacket[srmNum].WCSTOBUF[105] = (ushort)(gClass.str.SrmState[srmNum].fork1.fromBay);
            ////--------------------------------------------------D7606-------------------------------------------
            //gClass.str.WcsPacket[srmNum].WCSTOBUF[106] = (ushort)(gClass.str.SrmState[srmNum].fork1.fromLev);
            ////--------------------------------------------------D7607-------------------------------------------
            //gClass.str.WcsPacket[srmNum].WCSTOBUF[107] = (ushort)(gClass.str.SrmState[srmNum].fork1.toStation);
            ////--------------------------------------------------D7608-------------------------------------------
            //gClass.str.WcsPacket[srmNum].WCSTOBUF[108] = (ushort)(gClass.str.SrmState[srmNum].fork1.toRow);
            ////--------------------------------------------------D7609-------------------------------------------
            //gClass.str.WcsPacket[srmNum].WCSTOBUF[109] = (ushort)(gClass.str.SrmState[srmNum].fork1.toBay);
            ////--------------------------------------------------D7610-------------------------------------------
            //gClass.str.WcsPacket[srmNum].WCSTOBUF[110] = (ushort)(gClass.str.SrmState[srmNum].fork1.toLev);
            ////--------------------------------------------------D7611-------------------------------------------
            //gClass.str.WcsPacket[srmNum].WCSTOBUF[111] = (ushort)gClass.str.SrmPacket[srmNum].reqJobNoFk2;
            ////--------------------------------------------------D7612-------------------------------------------  to do 작업 명령 포크1 / 포크2 구분 필요
            //gClass.str.WcsPacket[srmNum].WCSTOBUF[112] = gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2JobCmd;
            ////--------------------------------------------------D7613-------------------------------------------
            //gClass.str.WcsPacket[srmNum].WCSTOBUF[113] = (ushort)(gClass.str.SrmState[srmNum].fork2.fromStation);
            ////--------------------------------------------------D7614-------------------------------------------
            //gClass.str.WcsPacket[srmNum].WCSTOBUF[114] = (ushort)(gClass.str.SrmState[srmNum].fork2.fromRow);
            ////--------------------------------------------------D7615-------------------------------------------
            //gClass.str.WcsPacket[srmNum].WCSTOBUF[115] = (ushort)(gClass.str.SrmState[srmNum].fork2.fromBay);
            ////--------------------------------------------------D7616-------------------------------------------
            //gClass.str.WcsPacket[srmNum].WCSTOBUF[116] = (ushort)(gClass.str.SrmState[srmNum].fork2.fromLev);
            ////--------------------------------------------------D7617-------------------------------------------
            //gClass.str.WcsPacket[srmNum].WCSTOBUF[117] = (ushort)(gClass.str.SrmState[srmNum].fork2.toStation);
            ////--------------------------------------------------D7618-------------------------------------------
            //gClass.str.WcsPacket[srmNum].WCSTOBUF[118] = (ushort)(gClass.str.SrmState[srmNum].fork2.toRow);
            ////--------------------------------------------------D7619-------------------------------------------
            //gClass.str.WcsPacket[srmNum].WCSTOBUF[119] = (ushort)(gClass.str.SrmState[srmNum].fork2.toBay);
            ////--------------------------------------------------D7620-------------------------------------------
            //gClass.str.WcsPacket[srmNum].WCSTOBUF[120] = (ushort)(gClass.str.SrmState[srmNum].fork2.toLev);


            //--------------------------------------------------D7602-------------------------------------------     DATA OK 확인 처리를 위해 송신 버퍼 데이터로 처리
            gClass.str.WcsPacket[srmNum].WCSTOBUF[102] = gClass.str.SrmPacket[srmNum].reqWcsCodeFk1;
            //--------------------------------------------------D7603-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[103] = (ushort)(gClass.str.SrmPacket[srmNum].reqFromStFk1);
            //--------------------------------------------------D7604-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[104] = (ushort)(gClass.str.SrmPacket[srmNum].reqFromRowFk1);
            //--------------------------------------------------D7605-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[105] = (ushort)(gClass.str.SrmPacket[srmNum].reqFromBayFk1);
            //--------------------------------------------------D7606-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[106] = (ushort)(gClass.str.SrmPacket[srmNum].reqFromLevFk1);
            //--------------------------------------------------D7607-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[107] = (ushort)(gClass.str.SrmPacket[srmNum].reqToStFk1);
            //--------------------------------------------------D7608-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[108] = (ushort)(gClass.str.SrmPacket[srmNum].reqToRowFk1);
            //--------------------------------------------------D7609-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[109] = (ushort)(gClass.str.SrmPacket[srmNum].reqToBayFk1);
            //--------------------------------------------------D7610-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[110] = (ushort)(gClass.str.SrmPacket[srmNum].reqToLevFk1);
            //--------------------------------------------------D7611-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[111] = (ushort)gClass.str.SrmPacket[srmNum].reqJobNoFk2;
            //--------------------------------------------------D7612-------------------------------------------  to do 작업 명령 포크1 / 포크2 구분 필요
            gClass.str.WcsPacket[srmNum].WCSTOBUF[112] = gClass.str.SrmPacket[srmNum].reqWcsCodeFk2;
            //--------------------------------------------------D7613-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[113] = (ushort)(gClass.str.SrmPacket[srmNum].reqFromStFk2);
            //--------------------------------------------------D7614-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[114] = (ushort)(gClass.str.SrmPacket[srmNum].reqFromRowFk2);
            //--------------------------------------------------D7615-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[115] = (ushort)(gClass.str.SrmPacket[srmNum].reqFromBayFk2);
            //--------------------------------------------------D7616-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[116] = (ushort)(gClass.str.SrmPacket[srmNum].reqFromLevFk2);
            //--------------------------------------------------D7617-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[117] = (ushort)(gClass.str.SrmPacket[srmNum].reqToStFk2);
            //--------------------------------------------------D7618-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[118] = (ushort)(gClass.str.SrmPacket[srmNum].reqToRowFk2);
            //--------------------------------------------------D7619-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[119] = (ushort)(gClass.str.SrmPacket[srmNum].reqToBayFk2);
            //--------------------------------------------------D7620-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[120] = (ushort)(gClass.str.SrmPacket[srmNum].reqToLevFk2);
            //--------------------------------------------------D7625-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[125] |= (ushort)(gClass.str.WcsPacket[srmNum].WCS_PARSE.heartBeat);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[125] |= (ushort)((gClass.str.SrmPacket[srmNum].wcsAckHomeReturn ? 1 : 0) << 1);        // 홈복귀 동작 피드백
            gClass.str.WcsPacket[srmNum].WCSTOBUF[125] |= (ushort)((gClass.str.SrmPacket[srmNum].wcsAckReset ? 1 : 0) << 2);             // 이상리셋 피드백
            gClass.str.WcsPacket[srmNum].WCSTOBUF[125] |= (ushort)((gClass.str.SrmPacket[srmNum].wcsAckDeleteAll ? 1 : 0) << 3);         // 전체작업삭제 피드백
            gClass.str.WcsPacket[srmNum].WCSTOBUF[125] |= (ushort)((gClass.str.SrmPacket[srmNum].wcsAckDeleteFork1 ? 1 : 0) << 4);       // 포크1 작업삭제 피드백
            gClass.str.WcsPacket[srmNum].WCSTOBUF[125] |= (ushort)((gClass.str.SrmPacket[srmNum].wcsAckDeleteFork2 ? 1 : 0) << 5);       // 포크2 작업삭제 피드백
            gClass.str.WcsPacket[srmNum].WCSTOBUF[125] |= (ushort)((gClass.str.WcsPacket[srmNum].WCS_PARSE.dataReportOK > 0 ? 1 : 0) << 8); // 데이터 송신확인 Bypass
            gClass.str.WcsPacket[srmNum].WCSTOBUF[125] |= (ushort)((gClass.str.SrmPacket[srmNum].startEnable ? 1 : 0) << 9);             // D7625 - Bit9 - Crane Online Ready 시작가능상태
            //--------------------------------------------------D7625:10~12-------------------------------------------
            flags = 11; // 기본: 수동상태
            if (gClass.str.SrmState[srmNum].dSt1StartSt > 0 &&
                gClass.str.SrmState[srmNum].autoMode > 0)
            {
                if (gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.AUTO].value)
                    flags = 10; // 자동상태
                else if (gClass.str.DioPacket[srmNum].DISET[(int)DISTATE.SEMI_AUTO].value)
                    flags = 12; // 반자동상태
            }
            gClass.str.WcsPacket[srmNum].WCSTOBUF[125] |= (ushort)(1 << flags);
            //--------------------------------------------------D7625:14~15-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[125] |= (ushort)((gClass.str.SrmPacket[srmNum].wcsAckCycleStop ? 1 : 0) << 14);       // 사이클 정지 피드백
            gClass.str.WcsPacket[srmNum].WCSTOBUF[125] |= (ushort)((gClass.str.SrmPacket[srmNum].wcsAckEmergencyStop ? 1 : 0) << 15);   // 긴급 정지 피드백
            //--------------------------------------------------D7626~29-------------------------------------------
            // D7626~D7629 Spare
            //--------------------------------------------------D7630-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[130] |= (ushort)(gClass.str.SrmPacket[srmNum].wcsReqCompleteFork1);            // Fork1 작업완료 요청
            gClass.str.WcsPacket[srmNum].WCSTOBUF[130] |= (ushort)(gClass.str.SrmPacket[srmNum].wcsReqCompleteFork2 << 1);            // Fork2 작업완료 요청
            gClass.str.WcsPacket[srmNum].WCSTOBUF[130] |= (ushort)(gClass.str.SrmPacket[srmNum].wcsReqDeleteFork1 << 4);                 // Fork1 작업삭제 요청
            gClass.str.WcsPacket[srmNum].WCSTOBUF[130] |= (ushort)(gClass.str.SrmPacket[srmNum].wcsReqDeleteFork2 << 5);            // Fork2 작업삭제 요청
            //--------------------------------------------------D7631~37-------------------------------------------
            gClass.str.WcsPacket[srmNum].WCSTOBUF[131] = (ushort)(gClass.str.WcsPacket[srmNum].WCS_PARSE.timeYear);                // 년
            gClass.str.WcsPacket[srmNum].WCSTOBUF[132] = (ushort)(gClass.str.WcsPacket[srmNum].WCS_PARSE.timeMonth);                // 월
            gClass.str.WcsPacket[srmNum].WCSTOBUF[133] = (ushort)(gClass.str.WcsPacket[srmNum].WCS_PARSE.timeDate);                // 일
            gClass.str.WcsPacket[srmNum].WCSTOBUF[134] = (ushort)(gClass.str.WcsPacket[srmNum].WCS_PARSE.timeHour);                // 시
            gClass.str.WcsPacket[srmNum].WCSTOBUF[135] = (ushort)(gClass.str.WcsPacket[srmNum].WCS_PARSE.timeMinute);                // 분
            gClass.str.WcsPacket[srmNum].WCSTOBUF[136] = (ushort)(gClass.str.WcsPacket[srmNum].WCS_PARSE.timeSecond);                // 초
            gClass.str.WcsPacket[srmNum].WCSTOBUF[137] = (ushort)(gClass.str.WcsPacket[srmNum].WCS_PARSE.timeDay);                // 요일

            gClass.str.WcsPacket[srmNum].WCSTOBUF[200] = (ushort)(gClass.str.SrmState[srmNum].extState.travSetSpd >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[201] = (ushort)gClass.str.SrmState[srmNum].extState.travSetSpd;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[202] = (ushort)(gClass.str.SrmState[srmNum].extState.liftSetSpd >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[203] = (ushort)gClass.str.SrmState[srmNum].extState.liftSetSpd;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[204] = (ushort)(gClass.str.SrmState[srmNum].extState.fork1SetSpd >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[205] = (ushort)gClass.str.SrmState[srmNum].extState.fork1SetSpd;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[206] = (ushort)(gClass.str.SrmState[srmNum].extState.fork2SetSpd >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[207] = (ushort)gClass.str.SrmState[srmNum].extState.fork2SetSpd;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[210] = (ushort)(gClass.str.SrmState[srmNum].extState.travSetAcc >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[211] = (ushort)gClass.str.SrmState[srmNum].extState.travSetAcc;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[212] = (ushort)(gClass.str.SrmState[srmNum].extState.liftSetAcc >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[213] = (ushort)gClass.str.SrmState[srmNum].extState.liftSetAcc;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[214] = (ushort)(gClass.str.SrmState[srmNum].extState.fork1SetAcc >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[215] = (ushort)gClass.str.SrmState[srmNum].extState.fork1SetAcc;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[216] = (ushort)(gClass.str.SrmState[srmNum].extState.fork2SetAcc >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[217] = (ushort)gClass.str.SrmState[srmNum].extState.fork2SetAcc;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[220] = (ushort)(gClass.str.SrmState[srmNum].extState.travSetDec >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[221] = (ushort)gClass.str.SrmState[srmNum].extState.travSetDec;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[222] = (ushort)(gClass.str.SrmState[srmNum].extState.liftSetDec >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[223] = (ushort)gClass.str.SrmState[srmNum].extState.liftSetDec;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[224 /*0xE0*/] = (ushort)(gClass.str.SrmState[srmNum].extState.fork1SetDec >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[225] = (ushort)gClass.str.SrmState[srmNum].extState.fork1SetDec;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[226] = (ushort)(gClass.str.SrmState[srmNum].extState.fork2SetDec >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[227] = (ushort)gClass.str.SrmState[srmNum].extState.fork2SetDec;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[230] = (ushort)(gClass.str.SrmState[srmNum].extState.travSetJerk >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[231] = (ushort)gClass.str.SrmState[srmNum].extState.travSetJerk;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[232] = (ushort)(gClass.str.SrmState[srmNum].extState.liftSetJerk >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[233] = (ushort)gClass.str.SrmState[srmNum].extState.liftSetJerk;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[234] = (ushort)(gClass.str.SrmState[srmNum].extState.fork1SetJerk >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[235] = (ushort)gClass.str.SrmState[srmNum].extState.fork1SetJerk;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[236] = (ushort)(gClass.str.SrmState[srmNum].extState.fork2SetJerk >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[237] = (ushort)gClass.str.SrmState[srmNum].extState.fork2SetJerk;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[240 /*0xF0*/] = (ushort)gClass.str.SrmState[srmNum].extState.preLoadMoveDelay;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[241] = (ushort)gClass.str.SrmState[srmNum].extState.postLoadMoveDelay;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[242] = (ushort)gClass.str.SrmState[srmNum].extState.preLoadForkExtendDelay;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[243] = (ushort)gClass.str.SrmState[srmNum].extState.postLoadForkExtendDelay;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[244] = (ushort)gClass.str.SrmState[srmNum].extState.preLoadForkLiftDelay;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[245] = (ushort)gClass.str.SrmState[srmNum].extState.postLoadForkLiftDelay;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[246] = (ushort)gClass.str.SrmState[srmNum].extState.preLoadForkRetractDelay;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[247] = (ushort)gClass.str.SrmState[srmNum].extState.postLoadForkRetractDelay;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[250] = (ushort)gClass.str.SrmState[srmNum].extState.preUnloadMoveDelay;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[251] = (ushort)gClass.str.SrmState[srmNum].extState.postUnloadMoveDelay;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[252] = (ushort)gClass.str.SrmState[srmNum].extState.preUnloadForkExtendDelay;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[253] = (ushort)gClass.str.SrmState[srmNum].extState.postUnloadForkExtendDelay;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[254] = (ushort)gClass.str.SrmState[srmNum].extState.preUnloadForkLiftDelay;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[(int)byte.MaxValue] = (ushort)gClass.str.SrmState[srmNum].extState.postUnloadForkLiftDelay;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[256 /*0x0100*/] = (ushort)gClass.str.SrmState[srmNum].extState.preUnloadForkRetractDelay;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[257] = (ushort)gClass.str.SrmState[srmNum].extState.postUnloadForkRetractDelay;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[260] = (ushort)(gClass.str.SrmState[srmNum].extState.travMotorTorque >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[261] = (ushort)gClass.str.SrmState[srmNum].extState.travMotorTorque;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[262] = (ushort)(gClass.str.SrmState[srmNum].extState.liftMotorTorque >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[263] = (ushort)gClass.str.SrmState[srmNum].extState.liftMotorTorque;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[264] = (ushort)(gClass.str.SrmState[srmNum].extState.fork1MotorTorque >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[265] = (ushort)gClass.str.SrmState[srmNum].extState.fork1MotorTorque;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[266] = (ushort)(gClass.str.SrmState[srmNum].extState.fork2MotorTorque >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[267] = (ushort)gClass.str.SrmState[srmNum].extState.fork2MotorTorque;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[280] = (ushort)(gClass.str.SrmState[srmNum].extState.totalOperationTime >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[281] = (ushort)gClass.str.SrmState[srmNum].extState.totalOperationTime;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[282] = (ushort)(gClass.str.SrmState[srmNum].extState.travOperationTime >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[283] = (ushort)gClass.str.SrmState[srmNum].extState.travOperationTime;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[284] = (ushort)(gClass.str.SrmState[srmNum].extState.liftOperationTime >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[285] = (ushort)gClass.str.SrmState[srmNum].extState.liftOperationTime;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[286] = (ushort)(gClass.str.SrmState[srmNum].extState.fork1OperationTime >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[287] = (ushort)gClass.str.SrmState[srmNum].extState.fork1OperationTime;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[288] = (ushort)(gClass.str.SrmState[srmNum].extState.fork2OperationTime >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[289] = (ushort)gClass.str.SrmState[srmNum].extState.fork2OperationTime;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[290] = (ushort)(gClass.str.SrmState[srmNum].extState.travBrakeOpenCount >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[291] = (ushort)gClass.str.SrmState[srmNum].extState.travBrakeOpenCount;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[292] = (ushort)(gClass.str.SrmState[srmNum].extState.liftBrakeOpenCount >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[293] = (ushort)gClass.str.SrmState[srmNum].extState.liftBrakeOpenCount;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[294] = (ushort)(gClass.str.SrmState[srmNum].extState.fork1BrakeOpenCount >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[295] = (ushort)gClass.str.SrmState[srmNum].extState.fork1BrakeOpenCount;
            gClass.str.WcsPacket[srmNum].WCSTOBUF[296] = (ushort)(gClass.str.SrmState[srmNum].extState.fork2BrakeOpenCount >> 16 /*0x10*/);
            gClass.str.WcsPacket[srmNum].WCSTOBUF[297] = (ushort)gClass.str.SrmState[srmNum].extState.fork2BrakeOpenCount;
            //gClass.str.WcsPacket[srmNum].WCSTO[0] += (0 << 14);            // DB1000 - D7000 - Bit14 - Crane Manual Job Complete  눌려있으면 플래그 살려놓고 처리되면 끔
            //gClass.str.WcsPacket[srmNum].WCSTO[0] += (0 << 15);            // DB1000 - D7000 - Bit15 - Heart beat  -> Fork2 작업 삭제 처리로 변경 - 동진 240315

            Array.Copy(gClass.str.WcsPacket[srmNum].WCSTOBUF, gClass.str.WcsPacket[srmNum].WCSTO, gClass.str.WcsPacket[srmNum].WCSTO.Length);

            //----------------------------WCS 작업 명령 응답코드 송신--240315--------------------------------------
#if DONGWON
            if (gClass.str.SrmPacket[srmNum].wcsJobReceive)     // 작업 수신 후 작업에러 응답코드 전송
            {
                //--------------------------------------------------D7001-------------------------------------------
                gClass.str.WcsPacket[srmNum].WCSTO[1] = (ushort)gClass.str.SrmPacket[srmNum].resMainCode;
                //--------------------------------------------------D7002-------------------------------------------
                gClass.str.WcsPacket[srmNum].WCSTO[2] = (ushort)gClass.str.SrmPacket[srmNum].resSubCode;
                gClass.str.SrmPacket[srmNum].wcsJobReceive = false;
            }
#endif
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

        private async void SaveLogFile(string text)
        {
            await Task.Run(() =>
            {
                mutex.WaitOne();

                if (!Directory.Exists(pathString))
                {
                    Directory.CreateDirectory(pathString);
                    Console.WriteLine("Folder created at: " + pathString);
                }

                string filePath = System.IO.Path.Combine(pathString, "HOSTLOG_" + DateTime.Now.ToString("yyyyMMdd") + ".log");

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

                mutex.ReleaseMutex();
            });


            // SRM Log Dialog SendMessage
            IntPtr WindowToFind = cConstDefine.FindWindow(null, "WindowHostLog" + (srmNum + 1));                // 로그 별 타이틀 설정 참조
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
                Console.WriteLine("Find Srm Window Fail WindowHostLog " + srmNum + " " + text);
            }
        }

        public void Close()
        {
            isRunning = false;
            isListening = false;
            Console.WriteLine("TcpServer Class Close  " + srmNum);

            // 소켓을 강제로 닫아서 Read 작업을 즉시 중단
            if (tcpClient != null)
            {
                try
                {
                    tcpClient.Close();
                    Console.WriteLine("TcpServer Client Socket Closed " + srmNum);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("TcpServer Close Socket Exception: " + ex.Message);
                }
            }

            // 리스너 중지
            if (tcpListener != null)
            {
                try
                {
                    tcpListener.Stop();
                    Console.WriteLine("TcpServer Listener Stopped " + srmNum);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("TcpServer Stop Listener Exception: " + ex.Message);
                }
            }

            // 스레드 종료 대기 (소켓이 닫혔으므로 빠르게 종료됨)
            if (clientThread != null)
            {
                Console.WriteLine("TcpServer thread Read Close " + srmNum);
                clientThread.Join();
            }
            if (listenThread != null)
            {
                if (cts != null)
                {
                    cts.Cancel();
                }
                Console.WriteLine("TcpServer thread Listen Close " + listenThread + " " + srmNum);
                listenThread.Join();
                Console.WriteLine("TcpServer thread Listen Close after join " + listenThread + " " + srmNum);
            }
        }

        private async Task MonitorStatus_AllFork()
        {
            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 전체 작업삭제 루프 시작");
            while (gClass.str.WcsPacket[srmNum].WCS_PARSE.jobDelete > (byte)0)
            {
                // ISSUE: reference to a compiler-generated method
                if (Enumerable.Range(100, 21).All<int>(index => gClass.str.WcsPacket[srmNum].WCSTO[index] == 0))
                {
                    gClass.str.SrmPacket[srmNum].wcsAckDeleteAll = true;
                    cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS 전체작업삭제 Ack : True");
                    break;
                }
                await Task.Delay(100);
            }
        }

        private async Task MonitorStatus_Fork1()
        {
            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS Fork1 작업삭제 루프 시작");
            while (gClass.str.WcsPacket[srmNum].WCS_PARSE.fork1Delete > 0)
            {
                // ISSUE: reference to a compiler-generated method
                if (Enumerable.Range(101, 10).All<int>(index => gClass.str.WcsPacket[srmNum].WCSTO[index] == 0))
                {
                    gClass.str.SrmPacket[srmNum].wcsAckDeleteFork1 = true;
                    cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS Fork1 작업삭제 Ack : True");
                    break;
                }
                await Task.Delay(100);
            }
        }
        private async Task MonitorStatus_Fork2()
        {
            cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS Fork2 작업삭제 루프 시작");

            while (gClass.str.WcsPacket[srmNum].WCS_PARSE.fork2Delete > 0)
            {
                if (gClass.str.WcsPacket[srmNum].WCSTOBUF[100] == 0 &&
                    Enumerable.Range(111, 10).All<int>(index => gClass.str.WcsPacket[srmNum].WCSTO[index] == 0))
                {
                    gClass.str.SrmPacket[srmNum].wcsAckDeleteFork2 = true;
                    cIniAccess.SaveJobLog(srmNum, "GCP -> WCS == WCS Fork2 작업삭제 Ack : True");
                    break;
                }
                await Task.Delay(100);
            }
        }
    }
    public static class TaskExtensions
    {
        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(() => tcs.TrySetResult(true)))
            {
                if (task != await Task.WhenAny(task, tcs.Task))
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }
            return await task;
        }
    }

}

