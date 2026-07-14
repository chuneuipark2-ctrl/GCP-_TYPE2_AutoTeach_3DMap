using System;

namespace LicenseGeneratorTool
{
    /// <summary>
    /// 라이선스 생성기 사용 가능 기간의 기준일.
    /// 빌드할 때 아래 기준일자를 수정한 뒤 빌드하면, 그 날짜로부터 2주 동안만 기능이 동작합니다.
    /// </summary>
    internal static class BuildInfo
    {
        /// <summary>기준일 (UTC). 이 날짜로부터 2주 동안만 라이선스 생성 기능 동작.</summary>
        public static readonly DateTime BuildDateUtc = new DateTime(2026, 7, 14, 0, 0, 0, DateTimeKind.Utc);
    }
}
