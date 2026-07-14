using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Globalization;

namespace gcp_Wpf
{
    /// <summary>
    /// PageAlarmLog.xaml에 대한 상호 작용 논리
    /// </summary>
    /// 

    using System.ComponentModel;

    public class Data : INotifyPropertyChanged
    {
        public int errNum { get; set; }
        public string occurredTime { get; set; }
        public string errCode { get; set; }
        public string subCode { get; set; }
        
        private string _errContent;
        public string errContent 
        { 
            get { return _errContent; }
            set 
            { 
                if (_errContent != value)
                {
                    _errContent = value;
                    OnPropertyChanged("errContent");
                }
            } 
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public partial class PageAlarmLog : Page
    {
        ObservableCollection<Data> errStrList = new ObservableCollection<Data>();           // 이상 발생내역
        ObservableCollection<Data> errCodeList = new ObservableCollection<Data>();          // 에러코드 리스트
        ObservableCollection<Data> errSubCodeList = new ObservableCollection<Data>();       // 에러리스트 (서브코드 포함)
        ObservableCollection<Data> warningCodeList = new ObservableCollection<Data>();      // 경고리스트

        static System.Threading.Mutex mutex = new System.Threading.Mutex();

        //Singletone
        singletonClass gClass;

        int occurredNum = 1;
        int alarmListNum = 1;
        
        // 에러 코드 캐싱을 위한 딕셔너리
        private Dictionary<string, string> errorCodeCache = new Dictionary<string, string>();
        
        // 경고 코드 캐싱을 위한 딕셔너리
        private Dictionary<string, string> warningCodeCache = new Dictionary<string, string>();

        // ErrCode 파일명을 언어에 따라 반환
        private string GetErrCodeFileName()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory + "Config\\";
            string langSuffix = "";
            
            if (TranslationSource.Instance.CurrentCulture != null)
            {
                string cultureName = TranslationSource.Instance.CurrentCulture.Name;
                if (cultureName == "en")
                    langSuffix = "_en";
                else if (cultureName == "zh")
                    langSuffix = "_zh";
                else if (cultureName == "ja")
                    langSuffix = "_ja";
                else if (cultureName == "vi")
                    langSuffix = "_vi";
            }
            
            string fileName = basePath + "ErrCode" + langSuffix + ".ini";
            
            Console.WriteLine("Current Culture File : " + fileName);
            // 언어별 파일이 없으면 기본 파일 사용
            if (!File.Exists(fileName))
            {
                Console.WriteLine("Current Culture File Not Exist Set Default ");
                fileName = basePath + "ErrCode.ini";
                Console.WriteLine("Current Culture File Default : " + fileName);
            }


            return fileName;
        }
        
        // WarningCode 파일명을 언어에 따라 반환
        private string GetWarningCodeFileName()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory + "Config\\";
            string langSuffix = "";
            
            if (TranslationSource.Instance.CurrentCulture != null)
            {
                string cultureName = TranslationSource.Instance.CurrentCulture.Name;
                if (cultureName == "en")
                    langSuffix = "_en";
                else if (cultureName == "zh")
                    langSuffix = "_zh";
                else if (cultureName == "ja")
                    langSuffix = "_ja";
                else if (cultureName == "vi")
                    langSuffix = "_vi";
            }
            
            string fileName = basePath + "WarningCode" + langSuffix + ".ini";
            
            Console.WriteLine("Current Culture Warning File : " + fileName);
            // 언어별 파일이 없으면 기본 파일 사용
            if (!File.Exists(fileName))
            {
                Console.WriteLine("Current Culture Warning File Not Exist Set Default ");
                fileName = basePath + "WarningCode.ini";
                Console.WriteLine("Current Culture Warning File Default : " + fileName);
            }

            return fileName;
        }
        
        // 에러 코드 파일 로드 및 파싱
        private void LoadErrCodeFileToCache(string fileName)
        {
            errorCodeCache.Clear();
            
            if (!File.Exists(fileName))
            {
                Console.WriteLine("File not found: " + fileName);
                return;
            }

            try
            {
                // 인코딩 결정: 기본 ErrCode.ini는 euc-kr(ANSI), 나머지는 UTF-8
                Encoding encoding = Encoding.UTF8;
                if (fileName.EndsWith("ErrCode.ini", StringComparison.OrdinalIgnoreCase))
                {
                    // euc-kr 시도, 실패하면 Default 사용
                    try 
                    {
                        encoding = Encoding.GetEncoding("euc-kr");
                    }
                    catch 
                    {
                        // euc-kr을 찾지 못하면 시스템 기본값 사용
                        encoding = Encoding.Default; 
                    }
                }

                // 파일 읽기
                string[] lines = File.ReadAllLines(fileName, encoding);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("[") || trimmed.StartsWith(";"))
                        continue;

                    int idx = trimmed.IndexOf('=');
                    if (idx > 0)
                    {
                        string key = trimmed.Substring(0, idx).Trim();
                        string value = trimmed.Substring(idx + 1).Trim();
                        errorCodeCache[key] = value;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading ErrCode file: " + ex.Message);
            }
        }

        // 캐시에서 에러 텍스트 가져오기
        private string GetErrorTextFromCache(string key)
        {
            if (errorCodeCache.ContainsKey(key))
            {
                return errorCodeCache[key];
            }
            return "";
        }
        
        // 경고 코드 파일 로드 및 파싱
        private void LoadWarningCodeFileToCache(string fileName)
        {
            warningCodeCache.Clear();
            
            if (!File.Exists(fileName))
            {
                Console.WriteLine("Warning file not found: " + fileName);
                return;
            }

            try
            {
                // 인코딩 결정: 기본 WarningCode.ini는 euc-kr(ANSI), 나머지는 UTF-8
                Encoding encoding = Encoding.UTF8;
                if (fileName.EndsWith("WarningCode.ini", StringComparison.OrdinalIgnoreCase))
                {
                    // euc-kr 시도, 실패하면 Default 사용
                    try 
                    {
                        encoding = Encoding.GetEncoding("euc-kr");
                    }
                    catch 
                    {
                        // euc-kr을 찾지 못하면 시스템 기본값 사용
                        encoding = Encoding.Default; 
                    }
                }

                // 파일 읽기
                string[] lines = File.ReadAllLines(fileName, encoding);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("[") || trimmed.StartsWith(";"))
                        continue;

                    int idx = trimmed.IndexOf('=');
                    if (idx > 0)
                    {
                        string key = trimmed.Substring(0, idx).Trim();
                        string value = trimmed.Substring(idx + 1).Trim();
                        warningCodeCache[key] = value;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading WarningCode file: " + ex.Message);
            }
        }

        // 캐시에서 경고 텍스트 가져오기
        private string GetWarningTextFromCache(string key)
        {
            if (warningCodeCache.ContainsKey(key))
            {
                return warningCodeCache[key];
            }
            return "";
        }

        // 에러 코드 리스트 재로딩
        public void ReloadErrorCodeList()
        {
            errCodeList.Clear();
            errSubCodeList.Clear();
            
            string errCodeFile = GetErrCodeFileName();
            
            Console.WriteLine("Error CodeList ReLoad  : " + errCodeFile);
            
            // 파일을 메모리에 로드
            LoadErrCodeFileToCache(errCodeFile);

            foreach (var kvp in errorCodeCache)
            {
                string key = kvp.Key;
                string value = kvp.Value ?? "";
                if (string.IsNullOrEmpty(value))
                    continue;

                if (key.Contains("-"))
                {
                    string[] parts = key.Split(new[] { '-' }, 2);
                    if (parts.Length >= 2)
                        errSubCodeList.Add(new Data { errCode = parts[0].Trim(), subCode = parts[1].Trim(), errContent = value });
                }
                else
                {
                    errCodeList.Add(new Data { errCode = key.Trim(), errContent = value });
                }
            }
            
            // 기존 에러 내역의 텍스트도 업데이트
            foreach (var item in errStrList)
            {
                Console.WriteLine("기존리스트 업데이트?? ");
                item.errContent = getErrorText(item.errCode, item.subCode);
            }
        }
        
        // 경고 코드 리스트 재로딩
        public void ReloadWarningCodeList()
        {
            warningCodeList.Clear();
            
            string warningCodeFile = GetWarningCodeFileName();
            
            Console.WriteLine("Warning CodeList ReLoad  : " + warningCodeFile);
            
            // 파일을 메모리에 로드
            LoadWarningCodeFileToCache(warningCodeFile);

            foreach (var kvp in warningCodeCache)
            {
                string key = kvp.Key;
                string value = kvp.Value ?? "";
                if (string.IsNullOrEmpty(value))
                    continue;

                if (key.Contains("-"))
                {
                    string[] parts = key.Split(new[] { '-' }, 2);
                    if (parts.Length >= 2)
                        warningCodeList.Add(new Data { errCode = parts[0].Trim(), subCode = parts[1].Trim(), errContent = value });
                }
                else
                {
                    warningCodeList.Add(new Data { errCode = key.Trim(), errContent = value });
                }
            }
        }
        
        // 경고 코드 리스트 초기 로드
        private void LoadWarningCodeList()
        {
            string warningCodeFile = GetWarningCodeFileName();
            
            Console.WriteLine("Warning CodeList Load Start : " + warningCodeFile);
            
            // 파일을 메모리에 로드
            LoadWarningCodeFileToCache(warningCodeFile);

            foreach (var kvp in warningCodeCache)
            {
                string key = kvp.Key;
                string value = kvp.Value ?? "";
                if (string.IsNullOrEmpty(value))
                    continue;

                if (key.Contains("-"))
                {
                    string[] parts = key.Split(new[] { '-' }, 2);
                    if (parts.Length >= 2)
                        warningCodeList.Add(new Data { errCode = parts[0].Trim(), subCode = parts[1].Trim(), errContent = value });
                }
                else
                {
                    warningCodeList.Add(new Data { errCode = key.Trim(), errContent = value });
                }
            }
        }
        
        public PageAlarmLog()
        {
            // 인코딩 프로바이더 등록 (euc-kr 지원을 위해 필요)
            try 
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Encoding Provider Register Error: " + ex.Message);
            }

            InitializeComponent();
            gClass = singletonClass.Instance;
            
            // 언어 변경 이벤트 구독
            TranslationSource.Instance.PropertyChanged += TranslationSource_PropertyChanged;

            //errStrList.Add(new Data { errNum = 1, occurredTime = "2023-03-31 15:02:26", errCode = "06", subCode = "1", errContent = "테스트" });
            //errStrList.Add(new Data { errNum = 2, occurredTime = "2023-03-31 15:02:26", errCode = "06", subCode = "2", errContent = "테스트" });
            //errStrList.Add(new Data { errNum = 3, occurredTime = "2023-03-31 15:02:26", errCode = "06", subCode = "3", errContent = "테스트" });
            //errStrList.Add(new Data { errNum = 4, occurredTime = "2023-03-31 15:02:26", errCode = "06", subCode = "4", errContent = "테스트" });
            //errStrList.Add(new Data { errNum = 5, occurredTime = "2023-03-31 15:02:26", errCode = "06", subCode = "5", errContent = "테스트" });
            //errStrList.Prepend(new Data { errNum = 6, occurredTime = "2023-03-31 15:02:26", errCode = "06", subCode = "1", errContent = "테스트" });
            


            // 초기 에러 코드 리스트 로드
            LoadErrorCodeList();
            
            // 초기 경고 코드 리스트 로드
            LoadWarningCodeList();

            AlarmList.ItemsSource = errStrList;
            AlarmCodeList.ItemsSource = errCodeList;
            AlarmSubCodeList.ItemsSource = errSubCodeList;
            AlarmWarningCodeList.ItemsSource = warningCodeList;

        }

        public void errorListClear()
        {
            occurredNum = 1;
            errStrList.Clear();
        }

        public void errorOccurred(int srmNum, string occurTime, string errCode, string subCode, bool save)
        {
            // Dispatcher.Invoke(() => logStrList.Add(message)); // to do 필요할 수 있음
            string errStr = "";

            if (gClass.srmNum == srmNum)             // 현재 호기와 에러 발생 호기가 동일할 경우 현재 테이블에 추가
            {
                errStr = getErrorText(errCode, subCode);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    errStrList.Insert(0, new Data { errNum = occurredNum, occurredTime = occurTime, errCode = errCode, subCode = subCode, errContent = errStr });
                    AlarmList.ScrollIntoView(AlarmList.Items[0]);
                    occurredNum += 1;
                    //Console.WriteLine("errorOccurred " + occurredNum);
                }));
            }
            if (save)
            {
                SaveAlarmLogFile(srmNum, occurTime + "/" + errCode + "/" + subCode + "/" + errStr);
            }
        }

        private async void SaveAlarmLogFile(int srmNum, string text)
        {
            await Task.Run(() =>
            {
                mutex.WaitOne();

                string pathString = System.IO.Path.Combine(Environment.CurrentDirectory, "SRM" + srmNum, cConstDefine.PATH_LOG, cConstDefine.PATH_ALARMLOG);
                string filePath = System.IO.Path.Combine(pathString, "ALARMLOG_" + DateTime.Now.ToString("yyyyMMdd") + ".log");

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
                }

                mutex.ReleaseMutex();
            });
        }

        // 에러 코드 리스트 초기 로드
        private void LoadErrorCodeList()
        {
            string errCodeFile = GetErrCodeFileName();
            
            Console.WriteLine("Error CodeList Load Start : " + errCodeFile);
            
            // 파일을 메모리에 로드
            LoadErrCodeFileToCache(errCodeFile);

            foreach (var kvp in errorCodeCache)
            {
                string key = kvp.Key;
                string value = kvp.Value ?? "";
                if (string.IsNullOrEmpty(value))
                    continue;

                if (key.Contains("-"))
                {
                    string[] parts = key.Split(new[] { '-' }, 2);
                    if (parts.Length >= 2)
                        errSubCodeList.Add(new Data { errCode = parts[0].Trim(), subCode = parts[1].Trim(), errContent = value });
                }
                else
                {
                    errCodeList.Add(new Data { errCode = key.Trim(), errContent = value });
                }
            }
        }
        
        // 언어 변경 이벤트 핸들러
        private void TranslationSource_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // UI 스레드에서 실행
            Dispatcher.Invoke(() =>
            {
                ReloadErrorCodeList();
                ReloadWarningCodeList();
            });
        }
        
        public string getErrorText(string errCode, string subCode)
        {
            // string errCodeFile = GetErrCodeFileName(); // 캐시 사용 시 불필요
            string alarmText;
            string subText = string.Empty;

            Console.WriteLine("Error Get CodeList : " + errCode + "-" + subCode);

            if (subCode == "00")
            {
                alarmText = GetErrorTextFromCache(errCode);
                if(alarmText == ""){
                    alarmText = GetErrorTextFromCache(errCode + "-" + subCode);
                }
            }
            else
            {
                alarmText = GetErrorTextFromCache(errCode + "-" + subCode);
            }

            // 에러에 따른 조건추가
            if (int.Parse(errCode) == 44)
            {
                subText = $"({gClass.str.SrmState[gClass.srmNum].invErrorFork1Main}-{gClass.str.SrmState[gClass.srmNum].invErrorFork1Sub})";
            }
            else if (int.Parse(errCode) == 45)
            {
                subText = $"({gClass.str.SrmState[gClass.srmNum].invErrorFork2Main}-{gClass.str.SrmState[gClass.srmNum].invErrorFork2Sub})";
            }
            else if (int.Parse(errCode) == 46)
            {
                subText = $"({gClass.str.SrmState[gClass.srmNum].invErrorTravMain}-{gClass.str.SrmState[gClass.srmNum].invErrorTravSub})";
            }
            else if (int.Parse(errCode) == 47)
            {
                subText = $"({gClass.str.SrmState[gClass.srmNum].invErrorLiftMain}-{gClass.str.SrmState[gClass.srmNum].invErrorLiftSub})";
            }

            alarmText = subText + alarmText;

            return alarmText;
        }
        
        public string getWarningText(string warningCode, string subCode)
        {
            string alarmText;

            Console.WriteLine("Warning Get CodeList : " + warningCode + "-" + subCode);

            // 경고 코드 캐시가 비어있으면 파일 로드
            if (warningCodeCache.Count == 0)
            {
                string warningCodeFile = GetWarningCodeFileName();
                LoadWarningCodeFileToCache(warningCodeFile);
            }

            if (subCode == "00")
            {
                alarmText = GetWarningTextFromCache(warningCode);
            }
            else
            {
                alarmText = GetWarningTextFromCache(warningCode + "-" + subCode);
            }
            return alarmText;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            errorOccurred(0, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), "03", "01", true);         // file save Flag = true
        }
    }
}
