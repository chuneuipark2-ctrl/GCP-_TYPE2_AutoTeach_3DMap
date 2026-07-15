using System;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using gcp_Wpf.Controls;

namespace gcp_Wpf
{
    /// <summary>
    /// 모니터 메뉴 — 3D MAP 전용 페이지 (Frame 전체).
    /// I/O LIST(PageDIO)와 분리. WebView2 는 진입 시 lazy init.
    /// </summary>
    public partial class Page3DMap : Page
    {
        private readonly singletonClass gClass = singletonClass.Instance;
        private Dio3DViewer _dio3dViewer;
        private bool _dio3dViewerLoading;
        private string _currentAssyId3d;
        private readonly Timer _ioTimer = new Timer(500);

        public Page3DMap()
        {
            InitializeComponent();
            _ioTimer.AutoReset = true;
            _ioTimer.Elapsed += IoTimer_Elapsed;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await EnsureDio3DViewerAsync();
            if (!_ioTimer.Enabled)
                _ioTimer.Start();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _ioTimer.Stop();
        }

        private async System.Threading.Tasks.Task EnsureDio3DViewerAsync()
        {
            if (_dio3dViewer != null)
            {
                if (!_dio3dViewer.IsInitialized)
                    await _dio3dViewer.InitializeViewerAsync();
                return;
            }
            if (_dio3dViewerLoading) return;

            _dio3dViewerLoading = true;
            try
            {
                _dio3dViewer = new Dio3DViewer();
                _dio3dViewer.AssyEntered += Dio3dViewer_AssyEntered;
                _dio3dViewer.AssyExited += Dio3dViewer_AssyExited;
                grid3dHost.Children.Add(_dio3dViewer);
                await _dio3dViewer.InitializeViewerAsync();
            }
            catch (Exception ex)
            {
                cIniAccess.SaveExLog(gClass.srmNum, "Page3DMap viewer load: " + ex.Message);
                lbl_Title.Content = "3D MAP — 로드 실패";
            }
            finally
            {
                _dio3dViewerLoading = false;
            }
        }

        private void Dio3dViewer_AssyEntered(object sender, string assyId)
        {
            _currentAssyId3d = assyId;
            Push3DViewerIoUpdates();
        }

        private void Dio3dViewer_AssyExited(object sender, EventArgs e)
        {
            _currentAssyId3d = null;
        }

        private void Push3DViewerIoUpdates()
        {
            if (_dio3dViewer == null || string.IsNullOrEmpty(_currentAssyId3d))
                return;
            try
            {
                var states = _3DMap.Build(_currentAssyId3d, ref gClass.str.SRMIO[gClass.srmNum]);
                if (states != null && states.Count > 0)
                    _dio3dViewer.PostUpdateIo(_currentAssyId3d, states);
            }
            catch (Exception ex)
            {
                cIniAccess.SaveExLog(gClass.srmNum, "Page3DMap PushIo: " + ex.Message);
            }
        }

        private void IoTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(Push3DViewerIoUpdates));
            }
            catch
            {
                /* dispose race */
            }
        }
    }
}
