using System;

namespace gcp_Wpf.Services
{
  /// <summary>
  /// 바탕화면 STP/GLB 자동 복사 — 비활성. ASSY 폴더는 사용자가 직접 관리.
  /// </summary>
  public static class Dio3DDesktopModelSync
  {
    public static void SyncOcctModelsFromDesktop(int srmNum)
    {
      Console.WriteLine(
          "desktop sync 비활성 — ASSY STP/GLB 는 사용자가 SRM{n}/3D_Drawing/assemblies/ 에 직접 넣으세요.");
    }
  }
}
