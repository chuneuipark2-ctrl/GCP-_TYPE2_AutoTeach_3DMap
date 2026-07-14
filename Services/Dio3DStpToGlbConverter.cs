using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace gcp_Wpf.Services
{
  public sealed class GlbConvertResult
  {
    public string AssyId { get; init; }
    public bool Success { get; init; }
    public bool Skipped { get; init; }
    public string Error { get; init; }
    public string StpPath { get; init; }
    public string ObjPath { get; init; }
    /// <summary>형체 소스: prebuilt</summary>
    public string ConvertSource { get; init; }
    public string GlbPath { get; init; }
  }

  /// <summary>
  /// 형체 GLB — 빌드/배포 패키지에 동봉된 prebuilt GLB만 사용 (SE OBJ → Assimp 빌드타임 변환).
  /// 현장 OBJ→GLB 변환 없음. 센서 좌표는 STP io_manifest.json.
  /// </summary>
  public static class Dio3DStpToGlbConverter
  {
    private static readonly Dictionary<string, GlbConvertResult> LastResults =
        new Dictionary<string, GlbConvertResult>(StringComparer.OrdinalIgnoreCase);

    public static GlbConvertResult GetLastResult(string assyId)
    {
      if (string.IsNullOrWhiteSpace(assyId))
      {
        return null;
      }

      return LastResults.TryGetValue(assyId, out var r) ? r : null;
    }

    /// <summary>모든 ASSY prebuilt GLB 존재 여부 확인 (변환 없음)</summary>
    public static void EnsureGlbFromStp(int srmNum)
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
        TryValidatePrebuiltGlb(drawingFolder, assy);
      }
    }

    /// <summary>단일 ASSY prebuilt GLB 확인 (변환 없음)</summary>
    public static bool EnsureGlbForAssy(int srmNum, string assyId)
    {
      if (string.IsNullOrWhiteSpace(assyId))
      {
        return false;
      }

      string drawingFolder = Dio3DDrawingPath.Get3DDrawingFolder(srmNum);
      if (!Directory.Exists(drawingFolder))
      {
        StoreResult(assyId, false, "3D_Drawing 폴더 없음");
        return false;
      }

      var catalog = Dio3DAssemblyCatalog.Load(srmNum);
      var assy = catalog?.Assemblies?.FirstOrDefault(
          a => string.Equals(a.Id, assyId, StringComparison.OrdinalIgnoreCase));
      if (assy == null)
      {
        StoreResult(assyId, false, "ASSY 정의 없음: " + assyId);
        return false;
      }

      return TryValidatePrebuiltGlb(drawingFolder, assy);
    }

    public static bool IsDisplayGlbReady(string drawingFolder, string detailModelRelative)
    {
      string stpPath = Dio3DDrawingPath.ResolveStpSourcePath(drawingFolder, detailModelRelative);
      string objPath = Dio3DDrawingPath.ResolveObjSourcePath(drawingFolder, detailModelRelative);
      string glbPath = Dio3DDrawingPath.ResolveAssyDisplayGlbPath(drawingFolder, detailModelRelative);
      return IsPlausibleDisplayGlb(objPath, stpPath, glbPath);
    }

    private static bool IsPlausibleDisplayGlb(string objPath, string stpPath, string glbPath)
    {
      string refPath = !string.IsNullOrEmpty(objPath) && File.Exists(objPath) ? objPath : stpPath;
      return Dio3DAssimpObjToGlbBuilder.IsPlausibleDisplayGlb(refPath, glbPath);
    }

    private static bool TryValidatePrebuiltGlb(string drawingFolder, Dio3DAssemblyEntry assy)
    {
      if (string.IsNullOrWhiteSpace(assy?.DetailModel))
      {
        return false;
      }

      string stpPath = Dio3DDrawingPath.ResolveStpSourcePath(drawingFolder, assy.DetailModel);
      string objPath = Dio3DDrawingPath.ResolveObjSourcePath(drawingFolder, assy.DetailModel);
      string glbPath = Dio3DDrawingPath.ResolveAssyDisplayGlbPath(drawingFolder, assy.DetailModel);

      if (IsPlausibleDisplayGlb(objPath, stpPath, glbPath))
      {
        LastResults[assy.Id] = new GlbConvertResult
        {
          AssyId = assy.Id,
          Success = true,
          Skipped = true,
          StpPath = stpPath,
          ObjPath = objPath,
          GlbPath = glbPath,
          ConvertSource = "prebuilt",
        };
        return true;
      }

      string msg = BuildMissingGlbMessage(assy.Id, glbPath, objPath, stpPath);
      StoreResult(assy.Id, false, msg, stpPath, objPath, glbPath);
      Console.WriteLine($"[3D] {assy.Id}: prebuilt GLB 없음 — {Path.GetFileName(glbPath)}");
      return false;
    }

    private static string BuildMissingGlbMessage(string assyId, string glbPath, string objPath, string stpPath)
    {
      string glbName = Path.GetFileName(glbPath ?? assyId + ".glb");
      string rootName = Path.GetFileNameWithoutExtension(glbName);
      string glbHint = Dio3DDrawingPath.FormatAssyGlbDeployHint(0, assyId, rootName);
      string objHint = Dio3DDrawingPath.FormatAssyObjSaveHint(0, assyId, rootName);
      string buildHint = "빌드: node tools/build-assy-artifacts.mjs --assy " + assyId;

      if (string.IsNullOrEmpty(objPath) || !File.Exists(objPath))
      {
        return "형체 GLB 미포함 — Solid Edge에서 OBJ export 후 빌드 필요\n" +
               "OBJ: " + objHint + "\n" +
               "GLB: " + glbHint + "\n" +
               buildHint;
      }

      return "형체 GLB 미포함 — 배포 패키지에 prebuilt GLB 필요\n" +
             glbHint + "\n" +
             buildHint;
    }

    private static void StoreResult(
        string assyId,
        bool success,
        string error,
        string stpPath = null,
        string objPath = null,
        string glbPath = null,
        bool skipped = false,
        string convertSource = null)
    {
      LastResults[assyId] = new GlbConvertResult
      {
        AssyId = assyId,
        Success = success,
        Skipped = skipped,
        Error = error,
        StpPath = stpPath,
        ObjPath = objPath,
        GlbPath = glbPath,
        ConvertSource = convertSource,
      };
    }
  }
}
