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
    /// PageMonitorJOB.xaml에 대한 상호 작용 논리
    /// </summary>
    /// 
    unsafe public class MonitorData
    {
        public Label lbl_word;
        public Label lbl_bit;
        public Label lbl_type;
        public Label lbl_value;
        public string typeStr;
        public string oldValue;
        public Func<bool>? getBit { get; }
        public Action<bool>? setBit { get; }

        public MonitorData(string type, string value)
        {
            lbl_type = new Label() { Content = type };
            lbl_value = new Label() { Content = value };
            lbl_type.Foreground = Brushes.White;
            lbl_value.Foreground = Brushes.White;
            lbl_type.HorizontalContentAlignment = HorizontalAlignment.Left;
            lbl_type.VerticalContentAlignment = VerticalAlignment.Center;
            lbl_value.HorizontalContentAlignment = HorizontalAlignment.Left;
            lbl_value.VerticalContentAlignment = VerticalAlignment.Center;
            lbl_value.FontWeight = FontWeights.Bold;
        }

        public MonitorData(string type, string value, Func<bool> get, string typeStr)
        {
            getBit = get;
            lbl_type = new Label() { Content = type };
            lbl_value = new Label() { Content = value };
            lbl_type.Foreground = Brushes.White;
            lbl_value.Foreground = Brushes.White;
            lbl_type.HorizontalContentAlignment = HorizontalAlignment.Left;
            lbl_type.VerticalContentAlignment = VerticalAlignment.Center;
            lbl_value.HorizontalContentAlignment = HorizontalAlignment.Left;
            lbl_value.VerticalContentAlignment = VerticalAlignment.Center;
            lbl_value.FontWeight = FontWeights.Bold;
            this.typeStr = typeStr;
        }
        public MonitorData(string type, string word, string bit, string typeStr)
        {
            lbl_word = new Label();
            lbl_bit = new Label();
            lbl_type = new Label();
            lbl_value = new Label();
            lbl_word.Foreground = Brushes.White;
            lbl_bit.Foreground = Brushes.White;
            lbl_type.Foreground = Brushes.White;
            lbl_value.Foreground = Brushes.White;
            lbl_word.Content = word;
            lbl_word.HorizontalContentAlignment = HorizontalAlignment.Left;
            lbl_word.VerticalContentAlignment = VerticalAlignment.Center;
            lbl_bit.Content = bit;
            lbl_bit.HorizontalContentAlignment = HorizontalAlignment.Left;
            lbl_bit.VerticalContentAlignment = VerticalAlignment.Center;
            lbl_type.Content = type;
            lbl_type.HorizontalContentAlignment = HorizontalAlignment.Left;
            lbl_type.VerticalContentAlignment = VerticalAlignment.Center;
            lbl_value.HorizontalContentAlignment = HorizontalAlignment.Left;
            lbl_value.VerticalContentAlignment = VerticalAlignment.Center;
            lbl_value.FontWeight = FontWeights.Bold;
            lbl_value.Content = "";
            this.typeStr = typeStr;
        }

        public void setMonitorData(string[] dataStr)
        {
            lbl_word.Content = dataStr[0];
            lbl_bit.Content = dataStr[1];
            lbl_type.Content = dataStr[2];
            lbl_value.Content = dataStr[3];
        }

        public bool BitValue
        {
            get
            {
                Func<bool> getBit = this.getBit;
                return getBit != null && getBit();
            }
            set
            {
                Action<bool> setBit = this.setBit;
                if (setBit == null)
                    return;
                setBit(value);
            }
        }
    }
    public partial class PageMonitorJOB : Page
    {
        List<MonitorData> monitorList = new List<MonitorData>();

        // 최대 페이지 수 계산
        int plcPageCount;
        int wcsPageCount;
        int maxPageCount;

        int plcIndex = 0;
        int wcsIndex = 0;
        //Singletone
        singletonClass gClass;
        MainWindow pMain;

        //Test Timer
        Timer dataCheckTimer = new Timer();

        string[][] dataStrList = new string[200][];
        //List<string[]> dataStrList = new List<string[]>();
        // 더미 스트링 초기화
        string[] dataStr = new string[4] { "", "", "", "" };
        int mapListCnt = 0;
        int dispPageNo = 0;

        public PageMonitorJOB(MainWindow parent)
        {
            InitializeComponent();
            gClass = singletonClass.Instance;
            pMain = parent;

            // Test Edit
            //edit_Bit.Visibility = Visibility.Hidden;
            //edit_Word.Visibility = Visibility.Hidden;
            //edit_DWord.Visibility = Visibility.Hidden;

            // Event init
            Btn_Left.Click += Click_LeftRight;
            Btn_Right.Click += Click_LeftRight;

            // UI 리스트 인덱스 ----------- 실제 데이터 맵핑 주소, 타입, 값
            // WCS to GCP Mapping Data
            //-----------------------------------기본상태 응답 데이터 7500~7699-----------------------------------------------

            monitorList.Add(new MonitorData("SRM Job Request", "0", "0", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Job Exist", "0", "1", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Job Exist", "0", "2", "PLC"));
            monitorList.Add(new MonitorData("SRM Busy", "0", "3", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Goods Detect", "0", "4", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Goods Detect", "0", "5", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Job Complete", "0", "6", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Job Complete", "0", "7", "PLC"));

            monitorList.Add(new MonitorData("Fork2 Loading Priority", "100", "0", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Unloading Priority", "100", "1", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Job Number", "101", "W", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Move Command", "102", "0", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Storage Command", "102", "1", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Retrieval Command", "102", "2", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Rack to Rack", "102", "3", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Station to Station", "102", "4", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Dest Change Rack", "102", "5", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Dest Change Station", "102", "6", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Sticky Command", "102", "7", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Loading S/T No", "103", "W", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Loading Row", "104", "W", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Loading Bay", "105", "W", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Loading Level", "106", "W", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Unloading S/T No", "107", "W", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Unloading Row", "108", "W", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Unloading Bay", "109", "W", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Unloading Level", "110", "W", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Job Number", "111", "W", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Move Command", "112", "0", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Storage Command", "112", "1", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Retrieval Command", "112", "2", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Rack to Rack", "112", "3", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Station to Station", "112", "4", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Dest Change Rack", "112", "5", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Dest Change Station", "112", "6", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Sticky Command", "112", "7", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Loading S/T No", "113", "W", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Loading Row", "114", "W", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Loading Bay", "115", "W", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Loading Level", "116", "W", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Unloading S/T No", "117", "W", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Unloading Row", "118", "W", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Unloading Bay", "119", "W", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Unloading Level", "120", "W", "PLC"));
            monitorList.Add(new MonitorData("Heart Beat", "125", "0", "PLC"));
            monitorList.Add(new MonitorData("Returning to Home", "125", "1", "PLC"));
            monitorList.Add(new MonitorData("Alarm Reset Ack", "125", "2", "PLC"));
            monitorList.Add(new MonitorData("All Data Clear Ack", "125", "3", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Data Clear Ack", "125", "4", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Data Clear Ack", "125", "5", "PLC"));
            monitorList.Add(new MonitorData("SRM Data Report OK Status", "125", "8", "PLC"));
            monitorList.Add(new MonitorData("SRM Auto Ready Status", "125", "9", "PLC"));
            monitorList.Add(new MonitorData("SRM Auto Status", "125", "10", "PLC"));
            monitorList.Add(new MonitorData("SRM Manual Status", "125", "11", "PLC"));
            monitorList.Add(new MonitorData("SRM Semi Auto Status", "125", "12", "PLC"));
            monitorList.Add(new MonitorData("SRM Cycle Stop Ack", "125", "14", "PLC"));
            monitorList.Add(new MonitorData("SRM Emergency Stop Ack", "125", "15", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Manual Job Complete", "130", "0", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Manual Job Complete", "130", "1", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Manual Data Clear", "130", "4", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Manual Data Clear", "130", "5", "PLC"));


            // WCS
            monitorList.Add(new MonitorData("", "", "", "WCS"));  // 빈 항목 처리용 예시
            monitorList.Add(new MonitorData("", "", "", "WCS"));  // 빈 항목 처리용 예시
            monitorList.Add(new MonitorData("", "", "", "WCS"));  // 빈 항목 처리용 예시
            monitorList.Add(new MonitorData("", "", "", "WCS"));  // 빈 항목 처리용 예시
            monitorList.Add(new MonitorData("", "", "", "WCS"));  // 빈 항목 처리용 예시
            monitorList.Add(new MonitorData("", "", "", "WCS"));  // 빈 항목 처리용 예시
            monitorList.Add(new MonitorData("", "", "", "WCS"));  // 빈 항목 처리용 예시
            monitorList.Add(new MonitorData("", "", "", "WCS"));  // 빈 항목 처리용 예시

            monitorList.Add(new MonitorData("Fork2 Loading Priority", "0", "0", "WCS"));
            monitorList.Add(new MonitorData("Fork2 Unloading Priority", "0", "1", "WCS"));
            monitorList.Add(new MonitorData("Fork1 Job Number", "1", "W", "WCS"));

            monitorList.Add(new MonitorData("Fork1 Move Command", "2", "0", "WCS"));
            monitorList.Add(new MonitorData("Fork1 Storage Command", "2", "1", "WCS"));
            monitorList.Add(new MonitorData("Fork1 Retrieval Command", "2", "2", "WCS"));
            monitorList.Add(new MonitorData("Fork1 Rack to Rack", "2", "3", "WCS"));
            monitorList.Add(new MonitorData("Fork1 Station to Station", "2", "4", "WCS"));
            monitorList.Add(new MonitorData("Fork1 Dest Change Rack", "2", "5", "WCS"));
            monitorList.Add(new MonitorData("Fork1 Dest Change Station", "2", "6", "WCS"));
            monitorList.Add(new MonitorData("Fork1 Sticky Command", "2", "7", "WCS"));

            monitorList.Add(new MonitorData("Fork1 Loading S/T No", "3", "W", "WCS"));
            monitorList.Add(new MonitorData("Fork1 Loading Row", "4", "W", "WCS"));
            monitorList.Add(new MonitorData("Fork1 Loading Bay", "5", "W", "WCS"));
            monitorList.Add(new MonitorData("Fork1 Loading Level", "6", "W", "WCS"));
            monitorList.Add(new MonitorData("Fork1 Unloading S/T No", "7", "W", "WCS"));
            monitorList.Add(new MonitorData("Fork1 Unloading Row", "8", "W", "WCS"));
            monitorList.Add(new MonitorData("Fork1 Unloading Bay", "9", "W", "WCS"));
            monitorList.Add(new MonitorData("Fork1 Unloading Level", "10", "W", "WCS"));

            monitorList.Add(new MonitorData("Fork2 Job Number", "11", "W", "WCS"));

            monitorList.Add(new MonitorData("Fork2 Move Command", "12", "0", "WCS"));
            monitorList.Add(new MonitorData("Fork2 Storage Command", "12", "1", "WCS"));
            monitorList.Add(new MonitorData("Fork2 Retrieval Command", "12", "2", "WCS"));
            monitorList.Add(new MonitorData("Fork2 Rack to Rack", "12", "3", "WCS"));
            monitorList.Add(new MonitorData("Fork2 Station to Station", "12", "4", "WCS"));
            monitorList.Add(new MonitorData("Fork2 Dest Change Rack", "12", "5", "WCS"));
            monitorList.Add(new MonitorData("Fork2 Dest Change Station", "12", "6", "WCS"));
            monitorList.Add(new MonitorData("Fork2 Sticky Command", "12", "7", "WCS"));

            monitorList.Add(new MonitorData("Fork2 Loading S/T No", "13", "W", "WCS"));
            monitorList.Add(new MonitorData("Fork2 Loading Row", "14", "W", "WCS"));
            monitorList.Add(new MonitorData("Fork2 Loading Bay", "15", "W", "WCS"));
            monitorList.Add(new MonitorData("Fork2 Loading Level", "16", "W", "WCS"));
            monitorList.Add(new MonitorData("Fork2 Unloading S/T No", "17", "W", "WCS"));
            monitorList.Add(new MonitorData("Fork2 Unloading Row", "18", "W", "WCS"));
            monitorList.Add(new MonitorData("Fork2 Unloading Bay", "19", "W", "WCS"));
            monitorList.Add(new MonitorData("Fork2 Unloading Level", "20", "W", "WCS"));

            monitorList.Add(new MonitorData("Heart Beat", "25", "0", "WCS"));
            monitorList.Add(new MonitorData("Home Return", "25", "1", "WCS"));
            monitorList.Add(new MonitorData("Alarm Reset", "25", "2", "WCS"));
            monitorList.Add(new MonitorData("All Data Clear", "25", "3", "WCS"));
            monitorList.Add(new MonitorData("Fork1 Data Clear", "25", "4", "WCS"));
            monitorList.Add(new MonitorData("Fork2 Data Clear", "25", "5", "WCS"));
            monitorList.Add(new MonitorData("Time Sync Request", "25", "6", "WCS"));
            monitorList.Add(new MonitorData("SRM Data Report OK", "25", "8", "WCS"));
            monitorList.Add(new MonitorData("SRM Auto Request", "25", "10", "WCS"));
            monitorList.Add(new MonitorData("SRM Manual Request", "25", "11", "WCS"));
            monitorList.Add(new MonitorData("SRM Cycle Stop", "25", "14", "WCS"));
            monitorList.Add(new MonitorData("SRM Emergency Stop", "25", "15", "WCS"));


            // 타입별로 리스트 분리
            var plcList = monitorList.Where(x => x.typeStr == "PLC").ToList();
            var wcsList = monitorList.Where(x => x.typeStr == "WCS").ToList();

            int rowCount = dataLeft.RowDefinitions.Count;
            // 최대 페이지 수 계산
            plcPageCount = (plcList.Count + rowCount - 1) / rowCount;
            wcsPageCount = (wcsList.Count + rowCount - 1) / rowCount;
            maxPageCount = Math.Max(plcPageCount, wcsPageCount);

            // 초기 페이지 init
            display_Change(dispPageNo);
            lbl_PageInfo.Content = $"{dispPageNo+1} / {maxPageCount}"; // 250528 페이지 표시

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
                    ushort compValue;
                    ushort compValue2;
                    for (int i = 0; i < monitorList.Count; i++)
                    {

                        // combinedArray[i] = (int)shortArray1[i] << 16 | shortArray2[i];
                        if (int.TryParse(monitorList[i].lbl_word.Content.ToString(), out tmpWord))          // 유효한 Word일 경우
                        {
                            if (monitorList[i].typeStr == "PLC")
                            {
                                compValue = gClass.str.WcsPacket[gClass.srmNum].WCSTO[tmpWord];
                                compValue2 = gClass.str.WcsPacket[gClass.srmNum].WCSTO[tmpWord+1];
                            }
                            else
                            {
                                compValue = gClass.str.WcsPacket[gClass.srmNum].WCSFROM[tmpWord];
                                compValue2 = gClass.str.WcsPacket[gClass.srmNum].WCSFROM[tmpWord+1];
                            }

                            if (int.TryParse(monitorList[i].lbl_bit.Content.ToString(), out tmpBit))         // 유효한 Bit일 경우
                            {
                                tmpValue = (compValue >> tmpBit) & 0x01;       // 설정 비트 시프트 후 값 표시       //250508 WCSFROMBUF->WCSFROM
                            }
                            else                                                                            // 유효한 Bit가 아닐경우 Word 데이터 자체 값을 표시
                            {
                                if (monitorList[i].lbl_bit.Content.ToString() == "DW")
                                {
                                    tmpValue = (int)compValue << 16 | compValue2;
                                    //tmpValue = (int)compValue | compValue2 << 16;
                                }
                                else
                                {
                                    tmpValue = compValue;
                                }
                            }

                            int.TryParse(monitorList[i].lbl_value.Content.ToString(), out curValue);
                            if (curValue != tmpValue)
                            {
                                monitorList[i].lbl_value.Foreground = Brushes.DarkTurquoise;
                                monitorList[i].lbl_value.FontWeight = FontWeights.Bold;
                                monitorList[i].lbl_value.FontSize = 13;
                                // 250509 어떤 값이 이전 값에서 다음 값으로 바뀌었는지 확인
                            }
                            else
                            {
                                monitorList[i].lbl_value.Foreground = Brushes.White;
                                monitorList[i].lbl_value.FontWeight = FontWeights.Regular;
                                monitorList[i].lbl_value.FontSize = 10;
                            }
                            monitorList[i].lbl_value.Content = tmpValue;
                        }
                        else
                        {
                            monitorList[i].lbl_type.Content = "-";
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
                    cIniAccess.SaveExLog(0, "EXCEPTION - PageMonitorJOBTimer : " + ex.Message);
                }
            });
        }

        private void Click_LeftRight(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;

            if (btn == Btn_Left)
            {
                dispPageNo -= 1;
                if (dispPageNo < 0) dispPageNo = maxPageCount - 1;
            }
            else if (btn == Btn_Right)
            {
                dispPageNo += 1;
                if (dispPageNo >= maxPageCount) dispPageNo = 0;
            }

            display_Change(dispPageNo);
            lbl_PageInfo.Content = $"{dispPageNo+1} / {maxPageCount}"; // 250528 페이지 표시
        }

        private void display_Change(int pageNo)
        {
            Dispatcher.Invoke(() =>
            {
                int rowCount = dataLeft.RowDefinitions.Count;
                var plcItems = monitorList.Where(x => x.typeStr == "PLC").Skip(pageNo * rowCount).ToList();
                var wcsItems = monitorList.Where(x => x.typeStr == "WCS").Skip(pageNo * rowCount).ToList();


                dataLeft.Children.Clear();
                dataRight.Children.Clear();


                //return;
                // SRM IO Data Init
                // Left Area
                for (int i = 0; i < rowCount; i++)
                {
                    if (dataLeft.ColumnDefinitions.Count < 4)
                    {
                        VarMessageBox.Show("CheckIndex", "SRM IO Column Count Error", VarMessageBoxButton.OK);
                        break;
                    }
                    if (plcItems.Count > i)
                    {
                        Grid.SetRow(plcItems[i].lbl_word, i);
                        Grid.SetColumn(plcItems[i].lbl_word, 0);
                        Grid.SetRow(plcItems[i].lbl_bit, i);
                        Grid.SetColumn(plcItems[i].lbl_bit, 1);
                        Grid.SetRow(plcItems[i].lbl_type, i);
                        Grid.SetColumn(plcItems[i].lbl_type, 2);
                        Grid.SetRow(plcItems[i].lbl_value, i);
                        Grid.SetColumn(plcItems[i].lbl_value, 3);
                        SafeAddToGrid(plcItems[i].lbl_word, dataLeft);
                        SafeAddToGrid(plcItems[i].lbl_bit, dataLeft);
                        SafeAddToGrid(plcItems[i].lbl_type, dataLeft);
                        SafeAddToGrid(plcItems[i].lbl_value, dataLeft);
                    }
                }

                // Right Area
                for (int i = 0; i < dataRight.RowDefinitions.Count; i++)
                {
                    if (dataRight.ColumnDefinitions.Count < 4)
                    {
                        VarMessageBox.Show("CheckIndex", "SRM IO Column Count Error", VarMessageBoxButton.OK);
                        break;
                    }
                    if (wcsItems.Count > i)
                    {
                        Grid.SetRow(wcsItems[i].lbl_word, i);
                        Grid.SetColumn(wcsItems[i].lbl_word, 0);
                        Grid.SetRow(wcsItems[i].lbl_bit, i);
                        Grid.SetColumn(wcsItems[i].lbl_bit, 1);
                        Grid.SetRow(wcsItems[i].lbl_type, i);
                        Grid.SetColumn(wcsItems[i].lbl_type, 2);
                        Grid.SetRow(wcsItems[i].lbl_value, i);
                        Grid.SetColumn(wcsItems[i].lbl_value, 3);
                        SafeAddToGrid(wcsItems[i].lbl_word, dataRight);
                        SafeAddToGrid(wcsItems[i].lbl_bit, dataRight);
                        SafeAddToGrid(wcsItems[i].lbl_type, dataRight);
                        SafeAddToGrid(wcsItems[i].lbl_value, dataRight);
                    }
                }
            });
        }

        private void Click_OpenNumpad(object sender, MouseButtonEventArgs e)
        {
            TextBox edit = sender as TextBox;
            edit.PointToScreen(new Point(0, 0));

            pMain.tmpNumPad.AttachTo(edit, Window.GetWindow(this), pMain.PointToScreen(new Point(0, 0)), pMain.ActualWidth, pMain.ActualHeight);
            Console.WriteLine("Btn_FromTo_Select : " + edit.Name);
        }

        void SafeAddToGrid(UIElement element, Grid grid)
        {
            if (element == null) return;

            var parent = VisualTreeHelper.GetParent(element) as Panel;
            if (parent != null)
            {
                parent.Children.Remove(element);
            }

            grid.Children.Add(element);
        }
    }
}
