using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace gcp_Wpf.Services
{
  /// <summary>
  /// io_manifest.json — STP mtime 변경 시 node로 자동 재파싱·좌표 diff
  /// </summary>
  public static class Dio3DIoManifestGenerator
  {
    private const double MtimeToleranceMs = 0.5;

    /// <summary>STP 날짜 변경 감지 → stale 시 manifest 재생성</summary>
    public static void EnsureIoManifestFromStp(int srmNum)
    {
      string drawingFolder = Dio3DDrawingPath.Get3DDrawingFolder(srmNum);
      if (!Directory.Exists(drawingFolder))
      {
        return;
      }

      var catalog = Dio3DAssemblyCatalog.Load(srmNum);
      if (catalog?.Assemblies == null)
      {
        return;
      }

      foreach (var assy in catalog.Assemblies)
      {
        if (string.IsNullOrWhiteSpace(assy.DetailModel) || assy.SignalKeys == null || assy.SignalKeys.Count == 0)
        {
          continue;
        }

        string manifestPath = ResolveManifestPath(drawingFolder, assy);
        string stpPath = Dio3DDrawingPath.ResolveStpSourcePath(drawingFolder, assy.DetailModel);
        if (string.IsNullOrEmpty(stpPath) || !File.Exists(stpPath))
        {
          if (!File.Exists(manifestPath) || new FileInfo(manifestPath).Length <= 32)
          {
            Console.WriteLine(
                $"io_manifest 없음: {assy.Id} — STP 없음\n" +
                $"  node tools/stp-io-manifest.mjs --assy {assy.Id}");
          }
          continue;
        }

        if (!IsManifestStale(manifestPath, stpPath))
        {
          continue;
        }

        string reason = DescribeStaleReason(manifestPath, stpPath);
        Console.WriteLine($"[io_manifest] 재파싱 필요 — {assy.Id}: {reason}");
        if (!RunManifestSync(drawingFolder, assy.Id))
        {
          Console.WriteLine(
              $"io_manifest 재생성 실패: {assy.Id}\n" +
              $"  node tools/stp-io-manifest.mjs --assy {assy.Id} --drawing \"{drawingFolder}\" --force");
          continue;
        }

        if (IsManifestContentStale(manifestPath))
        {
          Console.WriteLine(
              $"[io_manifest] 경고: {assy.Id} 재생성 후에도 신뢰 좌표 0개 — STP/manifest 확인:\n" +
              $"  node tools/check-assy-stp-export.mjs --assy {assy.Id}");
        }
      }
    }

    private static string DescribeStaleReason(string manifestPath, string stpPath)
    {
      if (!File.Exists(manifestPath))
      {
        return "manifest 없음";
      }

      if (IsManifestContentStale(manifestPath))
      {
        return "STP는 있으나 points/matchCount 비어 있음 (STP 재export 후 manifest 미동기화)";
      }

      return Path.GetFileName(stpPath) + " 변경됨";
    }

    private static bool IsManifestContentStale(string manifestPath)
    {
      if (!File.Exists(manifestPath))
      {
        return true;
      }

      try
      {
        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = doc.RootElement;
        int signalCount = root.TryGetProperty("signalCount", out JsonElement sc) ? sc.GetInt32() : 0;
        if (signalCount <= 0)
        {
          return false;
        }

        int matchCount = root.TryGetProperty("matchCount", out JsonElement mc) ? mc.GetInt32() : -1;
        int pointCount = 0;
        int reliableCount = 0;
        if (root.TryGetProperty("points", out JsonElement pts) && pts.ValueKind == JsonValueKind.Object)
        {
          foreach (JsonProperty prop in pts.EnumerateObject())
          {
            pointCount++;
            if (!prop.Value.TryGetProperty("matchMode", out JsonElement modeEl))
            {
              continue;
            }

            string mode = modeEl.GetString();
            if (!string.Equals(mode, "stp_occurrence", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(mode, "stp_occurrence_mesh_center", StringComparison.OrdinalIgnoreCase))
            {
              continue;
            }

            string sourceMesh = prop.Value.TryGetProperty("sourceMesh", out JsonElement smEl)
                ? smEl.GetString() ?? ""
                : "";
            if (sourceMesh.IndexOf("GDFL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sourceMesh.IndexOf("8BIT", StringComparison.OrdinalIgnoreCase) >= 0)
            {
              continue;
            }

            reliableCount++;
          }
        }

        if (reliableCount == 0)
        {
          return true;
        }

        return pointCount == 0 || matchCount == 0;
      }
      catch
      {
        return true;
      }
    }

    public static bool IsManifestStale(string manifestPath, string stpPath)
    {
      if (string.IsNullOrEmpty(stpPath) || !File.Exists(stpPath))
      {
        return false;
      }

      if (!File.Exists(manifestPath) || new FileInfo(manifestPath).Length <= 32)
      {
        return true;
      }

      var stpInfo = new FileInfo(stpPath);
      long stpMtimeMs = ToUnixMs(stpInfo.LastWriteTimeUtc);

      try
      {
        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = doc.RootElement;

        if (TryGetRegenBlockedForSameStp(root, stpPath, stpInfo, stpMtimeMs))
        {
          return false;
        }

        if (root.TryGetProperty("stpSource", out JsonElement src) &&
            src.TryGetProperty("mtimeMs", out JsonElement mtimeEl))
        {
          double recordedMtime = mtimeEl.GetDouble();
          if (src.TryGetProperty("file", out JsonElement fileEl))
          {
            string recordedFile = fileEl.GetString();
            if (!string.Equals(Path.GetFileName(stpPath), recordedFile, StringComparison.OrdinalIgnoreCase))
            {
              return true;
            }
          }

          if (Math.Abs(stpMtimeMs - recordedMtime) > MtimeToleranceMs)
          {
            return true;
          }

          if (src.TryGetProperty("sizeBytes", out JsonElement sizeEl) &&
              sizeEl.TryGetInt64(out long recordedSize) &&
              recordedSize != stpInfo.Length)
          {
            return true;
          }

          if (IsManifestContentStale(manifestPath))
          {
            return true;
          }

          return false;
        }

        if (root.TryGetProperty("stpFile", out JsonElement stpFileEl))
        {
          string recordedStp = stpFileEl.GetString();
          if (!string.IsNullOrEmpty(recordedStp) &&
              !string.Equals(Path.GetFileName(stpPath), recordedStp, StringComparison.OrdinalIgnoreCase))
          {
            return true;
          }
        }
      }
      catch
      {
        return true;
      }

      if (IsManifestContentStale(manifestPath))
      {
        return true;
      }

      var manifestInfo = new FileInfo(manifestPath);
      return stpInfo.LastWriteTimeUtc > manifestInfo.LastWriteTimeUtc.AddSeconds(1);
    }

    private static bool RunManifestSync(string drawingFolder, string assyId)
    {
      string script = ResolveManifestScript();
      if (script == null)
      {
        Console.WriteLine("io_manifest sync: tools/stp-io-manifest.mjs 없음");
        return false;
      }

      try
      {
        var psi = new ProcessStartInfo
        {
          FileName = "node",
          Arguments = $"\"{script}\" --assy \"{assyId}\" --drawing \"{drawingFolder}\" --force --no-desktop-sync",
          UseShellExecute = false,
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          CreateNoWindow = true,
        };

        using var proc = Process.Start(psi);
        if (proc == null)
        {
          return false;
        }

        string stdout = proc.StandardOutput.ReadToEnd();
        string stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(900000);

        if (!string.IsNullOrWhiteSpace(stdout))
        {
          Console.WriteLine(stdout.TrimEnd());
        }
        if (!string.IsNullOrWhiteSpace(stderr))
        {
          Console.WriteLine(stderr.TrimEnd());
        }

        return proc.ExitCode == 0;
      }
      catch (Exception ex)
      {
        Console.WriteLine("io_manifest sync 실행 오류: " + ex.Message);
        return false;
      }
    }

    /// <summary>불완전 STP로 재생성 실패 시 같은 STP에 대해 반복 재생성 방지</summary>
    private static bool TryGetRegenBlockedForSameStp(
        JsonElement root,
        string stpPath,
        FileInfo stpInfo,
        long stpMtimeMs)
    {
      if (!root.TryGetProperty("_regenBlocked", out JsonElement blocked) ||
          !blocked.TryGetProperty("attemptedStp", out JsonElement attempted))
      {
        return false;
      }

      if (attempted.TryGetProperty("file", out JsonElement fileEl))
      {
        string recordedFile = fileEl.GetString();
        if (!string.Equals(Path.GetFileName(stpPath), recordedFile, StringComparison.OrdinalIgnoreCase))
        {
          return false;
        }
      }

      if (attempted.TryGetProperty("mtimeMs", out JsonElement mtimeEl))
      {
        double recordedMtime = mtimeEl.GetDouble();
        if (Math.Abs(stpMtimeMs - recordedMtime) > MtimeToleranceMs)
        {
          return false;
        }
      }

      if (attempted.TryGetProperty("sizeBytes", out JsonElement sizeEl) &&
          sizeEl.TryGetInt64(out long recordedSize) &&
          recordedSize != stpInfo.Length)
      {
        return false;
      }

      return CountReliableManifestPoints(root) > 0;
    }

    private static int CountReliableManifestPoints(JsonElement root)
    {
      if (!root.TryGetProperty("points", out JsonElement pts) || pts.ValueKind != JsonValueKind.Object)
      {
        return 0;
      }

      int reliable = 0;
      foreach (JsonProperty prop in pts.EnumerateObject())
      {
        if (!prop.Value.TryGetProperty("matchMode", out JsonElement modeEl))
        {
          continue;
        }

        string mode = modeEl.GetString();
        if (!string.Equals(mode, "stp_occurrence", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(mode, "stp_occurrence_mesh_center", StringComparison.OrdinalIgnoreCase))
        {
          continue;
        }

        string sourceMesh = prop.Value.TryGetProperty("sourceMesh", out JsonElement smEl)
            ? smEl.GetString() ?? ""
            : "";
        if (sourceMesh.IndexOf("GDFL", StringComparison.OrdinalIgnoreCase) >= 0 ||
            sourceMesh.IndexOf("8BIT", StringComparison.OrdinalIgnoreCase) >= 0)
        {
          continue;
        }

        reliable++;
      }

      return reliable;
    }

    private static long ToUnixMs(DateTime utc)
    {
      return new DateTimeOffset(utc).ToUnixTimeMilliseconds();
    }

    private static string ResolveManifestScript()
    {
      string baseDir = AppDomain.CurrentDomain.BaseDirectory;
      string[] candidates =
      {
        Path.Combine(baseDir, "tools", "stp-io-manifest.mjs"),
        Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "tools", "stp-io-manifest.mjs")),
      };

      foreach (string path in candidates)
      {
        if (File.Exists(path))
        {
          return path;
        }
      }

      return null;
    }

    private static string ResolveManifestPath(string drawingFolder, Dio3DAssemblyEntry assy)
    {
      if (!string.IsNullOrWhiteSpace(assy.IoManifest))
      {
        return Path.Combine(drawingFolder, assy.IoManifest.Trim());
      }

      string detail = assy.DetailModel?.Replace('\\', '/').Trim() ?? "";
      string dir = Path.GetDirectoryName(Path.Combine(drawingFolder, detail)) ?? drawingFolder;
      return Path.Combine(dir, "io_manifest.json");
    }
  }
}
