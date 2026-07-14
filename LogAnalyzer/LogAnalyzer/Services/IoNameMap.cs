namespace LogAnalyzer.Services
{
    /// <summary>
    /// udpClientClass RX 0x30 상태 패킷의 dInput[439..454], dOutput[459..463] 비트별 IO 이름.
    /// refState.dInput[i] = dataArray[439+i], refState.dOutput[i] = dataArray[459+i] 참조.
    /// </summary>
    public static class IoNameMap
    {
        public const int DInputOffset = 439;
        public const int DOutputOffset = 459;
        public const int DInputBytes = 16;
        public const int DOutputBytes = 5;

        /// <summary>DI 이름 [byteIndex][bit0..7]</summary>
        public static readonly string[][] DiNames = new string[][]
        {
            new[] { "EM", "AUTO", "MAN", "RDF", "LST", "TST", "MFLT", "GOV" },           // dInput[0]
            new[] { "MCF", "MC1F", "PDR", "PTH", "MCTMF", "MCFMF", "T1PSF", "T1OSO" },   // 1
            new[] { "LBMMSF1", "TBMMSF1", "FBMMSF1", "CPTF", "TDF", "TDR", "THP", "TSP" },
            new[] { "CFLT", "CRD", "MC2F", "MCLMF", "MCFM2F", "L1PSF", "L1OSO", "FBMMSF2" },
            new[] { "CVOK1", "CVOK2", "CVOK3", "CVOK4", "CVOK5", "CVOK6", "CVOK7", "CVOK8" },
            new[] { "GRA", "DEVICE_FLT", "TS1_ENB", "TS2_ENB", "M_EST", "M_KEYSW", "M_FLT", "LBMMSF2" },
            new[] { "TBMMSF2", "F1ENC", "LDU", "LDD", "LHP", "LSP", "GOX1", "GOXH1" },
            new[] { "GOXM1", "GOXS1", "GWL1", "GWR1", "GWLe1", "GWRe1", "GDFL1", "GDFR1" },
            new[] { "GDRL1", "GDRR1", "GHL1", "GHR1", "FOKL1", "FOKR1", "FEL1", "FER1" },
            new[] { "FCL1", "FCR1", "DSTL1", "DSTR1", "DSTLe1", "DSTRe1", "RTF", "RTR" },
            new[] { "RTF2", "RTR2", "GOX2", "GOXH2", "GOXM2", "GOXS2", "GWL2", "GWR2" },
            new[] { "GWLe2", "GWRe2", "GDFL2", "GDFR2", "GDRL2", "GDRR2", "GHL2", "GHR2" },
            new[] { "FOKL2", "FOKR2", "FEL2", "FER2", "FCL2", "FCR2", "DSTL2", "DSTR2" },
            new[] { "DSTLe2", "DSTRe2", "ODSTL1", "ODSTR1", "DSTLR1", "DSTRR1", "ODSTL2", "ODSTR2" },
            new[] { "DSTLR2", "DSTRR2", "FML1", "FMR1", "FHL1", "FHR1", "FML2", "FMR2" },
            new[] { "FHL2", "FHR2", "", "", "", "", "", "" },
        };

        /// <summary>DO 이름 [byteIndex][bit0..7]</summary>
        public static readonly string[][] DoNames = new string[][]
        {
            new[] { "IINH", "FCD", "RDE", "RED", "YEL", "GRN", "SUD", "MCE" },           // dOutput[0]
            new[] { "MCUB", "PLAMP", "PFAN", "MCTM", "MCFM1", "T1FSPC", "T1SPO", "MCFB1" },
            new[] { "COSE", "CENB", "CRST", "MCLM", "MCFM2", "LFSPC", "LSPO", "MCFB2" },
            new[] { "CVNO1", "CVNO2", "CVNO3", "CVNO4", "CVNO5", "CVNO6", "CVNO7", "CVNO8" },
            new[] { "GRA_RST", "DEVICE_RST", "LED_RD", "LED_GR", "LED_BU", "", "", "" },
        };
    }
}
