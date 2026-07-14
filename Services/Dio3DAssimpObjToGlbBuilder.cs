using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Assimp;

namespace gcp_Wpf.Services
{
  public sealed class ObjToGlbResult
  {
    public bool Success { get; init; }
    public string ObjPath { get; init; }
    public string GlbPath { get; init; }
    public int MeshCount { get; init; }
    public long Bytes { get; init; }
    public string Error { get; init; }
  }

  /// <summary>
  /// 빌드타임 전용 — Solid Edge OBJ → GLB (Assimp.Net). 현장 HMI에서 실행 금지.
  /// 센서 좌표는 STP io_manifest.json 만 사용.
  /// </summary>
  public static class Dio3DAssimpObjToGlbBuilder
  {
    private const long MinGlbBytes = 1024;

    public static bool IsPlausibleDisplayGlb(string objPath, string glbPath)
    {
      if (string.IsNullOrEmpty(glbPath) || !File.Exists(glbPath))
      {
        return false;
      }

      long glbBytes = new FileInfo(glbPath).Length;
      if (glbBytes < MinGlbBytes)
      {
        return false;
      }

      if (string.IsNullOrEmpty(objPath) || !File.Exists(objPath))
      {
        return true;
      }

      long objBytes = new FileInfo(objPath).Length;
      if (objBytes > 30 * 1024 * 1024 && glbBytes < 5 * 1024 * 1024)
      {
        return false;
      }

      if (objBytes > 10 * 1024 * 1024 && glbBytes < 2 * 1024 * 1024)
      {
        return false;
      }

      return true;
    }

    public static ObjToGlbResult Convert(string objPath, string glbPath, bool force = false)
    {
      if (string.IsNullOrWhiteSpace(objPath) || !File.Exists(objPath))
      {
        return Fail(objPath, glbPath, "OBJ 없음: " + objPath);
      }

      if (string.IsNullOrWhiteSpace(glbPath))
      {
        return Fail(objPath, glbPath, "GLB 출력 경로 없음");
      }

      if (File.Exists(glbPath) && !force)
      {
        if (IsPlausibleDisplayGlb(objPath, glbPath))
        {
          return new ObjToGlbResult
          {
            Success = true,
            ObjPath = objPath,
            GlbPath = glbPath,
            MeshCount = -1,
            Bytes = new FileInfo(glbPath).Length,
          };
        }
      }

      string outDir = Path.GetDirectoryName(glbPath);
      if (!string.IsNullOrEmpty(outDir))
      {
        Directory.CreateDirectory(outDir);
      }

      try
      {
        using var ctx = new AssimpContext();
        // 대형 OBJ(100만+ face): GenerateNormals/OptimizeMeshes 제거 — 빌드시간·메모리 폭증 방지
        var post = PostProcessSteps.Triangulate | PostProcessSteps.JoinIdenticalVertices;

        Console.WriteLine("[Assimp] import 시작...");
        var t0 = DateTime.UtcNow;
        Scene scene = ctx.ImportFile(objPath, post);
        Console.WriteLine($"[Assimp] import 완료 {scene?.MeshCount ?? 0} mesh ({(DateTime.UtcNow - t0).TotalSeconds:F1}s)");

        if (scene == null || !scene.HasMeshes || scene.MeshCount < 1)
        {
          return Fail(objPath, glbPath, "OBJ mesh 없음 — Solid Edge 어셈블리 OBJ 재export 확인");
        }

        string formatId = ResolveGltf2ExportFormat(ctx);
        string gltf2Path = Path.Combine(
            outDir ?? Path.GetDirectoryName(glbPath) ?? ".",
            Path.GetFileNameWithoutExtension(glbPath) + ".gltf2");
        Console.WriteLine($"[Assimp] export glTF2 ({formatId})...");
        var t1 = DateTime.UtcNow;
        bool exported = ctx.ExportFile(scene, gltf2Path, formatId);
        Console.WriteLine($"[Assimp] glTF2 export 완료 ({(DateTime.UtcNow - t1).TotalSeconds:F1}s)");
        if (!exported || !File.Exists(gltf2Path))
        {
          return Fail(objPath, glbPath, "Assimp glTF2 export 실패 (format=" + formatId + ")");
        }

        if (!TryPackGltf2ToGlb(gltf2Path, glbPath))
        {
          return Fail(objPath, glbPath, "glTF2→GLB v2 패킹 실패 — node tools/pack-gltf2-glb.mjs 확인");
        }

        try { File.Delete(gltf2Path); } catch { /* ignore */ }
        if (!File.Exists(glbPath))
        {
          return Fail(objPath, glbPath, "GLB v2 출력 없음");
        }

        long bytes = new FileInfo(glbPath).Length;
        if (!IsPlausibleDisplayGlb(objPath, glbPath))
        {
          try { File.Delete(glbPath); } catch { /* ignore */ }
          return Fail(objPath, glbPath, "GLB 용량 비정상 — OBJ 품질 또는 Assimp export 확인");
        }

        return new ObjToGlbResult
        {
          Success = true,
          ObjPath = objPath,
          GlbPath = glbPath,
          MeshCount = scene.MeshCount,
          Bytes = bytes,
        };
      }
      catch (Exception ex)
      {
        return Fail(objPath, glbPath, ex.Message);
      }
    }

    private static string ResolveGltf2ExportFormat(AssimpContext ctx)
    {
      foreach (ExportFormatDescription fmt in ctx.GetSupportedExportFormats())
      {
        string id = (fmt.FormatId ?? string.Empty).Trim();
        if (id.Equals("gltf2", StringComparison.OrdinalIgnoreCase))
        {
          return fmt.FormatId;
        }
      }

      return "gltf2";
    }

    private static bool TryPackGltf2ToGlb(string gltf2Path, string glbPath)
    {
      string toolsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
      string kemcoRoot = Path.GetFullPath(Path.Combine(toolsDir, ".."));
      string script = Path.Combine(toolsDir, "pack-gltf2-glb.mjs");
      if (!File.Exists(script))
      {
        Console.WriteLine("[Assimp] pack script 없음: " + script);
        return false;
      }

      string nodeExe = "node";
      var psi = new ProcessStartInfo
      {
        FileName = nodeExe,
        Arguments = $"\"{script}\" \"{gltf2Path}\" \"{glbPath}\"",
        WorkingDirectory = kemcoRoot,
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
      proc.WaitForExit(120000);
      if (!string.IsNullOrWhiteSpace(stdout))
      {
        Console.WriteLine(stdout.Trim());
      }
      if (!string.IsNullOrWhiteSpace(stderr))
      {
        Console.WriteLine(stderr.Trim());
      }

      return proc.ExitCode == 0 && File.Exists(glbPath);
    }

    private static string ResolveGlbExportFormat(AssimpContext ctx)
    {
      ExportFormatDescription glb2 = null;
      ExportFormatDescription glb1 = null;
      foreach (ExportFormatDescription fmt in ctx.GetSupportedExportFormats())
      {
        string ext = (fmt.FileExtension ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
        string id = (fmt.FormatId ?? string.Empty).Trim().ToLowerInvariant();
        if (id == "glb2" || id.Contains("glb2"))
        {
          glb2 = fmt;
        }
        else if (ext == "glb" || id == "glb")
        {
          glb1 = fmt;
        }
      }

      if (glb2 != null)
      {
        return glb2.FormatId;
      }

      if (glb1 != null)
      {
        return glb1.FormatId;
      }

      return "glb2";
    }

    private static ObjToGlbResult Fail(string objPath, string glbPath, string error)
    {
      return new ObjToGlbResult
      {
        Success = false,
        ObjPath = objPath,
        GlbPath = glbPath,
        Error = error,
      };
    }
  }
}
