using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace gcp_Wpf.Services
{
  public static class Dio3DDrawingPath
  {
    // 3 ASSY 구조 (SCP / Lower_Frame_assy / Carriage_Assy) — 구 7분할 폴더는 시드 시 제거
    public static readonly string[] CurrentAssemblyFolderNames =
    {
      "SCP", "Lower_Frame_assy", "Carriage_Assy"
    };

    private static readonly HashSet<string> LegacyAssemblyFolderNames =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
          "hoist", "fork", "travel", "panel", "station", "lidar", "mcage",
          "carriage", "lower_frame", "Hoist", "Fork", "Travel", "Panel",
          "Station", "LiDAR", "Mcage", "Carriage", "Lower_Frame"
        };
    private const string ConfigIniRelative = "Config\\Config.ini";
    private const string PathSection = "PATH";
    private const string DrawingIniSection = "DRAWING";

    public static string GetDrawingRoot(int srmNum)
    {
      string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigIniRelative);
      string customRoot = cIniAccess.Read(configPath, PathSection, cConstDefine.INI_3D_DRAWING_ROOT, "");
      if (!string.IsNullOrWhiteSpace(customRoot))
      {
        return Path.GetFullPath(customRoot.Trim());
      }

      return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SRM" + srmNum);
    }

    public static string Get3DDrawingFolder(int srmNum)
    {
      // Config.ini 3D_DrawingRoot 로 bin 밖 경로 지정 가능 — 디버그/Clean 시 사용자 STP·GLB 보호
      return Path.Combine(GetDrawingRoot(srmNum), cConstDefine.PATH_3D_DRAWING);
    }

    /// <summary>사용자 안내용 STP 저장 경로 (ASSY 폴더 직하위, 하위 테스트 폴더 없음)</summary>
    public static string FormatAssyStpSaveHint(int srmNum, string assyId, string stpBaseName = null)
    {
      string baseName = string.IsNullOrWhiteSpace(stpBaseName) ? assyId : stpBaseName.Trim();
      return $"SRM{srmNum}/3D_Drawing/assemblies/{assyId}/{baseName}.stp";
    }

    /// <summary>배포 패키지에 동봉할 prebuilt GLB 경로 안내</summary>
    public static string FormatAssyGlbDeployHint(int srmNum, string assyId, string glbBaseName = null)
    {
      string baseName = string.IsNullOrWhiteSpace(glbBaseName) ? assyId : glbBaseName.Trim();
      return $"SRM{srmNum}/3D_Drawing/assemblies/{assyId}/{baseName}.glb";
    }

    /// <summary>Solid Edge 형체 OBJ 저장 경로 (빌드타임 Assimp 입력)</summary>
    public static string FormatAssyObjSaveHint(int srmNum, string assyId, string objBaseName = null)
    {
      string baseName = string.IsNullOrWhiteSpace(objBaseName) ? assyId : objBaseName.Trim();
      return $"SRM{srmNum}/3D_Drawing/assemblies/{assyId}/{baseName}.obj";
    }

    public static void EnsureFolderExists(int srmNum)
    {
      string folder = Get3DDrawingFolder(srmNum);
      if (Directory.Exists(folder))
      {
        return;
      }

      try
      {
        Directory.CreateDirectory(folder);
        Console.WriteLine("3D_Drawing folder created at: " + folder);
      }
      catch (Exception ex)
      {
        Console.WriteLine("3D_Drawing folder create failed: " + ex.Message);
      }
    }

    /// <summary>
    /// 3D_Drawing 폴더에서 표시할 3D PDF 경로를 찾습니다.
    /// 우선순위: drawing.ini PdfFile → SRM_3D.pdf → 폴더 내 첫 *.pdf
    /// </summary>
    public static string ResolvePdfFilePath(int srmNum)
    {
      string folder = Get3DDrawingFolder(srmNum);
      if (!Directory.Exists(folder))
      {
        return null;
      }

      string drawingIni = Path.Combine(folder, cConstDefine.DRAWING_INI);
      string pdfName = cIniAccess.Read(drawingIni, DrawingIniSection, "PdfFile", "");
      if (!string.IsNullOrWhiteSpace(pdfName))
      {
        string fromIni = Path.Combine(folder, pdfName.Trim());
        if (File.Exists(fromIni))
        {
          return fromIni;
        }
      }

      string defaultPdf = Path.Combine(folder, cConstDefine.DEFAULT_3D_PDF);
      if (File.Exists(defaultPdf))
      {
        return defaultPdf;
      }

      return Directory.GetFiles(folder, "*.pdf", SearchOption.TopDirectoryOnly).FirstOrDefault();
    }

    public static string GetViewerFolder()
    {
      return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Viewer3D");
    }

    private static readonly string[] ModelExtensions = { ".glb", ".gltf", ".stp", ".step", ".obj" };
    private static readonly string[] GlbExtensions = { ".glb", ".gltf" };
    private static readonly string[] StepExtensions = { ".stp", ".step" };
    private static readonly string[] ObjExtensions = { ".obj" };
    private static readonly string[] JtExtensions = { ".jt" };
    private static readonly Regex RevSuffixRegex = new Regex(@"_REV(\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly (string Folder, string CanonicalRoot)[] CanonicalStpRoots =
    {
      ("SCP", "SCP"),
      ("Lower_Frame_assy", "LOWER_FRAME_ASSY"),
      ("Carriage_Assy", "Carriage_Assy"),
    };

    private static string StripRevSuffix(string baseName)
    {
      if (string.IsNullOrWhiteSpace(baseName))
      {
        return baseName;
      }

      string stripped = RevSuffixRegex.Replace(baseName, string.Empty);
      if (stripped.EndsWith("_final", StringComparison.OrdinalIgnoreCase))
      {
        return stripped.Substring(0, stripped.Length - "_final".Length);
      }

      return stripped;
    }

    private static int ParseRevNumber(string fileNameWithoutExt)
    {
      Match m = RevSuffixRegex.Match(fileNameWithoutExt ?? string.Empty);
      if (!m.Success)
      {
        return 0;
      }

      return int.TryParse(m.Groups[1].Value, out int rev) ? rev : 0;
    }

    /// <summary>
    /// {root}_REV{n}.ext 중 n이 가장 큰 파일, 없으면 {root}.ext
    /// </summary>
    private static string FindHighestRevModelInDir(string dir, string rootName, string ext)
    {
      if (string.IsNullOrWhiteSpace(dir) || string.IsNullOrWhiteSpace(rootName) || !Directory.Exists(dir))
      {
        return null;
      }

      string bestPath = null;
      int bestRev = -1;

      string finalPath = Path.Combine(dir, rootName + "_final" + ext);
      if (File.Exists(finalPath))
      {
        return finalPath;
      }

      foreach (string file in Directory.GetFiles(dir, rootName + "_REV*" + ext))
      {
        if (IsExcludedModelFileName(Path.GetFileName(file), dir))
        {
          continue;
        }

        string name = Path.GetFileNameWithoutExtension(file);
        int rev = ParseRevNumber(name);
        if (rev > bestRev)
        {
          bestRev = rev;
          bestPath = file;
        }
      }

      if (!string.IsNullOrEmpty(bestPath))
      {
        return bestPath;
      }

      string plain = Path.Combine(dir, rootName + ext);
      if (File.Exists(plain) && !IsExcludedModelFileName(Path.GetFileName(plain), dir))
      {
        return plain;
      }

      return null;
    }

    /// <summary>ASSY_IO_PROFILE.json excludeStpNamePatterns — 전 ASSY 공통</summary>
    private static bool IsExcludedModelFileName(string fileName, string assyDir)
    {
      if (string.IsNullOrWhiteSpace(fileName))
      {
        return true;
      }

      foreach (string pattern in LoadExcludeStpPatterns(assyDir))
      {
        if (fileName.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
        {
          return true;
        }
      }

      return false;
    }

    private static string[] LoadExcludeStpPatterns(string assyDir)
    {
      if (string.IsNullOrWhiteSpace(assyDir))
      {
        return Array.Empty<string>();
      }

      try
      {
        string jsonPath = Path.Combine(assyDir, "ASSY_IO_PROFILE.json");
        if (!File.Exists(jsonPath))
        {
          return Array.Empty<string>();
        }

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        if (!doc.RootElement.TryGetProperty("excludeStpNamePatterns", out JsonElement arr) ||
            arr.ValueKind != JsonValueKind.Array)
        {
          return Array.Empty<string>();
        }

        var list = new List<string>();
        foreach (JsonElement item in arr.EnumerateArray())
        {
          string pat = item.GetString();
          if (!string.IsNullOrWhiteSpace(pat))
          {
            list.Add(pat.Trim());
          }
        }

        return list.ToArray();
      }
      catch
      {
        return Array.Empty<string>();
      }
    }

    private static string FindBestModelInDirByExtension(string dir, string ext)
    {
      if (!Directory.Exists(dir))
      {
        return null;
      }

      return Directory.GetFiles(dir, "*" + ext)
          .Where(f => !IsExcludedModelFileName(Path.GetFileName(f), dir))
          .Select(f => new { Path = f, Rev = ParseRevNumber(Path.GetFileNameWithoutExtension(f)) })
          .OrderByDescending(x => x.Rev)
          .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
          .Select(x => x.Path)
          .FirstOrDefault();
    }

    /// <summary>
    /// ASSY 원본 STP만 탐색 (_REV 최대). GLB 직접 탐색 없음.
    /// </summary>
    public static string ResolveStpSourcePath(string drawingFolder, string preferredRelativePath)
    {
      if (string.IsNullOrWhiteSpace(preferredRelativePath) || string.IsNullOrWhiteSpace(drawingFolder))
      {
        return null;
      }

      string preferredFull = Path.Combine(drawingFolder, preferredRelativePath.Trim());
      string dir = Path.GetDirectoryName(preferredFull) ?? drawingFolder;
      string baseName = Path.GetFileNameWithoutExtension(preferredFull);
      string rootName = StripRevSuffix(baseName);

      foreach (string ext in StepExtensions)
      {
        string stepCandidate = FindHighestRevModelInDir(dir, rootName, ext);
        if (!string.IsNullOrEmpty(stepCandidate))
        {
          return stepCandidate;
        }
      }

      string assyFolder = Path.GetFileName(dir);
      foreach (var (folder, canonRoot) in CanonicalStpRoots)
      {
        if (!string.Equals(assyFolder, folder, StringComparison.OrdinalIgnoreCase))
        {
          continue;
        }

        if (string.Equals(rootName, canonRoot, StringComparison.OrdinalIgnoreCase))
        {
          break;
        }

        foreach (string ext in StepExtensions)
        {
          string canonCandidate = FindHighestRevModelInDir(dir, canonRoot, ext);
          if (!string.IsNullOrEmpty(canonCandidate))
          {
            return canonCandidate;
          }
        }
        break;
      }

      if (File.Exists(preferredFull) && StepExtensions.Contains(Path.GetExtension(preferredFull).ToLowerInvariant()))
      {
        return preferredFull;
      }

      if (Directory.Exists(dir))
      {
        foreach (string ext in StepExtensions)
        {
          string found = FindBestModelInDirByExtension(dir, ext);
          if (!string.IsNullOrEmpty(found))
          {
            return found;
          }
        }
      }

      return null;
    }

    /// <summary>
    /// STP와 동일 basename의 JT (차선 형체). _REV/_final 규칙 동일.
    /// Carriage: Carriage_Assy_final.jt + Carriage_Assy_final/ 파트 폴더
    /// </summary>
    public static string ResolveJtSourcePath(string drawingFolder, string preferredRelativePath)
    {
      if (string.IsNullOrWhiteSpace(preferredRelativePath) || string.IsNullOrWhiteSpace(drawingFolder))
      {
        return null;
      }

      string preferredFull = Path.Combine(drawingFolder, preferredRelativePath.Trim());
      string dir = Path.GetDirectoryName(preferredFull) ?? drawingFolder;
      string baseName = Path.GetFileNameWithoutExtension(preferredFull);
      string rootName = StripRevSuffix(baseName);

      foreach (string ext in JtExtensions)
      {
        string jtCandidate = FindHighestRevModelInDir(dir, rootName, ext);
        if (!string.IsNullOrEmpty(jtCandidate))
        {
          return jtCandidate;
        }
      }

      string plainJt = Path.Combine(dir, rootName + ".jt");
      return File.Exists(plainJt) ? plainJt : null;
    }

    /// <summary>
    /// ASSY 형체 OBJ — STP와 동일 basename (_REV/_final 규칙 동일).
    /// </summary>
    public static string ResolveObjSourcePath(string drawingFolder, string preferredRelativePath)
    {
      if (string.IsNullOrWhiteSpace(preferredRelativePath) || string.IsNullOrWhiteSpace(drawingFolder))
      {
        return null;
      }

      string preferredFull = Path.Combine(drawingFolder, preferredRelativePath.Trim());
      string dir = Path.GetDirectoryName(preferredFull) ?? drawingFolder;
      string baseName = Path.GetFileNameWithoutExtension(preferredFull);
      string rootName = StripRevSuffix(baseName);

      foreach (string ext in ObjExtensions)
      {
        string objCandidate = FindHighestRevModelInDir(dir, rootName, ext);
        if (!string.IsNullOrEmpty(objCandidate))
        {
          return objCandidate;
        }
      }

      string plainObj = Path.Combine(dir, rootName + ".obj");
      return File.Exists(plainObj) ? plainObj : null;
    }

    /// <summary>
    /// 형체 표시용 GLB — STP/OBJ와 동일 basename. 폴더 내 임의 GLB 탐색 금지.
    /// </summary>
    public static string ResolveDisplayGlbFromStp(string stpFullPath)
    {
      if (string.IsNullOrWhiteSpace(stpFullPath))
      {
        return null;
      }

      return Path.ChangeExtension(stpFullPath, ".glb");
    }

    public static string ResolveAssyDisplayGlbPath(string drawingFolder, string preferredRelativePath)
    {
      string stp = ResolveStpSourcePath(drawingFolder, preferredRelativePath);
      return ResolveDisplayGlbFromStp(stp);
    }

    /// <summary>
    /// overview 등 STP 없는 단독 GLB. ASSY detail은 ResolveAssyDisplayGlbPath 사용.
    /// </summary>
    public static string ResolveModelFilePath(string drawingFolder, string preferredRelativePath, bool preferStep = false)
    {
      if (string.IsNullOrWhiteSpace(preferredRelativePath) || string.IsNullOrWhiteSpace(drawingFolder))
      {
        return null;
      }

      string preferredFull = Path.Combine(drawingFolder, preferredRelativePath.Trim());
      string dir = Path.GetDirectoryName(preferredFull) ?? drawingFolder;
      string baseName = Path.GetFileNameWithoutExtension(preferredFull);
      string rootName = StripRevSuffix(baseName);

      string[] extOrder = preferStep
          ? new[] { ".stp", ".step", ".glb", ".gltf" }
          : new[] { ".glb", ".gltf", ".stp", ".step" };

      foreach (string ext in extOrder)
      {
        if (GlbExtensions.Contains(ext))
        {
          string glbCandidate = FindHighestRevModelInDir(dir, rootName, ext);
          if (!string.IsNullOrEmpty(glbCandidate))
          {
            return glbCandidate;
          }
        }
        else if (StepExtensions.Contains(ext))
        {
          string stepCandidate = FindHighestRevModelInDir(dir, rootName, ext);
          if (!string.IsNullOrEmpty(stepCandidate))
          {
            return stepCandidate;
          }
        }
      }

      if (File.Exists(preferredFull))
      {
        return preferredFull;
      }

      // basename 불일치 시 폴더 내 _REV 번호가 가장 큰 3D 파일
      if (Directory.Exists(dir))
      {
        foreach (string ext in extOrder)
        {
          string found = FindBestModelInDirByExtension(dir, ext);
          if (!string.IsNullOrEmpty(found))
          {
            return found;
          }
        }
      }

      return null;
    }

    public static string ResolveOverviewModelPath(int srmNum, string preferredName = "srm_overview.glb")
    {
      string folder = Get3DDrawingFolder(srmNum);
      if (!Directory.Exists(folder))
      {
        return null;
      }

      var catalog = Dio3DAssemblyCatalog.Load(srmNum);
      if (!string.IsNullOrWhiteSpace(catalog?.OverviewModel))
      {
        preferredName = catalog.OverviewModel;
      }

      return ResolveModelFilePath(folder, preferredName);
    }

    public static string ResolveTemplateFolder()
    {
      string template = Path.Combine(GetViewerFolder(), "3D_Drawing_Template");
      if (!Directory.Exists(template))
      {
        template = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Viewer3D", "3D_Drawing_Template");
      }
      return template;
    }

    /// <summary>
    /// SRM{n}/3D_Drawing — 설정/정의만 최초 시드. STP·GLB·파일명 변경·삭제 금지.
    /// 사용자 CAD는 Config.ini 3D_DrawingRoot 또는 SRM{n}/3D_Drawing 에 직접 보관.
    /// </summary>
    public static void SeedDrawingFromTemplate(int srmNum)
    {
      string template = ResolveTemplateFolder();
      if (!Directory.Exists(template))
      {
        Console.WriteLine("3D_Drawing 템플릿 없음: " + template);
        return;
      }

      string dest = Get3DDrawingFolder(srmNum);
      Directory.CreateDirectory(dest);

      // 기존 SRM{n}/3D_Drawing 하위 트리는 절대 삭제·재배치하지 않음 (누락 파일만 시드)
      // CleanupLegacyAssemblyFolders 는 수동/도구에서만 호출

      foreach (string file in Directory.GetFiles(template))
      {
        string name = Path.GetFileName(file);
        string destFile = Path.Combine(dest, name);
        if (!File.Exists(destFile))
        {
          File.Copy(file, destFile, false);
        }
      }

      string templateAssy = Path.Combine(template, "assemblies");
      string destAssy = Path.Combine(dest, "assemblies");
      if (!Directory.Exists(templateAssy))
      {
        return;
      }

      Directory.CreateDirectory(destAssy);
      foreach (string folderName in CurrentAssemblyFolderNames)
      {
        string srcSub = Path.Combine(templateAssy, folderName);
        if (!Directory.Exists(srcSub))
        {
          continue;
        }

        string destSub = Path.Combine(destAssy, folderName);
        Directory.CreateDirectory(destSub);
        CopyTemplateTreeIfMissing(srcSub, destSub);
      }
    }

    public static void CleanupLegacyAssemblyFolders(string drawingFolder)
    {
      string assyRoot = Path.Combine(drawingFolder, "assemblies");
      if (!Directory.Exists(assyRoot))
      {
        return;
      }

      foreach (string dir in Directory.GetDirectories(assyRoot))
      {
        string name = Path.GetFileName(dir);
        if (CurrentAssemblyFolderNames.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase)))
        {
          continue;
        }
        if (!LegacyAssemblyFolderNames.Contains(name))
        {
          continue;
        }

        try
        {
          Directory.Delete(dir, true);
          Console.WriteLine("3D_Drawing 구 ASSY 폴더 삭제: " + name);
        }
        catch (Exception ex)
        {
          Console.WriteLine("구 ASSY 폴더 삭제 실패 (" + name + "): " + ex.Message);
        }
      }
    }

    /// <summary>
    /// ELEC_CONVERT 등 제외 패턴 — 수동 정리 전용. 앱 시작·시드에서 자동 호출 금지.
    /// node tools/clean-assy-runtime.mjs --drawing "..." 로만 실행.
    /// </summary>
    public static void PurgeExcludedAssyArtifacts(string drawingFolder)
    {
      if (string.IsNullOrWhiteSpace(drawingFolder))
      {
        return;
      }

      string assyRoot = Path.Combine(drawingFolder, "assemblies");
      if (!Directory.Exists(assyRoot))
      {
        return;
      }

      foreach (string folderName in CurrentAssemblyFolderNames)
      {
        string dir = Path.Combine(assyRoot, folderName);
        if (!Directory.Exists(dir))
        {
          continue;
        }

        foreach (string file in Directory.GetFiles(dir))
        {
          string name = Path.GetFileName(file);
          if (!IsExcludedModelFileName(name, dir))
          {
            continue;
          }

          try
          {
            File.Delete(file);
            Console.WriteLine("3D ASSY 제외 파일 삭제: " + name);
          }
          catch (Exception ex)
          {
            Console.WriteLine("제외 파일 삭제 실패 (" + name + "): " + ex.Message);
          }
        }
      }
    }

    private static void CopyTemplateTreeIfMissing(string templateDir, string destDir)
    {
      if (!Directory.Exists(templateDir))
      {
        return;
      }

      // ASSY 폴더 — 설정/정의만 시드. STP·GLB·ELEC_CONVERT 자동 복사 금지 (사용자가 직접 넣음)
      var allowedSeedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
      {
        "ASSY_IO_PROFILE.json",
        "io_layout.json",
        "io_manifest.json",
        "IO_STP_SENSOR_MAP.txt",
        "PLC_PART_IO_DEFINE.txt",
        "SOLID_EDGE_IO_DEFINE.txt",
        "IO_SIGNAL_LABELS.txt",
      };

      Directory.CreateDirectory(destDir);
      foreach (string file in Directory.GetFiles(templateDir))
      {
        string name = Path.GetFileName(file);
        if (IsExcludedModelFileName(name, destDir))
        {
          continue;
        }
        if (!allowedSeedNames.Contains(name))
        {
          continue;
        }

        string destFile = Path.Combine(destDir, name);
        if (!File.Exists(destFile))
        {
          File.Copy(file, destFile, false);
        }
      }

      foreach (string dir in Directory.GetDirectories(templateDir))
      {
        string destSub = Path.Combine(destDir, Path.GetFileName(dir));
        CopyTemplateTreeIfMissing(dir, destSub);
      }
    }
  }
}
