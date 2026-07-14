using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using gcp_Wpf.MenuWindow;

namespace gcp_Wpf
{
    /// <summary>
    /// PageCraneOperate.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PageCraneOperate : Page
    {
        private int bayCount = 10;
        private int levCount = 6;
        private int currentCraneBay = 1;
        private int currentCraneLev = 1;

        singletonClass gClass;
        int cntRow, cntBay, cntLev, cntStn;
        public PageCraneOperate()
        {
            gClass = singletonClass.Instance;
            InitializeComponent();
            this.Loaded += PageCraneOperate_Loaded;
            webView.NavigationCompleted += WebView_NavigationCompleted; // 이벤트 핸들러를 생성자에서 등록
        }

        private async void PageCraneOperate_Loaded(object sender, RoutedEventArgs e)
        {
            // Loaded 이벤트에서 singletonClass 데이터 초기화
            cntRow = gClass.str.SrmInfo[gClass.srmNum].row;
            cntBay = gClass.str.SrmInfo[gClass.srmNum].bay;
            cntLev = gClass.str.SrmInfo[gClass.srmNum].lev;
            cntStn = gClass.str.SrmInfo[gClass.srmNum].stn;

            System.Console.WriteLine("Current Bay : " + cntBay);
            System.Console.WriteLine("Current Lev : " + cntLev);
            System.Console.WriteLine("Current Row : " + cntRow);
            System.Console.WriteLine("Current Stn : " + cntStn);

            await InitializeWebView();
        }

        private async Task InitializeWebView()
        {
            try
            {
                await webView.EnsureCoreWebView2Async(null);
                // webView.NavigationCompleted += WebView_NavigationCompleted; // 여기서 제거
                await LoadHTMLFile();
            }
            catch (Exception ex)
            {
                VarMessageBox.Show(cConstDefine.tr("오류"), $"WebView 초기화 실패: {ex.Message}", VarMessageBoxButton.OK);
            }
        }

        private async void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                try
                {
                    if (webView?.CoreWebView2 != null)
                    {
                        // INI 파일에서 렉과 스테이션 정보 로드
                        await LoadRackAndStationLayout();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"INI 파일 로드 실패, 기본값 사용: {ex.Message}");
                    // INI 파일 로드 실패 시 기본값으로 레이아웃 설정
                    int tempExternalBayCount = cntBay;
                    int tempExternalLevCount = cntLev;
                    if (webView?.CoreWebView2 != null)
                    {
                        await webView.CoreWebView2.ExecuteScriptAsync($"setRackLayout({tempExternalBayCount}, {tempExternalLevCount});");
                    }
                }
            }
        }

        private async Task LoadHTMLFile()
        {
            try
            {
                string htmlPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "crane_operate.html");
                
                if (!File.Exists(htmlPath))
                {
                    VarMessageBox.Show(cConstDefine.tr("오류"), $"HTML 파일을 찾을 수 없습니다: {htmlPath}", VarMessageBoxButton.OK);
                    return;
                }

                string htmlUri = new Uri(htmlPath).ToString();
                webView.CoreWebView2.Navigate(htmlUri);
            }
            catch (Exception ex)
            {
                VarMessageBox.Show(cConstDefine.tr("오류"), $"HTML 파일 로드 실패: {ex.Message}", VarMessageBoxButton.OK);
            }
        }



        private async void btn_UpdateLayout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (int.TryParse(txt_BayCount.Text, out int newBayCount) && 
                    int.TryParse(txt_LevCount.Text, out int newLevCount))
                {
                    if (newBayCount > 0 && newBayCount <= 50 && newLevCount > 0 && newLevCount <= 20)
                    {
                        // JavaScript 함수 호출
                        if (webView?.CoreWebView2 != null)
                        {
                            await webView.CoreWebView2.ExecuteScriptAsync($"setRackLayout({newBayCount}, {newLevCount});");
                        }
                    }
                    else
                    {
VarMessageBox.Show(cConstDefine.tr("입력 오류"), cConstDefine.tr("BAY는 1-50, LEV는 1-20 범위 내에서 입력해주세요."), VarMessageBoxButton.OK);
                    }
                }
                else
                {
                    MessageBox.Show(cConstDefine.tr("올바른 숫자를 입력해주세요."), cConstDefine.tr("입력 오류"), 
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
VarMessageBox.Show(cConstDefine.tr("오류"), $"레이아웃 업데이트 실패: {ex.Message}", VarMessageBoxButton.OK);
            }
        }

        private async void btn_MoveCrane_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (int.TryParse(txt_CraneBay.Text, out int newBay) && int.TryParse(txt_CraneLev.Text, out int newLev))
                {
                    // JavaScript 함수 호출하여 크레인 이동
                    if (webView?.CoreWebView2 != null)
                    {
                        await webView.CoreWebView2.ExecuteScriptAsync($"moveCraneTo({newBay}, {newLev});");
                    }
                }
                else
                {
                    VarMessageBox.Show(cConstDefine.tr("입력 오류"), cConstDefine.tr("올바른 숫자를 입력해주세요."), VarMessageBoxButton.OK);
                }
            }
            catch (Exception ex)
            {
                VarMessageBox.Show(cConstDefine.tr("오류"), $"크레인 이동 실패: {ex.Message}", VarMessageBoxButton.OK);
            }
        }

        // 외부에서 크레인 위치를 업데이트하는 메서드
        public async Task UpdateCranePosition(int bay, int lev)
        {
            if (webView?.CoreWebView2 != null)
            {
                await webView.CoreWebView2.ExecuteScriptAsync($"moveCraneTo({bay}, {lev});");
            }
        }

        // 애니메이션 속도 설정 메서드
        public async Task SetAnimationSpeed(int speedMs)
        {
            if (webView?.CoreWebView2 != null)
            {
                await webView.CoreWebView2.ExecuteScriptAsync($"setAnimationSpeed({speedMs});");
            }
        }

        // 현재 크레인 위치 조회 메서드
        public async Task<string> GetCranePosition()
        {
            if (webView?.CoreWebView2 != null)
            {
                try
                {
                    string result = await webView.CoreWebView2.ExecuteScriptAsync("getCranePosition();");
                    return result; 
                }
                catch
                {
                    return "{\"bay\":1, \"lev\":1, \"isMoving\":false}";
                }
            }
            return "{\"bay\":1, \"lev\":1, \"isMoving\":false}";
        }

        // INI 파일 읽기 메서드들
        private async Task LoadRackAndStationLayout()
        {
            try
            {
                string exePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string rackIniPath = System.IO.Path.Combine(exePath, "SRM0", "RACK", "Rack.ini");
                string stationIniPath = System.IO.Path.Combine(exePath, "SRM0", "Station.ini");

                Console.WriteLine($"Rack INI 경로: {rackIniPath}");
                Console.WriteLine($"Station INI 경로: {stationIniPath}");

                // Rack 정보 읽기
                var rackInfo = ReadRackInfo(rackIniPath);
                if (rackInfo != null)
                {
                    // JavaScript로 렉 정보 전달
                    string rackInfoJson = $@"{{
                        ""maxBay"": {rackInfo.MaxBay},
                        ""maxLev"": {rackInfo.MaxLev},
                        ""bayPositions"": [{string.Join(",", rackInfo.BayPositions)}],
                        ""levPositions"": [{string.Join(",", rackInfo.LevPositions)}]
                    }}";

                    await webView.CoreWebView2.ExecuteScriptAsync($"setRackLayoutWithPositions({rackInfoJson});");
                }

                // Station 정보 읽기
                var stationInfo = ReadStationInfo(stationIniPath);
                if (stationInfo != null && stationInfo.Count > 0)
                {
                    // JavaScript로 스테이션 정보 전달
                    var stationArray = string.Join(",", stationInfo.Select(s => 
                        $@"{{""id"": ""{s.Id}"", ""travPos"": {s.TravPos}, ""liftPos"": {s.LiftPos}}}"));
                    
                    await webView.CoreWebView2.ExecuteScriptAsync($"setStationsWithPositions([{stationArray}]);");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"INI 파일 로드 오류: {ex.Message}");
                VarMessageBox.Show("오류", $"INI 파일 로드 실패: {ex.Message}", VarMessageBoxButton.OK);
            }
        }

        private RackInfo ReadRackInfo(string iniPath)
        {
            if (!File.Exists(iniPath))
            {
                Console.WriteLine($"Rack.ini 파일이 존재하지 않습니다: {iniPath}");
                return null;
            }

            try
            {
                var rackInfo = new RackInfo();
                
                // RACKINFO 섹션 읽기
                rackInfo.MaxBay = int.Parse(cIniAccess.Read(iniPath, "RACKINFO", "MaxBay", "10"));
                rackInfo.MaxLev = int.Parse(cIniAccess.Read(iniPath, "RACKINFO", "MaxLev", "6"));

                Console.WriteLine($"Rack 정보 - MaxBay: {rackInfo.MaxBay}, MaxLev: {rackInfo.MaxLev}");

                // BAY 위치값 읽기 (BAY0=1베이, BAY1=2베이... BAY4=5베이)
                rackInfo.BayPositions = new double[rackInfo.MaxBay];
                for (int i = 0; i < rackInfo.MaxBay; i++)
                {
                    string bayPos = cIniAccess.Read(iniPath, "RACK_BAY", $"BAY{i}", "0");
                    rackInfo.BayPositions[i] = double.Parse(bayPos);
                    Console.WriteLine($"BAY{i} -> {i + 1}베이 위치값: {rackInfo.BayPositions[i]}");
                }
                Console.WriteLine($"총 {rackInfo.MaxBay}개 베이 읽기 완료 (BAY0~BAY{rackInfo.MaxBay-1})");

                // LEV 위치값 읽기 (LEV0=1레벨, LEV1=2레벨... LEV4=5레벨)
                rackInfo.LevPositions = new double[rackInfo.MaxLev];
                for (int i = 0; i < rackInfo.MaxLev; i++)
                {
                    string levPos = cIniAccess.Read(iniPath, "RACK_LEV", $"LEV{i}", "0");
                    rackInfo.LevPositions[i] = double.Parse(levPos);
                    Console.WriteLine($"LEV{i} -> {i + 1}레벨 위치값: {rackInfo.LevPositions[i]}");
                }
                Console.WriteLine($"총 {rackInfo.MaxLev}개 레벨 읽기 완료 (LEV0~LEV{rackInfo.MaxLev-1})");

                return rackInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Rack.ini 읽기 오류: {ex.Message}");
                return null;
            }
        }

        private List<StationInfo> ReadStationInfo(string iniPath)
        {
            var stations = new List<StationInfo>();

            if (!File.Exists(iniPath))
            {
                Console.WriteLine($"Station.ini 파일이 존재하지 않습니다: {iniPath}");
                return stations;
            }

            try
            {
                // 스테이션 개수 읽기
                int stationCount = int.Parse(cIniAccess.Read(iniPath, "STATION", "COUNT", "0"));
                Console.WriteLine($"스테이션 개수: {stationCount}");

                // 각 스테이션 정보 읽기 (STATION0=스테이션1, STATION1=스테이션2...)
                for (int i = 0; i < stationCount; i++)
                {
                    string section = $"STATION{i}";
                    string travPos = cIniAccess.Read(iniPath, section, "TRAVPOS", "0");
                    string liftPos = cIniAccess.Read(iniPath, section, "LIFTPOS", "0");

                    var station = new StationInfo
                    {
                        Id = $"station{i + 1}",
                        TravPos = double.Parse(travPos),
                        LiftPos = double.Parse(liftPos)
                    };

                    stations.Add(station);
                    Console.WriteLine($"STATION{i} -> 스테이션 {i + 1}: TravPos={station.TravPos}, LiftPos={station.LiftPos}");
                }

                return stations;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Station.ini 읽기 오류: {ex.Message}");
                return stations;
            }
        }

        // INI 정보를 담을 클래스들
        private class RackInfo
        {
            public int MaxBay { get; set; }
            public int MaxLev { get; set; }
            public double[] BayPositions { get; set; }
            public double[] LevPositions { get; set; }
        }

        private class StationInfo
        {
            public string Id { get; set; }
            public double TravPos { get; set; }
            public double LiftPos { get; set; }
        }
    }
}