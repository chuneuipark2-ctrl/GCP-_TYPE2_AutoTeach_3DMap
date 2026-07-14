using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace gcp_Wpf.Services
{
  public sealed class Dio3DAssemblyCatalog
  {
    public string OverviewModel { get; set; } = "srm_overview.glb";
    public List<Dio3DAssemblyEntry> Assemblies { get; set; } = new List<Dio3DAssemblyEntry>();

    public static Dio3DAssemblyCatalog Load(int srmNum)
    {
      string path = Path.Combine(Dio3DDrawingPath.Get3DDrawingFolder(srmNum), "assemblies.json");
      if (!File.Exists(path))
      {
        return new Dio3DAssemblyCatalog();
      }

      string json = File.ReadAllText(path);
      var options = new JsonSerializerOptions
      {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
      };
      return JsonSerializer.Deserialize<Dio3DAssemblyCatalog>(json, options) ?? new Dio3DAssemblyCatalog();
    }

    public Dio3DAssemblyEntry FindById(string assyId)
    {
      return Assemblies.FirstOrDefault(x => string.Equals(x.Id, assyId, StringComparison.OrdinalIgnoreCase));
    }

    public HashSet<string> GetSignalKeys(string assyId)
    {
      var entry = FindById(assyId);
      if (entry == null)
      {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      }

      return new HashSet<string>(entry.SignalKeys ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
    }
  }

  public sealed class Dio3DAssemblyEntry
  {
    public string Id { get; set; }
    public string Label { get; set; }
    public string DrawingNo { get; set; }
    public string DetailModel { get; set; }
    public string Layout { get; set; }
    public string MeshName { get; set; }
    public Dio3DCamera Camera { get; set; }
    public List<string> SignalKeys { get; set; } = new List<string>();

    /** true면 GLB(ImageToStl) 대신 STP 우선 — part 이름으로 I/O 매칭 */
    [JsonPropertyName("preferStep")]
    public bool PreferStep { get; set; }

    [JsonPropertyName("glbMeshMap")]
    public string GlbMeshMap { get; set; }

  /** 형체 GLB + STP 기반 io_manifest.json 경로 (선택) */
    [JsonPropertyName("ioManifest")]
    public string IoManifest { get; set; }
  }

  public sealed class Dio3DCamera
  {
    [JsonPropertyName("pos")]
    public double[] Pos { get; set; }

    [JsonPropertyName("target")]
    public double[] Target { get; set; }
  }
}
