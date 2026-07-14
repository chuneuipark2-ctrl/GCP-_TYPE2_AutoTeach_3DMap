using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using gcp_Wpf.Services;
using Microsoft.Web.WebView2.Core;

namespace gcp_Wpf.Controls
{
  public partial class Dio3DViewer : UserControl
  {
    public event EventHandler<string> AssyEntered;
    public event EventHandler AssyExited;
    public event EventHandler<string> MarkerClicked;

    private bool _initialized;
    private bool _initInProgress;
    private string _currentAssyId;
    private string _loadedPdfPath;
    private readonly singletonClass _gClass = singletonClass.Instance;
    private readonly List<Button> _assyButtons = new List<Button>();

    public string CurrentAssyId => _currentAssyId;
    public bool IsInitialized => _initialized;

    public Dio3DViewer()
    {
      InitializeComponent();
    }

    public async Task InitializeViewerAsync()
    {
      if (_initialized || _initInProgress) return;
      _initInProgress = true;

      try
      {
        int srmNum = _gClass.srmNum;
        await Task.Run(() =>
        {
          Dio3DDrawingPath.EnsureFolderExists(srmNum);
          SeedConfigIfNeeded(srmNum);
        }).ConfigureAwait(true);

        await ValidateAllAssyPrebuiltGlbAsync(srmNum).ConfigureAwait(true);

        string drawingFolder = Dio3DDrawingPath.Get3DDrawingFolder(srmNum);
        string viewerFolder = Dio3DDrawingPath.GetViewerFolder();
        _loadedPdfPath = Dio3DDrawingPath.ResolvePdfFilePath(srmNum);

        BuildAssyButtons(srmNum);

        TxtPlaceholder.Visibility = Visibility.Collapsed;
        Panel3dPdfNotice.Visibility = Visibility.Collapsed;
        WebView.Visibility = Visibility.Visible;
        TxtBreadcrumb.Text = "3D MAP";
        TxtStatus.Text = "3D 뷰어 시작 중...";
        BtnOpenExternal.Visibility = !string.IsNullOrEmpty(_loadedPdfPath) ? Visibility.Visible : Visibility.Collapsed;
        if (BtnOpenExternal.Visibility == Visibility.Visible)
        {
          BtnOpenExternal.Content = "PDF (Acrobat)";
        }

        await WebView.EnsureCoreWebView2Async(null).ConfigureAwait(true);
        WebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        WebView.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
        WebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
        WebView.CoreWebView2.NavigationCompleted -= CoreWebView2_NavigationCompleted;
        WebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;

        if (!Directory.Exists(viewerFolder))
        {
          throw new DirectoryNotFoundException("Viewer3D 폴더 없음: " + viewerFolder);
        }
        if (!File.Exists(Path.Combine(viewerFolder, "viewer.html")))
        {
          throw new FileNotFoundException("viewer.html 없음: " + viewerFolder);
        }

        string overviewPath = Dio3DDrawingPath.ResolveOverviewModelPath(srmNum);
        if (!string.IsNullOrEmpty(overviewPath) && File.Exists(overviewPath))
        {
          long mb = new FileInfo(overviewPath).Length / (1024 * 1024);
          string ext = Path.GetExtension(overviewPath).ToUpperInvariant().TrimStart('.');
          TxtStatus.Text = $"{ext} {mb}MB — 로딩 중...  ({drawingFolder})";
        }
        else
        {
          TxtStatus.Text = $"3D 모델 없음 (GLB/STP) — ASSY 박스 표시  ({drawingFolder})";
        }

        WebView.CoreWebView2.ProcessFailed -= CoreWebView2_ProcessFailed;
        WebView.CoreWebView2.ProcessFailed += CoreWebView2_ProcessFailed;

        WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "app.local", viewerFolder, CoreWebView2HostResourceAccessKind.Allow);
        WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "drawing.local", drawingFolder, CoreWebView2HostResourceAccessKind.Allow);

        string cacheBust = GetViewerCacheBust(viewerFolder);

#if DEBUG
        try
        {
          await WebView.CoreWebView2.Profile.ClearBrowsingDataAsync(
              CoreWebView2BrowsingDataKinds.DiskCache).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
          Console.WriteLine("WebView cache clear (Debug): " + ex.Message);
        }
#endif

        WebView.CoreWebView2.Navigate($"https://app.local/viewer.html?v={cacheBust}");
        Console.WriteLine($"[3D] viewer cache bust v={cacheBust} ({viewerFolder})");
        _initialized = true;
      }
      catch (Exception ex)
      {
        TxtStatus.Text = "3D MAP 로드 실패: " + ex.Message;
        TxtPlaceholder.Visibility = Visibility.Visible;
        TxtPlaceholder.Text = "3D 뷰어 초기화 실패\n" + ex.Message;
        Console.WriteLine("Dio3DViewer init failed: " + ex.Message);
      }
      finally
      {
        _initInProgress = false;
      }
    }

    private string BuildStatusLine()
    {
      string pdf = string.IsNullOrEmpty(_loadedPdfPath)
          ? "PDF 없음"
          : Path.GetFileName(_loadedPdfPath) + " (Acrobat 버튼)";
      return "3D 뷰어 (GLB/STP)  |  " + pdf + "  |  ASSY 버튼 = I/O 필터";
    }

    /// <summary>viewer.js 변경 시 WebView2 캐시 무효화용</summary>
    private static string GetViewerCacheBust(string viewerFolder)
    {
      string[] watch =
      {
        "viewer.html", "viewer.js", "modelResolver.js",
      };
      long maxTicks = 0;
      foreach (string name in watch)
      {
        string path = Path.Combine(viewerFolder, name);
        if (!File.Exists(path)) continue;
        long t = File.GetLastWriteTimeUtc(path).Ticks;
        if (t > maxTicks) maxTicks = t;
      }
      return maxTicks > 0 ? maxTicks.ToString() : DateTime.UtcNow.Ticks.ToString();
    }

    private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
      try
      {
        if (!TryParseWebMessage(e, out JsonElement root))
        {
          return;
        }

        string type = root.TryGetProperty("type", out var typeEl) ? typeEl.GetString() ?? "" : "";

        if (type == "viewDiag"
            && root.TryGetProperty("lines", out var diagEl))
        {
          string lines = diagEl.GetString() ?? "";
          if (!string.IsNullOrWhiteSpace(lines))
          {
            Console.WriteLine("[3D diag] " + lines);
          }
          return;
        }

        if (type == "loadStatus")
        {
          string msg = root.TryGetProperty("message", out var m) ? m.GetString() : "";
          Dispatcher.Invoke(() =>
          {
            TxtStatus.Text = msg ?? "";
            if (root.TryGetProperty("error", out var err) && err.GetBoolean())
            {
              TxtStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                  System.Windows.Media.Color.FromRgb(0xFF, 0x88, 0x88));
            }
            else
            {
              TxtStatus.ClearValue(ForegroundProperty);
            }
          });
          return;
        }

        if (type == "markerClick"
            && root.TryGetProperty("signalKey", out var keyEl))
        {
          MarkerClicked?.Invoke(this, keyEl.GetString() ?? "");
          return;
        }

        if (type == "assyClick"
            && root.TryGetProperty("assyId", out var assyEl))
        {
          string assyId = assyEl.GetString();
          if (!string.IsNullOrEmpty(assyId))
          {
            ApplyAssyUiState(assyId, fromScript: true);
            AssyEntered?.Invoke(this, assyId);
          }
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine("WebMessage parse failed: " + ex.Message);
      }
    }

    private static bool TryParseWebMessage(CoreWebView2WebMessageReceivedEventArgs e, out JsonElement root)
    {
      root = default;
      // JS: postMessage({ type, ... }) → WebMessageAsJson 사용.
      // TryGetWebMessageAsString()은 plain string 전용이라 객체 전송 시 ArgumentException 발생.
      string json;
      try
      {
        json = e.WebMessageAsJson;
      }
      catch (ArgumentException)
      {
        try
        {
          json = e.TryGetWebMessageAsString();
        }
        catch (ArgumentException)
        {
          return false;
        }
      }

      if (string.IsNullOrWhiteSpace(json))
      {
        return false;
      }

      try
      {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.String)
        {
          string inner = doc.RootElement.GetString();
          if (string.IsNullOrWhiteSpace(inner))
          {
            return false;
          }

          using var innerDoc = JsonDocument.Parse(inner);
          root = innerDoc.RootElement.Clone();
          return true;
        }

        root = doc.RootElement.Clone();
        return true;
      }
      catch (JsonException)
      {
        return false;
      }
    }

    private void CoreWebView2_ProcessFailed(object sender, CoreWebView2ProcessFailedEventArgs e)
    {
      Dispatcher.Invoke(() =>
      {
        string detail = e.ProcessFailedKind.ToString();
        detail += " — " + e.Reason;
        TxtStatus.Text = "WebView2 오류: " + detail + " (앱 재시작)";
        TxtStatus.Foreground = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0xFF, 0x88, 0x88));
        Console.WriteLine("WebView2 ProcessFailed: " + detail);
      });
    }

    private void CoreWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
      if (!e.IsSuccess)
      {
        TxtStatus.Text = "3D 페이지 로드 실패: " + e.WebErrorStatus;
        TxtPlaceholder.Visibility = Visibility.Visible;
        TxtPlaceholder.Text = "viewer.html 로드 실패\nWebView2 Runtime 확인";
        return;
      }

      TxtStatus.Text = "3D 엔진 초기화 중...";
      _ = InjectResolvedModelsAsync(_gClass.srmNum);
    }

    private async Task InjectResolvedModelsAsync(int srmNum)
    {
      if (WebView?.CoreWebView2 == null) return;

      string folder = Dio3DDrawingPath.Get3DDrawingFolder(srmNum);
      var catalog = Dio3DAssemblyCatalog.Load(srmNum);
      var assemblies = new Dictionary<string, object>();

      if (catalog.Assemblies != null)
      {
        foreach (var assy in catalog.Assemblies)
        {
          if (string.IsNullOrWhiteSpace(assy.DetailModel)) continue;
          string stpFull = Dio3DDrawingPath.ResolveStpSourcePath(folder, assy.DetailModel);
          if (string.IsNullOrEmpty(stpFull)) continue;

          string glbFull = Dio3DDrawingPath.ResolveDisplayGlbFromStp(stpFull);
          string stpRel = Path.GetRelativePath(folder, stpFull).Replace('\\', '/');
          string glbRel = Path.GetRelativePath(folder, glbFull).Replace('\\', '/');
          bool glbReady = Dio3DStpToGlbConverter.IsDisplayGlbReady(folder, assy.DetailModel);

          long stpBytes = File.Exists(stpFull) ? new FileInfo(stpFull).Length : 0;
          var last = Dio3DStpToGlbConverter.GetLastResult(assy.Id);
          string convertError = glbReady ? null : (last?.Error ?? "prebuilt GLB 없음 — 배포 패키지 확인");
          string convertSource = last?.ConvertSource;
          assemblies[assy.Id] = new { path = glbRel, kind = "glb", stpPath = stpRel, stpBytes, glbReady, convertError, convertSource };
          Console.WriteLine($"[3D] {assy.Id} STP→{stpRel}  GLB→{glbRel}  ready={glbReady}" + (convertError != null ? $"  err={convertError}" : ""));
        }
      }

      string overviewRel = null;
      string overviewFull = Dio3DDrawingPath.ResolveOverviewModelPath(srmNum);
      if (!string.IsNullOrEmpty(overviewFull))
      {
        overviewRel = Path.GetRelativePath(folder, overviewFull).Replace('\\', '/');
      }

      var payload = new { assemblies, overview = overviewRel };
      string json = JsonSerializer.Serialize(payload);

      string script =
          "(() => { const d = " + json + "; if (window.setResolvedModels) window.setResolvedModels(d); else window.__resolvedModelsQueue = d; })()";
      for (int attempt = 0; attempt < 8; attempt++)
      {
        try
        {
          await WebView.CoreWebView2.ExecuteScriptAsync(script).ConfigureAwait(true);
          return;
        }
        catch (Exception ex)
        {
          if (attempt >= 7)
          {
            Console.WriteLine("InjectResolvedModels failed: " + ex.Message);
            return;
          }
          await Task.Delay(250).ConfigureAwait(true);
        }
      }
    }

    private void BuildAssyButtons(int srmNum)
    {
      PanelAssy.Children.Clear();
      _assyButtons.Clear();

      var catalog = Dio3DAssemblyCatalog.Load(srmNum);
      if (catalog.Assemblies == null || catalog.Assemblies.Count == 0)
      {
        return;
      }

      foreach (var assy in catalog.Assemblies)
      {
        var btn = new Button
        {
          Content = assy.Label ?? assy.Id?.ToUpper(),
          Tag = assy.Id,
          Style = (Style)FindResource("Dio3DAssyButtonStyle")
        };
        btn.Click += AssyButton_Click;
        PanelAssy.Children.Add(btn);
        _assyButtons.Add(btn);
      }
    }

    private async void AssyButton_Click(object sender, RoutedEventArgs e)
    {
      if (sender is not Button btn || btn.Tag is not string assyId) return;

      ApplyAssyUiState(assyId, fromScript: false);
      AssyEntered?.Invoke(this, assyId);
      await RunScriptAsync($"window.enterAssy && window.enterAssy({JsonSerializer.Serialize(assyId)})");
    }

    private void ApplyAssyUiState(string assyId, bool fromScript)
    {
      _currentAssyId = assyId;
      BtnBack.Visibility = Visibility.Visible;
      var entry = Dio3DAssemblyCatalog.Load(_gClass.srmNum).FindById(assyId);
      TxtBreadcrumb.Text = "3D MAP > " + (entry?.Label ?? assyId.ToUpper());

      foreach (var b in _assyButtons)
      {
        if (Equals(b.Tag, assyId))
        {
          b.Background = new System.Windows.Media.SolidColorBrush(
              System.Windows.Media.Color.FromArgb(0xFF, 0xB6, 0xD8, 0xFB));
          b.Foreground = System.Windows.Media.Brushes.Black;
        }
        else
        {
          b.ClearValue(BackgroundProperty);
          b.ClearValue(ForegroundProperty);
        }
      }

      if (!fromScript)
      {
        // WPF 버튼에서 들어온 경우만 JS 호출 (AssyButton_Click에서 처리)
      }
    }

    private async void BtnBack_Click(object sender, RoutedEventArgs e)
    {
      _currentAssyId = null;
      BtnBack.Visibility = Visibility.Collapsed;
      TxtBreadcrumb.Text = "3D MAP";

      foreach (var b in _assyButtons)
      {
        b.ClearValue(BackgroundProperty);
        b.ClearValue(ForegroundProperty);
      }

      await RunScriptAsync("window.exitAssy && window.exitAssy()");
      AssyExited?.Invoke(this, EventArgs.Empty);
    }

    public async void PostUpdateIo(string assyId, Dictionary<string, bool> states)
    {
      if (states == null || states.Count == 0) return;
      string json = JsonSerializer.Serialize(states);
      await RunScriptAsync($"window.updateIo && window.updateIo({json})");
    }

    public async void PostHighlightMarker(string signalKey)
    {
      if (string.IsNullOrEmpty(signalKey)) return;
      await RunScriptAsync($"window.highlightMarker && window.highlightMarker({JsonSerializer.Serialize(signalKey)})");
    }

    public void ExitAssy()
    {
      BtnBack_Click(this, new RoutedEventArgs());
    }

    private async Task RunScriptAsync(string script)
    {
      if (!_initialized || WebView?.CoreWebView2 == null) return;
      try
      {
        await WebView.CoreWebView2.ExecuteScriptAsync(script).ConfigureAwait(true);
      }
      catch (Exception ex)
      {
        Console.WriteLine("ExecuteScript failed: " + ex.Message);
      }
    }

    private void BtnOpenExternal_Click(object sender, RoutedEventArgs e)
    {
      OpenPdfExternally();
    }

    private void OpenPdfExternally()
    {
      if (string.IsNullOrEmpty(_loadedPdfPath) || !File.Exists(_loadedPdfPath))
      {
        MenuWindow.VarMessageBox.Show(
            "3D PDF",
            "PDF 파일이 없습니다.\n3D_Drawing 폴더에 SRM_3D.pdf 를 넣으세요.",
            MenuWindow.VarMessageBoxButton.OK);
        return;
      }

      try
      {
        Process.Start(new ProcessStartInfo
        {
          FileName = _loadedPdfPath,
          UseShellExecute = true
        });
      }
      catch (Exception ex)
      {
        MenuWindow.VarMessageBox.Show(
            "3D PDF",
            "외부 뷰어 실행 실패:\n" + ex.Message,
            MenuWindow.VarMessageBoxButton.OK);
      }
    }

    private static void SeedConfigIfNeeded(int srmNum)
    {
      string folder = Dio3DDrawingPath.Get3DDrawingFolder(srmNum);
      Directory.CreateDirectory(folder);

      string drawingIni = Path.Combine(folder, cConstDefine.DRAWING_INI);
      if (!File.Exists(drawingIni))
      {
        cIniAccess.Write(drawingIni, "DRAWING", "PdfFile", cConstDefine.DEFAULT_3D_PDF);
      }

      Dio3DDrawingPath.SeedDrawingFromTemplate(srmNum);
      Dio3DIoManifestGenerator.EnsureIoManifestFromStp(srmNum);
    }

    /// <summary>뷰어 열기 전 prebuilt GLB 존재 확인 (현장 변환 없음)</summary>
    private async Task ValidateAllAssyPrebuiltGlbAsync(int srmNum)
    {
      string folder = Dio3DDrawingPath.Get3DDrawingFolder(srmNum);
      var catalog = Dio3DAssemblyCatalog.Load(srmNum);
      if (catalog?.Assemblies == null)
      {
        return;
      }

      foreach (var assy in catalog.Assemblies)
      {
        if (string.IsNullOrWhiteSpace(assy.DetailModel))
        {
          continue;
        }

        if (Dio3DStpToGlbConverter.IsDisplayGlbReady(folder, assy.DetailModel))
        {
          continue;
        }

        string label = assy.Label ?? assy.Id;
        await Dispatcher.InvokeAsync(() =>
        {
          TxtStatus.Text = $"형체 GLB 없음: {label} — 배포 패키지에 GLB 동봉 필요";
        });

        bool ok = await Task.Run(() => Dio3DStpToGlbConverter.EnsureGlbForAssy(srmNum, assy.Id))
            .ConfigureAwait(true);

        if (!ok)
        {
          string err = Dio3DStpToGlbConverter.GetLastResult(assy.Id)?.Error ?? "prebuilt GLB 없음";
          await Dispatcher.InvokeAsync(() =>
          {
            TxtStatus.Text = $"형체 GLB 없음: {label}";
          });
          Console.WriteLine($"prebuilt GLB 없음: {assy.Id} — {err}");
        }
      }
    }
  }
}
