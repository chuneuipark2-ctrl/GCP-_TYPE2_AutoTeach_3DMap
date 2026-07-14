using System;
using System.IO;
using gcp_Wpf.Services;

namespace Dio3DAssyBuild
{
  internal static class Program
  {
    private static int Main(string[] args)
    {
      bool force = false;
      string objPath = null;
      string glbPath = null;

      for (int i = 0; i < args.Length; i++)
      {
        string a = args[i];
        if (a == "--force")
        {
          force = true;
        }
        else if (a == "--obj" && i + 1 < args.Length)
        {
          objPath = Path.GetFullPath(args[++i]);
        }
        else if (a == "--glb" && i + 1 < args.Length)
        {
          glbPath = Path.GetFullPath(args[++i]);
        }
        else if (!a.StartsWith("-", StringComparison.Ordinal))
        {
          if (objPath == null)
          {
            objPath = Path.GetFullPath(a);
          }
          else if (glbPath == null)
          {
            glbPath = Path.GetFullPath(a);
          }
        }
      }

      if (args.Length == 1 && args[0] == "--list-export-formats")
      {
        using var ctx = new Assimp.AssimpContext();
        foreach (var fmt in ctx.GetSupportedExportFormats())
        {
          Console.WriteLine($"{fmt.FormatId}\t.{fmt.FileExtension}\t{fmt.Description}");
        }
        return 0;
      }

      if (string.IsNullOrEmpty(objPath))
      {
        Console.Error.WriteLine("사용법: Dio3DAssyBuild --obj <input.obj> --glb <output.glb> [--force]");
        Console.Error.WriteLine("       Dio3DAssyBuild <input.obj> [output.glb] [--force]");
        return 1;
      }

      if (string.IsNullOrEmpty(glbPath))
      {
        glbPath = Path.ChangeExtension(objPath, ".glb");
      }

      Console.WriteLine($"[OBJ→GLB] {Path.GetFileName(objPath)} ({new FileInfo(objPath).Length / (1024.0 * 1024.0):F1} MB)");
      var t0 = DateTime.UtcNow;
      ObjToGlbResult r = Dio3DAssimpObjToGlbBuilder.Convert(objPath, glbPath, force);
      double sec = (DateTime.UtcNow - t0).TotalSeconds;

      if (!r.Success)
      {
        Console.Error.WriteLine("실패: " + r.Error);
        return 1;
      }

      string meshNote = r.MeshCount >= 0 ? r.MeshCount.ToString() : "cached";
      Console.WriteLine(
          $"  GLB 저장: {Path.GetFileName(r.GlbPath)} ({r.Bytes / (1024.0 * 1024.0):F2} MB, mesh={meshNote}, {sec:F1}s)");
      return 0;
    }
  }
}
