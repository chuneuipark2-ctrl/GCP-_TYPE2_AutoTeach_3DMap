using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using gcp_Wpf.MenuWindow;

namespace gcp_Wpf
{
    /// <summary>
    /// PageMonitorToWCS.xaml에 대한 상호 작용 논리
    /// </summary>
    /// 
   
    public partial class PageMonitorToWCS : Page
    {
        List<MonitorData> monitorList = new List<MonitorData>();

        //Singletone
        singletonClass gClass;
        MainWindow pMain;

        //Test Timer
        Timer dataCheckTimer = new Timer();

        string[][] dataStrList = new string[200][];
        // 더미 스트링 초기화
        string[] dataStr = new string[4] { "", "", "", "" };
        int mapListCnt = 0;
        int dispPageNo = 0;

        public PageMonitorToWCS(MainWindow parent)
        {
            InitializeComponent();
            gClass = singletonClass.Instance;
            pMain = parent;

            // Test Edit
            edit_Bit.Visibility = Visibility.Hidden;
            edit_Word.Visibility = Visibility.Hidden;
            edit_DWord.Visibility = Visibility.Hidden;

            // Event init
            Btn_Left.Click += Click_LeftRight;
            Btn_Right.Click += Click_LeftRight;

            // UI 리스트 인덱스 ----------- 실제 데이터 맵핑 주소, 타입, 값
            // WCS to GCP Mapping Data
            //-----------------------------------기본상태 응답 데이터 7500~7699-----------------------------------------------
            dataStrList[mapListCnt++] = new string[4] { "7500", "0", "SRM 작업요구", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7500", "1", "Fork#1 작업유무", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7500", "2", "Fork#2 작업유무", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7500", "3", "SRM 동작중", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7500", "4", "Fork#1 화물감지", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7500", "5", "Fork#2 화물감지", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7500", "6", "Fork#1 작업완료", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7500", "7", "Fork#2 작업완료", "0" };
            //dataStrList[mapListCnt++] = new string[4]{"0", "7", "SP AREA", "0"};
            dataStrList[mapListCnt++] = new string[4] { "7500", "10", "크레인 에러", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7500", "11", "크레인 복구가능에러", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7500", "12", "크레인 홈", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7501", "W", "메인에러코드", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7502", "W", "서브에러코드", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7503", "W", "현재 ST", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7504", "W", "현재 ROW", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7505", "W", "현재 BAY", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7506", "W", "현재 LEV", "0" };

            //dataStrList[mapListCnt++] = new string[4]{"7", "-", "SP AREA", "0"};
            dataStrList[mapListCnt++] = new string[4] { "7508", "0", "Fork#1 좌 정위치", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7508", "1", "Fork#1 좌 이동중", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7508", "2", "Fork#1 중심 정위치", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7508", "3", "Fork#1 중심 이동중", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7508", "4", "Fork#1 우 정위치", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7508", "5", "Fork#1 우 이동중", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7508", "8", "Fork#2 좌 정위치", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7508", "9", "Fork#2 좌 이동중", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7508", "10", "Fork#2 중심 정위치", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7508", "11", "Fork#2 중심 이동중", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7508", "12", "Fork#2 우 정위치", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7508", "13", "Fork#2 우 이동중", "0" };

            dataStrList[mapListCnt++] = new string[4] { "7509", "0", "주행 운전중", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7509", "1", "승강 운전중", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7509", "2", "Fork#1 운전중", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7509", "3", "Fork#2 운전중", "0" };

            dataStrList[mapListCnt++] = new string[4] { "7510", "W", "응답코드", "0" };

            dataStrList[mapListCnt++] = new string[4] { "7515", "0", "주행 홈 위치", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7515", "1", "승강 홈 위치", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7515", "2", "포크1 홈 위치", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7515", "3", "포크2 홈 위치", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7515", "4", "주행 정 위치", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7515", "5", "승강 정 위치", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7515", "6", "포크1 정 위치", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7515", "7", "포크2 정 위치", "0" };

            dataStrList[mapListCnt++] = new string[4] { "7520", "DW", "주행 현재 위치", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7522", "DW", "승강 현재 위치", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7524", "DW", "포크1 현재 위치", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7526", "DW", "포크2 현재 위치", "0" };

            dataStrList[mapListCnt++] = new string[4] { "7530", "DW", "주행 목적 위치", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7532", "DW", "승강 목적 위치", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7534", "DW", "포크1 목적 위치", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7536", "DW", "포크2 목적 위치", "0" };

            dataStrList[mapListCnt++] = new string[4] { "7540", "DW", "주행 현재 속도", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7542", "DW", "승강 현재 속도", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7544", "DW", "포크1 현재 속도", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7546", "DW", "포크2 현재 속도", "0" };

            dataStrList[mapListCnt++] = new string[4] { "7600", "0", "Fork#2 Loading 우선비트", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7600", "1", "Fork#2 Unloading 우선비트", "0" };

            dataStrList[mapListCnt++] = new string[4] { "7601", "W", "Fork#1 작업번호", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7602", "0", "이동명령", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7602", "1", "입고명령", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7602", "2", "출고명령", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7602", "3", "Rack to Rack", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7602", "4", "Station to Station", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7602", "5", "랙 목적지 변경", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7602", "6", "스테이션 목적지 변경", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7602", "7", "화물 재위치 명령", "0" };

            dataStrList[mapListCnt++] = new string[4] { "7603", "W", "Fork#1 From Station", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7604", "W", "Fork#1 From Row", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7605", "W", "Fork#1 From Bay", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7606", "W", "Fork#1 From Lev", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7607", "W", "Fork#1 To Station", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7608", "W", "Fork#1 To Row", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7609", "W", "Fork#1 To Bay", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7610", "W", "Fork#1 To Lev", "0" };

            dataStrList[mapListCnt++] = new string[4] { "7611", "W", "Fork#2 작업번호", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7612", "0", "이동명령", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7612", "1", "입고명령", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7612", "2", "출고명령", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7612", "3", "Rack to Rack", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7612", "4", "Station to Station", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7612", "5", "랙 목적지 변경", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7612", "6", "스테이션 목적지 변경", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7612", "7", "화물 재위치 명령", "0" };

            dataStrList[mapListCnt++] = new string[4] { "7613", "W", "Fork#2 From Station", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7614", "W", "Fork#2 From Row", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7615", "W", "Fork#2 From Bay", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7616", "W", "Fork#2 From Lev", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7617", "W", "Fork#2 To Station", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7618", "W", "Fork#2 To Row", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7619", "W", "Fork#2 To Bay", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7620", "W", "Fork#2 To Lev", "0" };

            dataStrList[mapListCnt++] = new string[4] { "7625", "0", "Heart Beat", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7625", "1", "홈복귀", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7625", "2", "이상리셋 ACK", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7625", "3", "전체 작업삭제 ACK", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7625", "4", "Fork#1 작업삭제 ACK", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7625", "5", "Fork#2 작업삭제 ACK", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7625", "8", "Data Report OK Status", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7625", "10", "SRM Online Status", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7625", "11", "SRM Manual Status", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7625", "12", "SRM Semi Status", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7625", "14", "SRM Cycle Stop Ack", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7625", "15", "SRM Em Stop Ack", "0" };

            dataStrList[mapListCnt++] = new string[4] { "7630", "0", "Fork#1 수동작업완료", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7630", "1", "Fork#2 수동작업완료", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7630", "4", "Fork#1 수동작업삭제", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7630", "5", "Fork#2 수동작업삭제", "0" };

            dataStrList[mapListCnt++] = new string[4] { "7631", "W", "Time Sync Year", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7632", "W", "Time Sync Month", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7633", "W", "Time Sync Date", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7634", "W", "Time Sync Hour", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7635", "W", "Time Sync Minute", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7636", "W", "Time Sync Second", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7637", "W", "Time Sync DayofWeek", "0" };
            //-----------------------------------확장상태 응답 데이터 7700~7799-----------------------------------------------
            dataStrList[mapListCnt++] = new string[4] { "7700", "DW", "주행 설정 속도", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7702", "DW", "승강 설정 속도", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7704", "DW", "포크1 설정 속도", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7706", "DW", "포크2 설정 속도", "0" };

            dataStrList[mapListCnt++] = new string[4] { "7700", "DW", "주행 설정 속도", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7702", "DW", "승강 설정 속도", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7704", "DW", "포크1 설정 속도", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7706", "DW", "포크2 설정 속도", "0" };

            dataStrList[mapListCnt++] = new string[4] { "7710", "DW", "주행 설정 가속도", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7712", "DW", "승강 설정 가속도", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7714", "DW", "포크1 설정 가속도", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7716", "DW", "포크2 설정 가속도", "0" };

            dataStrList[mapListCnt++] = new string[4] { "7720", "DW", "주행 설정 감속도", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7722", "DW", "승강 설정 감속도", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7724", "DW", "포크1 설정 감속도", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7726", "DW", "포크2 설정 감속도", "0" };

            dataStrList[mapListCnt++] = new string[4] { "7730", "DW", "주행 설정 저크", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7732", "DW", "승강 설정 저크", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7734", "DW", "포크1 설정 저크", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7736", "DW", "포크2 설정 저크", "0" };

            dataStrList[mapListCnt++] = new string[4] { "7740", "W", "로딩 이동 전 딜레이", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7741", "W", "로딩 이동 후 딜레이", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7742", "W", "로딩 포크 OUT 전 딜레이", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7743", "W", "로딩 포크 OUT 후 딜레이", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7744", "W", "로딩 포크 OUT 승강 전 딜레이", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7745", "W", "로딩 포크 OUT 승강 후 딜레이", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7746", "W", "로딩 포크 IN 전 딜레이", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7747", "W", "로딩 포크 IN 후 딜레이", "0" };

            dataStrList[mapListCnt++] = new string[4] { "7750", "W", "언로딩 이동 전 딜레이", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7751", "W", "언로딩 이동 후 딜레이", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7752", "W", "언로딩 포크 OUT 전 딜레이", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7753", "W", "언로딩 포크 OUT 후 딜레이", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7754", "W", "언로딩 포크 OUT 승강 전 딜레이", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7755", "W", "언로딩 포크 OUT 승강 후 딜레이", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7756", "W", "언로딩 포크 IN 전 딜레이", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7757", "W", "언로딩 포크 IN 후 딜레이", "0" };

            dataStrList[mapListCnt++] = new string[4] { "7760", "DW", "주행 부하율(%)", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7762", "DW", "승강 부하율(%)", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7764", "DW", "포크1 부하율(%)", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7766", "DW", "포크2 부하율(%)", "0" };

            dataStrList[mapListCnt++] = new string[4] { "7780", "DW", "총 동작시간", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7782", "DW", "주행 동작시간", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7784", "DW", "승강 동작시간", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7786", "DW", "포크1 동작시간", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7788", "DW", "포크2 동작시간", "0" };

            dataStrList[mapListCnt++] = new string[4] { "7790", "DW", "주행 브레이크 동작횟수", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7792", "DW", "승강 브레이크 동작횟수", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7794", "DW", "포크1 브레이크 동작횟수", "0" };
            dataStrList[mapListCnt++] = new string[4] { "7796", "DW", "포크2 브레이크 동작횟수", "0" };
            //---------------------------------------------------------------------------------------------------------
            Console.WriteLine("Check mapList Count : " + mapListCnt);

            // Left Table Data Init
            for (int i = 0; i < dataLeft.RowDefinitions.Count; i++)
            {
                if (dataLeft.ColumnDefinitions.Count < 4)
                {
                    VarMessageBox.Show(cConstDefine.tr("변경확인"), cConstDefine.tr("WCS TO 모니터 컬럼 카운트 오류"), VarMessageBoxButton.OK);
                    break;
                }
                //if (mapListCnt <= i)
                //{
                //    //MessageBox.Show("맵핑 리스트 카운트 오류", "변경확인", MessageBoxButton.OK, MessageBoxImage.Information);
                //    break;
                //}
                MonitorData monData = new MonitorData("", "", "", "");
                //MonitorData monData = new MonitorData(dataStrList[i][0], dataStrList[i][1], dataStrList[i][2], dataStrList[i][3]);
                Grid.SetRow(monData.lbl_word, i);
                Grid.SetColumn(monData.lbl_word, 0);
                Grid.SetRow(monData.lbl_bit, i);
                Grid.SetColumn(monData.lbl_bit, 1);
                Grid.SetRow(monData.lbl_type, i);
                Grid.SetColumn(monData.lbl_type, 2);
                Grid.SetRow(monData.lbl_value, i);
                Grid.SetColumn(monData.lbl_value, 3);
                monData.lbl_value.FontWeight = FontWeights.Bold;
                dataLeft.Children.Add(monData.lbl_word);
                dataLeft.Children.Add(monData.lbl_bit);
                dataLeft.Children.Add(monData.lbl_type);
                dataLeft.Children.Add(monData.lbl_value);
                monitorList.Add(monData);
            }

            // Right Table Data Init
            for (int i = 0; i < dataRight.RowDefinitions.Count; i++)
            {
                if (dataRight.ColumnDefinitions.Count < 4)
                {
                    VarMessageBox.Show(cConstDefine.tr("변경확인"), cConstDefine.tr("WCS TO 모니터 컬럼 카운트 오류"), VarMessageBoxButton.OK);
                    break;
                }
                //if (mapListCnt <= (i+ dataLeft.RowDefinitions.Count))
                //{
                //    //MessageBox.Show("맵핑 리스트 카운트 오류", "변경확인", MessageBoxButton.OK, MessageBoxImage.Information);
                //    break;
                //}
                MonitorData monData = new MonitorData("", "", "", "");
                //MonitorData monData = new MonitorData(dataStrList[i + dataLeft.RowDefinitions.Count][0], dataStrList[i + dataLeft.RowDefinitions.Count][1], dataStrList[i + dataLeft.RowDefinitions.Count][2], dataStrList[i + dataLeft.RowDefinitions.Count][3]);
                Grid.SetRow(monData.lbl_word, i);
                Grid.SetColumn(monData.lbl_word, 0);
                Grid.SetRow(monData.lbl_bit, i);
                Grid.SetColumn(monData.lbl_bit, 1);
                Grid.SetRow(monData.lbl_type, i);
                Grid.SetColumn(monData.lbl_type, 2);
                Grid.SetRow(monData.lbl_value, i);
                Grid.SetColumn(monData.lbl_value, 3);
                monData.lbl_value.FontWeight = FontWeights.Bold;
                dataRight.Children.Add(monData.lbl_word);
                dataRight.Children.Add(monData.lbl_bit);
                dataRight.Children.Add(monData.lbl_type);
                dataRight.Children.Add(monData.lbl_value);
                monitorList.Add(monData);
            }

            // 초기 페이지 init
            display_Change(0);


            // to do 타이머 켜는 시점 정리 필요
            dataCheckTimer.Interval = 1000; // 1 second
            dataCheckTimer.AutoReset = true; // Repeat the timer
            dataCheckTimer.Elapsed += dataTimer_Elapsed;
            dataCheckTimer.Start();
        }

        private void dataTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            //
            Dispatcher.Invoke(() =>
            {
                try
                {
                    int tmpWord = 0;
                    int tmpBit = 0;
                    int tmpValue = 0;
                    int curValue = 0;
                    for (int i = 0; i < monitorList.Count; i++)
                    {
                        // combinedArray[i] = (int)shortArray1[i] << 16 | shortArray2[i];
                        if (int.TryParse(monitorList[i].lbl_word.Content.ToString(), out tmpWord))          // 유효한 Word일 경우
                        {
                            tmpWord -= 7500;
                            if (int.TryParse(monitorList[i].lbl_bit.Content.ToString(), out tmpBit))         // 유효한 Bit일 경우
                            {
                                tmpValue = (gClass.str.WcsPacket[gClass.srmNum].WCSTO[tmpWord] >> tmpBit) & 0x01;       // 설정 비트 시프트 후 값 표시       
                            }
                            else                                                                            // 유효한 Bit가 아닐경우 Word 데이터 자체 값을 표시
                            {
                                if (monitorList[i].lbl_bit.Content.ToString() == "DW")
                                {
                                    tmpValue = (int)gClass.str.WcsPacket[gClass.srmNum].WCSTO[tmpWord] << 16 | gClass.str.WcsPacket[gClass.srmNum].WCSTO[tmpWord + 1];
                                }
                                else
                                {
                                    tmpValue = gClass.str.WcsPacket[gClass.srmNum].WCSTO[tmpWord];
                                }
                            }

                            int.TryParse(monitorList[i].lbl_value.Content.ToString(), out curValue);

                            if (curValue != tmpValue)
                            {
                                monitorList[i].lbl_value.Foreground = Brushes.DarkTurquoise;
                                monitorList[i].lbl_value.FontSize = 15;
                            }
                            else
                            {
                                monitorList[i].lbl_value.Foreground = Brushes.White;
                                monitorList[i].lbl_value.FontSize = 12;
                            }
                            monitorList[i].lbl_value.Content = tmpValue;
                        }
                        else
                        {
                            monitorList[i].lbl_type.Content = "Word Exception";
                        }
                    }


                    // to do 240312 테스트코드 직접입력 테스트
                    //gClass.str.WcsPacket[gClass.srmNum].WCSTO[0] = ushort.Parse(edit_Bit.Text);
                    //gClass.str.WcsPacket[gClass.srmNum].WCSTO[1] = ushort.Parse(edit_Word.Text);
                    //gClass.str.WcsPacket[gClass.srmNum].WCSTO[11] = (ushort)(ushort.Parse(edit_DWord.Text) >> 16);
                    //gClass.str.WcsPacket[gClass.srmNum].WCSTO[12] = ushort.Parse(edit_DWord.Text);
                }
                catch (Exception ex)
                {
                    cIniAccess.SaveExLog(0, "EXCEPTION - PageMonitorToWCSTimer : " + ex.Message);
                }
            });
        }

        private void Click_LeftRight(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;

            if (btn == Btn_Left)
            {
                if (dispPageNo > 0)
                {
                    dispPageNo--;
                }
                else
                {
                    // LastPage No
                    dispPageNo = 4;
                }
            }
            else if (btn == Btn_Right)
            {
                if (dispPageNo < 4) // LastPage No
                {
                    dispPageNo++;
                }
                else
                {
                    dispPageNo = 0;
                }
            }

            display_Change(dispPageNo);
        }

        private void display_Change(int pageNo)
        {
            int curListCnt = 0;

            switch (pageNo)
            {
                case 0:
                    curListCnt = 0;
                    break;
                case 1:
                    curListCnt = 40;
                    break;
                case 2:
                    curListCnt = 80;
                    break;
                case 3:
                    curListCnt = 120;
                    break;
                case 4:
                    curListCnt = 160;
                    break;
            }

            for (int i = 0; i < (dataLeft.RowDefinitions.Count + dataRight.RowDefinitions.Count); i++)
            {
                if (curListCnt + i < mapListCnt)
                {
                    monitorList[i].setMonitorData(dataStrList[curListCnt + i]);
                }
                else
                {
                    monitorList[i].setMonitorData(dataStr);
                }
            }
        }

        private void Click_OpenNumpad(object sender, MouseButtonEventArgs e)
        {
            TextBox edit = sender as TextBox;
            edit.PointToScreen(new Point(0, 0));

            pMain.tmpNumPad.AttachTo(edit, Window.GetWindow(this), pMain.PointToScreen(new Point(0, 0)), pMain.ActualWidth, pMain.ActualHeight);
            Console.WriteLine("Btn_FromTo_Select : " + edit.Name);
        }
    }
}
