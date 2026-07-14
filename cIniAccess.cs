using System;
using System.Text;
using System.Runtime.InteropServices;   //DllImport
using System.Threading.Tasks;
using System.IO;

namespace gcp_Wpf
{
    public static class cIniAccess
    {
        public static volatile int watchDogCnt = 1;

        public static long dioAliveCnt = 1;     // DIO 스레드 메소드 Alive Check Count

        static System.Threading.Mutex mutex = new System.Threading.Mutex();

        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string value, string filePath);

        [DllImport("kernel32")]
        private static extern uint GetPrivateProfileInt(string lpAppName, string lpKeyName, int nDefault, string lpFileName);

        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string defaultValue, StringBuilder retVal, int size, string filePath);

        public static void Write(string filePath, string section, string key, string value)
        {
            WritePrivateProfileString(section, key, value, filePath);
        }

        public static string Read(string filePath, string section, string key, string defaultValue = "0")
        {
            var retVal = new StringBuilder(255);
            GetPrivateProfileString(section, key, "", retVal, 255, filePath);
            if(string.IsNullOrEmpty(retVal.ToString()))
            {
                if(defaultValue != "nowrite")
                {
                    WritePrivateProfileString(section, key, defaultValue, filePath);
                }
                else
                {
                    defaultValue = "";
                }
            }
            else
            {
                defaultValue = retVal.ToString();
            }

            return defaultValue;
        }


        public static string getJobString(byte jobNo)
        {
            string jobStr = "";
            switch (jobNo)
            {
                case 0:      
                    jobStr = "NONE";
                    break;
                case 1:          // 이동명령
                    jobStr = "이동명령";
                    break;
                case 2:          // 입고명령
                    jobStr = "입고명령";
                    break;
                case 4:          // 출고명령
                    jobStr = "출고명령";
                    break;
                case 8:          // Rack to Rack
                    jobStr = "렉 간 이동";
                    break;
                case 16:         // Station to Station
                    jobStr = "스테이션 간 이동";
                    break;
                case 32:         // 목적지 변경 (Rack)
                    jobStr = "목적지 변경 (Rack)";
                    break;
                case 64:         // 목적지 변경 (Station)
                    jobStr = "목적지 변경(Station)";
                    break;
                case 128:         // 점착방지 명령 (Sticky)
                    jobStr = "점착방지 명령";
                    break;
            }

            return jobStr;
        }

        public static async void SaveExLog(int srmNum, string text)         // Exception LOG
        {
            await Task.Run(() =>
            {
                mutex.WaitOne();
                // try/finally — 파일IO 예외(디스크풀/권한/공유위반) 시에도 반드시 ReleaseMutex 하여 공유 static 뮤텍스
                //   영구점유(→이후 모든 cIniAccess 로깅 영구 교착)를 방지. (udpClientClass.SaveLogFile과 동일 패턴)
                try
                {

                string folderPath = System.IO.Path.Combine(Environment.CurrentDirectory, cConstDefine.PATH_EX);

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                    Console.WriteLine("Folder created at: " + folderPath);
                }


                string filePath = System.IO.Path.Combine(folderPath, "EXLOG_" + DateTime.Now.ToString("yyyyMMdd") + ".log");

                if (!File.Exists(filePath))
                {
                    using (StreamWriter writer = File.CreateText(filePath))
                    {
                        writer.WriteLine("File created on " + DateTime.Now.ToString());
                        cIniAccess.DeleteOldFiles(srmNum, folderPath, 15);
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
                finally { try { mutex.ReleaseMutex(); } catch { } }
            });
        }


        //---------------------------------------------------------File Process--------------------------------------------------------

        public static async void SaveOPLog(int srmNum, string text)
        {
            await Task.Run(() =>
            {
                mutex.WaitOne();
                try
                {

                string folderPath = System.IO.Path.Combine(Environment.CurrentDirectory, "SRM" + srmNum, cConstDefine.PATH_LOG, cConstDefine.PATH_OPLOG);

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                    Console.WriteLine("Folder created at: " + folderPath);
                }


                string filePath = System.IO.Path.Combine(folderPath, "OPLOG_" + DateTime.Now.ToString("yyyyMMdd") + ".log");

                if (!File.Exists(filePath))
                {
                    using (StreamWriter writer = File.CreateText(filePath))
                    {
                        writer.WriteLine("File created on " + DateTime.Now.ToString());
                        cIniAccess.DeleteOldFiles(srmNum, folderPath, 15);
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
                finally { try { mutex.ReleaseMutex(); } catch { } }
            });


            //// SRM Log Dialog SendMessage
            //IntPtr WindowToFind = cConstDefine.FindWindow(null, "WindowOpLog " + (srmNum + 1));  //(srmNum + 1));
            ////IntPtr WindowToFind = win_SrmLog.GetHandle();
            //if (WindowToFind != IntPtr.Zero)
            //{
            //    //Console.WriteLine("Send Udp Message " + WindowToFind);
            //    IntPtr hwnd = WindowToFind;
            //    var copyData = new cConstDefine.COPYDATASTRUCT();
            //    copyData.dwData = IntPtr.Zero;
            //    copyData.lpData = text;
            //    copyData.cbData = Encoding.Unicode.GetBytes(text).Length + 1; // add 1 for null-terminator
            //    cConstDefine.SendMessage(WindowToFind, cConstDefine.WM_USER, IntPtr.Zero, ref copyData);               // Send - Post 차이 비교 필요
            //    //PostMessage(WindowToFind, cConstDefine.WM_USER, IntPtr.Zero, ref copyData);

            //}
            //else
            //{
            //    Console.WriteLine("Find Operation Window Fail WindowOpLog " + (srmNum+1));
            //}
        }

        public static async void SaveJobLog(int srmNum, string text)
        {
            await Task.Run(() =>
            {
                mutex.WaitOne();
                try
                {

                string folderPath = System.IO.Path.Combine(Environment.CurrentDirectory, "SRM" + srmNum, cConstDefine.PATH_LOG, cConstDefine.PATH_JOBLOG);

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                    Console.WriteLine("Folder created at: " + folderPath);
                }


                string filePath = System.IO.Path.Combine(folderPath, "JOBLOG_" + DateTime.Now.ToString("yyyyMMdd") + ".log");

                if (!File.Exists(filePath))
                {
                    using (StreamWriter writer = File.CreateText(filePath))
                    {
                        writer.WriteLine("File created on " + DateTime.Now.ToString());
                        cIniAccess.DeleteOldFiles(srmNum, folderPath, 15);
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
                finally { try { mutex.ReleaseMutex(); } catch { } }
            });


            // SRM Log Dialog SendMessage
            IntPtr WindowToFind = cConstDefine.FindWindow(null, "WindowJobLog" + (srmNum+1));  //(srmNum + 1));
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
                Console.WriteLine("Find Operation Window Fail WindowJobLog " + srmNum + " " + text);
            }
        }


        public static void DeleteOldFiles(int srmNum, string folderPath, int daysToKeep)
        {
            try
            {
                // Get the current date
                DateTime currentDate = DateTime.Now;

                // Calculate the date threshold for deletion (7 days ago)
                DateTime thresholdDate = currentDate.AddDays(-daysToKeep);

                // Iterate through files in the folder
                string[] files = Directory.GetFiles(folderPath);
                foreach (string filePath in files)
                {
                    // Get the last modification time of the file
                    DateTime modifiedTime = File.GetLastWriteTime(filePath);

                    // Check if the file is older than the threshold date
                    if (modifiedTime < thresholdDate)
                    {
                        // 주의: DeleteOldFiles는 SaveExLog/SaveOPLog/SaveJobLog가 뮤텍스 보유 중 호출하므로
                        //       여기서 SaveOPLog(또 다른 Task가 같은 뮤텍스 대기)를 부르면 뮤텍스 경합/재진입 유발 → Console 로깅만 사용
                        Console.WriteLine($"Deleting file: {filePath}");
                        File.Delete(filePath);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
            }
        }

        /// <summary>
        /// 이전 스텝과 다를 때만 oldJobState·jobState 등 패킷 필드를 갱신하고 작업 로그를 기록합니다.
        /// </summary>
        /// <param name="srmNum">SRM 번호</param>
        /// <param name="newState">새로운 작업 스텝</param>
        /// <param name="additionalMessage">추가 로그 메시지 (옵션)</param>
        public static void ChangeJobState(int srmNum, JOBSTATE newState, string additionalMessage = "")
        {
            singletonClass gClass = singletonClass.Instance;

            int oldState = gClass.str.SrmPacket[srmNum].jobState;
            int newStateInt = (int)newState;

            // 이전 스텝과 동일하면 로그·패킷 필드 갱신 없음
            if (oldState == newStateInt)
                return;

            gClass.str.SrmPacket[srmNum].oldJobState = oldState;
            gClass.str.SrmPacket[srmNum].jobState = newStateInt;

            // CLEARJOB → COMPLETE 진입 시 dataReportOK==0 대기 시작 시각 설정
            if (newState == JOBSTATE.COMPLETE)
            {
                gClass.str.SrmPacket[srmNum].completeStateDataReportOKWaitTime = DateTime.Now;
                gClass.str.SrmPacket[srmNum].dataReportOKTimeoutError = false;
            }

            string oldStateName = Enum.GetName(typeof(JOBSTATE), oldState) ?? GetJobStateName(oldState);
            string newStateName = Enum.GetName(typeof(JOBSTATE), newState) ?? newState.ToString();

            string logMessage = "GCP STEP == " + oldStateName + " -> " + newStateName;
            if (!string.IsNullOrEmpty(additionalMessage))
            {
                logMessage += " " + additionalMessage;
            }

            SaveJobLog(srmNum, logMessage);
        }

        /// <summary>
        /// JOBSTATE enum 값을 문자열로 변환합니다. (Enum.GetName이 실패할 경우를 위한 fallback)
        /// </summary>
        private static string GetJobStateName(int stateValue)
        {
            // JOBSTATE enum 값에 따른 이름 반환 (singletonClass의 JOBSTATE enum 순서와 일치)
            switch (stateValue)
            {
                case 0: return "NONE";
                case 1: return "WAIT";
                case 2: return "RECEIVE";
                case 3: return "DATAOK";
                case 4: return "SEND";
                case 5: return "PEND";
                case 6: return "EXEC";
                case 7: return "COMPLETE";
                case 8: return "CLEARJOB";
                case 9: return "STOP";
                case 10: return "EMSTOP";
                default: return stateValue.ToString();
            }
        }
    }
}
