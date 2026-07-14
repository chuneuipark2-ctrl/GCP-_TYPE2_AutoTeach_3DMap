using System;
using System.Collections.Generic;
using System.Data;
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
    /// PageMonitorFromWCS.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PageMonitorFromWCS : Page
    {
        private List<MonitorData> monitorList = new List<MonitorData>();
        private int plcPageCount;
        private int wcsPageCount;
        private int maxPageCount;
        private singletonClass gClass;

        //Test Timer
        Timer dataCheckTimer = new Timer();

        private string[][] dataStrList = new string[100][];
        // 더미 스트링 초기화
        string[] dataStr = new string[4] { "", "", "", "" };
        int mapListCnt = 0;
        int dispPageNo = 0;
        int currentPage = 1;
        const int pageCount = 100;
        public PageMonitorFromWCS()
        {
            InitializeComponent();
            gClass = singletonClass.Instance;

            // Event init
            Btn_Left.Click += new RoutedEventHandler(Click_LeftRight);
            Btn_Right.Click += new RoutedEventHandler(Click_LeftRight);
            monitorList.Add(new MonitorData("Time Sync - Year", "131", "W", "PLC"));
            monitorList.Add(new MonitorData("Time Sync - Month", "132", "W", "PLC"));
            monitorList.Add(new MonitorData("Time Sync - Day", "133", "W", "PLC"));
            monitorList.Add(new MonitorData("Time Sync - Hour", "134", "W", "PLC"));
            monitorList.Add(new MonitorData("Time Sync - Minute", "135", "W", "PLC"));
            monitorList.Add(new MonitorData("Time Sync - Second", "136", "W", "PLC"));
            monitorList.Add(new MonitorData("Time Sync - Day of Week", "137", "W", "PLC"));
            monitorList.Add(new MonitorData("SRM Alarm", "0", "10", "PLC"));
            monitorList.Add(new MonitorData("SRM Recoverable Alarm", "0", "11", "PLC"));
            monitorList.Add(new MonitorData("SRM Home Position", "0", "12", "PLC"));
            monitorList.Add(new MonitorData("Main Alarm Code", "1", "W", "PLC"));
            monitorList.Add(new MonitorData("Sub Alarm Code", "2", "W", "PLC"));
            monitorList.Add(new MonitorData("SRM Station Current Location", "3", "W", "PLC"));
            monitorList.Add(new MonitorData("SRM Row Current Location", "4", "W", "PLC"));
            monitorList.Add(new MonitorData("SRM Bay Current Location", "5", "W", "PLC"));
            monitorList.Add(new MonitorData("SRM Level Current Location", "6", "W", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Left In-Position", "8", "0", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Left Moving", "8", "1", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Center In-Position", "8", "2", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Center Moving", "8", "3", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Right In-Position", "8", "4", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Right Moving", "8", "5", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Left In-Position", "8", "8", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Left Moving", "8", "9", "PLC"));


            monitorList.Add(new MonitorData("Fork2 Center In-Position", "8", "10", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Center Moving", "8", "11", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Right In-Position", "8", "12", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Right Moving", "8", "13", "PLC"));
            monitorList.Add(new MonitorData("Travel Motor Busy", "9", "0", "PLC"));
            monitorList.Add(new MonitorData("Lift Motor Busy", "9", "1", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Motor Busy", "9", "2", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Motor Busy", "9", "3", "PLC"));
            monitorList.Add(new MonitorData("Response Code", "10", "W", "PLC"));
            monitorList.Add(new MonitorData("Travel Home Position", "15", "0", "PLC"));
            monitorList.Add(new MonitorData("Lift Home Position", "15", "1", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Home Position", "15", "2", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Home Position", "15", "3", "PLC"));
            monitorList.Add(new MonitorData("Travel Dest In-Position", "15", "4", "PLC"));
            monitorList.Add(new MonitorData("Lift Dest In-Position", "15", "5", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Dest In-Position", "15", "6", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Dest In-Position", "15", "7", "PLC"));
            monitorList.Add(new MonitorData("Current Travel Position(mm)", "20", "DW", "PLC"));
            monitorList.Add(new MonitorData("Current Lift Position(mm)", "22", "DW", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Current Position(mm)", "24", "DW", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Current Position(mm)", "26", "DW", "PLC"));
            monitorList.Add(new MonitorData("Current Travel Destination(mm)", "30", "DW", "PLC"));
            monitorList.Add(new MonitorData("Current Lift Destination(mm)", "32", "DW", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Destination(mm)", "34", "DW", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Destination(mm)", "36", "DW", "PLC"));
            monitorList.Add(new MonitorData("Travel Current Speed(m/min)", "40", "DW", "PLC"));
            monitorList.Add(new MonitorData("Lift Current Speed(m/min)", "42", "DW", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Current Speed(m/min)", "44", "DW", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Current Speed(m/min)", "46", "DW", "PLC"));
            monitorList.Add(new MonitorData("Travel Setting Speed (m/min)", "200", "DW", "PLC"));
            monitorList.Add(new MonitorData("Lift Setting Speed (m/min)", "202", "DW", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Setting Speed (m/min)", "204", "DW", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Setting Speed (m/min)", "206", "DW", "PLC"));
            monitorList.Add(new MonitorData("Travel Setting Acceleration (mm/sec^2)", "210", "DW", "PLC"));
            monitorList.Add(new MonitorData("Lift Setting Acceleration (mm/sec^2)", "212", "DW", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Setting Acceleration (mm/sec^2)", "214", "DW", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Setting Acceleration (mm/sec^2)", "216", "DW", "PLC"));
            monitorList.Add(new MonitorData("Travel Setting Deceleration (mm/sec^2)", "220", "DW", "PLC"));
            monitorList.Add(new MonitorData("Lift Setting Deceleration (mm/sec^2)", "222", "DW", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Setting Deceleration (mm/sec^2)", "224", "DW", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Setting Deceleration (mm/sec^2)", "226", "DW", "PLC"));
            monitorList.Add(new MonitorData("Travel Setting Jerk (mm/sec^3)", "230", "DW", "PLC"));
            monitorList.Add(new MonitorData("Lift Setting Jerk (mm/sec^3)", "232", "DW", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Setting Jerk (mm/sec^3)", "234", "DW", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Setting Jerk (mm/sec^3)", "236", "DW", "PLC"));
            monitorList.Add(new MonitorData("Load Travel, Lift Moving Before Delay (ms)", "240", "W", "PLC"));
            monitorList.Add(new MonitorData("Load Travel, Lift Moving After Delay (ms)", "241", "W", "PLC"));
            monitorList.Add(new MonitorData("Load Fork Extend Moving Before Delay (ms)", "242", "W", "PLC"));
            monitorList.Add(new MonitorData("Load Fork Extend Moving After Delay (ms)", "243", "W", "PLC"));
            monitorList.Add(new MonitorData("Load Forking Lift Moving Before Delay (ms)", "244", "W", "PLC"));
            monitorList.Add(new MonitorData("Load Forking Lift Moving After Delay (ms)", "245", "W", "PLC"));
            monitorList.Add(new MonitorData("Load Fork Fold Moving Before Delay (ms)", "246", "W", "PLC"));
            monitorList.Add(new MonitorData("Load Fork Fold Moving After Delay (ms)", "247", "W", "PLC"));
            monitorList.Add(new MonitorData("Unload Travel, Lift Moving Before Delay (ms)", "250", "W", "PLC"));
            monitorList.Add(new MonitorData("Unload Travel, Lift Moving After Delay (ms)", "251", "W", "PLC"));
            monitorList.Add(new MonitorData("Unload Fork Extend Moving Before Delay (ms)", "252", "W", "PLC"));
            monitorList.Add(new MonitorData("Unload Fork Extend Moving After Delay (ms)", "253", "W", "PLC"));
            monitorList.Add(new MonitorData("Unload Forking Lift Moving Before Delay (ms)", "254", "W", "PLC"));
            monitorList.Add(new MonitorData("Unload Forking Lift Moving After Delay (ms)", "255", "W", "PLC"));
            monitorList.Add(new MonitorData("Unload Fork Fold Moving Before Delay (ms)", "256", "W", "PLC"));
            monitorList.Add(new MonitorData("Unload Fork Fold Moving After Delay (ms)", "257", "W", "PLC"));
            monitorList.Add(new MonitorData("PLC Operation Time (Sec)", "280", "DW", "PLC"));
            monitorList.Add(new MonitorData("Travel Operation Time (Sec)", "282", "DW", "PLC"));
            monitorList.Add(new MonitorData("Lift Operation Time (Sec)", "284", "DW", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Operation Time (Sec)", "286", "DW", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Operation Time (Sec)", "288", "DW", "PLC"));
            monitorList.Add(new MonitorData("Travel Brake Open Count", "290", "DW", "PLC"));
            monitorList.Add(new MonitorData("Lift Brake Open Count", "292", "DW", "PLC"));
            monitorList.Add(new MonitorData("Fork1 Brake Open Count", "294", "DW", "PLC"));
            monitorList.Add(new MonitorData("Fork2 Brake Open Count", "296", "DW", "PLC"));
            monitorList.Add(new MonitorData("Time Sync Year", "31", "W", "WCS"));
            monitorList.Add(new MonitorData("Time Sync Month", "32", "W", "WCS"));
            monitorList.Add(new MonitorData("Time Sync Day", "33", "W", "WCS"));
            monitorList.Add(new MonitorData("Time Sync Hour", "34", "W", "WCS"));
            monitorList.Add(new MonitorData("Time Sync Minute", "35", "W", "WCS"));
            monitorList.Add(new MonitorData("Time Sync Second", "36", "W", "WCS"));
            monitorList.Add(new MonitorData("Time Sync Day of the Week", "37", "W", "WCS"));

            // 타입별로 리스트 분리
            var plcList = monitorList.Where(x => x.typeStr == "PLC").ToList();
            var wcsList = monitorList.Where(x => x.typeStr == "WCS").ToList();

            // Left Table Data Init
            int count = dataLeft.RowDefinitions.Count;
            //MessageBox.Show("맵핑 리스트 카운트 오류", "변경확인", MessageBoxButton.OK, MessageBoxImage.Information);

            // Right Table Data Init
            plcPageCount = (plcList.Count + count - 1) / count;
            wcsPageCount = (wcsList.Count + count - 1) / count;
            maxPageCount = Math.Max(plcPageCount, wcsPageCount);
            //MessageBox.Show("맵핑 리스트 카운트 오류", "변경확인", MessageBoxButton.OK, MessageBoxImage.Information);
            display_Change(dispPageNo);
            //lbl_PageInfo.Content = $"{dispPageNo + 1} / {maxPageCount}";


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
                    for (int srmCnt = 0; srmCnt < gClass.str.GcpInfo.srmCount; srmCnt++)
                    {
                        if (srmCnt == gClass.srmNum)
                        {
                            int tmpWord = 0;
                            int tmpBit = 0;
                            int tmpValue = 0;
                            int curValue = 0;
                            ushort compValue;
                            ushort compValue2;
                            for (int i = 0; i < monitorList.Count; i++)
                            {
                                if (int.TryParse(monitorList[i].lbl_word.Content.ToString(), out tmpWord))          // 유효한 Word일 경우
                                {
                                    if (monitorList[i].typeStr == "PLC")
                                    {
                                        compValue = gClass.str.WcsPacket[gClass.srmNum].WCSTO[tmpWord];
                                        compValue2 = gClass.str.WcsPacket[gClass.srmNum].WCSTO[tmpWord + 1];
                                    }
                                    else                                                                            // 유효한 Bit가 아닐경우 Word 데이터 자체 값을 표시
                                    {
                                        compValue = gClass.str.WcsPacket[gClass.srmNum].WCSFROM[tmpWord];
                                        compValue2 = gClass.str.WcsPacket[gClass.srmNum].WCSFROM[tmpWord + 1];
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
                                    monitorList[i].lbl_word.Content = "Word Exception";
                                }
                            }
                        }
                    }


                }
                catch (Exception ex)
                {
                    cIniAccess.SaveExLog(0, "EXCEPTION - PageMonitorTimer : " + ex.Message);
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
            //lbl_PageInfo.Content = $"{dispPageNo + 1} / {maxPageCount}"; // 250528 페이지 표시
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
