using EasyModbus;
using System;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.IO;
using EasyModbus;
using System.Text;
using System.Net;
using System.Windows.Controls.Primitives;
using System.Xml;
using System.Runtime.CompilerServices;

namespace gcp_Wpf.commClass
{
    internal class modbusRtuClass
    {
        private readonly SerialPort _serialPort;
        private CancellationTokenSource cts;
        private ModbusClient _modbusClient;
        private Thread _modbusThread;
        public bool _isRunning;
        int srmNum;
        string port;
        int baudRate;
        int parity;
        int dataBits;
        int stopBits;


        // Log 파일 저장 관련
        private static Mutex mutex = new Mutex();
        private string pathString;
        public bool[] DIBUF;
        public bool[] DOBUF;
        public bool[] DOCMDBUF;
        //Singletone
        singletonClass gClass;
        public modbusRtuClass(int srmNum, string port, int baudRate, int parity, int dataBits, int stopBits)
        {

            gClass = singletonClass.Instance;
            // DIO 변경 감지용 버퍼
            DIBUF = new bool[cConstDefine.IOCOUNT];
            DOBUF = new bool[cConstDefine.IOCOUNT];
            DOCMDBUF = new bool[cConstDefine.IOCOUNT];

            this.srmNum = srmNum;
            this.port = "COM" + port;
            this.baudRate = baudRate;
            this.parity = parity;
            this.dataBits = dataBits;
            this.stopBits = stopBits;

            // type 이 달라질 경우 대비  -------------------- 230614 현재는 미사용
            int type = 3;

            switch (type)
            {
                case 1:             // 로그 저장을 위함
                    pathString = System.IO.Path.Combine(Environment.CurrentDirectory, "SRM" + srmNum, cConstDefine.PATH_LOG, cConstDefine.PATH_SRMLOG);
                    break;
                case 2:
                    pathString = System.IO.Path.Combine(Environment.CurrentDirectory, "SRM" + srmNum, cConstDefine.PATH_LOG, cConstDefine.PATH_HOSTLOG);
                    break;
                case 3:
                    pathString = System.IO.Path.Combine(Environment.CurrentDirectory, "SRM" + srmNum, cConstDefine.PATH_LOG, cConstDefine.PATH_DIOLOG);
                    break;
            }
        }

        public void StartModbusClient()
        {
            try
            {
                // test
                //_serialPort = new SerialPort(this.port, 57600, Parity.None, 8, StopBits.One);
                //_serialPort = new SerialPort(port, baudRate, parity, dataBits, stopBits);
                //_serialPort.Open();


                /*                if (!_serialPort.IsOpen)
                                {
                                    System.Console.WriteLine("Modbus RTU Comm Open - " + this.port + "is Error");
                                    //throw new Exception("Failed to open serial port");
                                }
                                else
                                {
                                    System.Console.WriteLine("Modbus RTU Comm Opened - " + this.port);

                                }*/
                // Initialize ModbusClient

                if (gClass.str.SrmInfo[srmNum].dioUse == 1)
                {
                    Console.WriteLine("Connect RTU Port = " + this.port);

                    _modbusClient = new ModbusClient(this.port);
                    //_modbusClient = new ModbusClient(port);
                    //_modbusClient.SerialPort = "COM8";
                    _modbusClient.Baudrate = 57600;
                    _modbusClient.Parity = Parity.None;
                    _modbusClient.StopBits = StopBits.One;
                    _modbusClient.UnitIdentifier = 1;
                    _modbusClient.ConnectionTimeout = 1000;
                    _modbusClient.Disconnect();

                    _modbusClient.SendDataChanged += ModbusDataTransferred;
                    _modbusClient.ReceiveDataChanged += ModbusDataReceived;
                    _modbusClient.ConnectedChanged += ModbusConnectionChanged;
                    // Initialize Modbus thread
                    _modbusThread = new Thread(ModbusThreadFunction);
                    _modbusThread.Start();

                    //_modbusClient.Connect();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Modbus Comm Error: " + ex.Message);
                // Handle the exception, e.g. display an error message
                SaveLogFile("Modbus Comm Error: " + ex.Message);
            }
            finally
            {
                // Disconnect from the Modbus device
                if (_modbusThread != null)
                {
                    if (_modbusThread.IsAlive)
                    {
                        _isRunning = true;
                        //_modbusThread.Abort();
                        //_serialPort.Close();
                    }
                }
            }
        }



        public void RestartModbusThread()
        {
            SaveLogFile("Modbus Thread ReStart");
            if (_modbusThread != null)
            {
                if (_modbusThread.IsAlive)
                {
                    Console.WriteLine("Modbus Thread Enable");
                    SaveLogFile("Modbus Thread Enable");
                    _isRunning = true;
                }
                else
                {
                    Console.WriteLine("Modbus Thread Clear");
                    SaveLogFile("Modbus Thread Clear");
                    _modbusThread = null;
                    Console.WriteLine("Modbus Thread Create");
                    SaveLogFile("Modbus Thread Create");
                    _modbusThread = new Thread(ModbusThreadFunction);
                    _modbusThread.Start();
                    _isRunning = true;
                }
            }
            else
            {
                Console.WriteLine("Modbus Thread Create");
                SaveLogFile("Modbus Thread Create");
                _modbusThread = new Thread(ModbusThreadFunction);
                _modbusThread.Start();
                _isRunning = true;
            }
        }


        private void ModbusDataTransferred(object sender)
        {
            gClass.str.SrmPacket[gClass.srmNum].txDioComm = true;       // MODBUS TX STATE ON
            //Console.WriteLine("ModbusData Transferred");
        }

        private void ModbusDataReceived(object sender)
        {
            gClass.str.SrmPacket[gClass.srmNum].rxDioComm = true;       // MODBUS RX STATE ON
            //Console.WriteLine("ModbusData Received");
        }

        private void ModbusConnectionChanged(object sender)
        {
            gClass.str.SrmPacket[gClass.srmNum].stDioComm = _modbusClient.Connected;        // MODBUS COMM STATE SET
            //Console.WriteLine("Modbus Connection Changed " + _modbusClient.Connected);


            // 스레드 통신 종료 시 재시작
        }


        private void ModbusThreadFunction()
        {
            bool dioParse = false;

            // Check Main Process
            int watchDogCnt = 0;
            int watchCnt = 3;

            bool sPlugTmp = true;
            int sPlugCnt = 0;

            int commFltCnt = 0;

            // Log 카운트
            int logCnt = 0;

            string logStr = string.Empty;

            try
            {
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
                            Console.WriteLine("Modbus Thread Exit - " + watchDogCnt + " " + cIniAccess.watchDogCnt);
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

                        // Connect to the Modbus device
                        //Console.WriteLine("Debug Test 6");
                        _modbusClient.Connect();
                        //Console.WriteLine("Debug Test 7 " + srmNum);
                        //Console.WriteLine("Modbus Read/Write Start");
                        try
                        {
                            this.gClass.str.DioPacket[this.srmNum].DOSET[4].value = this.gClass.str.SrmInfo[this.srmNum].sfUse == 0 || this.gClass.str.DioPacket[this.srmNum].DISET[4].value;

                            for (int key = 0; key < 8; ++key)
                                //this.gClass.str.DioPacket[this.srmNum].DOCMD[key] = this.gClass.str.DioPacket[this.srmNum].outputControl[this.gClass.str.DioPacket[this.srmNum].outputReference[key]];
                                dioParse = true;
                            //Console.WriteLine("Debug Test 8 " + srmNum);
                            // ------------------------------------------------------------------------
                            // -----------------------------USING FC01 Read Single Coil ----------------------------------
                            // 0-7	MD-DIDC8 디지털입력  Comfile Tech Module            -- 입력 데이터 읽어오기
                            //Thread.Sleep(300);
                            gClass.str.DioPacket[srmNum].DIBIT = _modbusClient.ReadCoils(0, 8);
                            // -----------------------------USING FC15 Write Multiple Coil ----------------------------------

                            //Console.WriteLine("Debug Test 9 " + srmNum);
                            // 3200-3207	MD-DORL8 릴레이 출력 Comfile Tech Module    -- 출력 데이터 쓰기
                            //Thread.Sleep(300);
                            _modbusClient.WriteMultipleCoils(3200, gClass.str.DioPacket[srmNum].DOCMD);
                            //Console.WriteLine("Debug Test 10");
                            // -----------------------------USING FC01 Read Single Coil ----------------------------------
                            // 3200-3207	MD-DORL8 릴레이 출력  Comfile Tech Module   -- 출력 상태 읽어오기
                            //Thread.Sleep(300);
                            gClass.str.DioPacket[srmNum].DOBIT = _modbusClient.ReadCoils(3200, 8);
                            //Console.WriteLine("Debug Test 11");

                        }
                        catch (Exception ex)
                        {
                            dioParse = false;
                            SaveLogFile("Modbus Read Exception : " + ex.Message + " " + srmNum);
                            Console.WriteLine($"Modbus error occurred: {ex.Message} " + srmNum);

                            // 테스트 입력
                            // Parse DIO Data  - I/O 결선 위치 변경 시 해당 맵핑을 변경해야 함
                            if (gClass.str.DioPacket[srmNum].DO_TESTMODE == false)
                            {
                                if (gClass.str.SrmInfo[srmNum].dioUse == 0)
                                {
                                    //gClass.str.DioPacket[srmNum].inputControl[(int)DISTATE.EM_SW] = true;
                                    //gClass.str.DioPacket[srmNum].inputControl[(int)DISTATE.SF_PLUG] = true;
                                    //gClass.str.DioPacket[srmNum].inputControl[(int)DISTATE.MODEM_EN] = true;
                                }
                                else
                                {
                                    commFltCnt += 1;
                                    if (commFltCnt > 4)
                                    {
                                        //gClass.str.DioPacket[srmNum].inputControl[(int)DISTATE.EM_SW] = false;
                                        //gClass.str.DioPacket[srmNum].inputControl[(int)DISTATE.SF_PLUG] = false;
                                        //gClass.str.DioPacket[srmNum].inputControl[(int)DISTATE.MODEM_EN] = false;
                                        commFltCnt = 0;
                                    }
                                }
                            }
                        }
                        finally
                        {
                            gClass.str.SrmPacket[srmNum].dioCommDiscCnt = 2;            // Receive 여부 확인 후 접속 카운트 초기화
                            if (dioParse || gClass.str.DioPacket[srmNum].DO_TESTMODE == false)
                            {
                                for (int i = 0; i < 8; i++)
                                {
                                    //gClass.str.DioPacket[srmNum].inputControl[gClass.str.DioPacket[srmNum].inputReference[i]] = gClass.str.DioPacket[srmNum].DIBIT[i];      // 중복 선택 시 나중 인덱스 값으로 덮어써짐 주의
                                }
                                //sPlugTmp = gClass.str.DioPacket[srmNum].inputControl[(int)DISTATE.SF_PLUG];


                                if (gClass.str.SrmInfo[srmNum].dioUse == 0)
                                {
                                    //gClass.str.DioPacket[srmNum].inputControl[(int)DISTATE.EM_SW] = true;
                                    //gClass.str.DioPacket[srmNum].inputControl[(int)DISTATE.SF_PLUG] = true;
                                    //gClass.str.DioPacket[srmNum].inputControl[(int)DISTATE.MODEM_EN] = true;
                                }
                                else if (gClass.str.SrmInfo[srmNum].sfUse == 0)      // 안전플러그 미사용 시
                                {
                                    //gClass.str.DioPacket[srmNum].inputControl[(int)DISTATE.SF_PLUG] = true;
                                }
                                else if (!sPlugTmp)          // 안전플러그 해제
                                {
                                    sPlugCnt += 1;
                                    if (sPlugCnt > 1)
                                    {
                                        //gClass.str.DioPacket[srmNum].inputControl[(int)DISTATE.SF_PLUG] = false;
                                        sPlugCnt = 0;
                                    }
                                }
                                else
                                {
                                    //gClass.str.DioPacket[srmNum].inputControl[(int)DISTATE.SF_PLUG] = sPlugTmp;
                                    sPlugCnt = 0;
                                }
                            }

                            commFltCnt = 0;
                            //Console.WriteLine($"ReadCoils Finally");
                            // Ensure that you close the connection
                            logStr = ParseChangeLog();
                            if (logStr != String.Empty)
                            {
                                SaveLogFile(logStr);
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
                            if (_modbusClient.Connected)
                            {
                                _modbusClient.Disconnect();
                            }
                        }
                        //Console.WriteLine("Modbus Read/Write Finish");
                    }
                    catch (ThreadAbortException)
                    {
                        Console.WriteLine("Modbus Thread Abort Exception Internal");
                        // Thread was aborted, stop receiving
                    }
                    catch (Exception ex)
                    {
                        SaveLogFile("Modbus Exception : " + ex.Message + " " + srmNum);
                        //Console.WriteLine("Modbus Comm Error: " + ex.Message + " " + srmNum);
                        // Handle the exception, e.g. display an error message
                        // ...

                        // # Test Code
                        //SaveLogFile("DIOLOG");

                        gClass.str.SrmPacket[srmNum].dioCommDiscCnt -= 1;
                        // 테스트 입력
                        // Parse DIO Data  - I/O 결선 위치 변경 시 해당 맵핑을 변경해야 함
                        if (gClass.str.DioPacket[srmNum].DO_TESTMODE == false)
                        {
                            if (gClass.str.SrmInfo[srmNum].dioUse == 0)
                            {
                                //gClass.str.DioPacket[srmNum].inputControl[(int)DISTATE.EM_SW] = true;
                                //gClass.str.DioPacket[srmNum].inputControl[(int)DISTATE.SF_PLUG] = true;
                                //gClass.str.DioPacket[srmNum].inputControl[(int)DISTATE.MODEM_EN] = true;
                            }
                            else
                            {
                                commFltCnt += 1;
                                if (commFltCnt > 4)
                                {
                                    //gClass.str.DioPacket[srmNum].inputControl[(int)DISTATE.EM_SW] = false;
                                    //gClass.str.DioPacket[srmNum].inputControl[(int)DISTATE.SF_PLUG] = false;
                                    //gClass.str.DioPacket[srmNum].inputControl[(int)DISTATE.MODEM_EN] = false;
                                    commFltCnt = 0;
                                }
                            }
                        }
                    }

                    // Wait for 1 second before reading again
                    Thread.Sleep(500);

                    if (gClass.str.SrmInfo[srmNum].dioAliveCnt > 0)          // 프로그램 종료 시 음수 값으로 변경 필
                    {
                        gClass.str.SrmInfo[srmNum].dioAliveCnt += 1;

                        //if(gClass.str.SrmInfo[srmNum].dioAliveCnt > 30)         // TEST CODE 
                        //{
                        //    gClass.str.SrmInfo[srmNum].dioAliveCnt = 2;
                        //    _isRunning = false;                                 // Forced Close Thread
                        //}
                    }

                    //if (_modbusClient.Connected)
                    //{
                    //    _modbusClient.Disconnect();
                    //}
                    //SaveLogFile("Modbus Thread is Running");
                }
                if (_modbusClient.Connected)
                {
                    _modbusClient.Disconnect();
                }

            }
            catch (ThreadAbortException)
            {
                Console.WriteLine("Modbus Thread Abort Exception");
                cIniAccess.SaveExLog(0, "EXCEPTION - ModbusThreadAbort");
                // Thread was aborted, stop receiving
            }
            catch (Exception ex)
            {
                Console.WriteLine("Modbus Thread Exception " + ex.Message);
                cIniAccess.SaveExLog(0, "EXCEPTION - ModbusThreadFunction : " + ex.Message);
            }
            finally
            {
                _isRunning = false;
                Console.WriteLine("Modbus Thread finally." + srmNum);
            }

            //if (_modbusClient.Connected)
            //{
            //    _modbusClient.Disconnect();
            //}

            //if (_serialPort != null && _serialPort.IsOpen)
            //{
            //    _serialPort.Close();
            //}
            Console.WriteLine("Modbus Thread Finished " + srmNum);
            SaveLogFile("Modbus Thread is Finished");
        }

        private string ParseChangeLog()
        {
            string inputText = string.Empty;
            string outputText = string.Empty;
            string outCmdText = string.Empty;
            string enumName = string.Empty;

            string resultStr = string.Empty;

            for (int i = 0; i < cConstDefine.IOCOUNT; i++)
            {
                if (gClass.str.DioPacket[srmNum].DIBIT[i] != DIBUF[i])
                {
                    //enumName = ((DISTATE)gClass.str.DioPacket[srmNum].inputReference[i]).ToString();
                    inputText += string.Format("[{0}]= {1} -> {2} / ", enumName, DIBUF[i].ToString(), gClass.str.DioPacket[srmNum].DIBIT[i].ToString());
                    DIBUF[i] = gClass.str.DioPacket[srmNum].DIBIT[i];
                }

                if (gClass.str.DioPacket[srmNum].DOCMD[i] != DOCMDBUF[i])
                {
                    //enumName = ((DISTATE)gClass.str.DioPacket[srmNum].inputReference[i]).ToString();
                    outCmdText += string.Format("[{0}]= {1} -> {2} / ", enumName, DOCMDBUF[i].ToString(), gClass.str.DioPacket[srmNum].DOCMD[i].ToString());
                    DOCMDBUF[i] = gClass.str.DioPacket[srmNum].DOCMD[i];
                }

                if (gClass.str.DioPacket[srmNum].DOBIT[i] != DOBUF[i])
                {
                    //enumName = ((DISTATE)gClass.str.DioPacket[srmNum].inputReference[i]).ToString();
                    outputText += string.Format("[{0}]= {1} -> {2} / ", enumName, DOBUF[i].ToString(), gClass.str.DioPacket[srmNum].DOBIT[i].ToString());
                    DOBUF[i] = gClass.str.DioPacket[srmNum].DOBIT[i];
                }
            }

            if(inputText != string.Empty)
            {
                inputText = "INPUT : " + inputText + "\n";
            }
            if (outCmdText != string.Empty)
            {
                outCmdText = "OUTCMD : " + outCmdText + "\n";
            }
            if (outputText != string.Empty)
            {
                outputText = "OUTPUT : " + outputText + "\n";
            }

            resultStr = inputText + outCmdText + outputText;

            return resultStr;
        }

        public void Close()
        {
            // Stop Modbus thread and clean up resources
            _isRunning = false;
            gClass.str.SrmInfo[srmNum].dioAliveCnt = -99;
            if (_modbusClient != null)
            {
                Console.WriteLine("Modbus thread Exist - Join" + _modbusThread + " " + srmNum);
                _modbusThread.Join();
            }
            Console.WriteLine("Modbus thread After Join " + _modbusThread + " " + srmNum);

            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
            }
        }

        private async void SaveLogFile(string text)
        {
            await Task.Run(() =>
            {
                //----------------------------------------------------------------------------------------
                text = DateTime.Now.ToString("HH:mm:ss:fff ") + text;           // 현재시간 추가

                mutex.WaitOne();

                if (!Directory.Exists(pathString))
                {
                    Directory.CreateDirectory(pathString);
                    Console.WriteLine("Folder created at: " + pathString);
                }


                string filePath = System.IO.Path.Combine(pathString, "DIOLOG_" + DateTime.Now.ToString("yyyyMMdd") + ".log");

                if (!File.Exists(filePath))
                {
                    using (StreamWriter writer = File.CreateText(filePath))
                    {
                        writer.WriteLine("File created on " + DateTime.Now.ToString());
                        cIniAccess.DeleteOldFiles(srmNum, pathString, 15);
                    }
                }

                // Write the text to the file
                using (StreamWriter writer = new StreamWriter(filePath, true))
                {
                    writer.WriteLine(text);
                    //Console.WriteLine(text + "  " + srmNum);                  // 로그 콘솔화면 출력
                }

                mutex.ReleaseMutex();
            });


            // SRM Log Dialog SendMessage
            IntPtr WindowToFind = cConstDefine.FindWindow(null, "WindowDioLog" + (srmNum + 1));
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
}
