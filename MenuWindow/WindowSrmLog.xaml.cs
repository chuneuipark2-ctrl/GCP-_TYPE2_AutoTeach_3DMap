using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.IO;
using System.Data;

namespace gcp_Wpf.MenuWindow
{
    /// <summary>
    /// WindowSrmLog.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class WindowSrmLog : Window
    {
        private HwndSource hwndSource;

        ObservableCollection<String> logStrList = new ObservableCollection<String>();
        Timer myTimer = new Timer();
        static System.Threading.Mutex mutex = new System.Threading.Mutex();
        int srmNum;
        string pathString;

        bool visible = false;

        //Singletone
        singletonClass gClass;
        public WindowSrmLog()
        {
            gClass = singletonClass.Instance;
            InitializeComponent();
        }

        public WindowSrmLog(int type, int srmNum)               // 클래스 공유 사용  - type = 1,2,3   1= HOST-GCP, 2= SRM-GCP, 3= DIO-GCP
        {
            gClass = singletonClass.Instance;
            Console.WriteLine("DebugTest SRM LOG - type: " + type + "srmNum: " + srmNum);
            InitializeComponent();
            this.srmNum = srmNum;
            string strTitle = "";
            switch (type)
            {
                case 1:
                    pathString = System.IO.Path.Combine(Environment.CurrentDirectory, "SRM" + srmNum, cConstDefine.PATH_LOG, cConstDefine.PATH_HOSTLOG);
                    strTitle = "WindowHostLog";
					Btn_PollingStop.Visibility = Visibility.Collapsed;
                    break;
                case 2:
                    pathString = System.IO.Path.Combine(Environment.CurrentDirectory, "SRM" + srmNum, cConstDefine.PATH_LOG, cConstDefine.PATH_SRMLOG);
                    strTitle = "WindowSrmLog";
                    break;
                case 3:
                    pathString = System.IO.Path.Combine(Environment.CurrentDirectory, "SRM" + srmNum, cConstDefine.PATH_LOG, cConstDefine.PATH_DIOLOG);
                    strTitle = "WindowDioLog";
					Btn_PollingStop.Visibility = Visibility.Collapsed;
                    break;
                case 4:
                    visible = true;
                    pathString = System.IO.Path.Combine(Environment.CurrentDirectory, "SRM" + srmNum, cConstDefine.PATH_LOG, cConstDefine.PATH_JOBLOG);
                    strTitle = "WindowJobLog";
					Btn_PollingStop.Visibility = Visibility.Collapsed;
                    break;
                case 99:
                    pathString = System.IO.Path.Combine(Environment.CurrentDirectory, "SRM" + srmNum, cConstDefine.PATH_LOG, cConstDefine.PATH_OPLOG);
                    strTitle = "WindowOpLog";
					Btn_PollingStop.Visibility = Visibility.Collapsed;
                    break;
            }
            this.Title = strTitle + srmNum;

            
            //gClass.str.test1 = 10;
            //gClass.Name = "test";

            //Console.WriteLine("Get SingleTone Class with WindowSrmLog = " + gClass.test);

            //---------------------Invisible------------------------------
            WindowState initialWindowState = WindowState;

            // making window invisible
            ShowInTaskbar = false;
            WindowState = WindowState.Minimized;

            // showing and hiding window
            Visibility = Visibility.Visible;
            Visibility = Visibility.Hidden;

            WindowState = initialWindowState;
            //------------------------------------------------------------


            myTimer.Interval = 1000; // 1 second
            myTimer.AutoReset = true; // Repeat the timer
            myTimer.Elapsed += LogTimer_Elapsed;
            //myTimer.Start();
            //this.Hide();

            List_SrmLog.ItemsSource = logStrList;
            //logStrList.CollectionChanged += Log_CollectionChanged;

            //Console.WriteLine("Log SRM Init");
        }

        ~WindowSrmLog() {
            Console.WriteLine("WindowSrmLog Delete");
            Finished();
        }

        public void Finished()
        {
            myTimer.Stop();
            this.Close ();
        }

        private void Log_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null && e.NewItems.Count > 0)
            {
                var lastItem = e.NewItems[e.NewItems.Count - 1];
                //List_SrmLog.ScrollIntoView(lastItem);
                //List_SrmLog.EnsureVisible(listView.Items.Count - 1);
                //List_SrmLog.TabIndex = e.NewItems.Count - 1;
                //List_SrmLog.ScrollIntoView(List_SrmLog.Items[List_SrmLog.Items.Count - 1]);
                Console.WriteLine("ListBox Item Count = " + lastItem);
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            myTimer.Stop();
            e.Cancel = true; // Cancel the close
            this.Hide(); // Hide the window
        }

        private void Window_FormShow(object sender, EventArgs e)
        {
            //Console.WriteLine("Window_FormShow SrmLog");
            hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            hwndSource.AddHook(WndProc);
            
            // 창이 표시될 때 스크롤을 맨 아래로 이동
            ScrollToBottom();
        }
        
        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // 창이 표시될 때 (IsVisible이 true가 될 때) 스크롤을 맨 아래로 이동
            if ((bool)e.NewValue == true)
            {
                ScrollToBottom();
            }
        }
        
        private void ScrollToBottom()
        {
            // ListBox의 ScrollViewer를 찾아서 맨 아래로 스크롤
            if (List_SrmLog.Items.Count > 0)
            {
                // Dispatcher를 사용하여 UI 렌더링 후 스크롤
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // ScrollViewer 찾기 (재귀적으로 검색)
                        ScrollViewer scrollViewer = FindVisualChild<ScrollViewer>(List_SrmLog);
                        if (scrollViewer != null)
                        {
                            scrollViewer.ScrollToEnd();
                        }
                        else
                        {
                            // ScrollViewer를 찾지 못한 경우 ScrollIntoView 사용
                            List_SrmLog.ScrollIntoView(List_SrmLog.Items[List_SrmLog.Items.Count - 1]);
                        }
                    }
                    catch (Exception ex)
                    {
                        // 예외 발생 시 ScrollIntoView 사용
                        try
                        {
                            List_SrmLog.ScrollIntoView(List_SrmLog.Items[List_SrmLog.Items.Count - 1]);
                        }
                        catch { }
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }
        
        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T)
                {
                    return (T)child;
                }
                
                T childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }

        private void LogTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
        }

        private void Btn_AlwaysTop_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton toggleButton = sender as ToggleButton;

            if (toggleButton.IsChecked == true)
            {
                Topmost = true;
            }
            else
            {
                Topmost = false;
            }
        }

        private void Btn_Stop_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton toggleButton = sender as ToggleButton;

            if (toggleButton.IsChecked == true)
            {
            }
            else
            {
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == cConstDefine.WM_USER)
            {
                // Handle the message here
                var data = (cConstDefine.COPYDATASTRUCT)Marshal.PtrToStructure(lParam, typeof(cConstDefine.COPYDATASTRUCT));            // 단순하게 바꾸자
                string message = data.lpData;

                try
                {
                    // Handle the message here
                    if (this.IsVisible || visible)
                    {
                        if (Btn_Stop.IsChecked == false)
                        {
                            Dispatcher.Invoke(() => {
                                //Console.WriteLine("SendMessage Called On Visible " + cConstDefine.WM_USER);
                                logStrList.Add(message);
                                //Dispatcher.Invoke(() => logStrList.Add(DateTime.Now.ToString("HH:mm:ss:FFF ") + message));
                                if (logStrList.Count > 255)
                                {
                                    logStrList.RemoveAt(0);
                                }

                                if(logStrList.Count > 0)        // 자꾸 Exception 걸림, ObservableCollection 문제인듯
                                {
                                    List_SrmLog.ScrollIntoView(List_SrmLog.Items[logStrList.Count - 1]);
                                }
                                //Console.WriteLine(logStrList.Count +" - "+ List_SrmLog.Items.Count) ;
                                //List_SrmLog.ScrollIntoView(List_SrmLog.Items[List_SrmLog.Items.Count - 1]);
                            });
                        }
                    }
                }
                catch(Exception ex)
                {
                    cIniAccess.SaveExLog(0, "EXCEPTION - WindowSrmLog "+ "List_SrmLog.Items.Count" + this.Title + " " + ex.Message);
                }

                //SaveLogFile(DateTime.Now.ToString("HH:mm:ss ") + message);

                handled = true;
                return IntPtr.Zero;
            }
            else
            {
                //Console.WriteLine("ANOTHER Called");
                return IntPtr.Zero;
            }
        }

		private void Btn_PollingStop_Click(object sender, RoutedEventArgs e)
		  {
		    if ((sender as ToggleButton).IsChecked.GetValueOrDefault())
		      this.gClass.str.SrmInfo[srmNum - 1].pollingStop = true;
		    else
		      this.gClass.str.SrmInfo[srmNum - 1].pollingStop = false;
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


                string filePath = System.IO.Path.Combine(pathString,"SRMLOG_" + srmNum + "_" + DateTime.Now.ToString("yyyyMMdd") + ".log");

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
    }
}
