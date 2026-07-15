using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Point = System.Windows.Point;

namespace gcp_Wpf
{
    public static class cConstDefine
    {
        public static string tr(string key)
        {
            return Properties.Resources.ResourceManager.GetString(key, TranslationSource.Instance.CurrentCulture);          // 다국어 변환 방법
        }

        // Manual Command ENUM

        public const int WM_MYMESSAGE = 0x8000;
        public const int WM_COPYDATA = 0x004A;
        public const int WM_USER = 0x0400;

        public const char STX = '\x02';
        public const char ETX = '\x03';

        public const int IOCOUNT = 16;

        // tp2
        public const int WCSTO = 300;        // 전송 Word 최대 400byte - 22 Bytes + 52 Bytes 
        public const int WCSTOEX = 200;        // 전송 Word 최대 400byte - 22 Bytes + 52 Bytes 
        public const int WCSFROM = 100;      // 수신 Word 최대 200byte - 52 Bytes
 
        // 수동동작 버튼 플래그 - Const 로 사용 -
        public const int MAN_BTN_NONE = 0;
        public const int TRAV_FW_FAST = 1;
        public const int TRAV_FW_SLOW = 2;
        public const int TRAV_BW_FAST = 3;
        public const int TRAV_BW_SLOW = 4;
        public const int LIFT_UP_FAST = 5;
        public const int LIFT_UP_SLOW = 6;
        public const int LIFT_DW_FAST = 7;
        public const int LIFT_DW_SLOW = 8;
        public const int FORK_MOVE_L = 9;
        public const int FORK_MOVE_R = 10;
        public const int FORK_MOVE_C = 11;

        // 페이지 인덱스 DEFINE
        public const int PAGE_AUTO = 0;
        public const int PAGE_MANUAL = 1;
        public const int PAGE_SEMI = 2;
        public const int PAGE_PROHRACK = 3;
        public const int PAGE_COMMSET = 4;
        public const int PAGE_SRMSET = 5;
        public const int PAGE_STATION = 6;
        public const int PAGE_ALARM = 7;
        public const int PAGE_MAIN = 8;
        public const int PAGE_DIO = 9;
        public const int PAGE_TOWCS = 10;
        public const int PAGE_FROMWCS = 11;
        public const int PAGE_SRMIO = 12;
        public const int PAGE_CRANE_OPERATE = 13;
        public const int PAGE_AUTOTEACHING = 14;
        public const int PAGE_3DMAP = 15;

        // 통신 DATA 바이트 사이즈
        public const int DATACOUNT_0X10 = 89;  // 118-29(reserved 제외)  
        public const int DATACOUNT_0X30 = 473;  // 492-19(reserved 제외)  
        public const int DATACOUNT_0X41 = 12;  // reserved 제외 
        public const int DATACOUNT_0X25 = 37;  // 39-2(reserved 제외)
        public const int DATACOUNT_0X80 = 1;   // 
        public const int DATACOUNT_0X92 = 17;  // reserved 제외 
        public const int DATACOUNT_0X94 = 8;  //
        public const int DATACOUNT_0X98 = 2; // 0개 스테이션 기준
        public const int DATACOUNT_0X9C = 1; // 0개 금지렉 기준
        public const int DATACOUNT_0XA7 = 271; // 최대 카운트 기준

        public const string PATH_CONFIG = "CONFIG";
        public const string PATH_PRHRACK = "PRHRACK";
        public const string PATH_RACK = "RACK";
        public const string PATH_LOG = "LOG";
        public const string PATH_ALARMLOG = "ALARM";
        public const string PATH_SRMLOG = "SRM";
		public const string PATH_HOSTLOG = "HOST";
		public const string PATH_DIOLOG = "DIO";
        public const string PATH_OPLOG = "OPERATION";
        public const string PATH_JOBLOG = "JOB";
        public const string PATH_EX = "EXCEPTION";
        public const string PATH_3D_DRAWING = "3D_Drawing";
        public const string INI_3D_DRAWING_ROOT = "3D_DrawingRoot";
        public const string DRAWING_INI = "drawing.ini";
        public const string DEFAULT_3D_PDF = "SRM_3D.pdf";

        public static ushort[] crc16tab = {
			0x0000,0x1021,0x2042,0x3063,0x4084,0x50a5,0x60c6,0x70e7,
			0x8108,0x9129,0xa14a,0xb16b,0xc18c,0xd1ad,0xe1ce,0xf1ef,
			0x1231,0x0210,0x3273,0x2252,0x52b5,0x4294,0x72f7,0x62d6,
			0x9339,0x8318,0xb37b,0xa35a,0xd3bd,0xc39c,0xf3ff,0xe3de,
			0x2462,0x3443,0x0420,0x1401,0x64e6,0x74c7,0x44a4,0x5485,
			0xa56a,0xb54b,0x8528,0x9509,0xe5ee,0xf5cf,0xc5ac,0xd58d,
			0x3653,0x2672,0x1611,0x0630,0x76d7,0x66f6,0x5695,0x46b4,
			0xb75b,0xa77a,0x9719,0x8738,0xf7df,0xe7fe,0xd79d,0xc7bc,
			0x48c4,0x58e5,0x6886,0x78a7,0x0840,0x1861,0x2802,0x3823,
			0xc9cc,0xd9ed,0xe98e,0xf9af,0x8948,0x9969,0xa90a,0xb92b,
			0x5af5,0x4ad4,0x7ab7,0x6a96,0x1a71,0x0a50,0x3a33,0x2a12,
			0xdbfd,0xcbdc,0xfbbf,0xeb9e,0x9b79,0x8b58,0xbb3b,0xab1a,
			0x6ca6,0x7c87,0x4ce4,0x5cc5,0x2c22,0x3c03,0x0c60,0x1c41,
			0xedae,0xfd8f,0xcdec,0xddcd,0xad2a,0xbd0b,0x8d68,0x9d49,
			0x7e97,0x6eb6,0x5ed5,0x4ef4,0x3e13,0x2e32,0x1e51,0x0e70,
			0xff9f,0xefbe,0xdfdd,0xcffc,0xbf1b,0xaf3a,0x9f59,0x8f78,
			0x9188,0x81a9,0xb1ca,0xa1eb,0xd10c,0xc12d,0xf14e,0xe16f,
			0x1080,0x00a1,0x30c2,0x20e3,0x5004,0x4025,0x7046,0x6067,
			0x83b9,0x9398,0xa3fb,0xb3da,0xc33d,0xd31c,0xe37f,0xf35e,
			0x02b1,0x1290,0x22f3,0x32d2,0x4235,0x5214,0x6277,0x7256,
			0xb5ea,0xa5cb,0x95a8,0x8589,0xf56e,0xe54f,0xd52c,0xc50d,
			0x34e2,0x24c3,0x14a0,0x0481,0x7466,0x6447,0x5424,0x4405,
			0xa7db,0xb7fa,0x8799,0x97b8,0xe75f,0xf77e,0xc71d,0xd73c,
			0x26d3,0x36f2,0x0691,0x16b0,0x6657,0x7676,0x4615,0x5634,
			0xd94c,0xc96d,0xf90e,0xe92f,0x99c8,0x89e9,0xb98a,0xa9ab,
			0x5844,0x4865,0x7806,0x6827,0x18c0,0x08e1,0x3882,0x28a3,
			0xcb7d,0xdb5c,0xeb3f,0xfb1e,0x8bf9,0x9bd8,0xabbb,0xbb9a,
			0x4a75,0x5a54,0x6a37,0x7a16,0x0af1,0x1ad0,0x2ab3,0x3a92,
			0xfd2e,0xed0f,0xdd6c,0xcd4d,0xbdaa,0xad8b,0x9de8,0x8dc9,
			0x7c26,0x6c07,0x5c64,0x4c45,0x3ca2,0x2c83,0x1ce0,0x0cc1,
			0xef1f,0xff3e,0xcf5d,0xdf7c,0xaf9b,0xbfba,0x8fd9,0x9ff8,
			0x6e17,0x7e36,0x4e55,0x5e74,0x2e93,0x3eb2,0x0ed1,0x1ef0
		};

		// Window Message Use
		[DllImport("User32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref cConstDefine.COPYDATASTRUCT lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, ref cConstDefine.COPYDATASTRUCT lParam);

        [DllImport("User32.dll", EntryPoint = "FindWindow")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("User32.dll")]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
		[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern bool SetWindowText(IntPtr hwnd, string lpString);


        internal enum SHOW_WINDOW_COMMANDS : int
        {
            HIDE = 0,
            NORMAL = 1,
            MINIMIZED = 2,
            MAXIMIZED = 3,
        }

        internal struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public SHOW_WINDOW_COMMANDS showc_cmd;
            public Point min_position;
            public Point max_position;
            public Rectangle normal_position;
        }


        [DllImport("user32.dll")]
        internal static extern bool GetWindowPlacement(IntPtr handle, ref WINDOWPLACEMENT placement);


        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

        public static IntPtr CreateWpfWindowHandle(string className)
        {
            // Get the HINSTANCE of the current application
            var hInstance = Marshal.GetHINSTANCE(typeof(App).Module);

            // Create the window handle without showing the window
            var handle = CreateWindowEx(
                0,
                className,
                null,
                0,
                0,
                0,
                0,
                0,
                IntPtr.Zero,
                IntPtr.Zero,
                hInstance,
                IntPtr.Zero);

            return handle;
        }

        public struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public string lpData;
        }
    }
}
