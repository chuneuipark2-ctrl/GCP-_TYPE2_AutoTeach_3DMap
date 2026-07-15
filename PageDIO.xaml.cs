using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml;
using gcp_Wpf.MenuWindow;


namespace gcp_Wpf
{
    /// <summary>
    /// PageDIO.xaml에 대한 상호 작용 논리
    /// </summary>
    /// 
    /// 
    /// 
    public enum DISTATE
    {
        EM_SW,          
        RESET,          
        AUTO,           
        SEMI_AUTO,      
        SF_PLUG,        
        MODEM_EN,       
        MANUAL,         
        MAINT,
        REQ_STOP            // 화재경보 정지요청신호 251107
    }
    public enum DOSTATE
    {
        RED,            
        YELLOW,         
        GREEN,          
        BUZZER,         
        MODEM_PW,       
        BLUE,            
        WHITE,           
        FAN_ONOFF
    }

    unsafe public struct inoutData
    {
        public string str;
        public bool ptrBit;
    }
    public partial class PageDIO : Page
    {

        private List<MonitorData> monitorList = new List<MonitorData>();
        private List<ComboBox> combo_DIList;
        private List<ComboBox> combo_DOList;
        private List<Border> bdr_DI;
        private List<Border> bdr_DO;
        private List<ToggleButton> btnStatus_DI;
        private List<ToggleButton> btnStatus_DO;
        private List<Ellipse> EliStatus_DI;
        private List<Ellipse> EliStatus_DO;
        private List<ToggleButton> btnList_DI;
        private List<Button> btnMaskList_DI;
        private List<ToggleButton> btnList_DO;
        private List<Ellipse> EliList_DI;
        private List<Line> Mask_DI;
        private List<Ellipse> EliList_DO;
        private int dispPageNo;
        private int inputIndex;
        private int outputIndex;
        private int stateIndex;
        private Timer dioTimer = new Timer();
        private singletonClass gClass;

        public PageDIO()
        {
            InitializeComponent();
            gClass = singletonClass.Instance;
            btnStatus_DI = new List<ToggleButton>();
            btnStatus_DO = new List<ToggleButton>();
            EliStatus_DI = new List<Ellipse>();
            EliStatus_DO = new List<Ellipse>();

            for (int index = 0; index <= 15; ++index)
            {
                int num = index;
                ToggleButton toggleButton1 = new ToggleButton();
                toggleButton1.Content = $"DI {num}";
                toggleButton1.Style = (Style)FindResource("ToggleButtonStyle_DI");
                toggleButton1.Margin = new Thickness(5.0);
                ToggleButton element1 = toggleButton1;
                Grid.SetColumn((UIElement)element1, 0);
                Grid.SetRow((UIElement)element1, num);
                element1.Click += new RoutedEventHandler(Btn_Module_DI_Click);
                grid_StatusIO.Children.Add((UIElement)element1);
                btnStatus_DI.Add(element1);
                Ellipse ellipse1 = new Ellipse();
                ellipse1.Width = 20.0;
                ellipse1.Height = 20.0;
                ellipse1.Fill = (Brush)Brushes.Gray;
                ellipse1.Stroke = (Brush)Brushes.Black;
                ellipse1.StrokeThickness = 1.0;
                Ellipse element2 = ellipse1;
                Grid.SetColumn((UIElement)element2, 1);
                Grid.SetRow((UIElement)element2, num);
                grid_StatusIO.Children.Add((UIElement)element2);
                EliStatus_DI.Add(element2);
                ToggleButton toggleButton2 = new ToggleButton();
                toggleButton2.Content = $"DO {num}";
                toggleButton2.Style = (Style)FindResource("ToggleButtonStyle_DI");
                toggleButton2.Margin = new Thickness(5.0);
                ToggleButton element3 = toggleButton2;
                Grid.SetColumn((UIElement)element3, 2);
                Grid.SetRow((UIElement)element3, num);
                element3.Click += new RoutedEventHandler(Btn_Module_DO_Click);
                grid_StatusIO.Children.Add((UIElement)element3);
                btnStatus_DO.Add(element3);
                Ellipse ellipse2 = new Ellipse();
                ellipse2.Width = 20.0;
                ellipse2.Height = 20.0;
                ellipse2.Fill = (Brush)Brushes.Gray;
                ellipse2.Stroke = (Brush)Brushes.Black;
                ellipse2.StrokeThickness = 1.0;
                Ellipse element4 = ellipse2;
                Grid.SetColumn((UIElement)element4, 3);
                Grid.SetRow((UIElement)element4, num);
                grid_StatusIO.Children.Add((UIElement)element4);
                EliStatus_DO.Add(element4);
            }
            btnList_DI = new List<ToggleButton>();
            btnMaskList_DI = new List<Button>();
            btnList_DO = new List<ToggleButton>();
            EliList_DI = new List<Ellipse>();
            Mask_DI = new List<Line>();
            EliList_DO = new List<Ellipse>();
            bdr_DI = new List<Border>();
            InitializeGridButtonLayout();
            Panel.SetZIndex((UIElement)lbl_disableBtn, 1);
            Panel.SetZIndex((UIElement)grid_StatusIO, 0);
            Panel.SetZIndex((UIElement)Grid_SrmIO, 1);
            Panel.SetZIndex((UIElement)Btn_Right, 1);
            dioTimer.Interval = 500.0;
            dioTimer.AutoReset = true;
            dioTimer.Elapsed += new ElapsedEventHandler(dioTimer_Elapsed);
            Btn_OutTest.Click += new RoutedEventHandler(Select_OutputTestMode);
            Btn_SetDIO.Click += new RoutedEventHandler(Select_DIOIndexSetMode);
            SetPageMode(false); // 초기 상태: 관리자 모드 아님
            monitorList.Add(new MonitorData("기상반 비상정지/EMO", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].EM), "INPUT"));
            monitorList.Add(new MonitorData("기상반 키(자동)/AUTO", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].AUTO), "INPUT"));
            monitorList.Add(new MonitorData("기상반 키(수동)/MANUAL", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].MAN), "INPUT"));
            monitorList.Add(new MonitorData("장비준비 피드백/RDF", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].RDF), "INPUT"));
            monitorList.Add(new MonitorData("승강 리미트센서/LLS", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].LST), "INPUT"));
            monitorList.Add(new MonitorData("주행 리미트센서/TLS", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].TST), "INPUT"));
            monitorList.Add(new MonitorData("광모뎀 이상/MFLT", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].MFLT), "INPUT"));
            monitorList.Add(new MonitorData("승강 MMS FLT/LBMS", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].LBMMSF1), "INPUT"));
            monitorList.Add(new MonitorData("주행 MMS FLT/TBMS", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].TBMMSF1), "INPUT"));
            monitorList.Add(new MonitorData("포크 MMS FLT/FBMS", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].FBMMSF1), "INPUT"));
            monitorList.Add(new MonitorData("포크2 MMS FLT/F2BMF", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].FBMMSF2), "INPUT"));
            monitorList.Add(new MonitorData("주행 전진 감속/TDF", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].TDF), "INPUT"));
            monitorList.Add(new MonitorData("주행 후진 감속/TDR", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].TDR), "INPUT"));
            monitorList.Add(new MonitorData("주행 원점/THP", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].THP), "INPUT"));
            monitorList.Add(new MonitorData("승강 상승 감속/LDU", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].LDU), "INPUT"));
            monitorList.Add(new MonitorData("승강 하강 감속/LDD", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].LDD), "INPUT"));
            monitorList.Add(new MonitorData("승강 원점/LHP", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].LHP), "INPUT"));
            monitorList.Add(new MonitorData("화물감지/GOX1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GOX1), "INPUT"));
            monitorList.Add(new MonitorData("화물감지/GOX2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GOX2), "INPUT"));
            monitorList.Add(new MonitorData("소형화물감지/GOXS1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GOXS1), "INPUT"));
            monitorList.Add(new MonitorData("중형화물감지/GOXM1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GOXM1), "INPUT"));
            monitorList.Add(new MonitorData("대형화물감지/GOXH1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GOXH1), "INPUT"));
            monitorList.Add(new MonitorData("소형화물감지/GOXS2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GOXS2), "INPUT"));
            monitorList.Add(new MonitorData("중형화물감지/GOXM2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GOXM2), "INPUT"));
            monitorList.Add(new MonitorData("대형화물감지/GOXH2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GOXH2), "INPUT"));
            monitorList.Add(new MonitorData("화물감지/GOX2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GOX2), "INPUT"));
            monitorList.Add(new MonitorData("가로폭이탈 좌/GWL", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GWL1), "INPUT"));
            monitorList.Add(new MonitorData("가로폭이탈 우/GWR", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GWR1), "INPUT"));
            monitorList.Add(new MonitorData("가로폭이탈 좌/GWL2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GWL2), "INPUT"));
            monitorList.Add(new MonitorData("가로폭이탈 우/GWR2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GWR2), "INPUT"));
            monitorList.Add(new MonitorData("가로폭이탈 좌e/GWLe1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GWLe1), "INPUT"));
            monitorList.Add(new MonitorData("가로폭이탈 우e/GWRe1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GWRe1), "INPUT"));
            monitorList.Add(new MonitorData("가로폭이탈 좌e/GWLe2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GWLe2), "INPUT"));
            monitorList.Add(new MonitorData("가로폭이탈 우e/GWRe2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GWRe2), "INPUT"));
            monitorList.Add(new MonitorData("세로폭이탈 앞좌/GDFL1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GDFL1), "INPUT"));
            monitorList.Add(new MonitorData("세로폭이탈 앞우/GDFR1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GDFR1), "INPUT"));
            monitorList.Add(new MonitorData("세로폭이탈 뒤좌/GDRL1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GDRL1), "INPUT"));
            monitorList.Add(new MonitorData("세로폭이탈 뒤우/GDRR1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GDRR1), "INPUT"));
            monitorList.Add(new MonitorData("세로폭이탈 앞좌/GDFL2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GDFL2), "INPUT"));
            monitorList.Add(new MonitorData("세로폭이탈 앞우/GDFR2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GDFR2), "INPUT"));
            monitorList.Add(new MonitorData("세로폭이탈 뒤좌/GDRL2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GDRL2), "INPUT"));
            monitorList.Add(new MonitorData("세로폭이탈 뒤우/GDRR2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GDRR2), "INPUT"));
            monitorList.Add(new MonitorData("높이이탈 좌/GHL1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GHL1), "INPUT"));
            monitorList.Add(new MonitorData("높이이탈 우/GHR1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GHR1), "INPUT"));
            monitorList.Add(new MonitorData("높이이탈 좌/GHL2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GHL2), "INPUT"));
            monitorList.Add(new MonitorData("높이이탈 우/GHR2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GHR2), "INPUT"));
            monitorList.Add(new MonitorData("포크 끝단 감지 좌/FORKL1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].FOKL1), "INPUT"));
            monitorList.Add(new MonitorData("포크 끝단 감지 우/FORKR1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].FOKR1), "INPUT"));
            monitorList.Add(new MonitorData("포크 끝단 감지 좌/FORKL2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].FOKL2), "INPUT"));
            monitorList.Add(new MonitorData("포크 끝단 감지 우/FORKR2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].FOKR2), "INPUT"));
            monitorList.Add(new MonitorData("포크 센터 좌/FCL1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].FCL1), "INPUT"));
            monitorList.Add(new MonitorData("포크 센터 우/FCR1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].FCR1), "INPUT"));
            monitorList.Add(new MonitorData("포크 센터 좌/FCL2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].FCL2), "INPUT"));
            monitorList.Add(new MonitorData("포크 센터 우/FCR2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].FCR2), "INPUT"));
            monitorList.Add(new MonitorData("포크 HALF 좌/FHL1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].FHL1), "INPUT"));
            monitorList.Add(new MonitorData("포크 HALF 우/FHR1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].FHR1), "INPUT"));
            monitorList.Add(new MonitorData("포크 HALF 좌/FHL2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].FHL2), "INPUT"));
            monitorList.Add(new MonitorData("포크 HALF 우/FHR2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].FHR2), "INPUT"));
            monitorList.Add(new MonitorData("포크 MID 좌/FML1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].FML1), "INPUT"));
            monitorList.Add(new MonitorData("포크 MID 우/FMR1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].FMR1), "INPUT"));
            monitorList.Add(new MonitorData("포크 MID 좌/FML2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].FML2), "INPUT"));
            monitorList.Add(new MonitorData("포크 MID 우/FMR2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].FMR2), "INPUT"));
            monitorList.Add(new MonitorData("포크 엔드 좌/FEL1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].FEL1), "INPUT"));
            monitorList.Add(new MonitorData("포크 엔드 우/FER1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].FER1), "INPUT"));
            monitorList.Add(new MonitorData("포크 엔드 좌/FEL2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].FEL2), "INPUT"));
            monitorList.Add(new MonitorData("포크 엔드 우/FER2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].FER2), "INPUT"));
            monitorList.Add(new MonitorData("이중입고 좌/DSTL1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].DSTL1), "INPUT"));
            monitorList.Add(new MonitorData("이중입고 우/DSTR1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].DSTR1), "INPUT"));
            monitorList.Add(new MonitorData("이중입고 좌/DSTL2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].DSTL2), "INPUT"));
            monitorList.Add(new MonitorData("이중입고 우/DSTR2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].DSTR2), "INPUT"));
            monitorList.Add(new MonitorData("이중입고 좌e/DSTLe1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].DSTLe1), "INPUT"));
            monitorList.Add(new MonitorData("이중입고 우e/DSTRe1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].DSTRe1), "INPUT"));
            monitorList.Add(new MonitorData("이중입고 좌e/DSTLe2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].DSTLe2), "INPUT"));
            monitorList.Add(new MonitorData("이중입고 우e/DSTRe2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].DSTRe2), "INPUT"));
            monitorList.Add(new MonitorData("이중입고 후측 좌/DSTLR1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].DSTLR1), "INPUT"));
            monitorList.Add(new MonitorData("이중입고 후측 우/DSTRR1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].DSTRR1), "INPUT"));
            monitorList.Add(new MonitorData("이중입고 후측 좌/DSTLR2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].DSTLR2), "INPUT"));
            monitorList.Add(new MonitorData("이중입고 후측 우/DSTRR2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].DSTRR2), "INPUT"));
            monitorList.Add(new MonitorData("출고감지 전측 좌/ODSTL1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].ODSTL1), "INPUT"));
            monitorList.Add(new MonitorData("출고감지 전측 우/ODSTR1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].ODSTR1), "INPUT"));
            monitorList.Add(new MonitorData("출고감지 전측 좌/ODSTL2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].ODSTL2), "INPUT"));
            monitorList.Add(new MonitorData("출고감지 전측 우/ODSTR2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].ODSTR2), "INPUT"));
            monitorList.Add(new MonitorData("로프 텐션 감지(앞)/RTF1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].RTF), "INPUT"));
            monitorList.Add(new MonitorData("로프 텐션 감지(뒤)/RTR1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].RTR), "INPUT"));
            monitorList.Add(new MonitorData("승강 세이프티 조속기/GOV", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GOV), "INPUT"));
            monitorList.Add(new MonitorData("CV->SRM 구동가능1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].CVOK1), "8BIT_IN"));
            monitorList.Add(new MonitorData("CV->SRM 구동가능2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].CVOK2), "8BIT_IN"));
            monitorList.Add(new MonitorData("CV->SRM 구동가능3", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].CVOK3), "8BIT_IN"));
            monitorList.Add(new MonitorData("CV->SRM 구동가능4", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].CVOK4), "8BIT_IN"));
            monitorList.Add(new MonitorData("CV->SRM 구동가능5", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].CVOK5), "8BIT_IN"));
            monitorList.Add(new MonitorData("CV->SRM 구동가능6", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].CVOK6), "8BIT_IN"));
            monitorList.Add(new MonitorData("CV->SRM 구동가능7", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].CVOK7), "8BIT_IN"));
            monitorList.Add(new MonitorData("CV->SRM 구동가능8", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].CVOK8), "8BIT_IN"));
            monitorList.Add(new MonitorData("인버터 금지/INHB", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].IINH), "OUTPUT"));
            monitorList.Add(new MonitorData("인버터 리셋/INV Reset", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].DEVICE_RST), "OUTPUT"));
            monitorList.Add(new MonitorData("강제 모드/FCD", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].FCD), "OUTPUT"));
            monitorList.Add(new MonitorData("장비 준비/EQ Ready", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].RDE), "OUTPUT"));
            monitorList.Add(new MonitorData("타워 램프 (적색)/RED", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].RED), "OUTPUT"));
            monitorList.Add(new MonitorData("타워 램프 (노랑)/Yellow", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].YEL), "OUTPUT"));
            monitorList.Add(new MonitorData("타워 램프 (녹색)/Green", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].GRN), "OUTPUT"));
            monitorList.Add(new MonitorData("타워 램프 (부저)/Buzzer", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].SUD), "OUTPUT"));
            monitorList.Add(new MonitorData("SRM->CV 구동금지1", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].CVNO1), "8BIT_OUT"));
            monitorList.Add(new MonitorData("SRM->CV 구동금지2", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].CVNO2), "8BIT_OUT"));
            monitorList.Add(new MonitorData("SRM->CV 구동금지3", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].CVNO3), "8BIT_OUT"));
            monitorList.Add(new MonitorData("SRM->CV 구동금지4", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].CVNO4), "8BIT_OUT"));
            monitorList.Add(new MonitorData("SRM->CV 구동금지5", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].CVNO5), "8BIT_OUT"));
            monitorList.Add(new MonitorData("SRM->CV 구동금지6", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].CVNO6), "8BIT_OUT"));
            monitorList.Add(new MonitorData("SRM->CV 구동금지7", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].CVNO7), "8BIT_OUT"));
            monitorList.Add(new MonitorData("SRM->CV 구동금지8", "", (Func<bool>)(() => gClass.str.SRMIO[gClass.srmNum].CVNO8), "8BIT_OUT"));
            display_Change(0);
            lbl_Left.Content = "IN";
            lbl_Right.Content = "OUT";
            Dio_Change();
            dioTimer.Start();
        }

        private void InitializeGridButtonLayout()
        {
            int diCount = Enum.GetValues(typeof(DISTATE)).Length;
            int doCount = Enum.GetValues(typeof(DOSTATE)).Length;
            int totalRows = Math.Max(diCount, doCount);

            while (Grid_Button.RowDefinitions.Count < totalRows + 1)
                Grid_Button.RowDefinitions.Add(new RowDefinition());

            Grid.SetRowSpan(lbl_disableBtn, totalRows);
            GridSplitter? splitter = Grid_Button.Children.OfType<GridSplitter>().FirstOrDefault();
            if (splitter != null)
                Grid.SetRowSpan(splitter, totalRows + 1);

            for (int index = 0; index < totalRows; ++index)
            {
                int gridRow = index + 1;

                if (index < diCount)
                {
                    string diName = Enum.GetName(typeof(DISTATE), index) ?? $"DI_{index}";
                    ToggleButton diButton = new ToggleButton();
                    diButton.Name = $"Btn_DI{index}";
                    diButton.Content = diName;
                    diButton.Style = (Style)FindResource("ToggleButtonStyle_DI");
                    diButton.Margin = new Thickness(5.0);
                    diButton.BorderBrush = (Brush)null;
                    Grid.SetRow(diButton, gridRow);
                    Grid.SetColumn(diButton, 0);
                    diButton.Click += new RoutedEventHandler(Btn_DI_Click);
                    Grid_Button.Children.Add(diButton);
                    btnList_DI.Add(diButton);

                    Grid indicatorGrid = new Grid();
                    indicatorGrid.Width = 20.0;
                    indicatorGrid.Height = 20.0;
                    Ellipse diEllipse = new Ellipse();
                    diEllipse.Width = 20.0;
                    diEllipse.Height = 20.0;
                    diEllipse.Fill = Brushes.Gray;
                    diEllipse.Stroke = Brushes.Black;
                    diEllipse.StrokeThickness = 1.0;
                    Line maskLine = new Line();
                    maskLine.X1 = 0.0;
                    maskLine.Y1 = 10.0;
                    maskLine.X2 = 20.0;
                    maskLine.Y2 = 10.0;
                    maskLine.Stroke = Brushes.Black;
                    maskLine.StrokeThickness = 1.0;
                    maskLine.Visibility = Visibility.Collapsed;
                    indicatorGrid.Children.Add(diEllipse);
                    indicatorGrid.Children.Add(maskLine);
                    Grid.SetColumn(indicatorGrid, 1);
                    Grid.SetRow(indicatorGrid, gridRow);
                    Grid_Button.Children.Add(indicatorGrid);
                    EliList_DI.Add(diEllipse);
                    Mask_DI.Add(maskLine);

                    Button maskButton = new Button();
                    maskButton.Style = (Style)FindResource("ButtonStyle_DIO");
                    maskButton.BorderBrush = (Brush)null;
                    maskButton.IsEnabled = false;
                    Grid.SetRow(maskButton, gridRow);
                    Grid.SetColumn(maskButton, 1);
                    maskButton.Click += new RoutedEventHandler(Btn_DI_Mask_Click);
                    Grid_Button.Children.Add(maskButton);
                    btnMaskList_DI.Add(maskButton);
                }

                if (index < doCount)
                {
                    string doName = Enum.GetName(typeof(DOSTATE), index) ?? $"DO_{index}";
                    ToggleButton doButton = new ToggleButton();
                    doButton.Name = $"Btn_DO{index}";
                    doButton.Content = doName;
                    doButton.Style = (Style)FindResource("ToggleButtonStyle_DI");
                    doButton.Margin = new Thickness(5.0);
                    doButton.BorderBrush = (Brush)null;
                    Grid.SetRow(doButton, gridRow);
                    Grid.SetColumn(doButton, 3);
                    doButton.Click += new RoutedEventHandler(Btn_DO_Click);
                    Grid_Button.Children.Add(doButton);
                    btnList_DO.Add(doButton);

                    Ellipse doEllipse = new Ellipse();
                    doEllipse.Width = 20.0;
                    doEllipse.Height = 20.0;
                    doEllipse.Fill = Brushes.Gray;
                    doEllipse.Stroke = Brushes.Black;
                    doEllipse.StrokeThickness = 1.0;
                    Grid.SetColumn(doEllipse, 4);
                    Grid.SetRow(doEllipse, gridRow);
                    Grid_Button.Children.Add(doEllipse);
                    EliList_DO.Add(doEllipse);
                }
            }
        }

        public void Dio_Change()
        {
            ref Dio_Packet local = ref gClass.str.DioPacket[gClass.srmNum];
            for (int index = 0; index < btnMaskList_DI.Count; ++index)
                Mask_DI[index].Visibility = local.DISET[index].mask ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Select_OutputTestMode(
#nullable enable
  object sender, RoutedEventArgs e)
        {
            // 관리자 모드가 아니면 동작하지 않음
            if (!gClass.str.GcpInfo.isAdminMode)
            {
                ToggleButton btn = sender as ToggleButton;
                if (btn != null)
                {
                    btn.IsChecked = false;
                }
                return;
            }

            ToggleButton toggleButton = sender as ToggleButton;
            foreach (Border border in bdr_DI)
                border.BorderBrush = null;
            foreach (Button button in btnMaskList_DI)
            {
                button.BorderBrush = (Brush)null;
                button.IsEnabled = false;
            }
            foreach (ToggleButton templatedParent in btnStatus_DI)
            {
                Border name = (Border)templatedParent.Template.FindName("border", (FrameworkElement)templatedParent);
                if (name != null)
                    name.BorderBrush = (Brush)null;
                templatedParent.IsChecked = new bool?(false);
            }
            foreach (ToggleButton templatedParent in btnStatus_DO)
            {
                Border name = (Border)templatedParent.Template.FindName("border", (FrameworkElement)templatedParent);
                if (name != null)
                    name.BorderBrush = (Brush)null;
                templatedParent.IsChecked = new bool?(false);
            }
            if (toggleButton.IsChecked.GetValueOrDefault())
            {
                Btn_SetDIO.IsChecked = new bool?(false);
                Btn_StatusDIO.IsChecked = new bool?(true);
                Panel.SetZIndex((UIElement)grid_StatusIO, 1);
                Panel.SetZIndex((UIElement)Grid_SrmIO, 0);
                Panel.SetZIndex((UIElement)Btn_Right, 0);
                lbl_statusName.Content = "Module IO";
                Panel.SetZIndex((UIElement)lbl_disableBtn, 0);
                Panel.SetZIndex((UIElement)lbl_disableStatusBtn, 1);
                SolidColorBrush solidColorBrush = new SolidColorBrush(Color.FromRgb((byte)184, (byte)216, (byte)252));
                foreach (ToggleButton templatedParent in btnList_DI)
                {
                    Border name = (Border)templatedParent.Template.FindName("border", (FrameworkElement)templatedParent);
                    if (name != null)
                        name.BorderBrush = (Brush)solidColorBrush;
                    templatedParent.IsChecked = new bool?(false);
                }
                foreach (ToggleButton templatedParent in btnList_DO)
                {
                    Border name = (Border)templatedParent.Template.FindName("border", (FrameworkElement)templatedParent);
                    if (name != null)
                        name.BorderBrush = (Brush)solidColorBrush;
                    templatedParent.IsChecked = new bool?(false);
                }
                gClass.str.DioPacket[gClass.srmNum].DO_TESTMODE = true;
            }
            else
            {
                Btn_StatusDIO.IsChecked = new bool?(false);
                Panel.SetZIndex((UIElement)grid_StatusIO, 0);
                Panel.SetZIndex((UIElement)Grid_SrmIO, 1);
                Panel.SetZIndex((UIElement)Btn_Right, 1);
                lbl_statusName.Content = "SRM IO";
                Panel.SetZIndex((UIElement)lbl_disableBtn, 1);
                Panel.SetZIndex((UIElement)lbl_disableStatusBtn, 1);
                foreach (ToggleButton templatedParent in btnList_DI)
                {
                    Border name = (Border)templatedParent.Template.FindName("border", (FrameworkElement)templatedParent);
                    if (name != null)
                        name.BorderBrush = (Brush)null;
                    templatedParent.IsChecked = new bool?(false);
                }
                foreach (ToggleButton templatedParent in btnList_DO)
                {
                    Border name = (Border)templatedParent.Template.FindName("border", (FrameworkElement)templatedParent);
                    if (name != null)
                        name.BorderBrush = (Brush)null;
                    templatedParent.IsChecked = new bool?(false);
                }
                foreach (ToggleButton templatedParent in btnStatus_DI)
                {
                    Border name = (Border)templatedParent.Template.FindName("border", (FrameworkElement)templatedParent);
                    if (name != null)
                        name.BorderBrush = (Brush)null;
                }
                foreach (ToggleButton templatedParent in btnStatus_DO)
                {
                    Border name = (Border)templatedParent.Template.FindName("border", (FrameworkElement)templatedParent);
                    if (name != null)
                        name.BorderBrush = (Brush)null;
                }
                gClass.str.DioPacket[gClass.srmNum].DO_TESTMODE = false;
            }
        }

        private void Select_DIOIndexSetMode(object sender, RoutedEventArgs e)
        {
            // 관리자 모드가 아니면 동작하지 않음
            if (!gClass.str.GcpInfo.isAdminMode)
            {
                ToggleButton btn = sender as ToggleButton;
                if (btn != null)
                {
                    btn.IsChecked = false;
                }
                return;
            }

            ToggleButton toggleButton1 = sender as ToggleButton;
            foreach (ToggleButton toggleButton3 in btnStatus_DI)
                toggleButton3.IsChecked = new bool?(false);
            if (toggleButton1.IsChecked.GetValueOrDefault())
            {
                Btn_OutTest.IsChecked = new bool?(false);
                Btn_StatusDIO.IsChecked = new bool?(true);
                Panel.SetZIndex((UIElement)grid_StatusIO, 1);
                Panel.SetZIndex((UIElement)Grid_SrmIO, 0);
                Panel.SetZIndex((UIElement)Btn_Right, 0);
                lbl_statusName.Content = "Module IO";
                Panel.SetZIndex((UIElement)lbl_disableBtn, 0);
                Panel.SetZIndex((UIElement)lbl_disableStatusBtn, 0);
                SolidColorBrush solidColorBrush = new SolidColorBrush(Color.FromRgb((byte)184, (byte)216, (byte)252));
                foreach (ToggleButton templatedParent in btnList_DI)
                {
                    Border name = (Border)templatedParent.Template.FindName("border", (FrameworkElement)templatedParent);
                    if (name != null)
                        name.BorderBrush = (Brush)solidColorBrush;
                    templatedParent.IsChecked = new bool?(false);
                }
                foreach (ToggleButton templatedParent in btnList_DO)
                {
                    Border name = (Border)templatedParent.Template.FindName("border", (FrameworkElement)templatedParent);
                    if (name != null)
                        name.BorderBrush = (Brush)solidColorBrush;
                    templatedParent.IsChecked = new bool?(false);
                }
                foreach (Button button in btnMaskList_DI)
                {
                    button.BorderBrush = (Brush)solidColorBrush;
                    button.IsEnabled = true;
                }
                gClass.str.DioPacket[gClass.srmNum].DO_TESTMODE = true;
            }
            else
            {
                Btn_StatusDIO.IsChecked = new bool?(false);
                Panel.SetZIndex((UIElement)grid_StatusIO, 0);
                Panel.SetZIndex((UIElement)Grid_SrmIO, 1);
                Panel.SetZIndex((UIElement)Btn_Right, 1);
                lbl_statusName.Content = "SRM IO";
                Panel.SetZIndex((UIElement)lbl_disableBtn, 1);
                Panel.SetZIndex((UIElement)lbl_disableStatusBtn, 1);
                foreach (ToggleButton templatedParent in btnList_DI)
                {
                    Border name = (Border)templatedParent.Template.FindName("border", (FrameworkElement)templatedParent);
                    if (name != null)
                        name.BorderBrush = (Brush)null;
                    templatedParent.IsChecked = new bool?(false);
                }
                foreach (ToggleButton templatedParent in btnList_DO)
                {
                    Border name = (Border)templatedParent.Template.FindName("border", (FrameworkElement)templatedParent);
                    if (name != null)
                        name.BorderBrush = (Brush)null;
                    templatedParent.IsChecked = new bool?(false);
                }
                foreach (ToggleButton templatedParent in btnStatus_DI)
                {
                    Border name = (Border)templatedParent.Template.FindName("border", (FrameworkElement)templatedParent);
                    if (name != null)
                        name.BorderBrush = (Brush)null;
                }
                foreach (ToggleButton templatedParent in btnStatus_DO)
                {
                    Border name = (Border)templatedParent.Template.FindName("border", (FrameworkElement)templatedParent);
                    if (name != null)
                        name.BorderBrush = (Brush)null;
                }
                foreach (Border border in bdr_DI)
                    border.BorderBrush = (Brush)null;
                foreach (Button button in btnMaskList_DI)
                {
                    button.BorderBrush = (Brush)null;
                    button.IsEnabled = false;
                }
                gClass.str.DioPacket[gClass.srmNum].DO_TESTMODE = false;
            }
        }

        private void Btn_StatusDIO_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton toggleButton = sender as ToggleButton;
            Panel.SetZIndex((UIElement)lbl_disableStatusBtn, 1);
            gClass.str.DioPacket[gClass.srmNum].DO_TESTMODE = false;
            foreach (ToggleButton templatedParent in btnList_DI)
            {
                Border name = (Border)templatedParent.Template.FindName("border", (FrameworkElement)templatedParent);
                if (name != null)
                    name.BorderBrush = (Brush)null;
                templatedParent.IsChecked = new bool?(false);
            }
            foreach (ToggleButton templatedParent in btnList_DO)
            {
                Border name = (Border)templatedParent.Template.FindName("border", (FrameworkElement)templatedParent);
                if (name != null)
                    name.BorderBrush = (Brush)null;
                templatedParent.IsChecked = new bool?(false);
            }
            foreach (ToggleButton templatedParent in btnStatus_DI)
            {
                Border name = (Border)templatedParent.Template.FindName("border", (FrameworkElement)templatedParent);
                if (name != null)
                    name.BorderBrush = (Brush)null;
                templatedParent.IsChecked = new bool?(false);
            }
            foreach (ToggleButton templatedParent in btnStatus_DO)
            {
                Border name = (Border)templatedParent.Template.FindName("border", (FrameworkElement)templatedParent);
                if (name != null)
                    name.BorderBrush = (Brush)null;
                templatedParent.IsChecked = new bool?(false);
            }
            foreach (Control control in btnMaskList_DI)
                control.BorderBrush = (Brush)null;
            if (toggleButton.IsChecked.GetValueOrDefault())
            {
                Btn_OutTest.IsChecked = new bool?(false);
                Btn_SetDIO.IsChecked = new bool?(false);
                Panel.SetZIndex((UIElement)grid_StatusIO, 1);
                Panel.SetZIndex((UIElement)Grid_SrmIO, 0);
                Panel.SetZIndex((UIElement)Btn_Right, 0);
                lbl_statusName.Content = "Module IO";
            }
            else
            {
                Btn_OutTest.IsChecked = new bool?(false);
                Btn_SetDIO.IsChecked = new bool?(false);
                Btn_StatusDIO.IsChecked = new bool?(false);
                Panel.SetZIndex((UIElement)grid_StatusIO, 0);
                Panel.SetZIndex((UIElement)Grid_SrmIO, 1);
                Panel.SetZIndex((UIElement)Btn_Right, 1);
                lbl_statusName.Content = "SRM IO";
                Panel.SetZIndex((UIElement)lbl_disableBtn, 1);
            }
        }

        private void Btn_Module_DI_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton toggleButton = sender as ToggleButton;
            ref Dio_Packet local = ref gClass.str.DioPacket[gClass.srmNum];
            Console.WriteLine("Btn Module DI Click " + toggleButton.Name);
            for (int index1 = 0; index1 < btnList_DI.Count; ++index1)
            {
                bool? isChecked = btnList_DI[index1].IsChecked;
                if (isChecked.GetValueOrDefault())
                {
                    if (toggleButton == null)
                        break;
                    uint num = 0;
                    string name = Enum.GetName(typeof(DISTATE), index1);
                    for (int index2 = 0; index2 < btnStatus_DI.Count; ++index2)
                    {
                        isChecked = btnStatus_DI[index2].IsChecked;
                        if (isChecked.GetValueOrDefault())
                            num += (uint)(1 << index2);
                    }
                    local.DISET[index1].pin = num;
                    cIniAccess.Write($"{AppDomain.CurrentDomain.BaseDirectory}\\SRM{gClass.srmNum.ToString()}\\DioInfo.ini", name, "PIN", num.ToString());
                    break;
                }
            }
        }

        private void Btn_Module_DO_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton toggleButton = sender as ToggleButton;
            ref Dio_Packet local = ref gClass.str.DioPacket[gClass.srmNum];
            Console.WriteLine("Btn Module DO Click " + toggleButton.Name);
            for (int index1 = 0; index1 < btnList_DO.Count; ++index1)
            {
                bool? isChecked = btnList_DO[index1].IsChecked;
                if (isChecked.GetValueOrDefault())
                {
                    if (toggleButton == null)
                        break;
                    uint num = 0;
                    string name = Enum.GetName(typeof(DOSTATE), index1);
                    for (int index2 = 0; index2 < btnStatus_DO.Count; ++index2)
                    {
                        isChecked = btnStatus_DO[index2].IsChecked;
                        if (isChecked.GetValueOrDefault())
                            num += (uint)(1 << index2);
                    }
                    local.DOSET[index1].pin = num;
                    cIniAccess.Write($"{AppDomain.CurrentDomain.BaseDirectory}\\SRM{gClass.srmNum.ToString()}\\DioInfo.ini", name, "PIN", num.ToString());
                    break;
                }
            }
        }

        private void Btn_DO_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton toggleButton = sender as ToggleButton;
            ref Dio_Packet local = ref gClass.str.DioPacket[gClass.srmNum];
            Console.WriteLine("Btn DO Click " + toggleButton.Name);
            if (Btn_SetDIO.IsChecked.GetValueOrDefault())
            {
                if (toggleButton == null)
                    return;
                bool valueOrDefault = toggleButton.IsChecked.GetValueOrDefault();
                SolidColorBrush solidColorBrush = new SolidColorBrush(Color.FromRgb((byte)184, (byte)216, (byte)252));
                foreach (ToggleButton templatedParent in btnStatus_DO)
                {
                    Border name = (Border)templatedParent.Template.FindName("border", (FrameworkElement)templatedParent);
                    if (name != null & valueOrDefault)
                    {
                        name.BorderBrush = (Brush)solidColorBrush;
                        templatedParent.IsEnabled = true;
                    }
                    else
                    {
                        name.BorderBrush = (Brush)null;
                        templatedParent.IsEnabled = false;
                    }
                }
                foreach (ToggleButton templatedParent in btnStatus_DI)
                {
                    if (templatedParent.Template.FindName("border", (FrameworkElement)templatedParent) is Border name)
                        name.BorderBrush = (Brush)null;
                    templatedParent.IsChecked = new bool?(false);
                    templatedParent.IsEnabled = false;
                }
                foreach (ToggleButton templatedParent in btnList_DI)
                {
                    if (templatedParent.Template.FindName("border", (FrameworkElement)templatedParent) is Border name)
                        name.BorderBrush = (Brush)solidColorBrush;
                    templatedParent.IsChecked = new bool?(false);
                }
                if (toggleButton == null)
                    return;
                int doLoopCount = Math.Min(btnList_DO.Count, Enum.GetValues(typeof(DOSTATE)).Length);
                for (int index1 = 0; index1 < doLoopCount; ++index1)
                {
                    bool flag = toggleButton.Name == $"Btn_DO{index1}";
                    btnList_DO[index1].IsChecked = new bool?(flag && toggleButton.IsChecked.GetValueOrDefault());
                    if (flag)
                    {
                        uint pin = index1 < local.DOSET.Length ? local.DOSET[index1].pin : 0;
                        if (flag)
                        {
                            for (int index2 = 0; index2 < btnStatus_DO.Count; ++index2)
                                btnStatus_DO[index2].IsChecked = new bool?(valueOrDefault && ((ulong)pin & (ulong)(1 << index2)) > 0UL);
                            for (int index3 = 0; index3 < btnStatus_DI.Count; ++index3)
                                btnStatus_DI[index3].IsChecked = new bool?(false);
                        }
                    }
                }
            }
            else
            {
                bool? isChecked = Btn_OutTest.IsChecked;
                if (!isChecked.GetValueOrDefault() || toggleButton == null)
                    return;
                isChecked = toggleButton.IsChecked;
                bool valueOrDefault = isChecked.GetValueOrDefault();
                for (int index = 0; index < Enum.GetValues(typeof(DOSTATE)).Length; ++index)
                {
                    if (toggleButton.Name == $"Btn_DO{index}")
                    {
                        local.DOSET[index].value = valueOrDefault;
                        Console.WriteLine("Clicked DO : " + toggleButton.Name);
                        break;
                    }
                }
            }
        }

        private void Btn_DI_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton toggleButton1 = sender as ToggleButton;
            ref Dio_Packet local = ref gClass.str.DioPacket[gClass.srmNum];
            Console.WriteLine("Btn DI Click " + toggleButton1.Name);
            if (Btn_SetDIO.IsChecked.GetValueOrDefault())
            {
                if (toggleButton1 == null)
                    return;
                bool valueOrDefault = toggleButton1.IsChecked.GetValueOrDefault();
                SolidColorBrush solidColorBrush = new SolidColorBrush(Color.FromRgb((byte)184, (byte)216, (byte)252));
                foreach (ToggleButton templatedParent in btnStatus_DI)
                {
                    Border name = (Border)templatedParent.Template.FindName("border", (FrameworkElement)templatedParent);
                    if (name != null & valueOrDefault)
                    {
                        name.BorderBrush = (Brush)solidColorBrush;
                        templatedParent.IsEnabled = true;
                    }
                    else
                    {
                        name.BorderBrush = (Brush)null;
                        templatedParent.IsEnabled = false;
                    }
                }
                foreach (ToggleButton templatedParent in btnStatus_DO)
                {
                    if (templatedParent.Template.FindName("border", (FrameworkElement)templatedParent) is Border name)
                        name.BorderBrush = (Brush)null;
                    templatedParent.IsChecked = new bool?(false);
                    templatedParent.IsEnabled = false;
                }
                foreach (ToggleButton templatedParent in btnList_DO)
                {
                    if (templatedParent.Template.FindName("border", (FrameworkElement)templatedParent) is Border name)
                        name.BorderBrush = (Brush)solidColorBrush;
                    templatedParent.IsChecked = new bool?(false);
                }
                int diLoopCount = Math.Min(btnList_DI.Count, Enum.GetValues(typeof(DISTATE)).Length);
                for (int index1 = 0; index1 < diLoopCount; ++index1)
                {
                    bool flag = toggleButton1.Name == $"Btn_DI{index1}";
                    btnList_DI[index1].IsChecked = new bool?(valueOrDefault & flag);
                    if (flag)
                    {
                        uint pin = index1 < local.DISET.Length ? local.DISET[index1].pin : 0;
                        for (int index2 = 0; index2 < btnStatus_DI.Count; ++index2)
                        {
                            btnStatus_DI[index2].IsChecked = new bool?(valueOrDefault && ((ulong)pin & (ulong)(1 << index2)) > 0);
                        }
                        foreach (ToggleButton toggleButton2 in btnStatus_DO)
                            toggleButton2.IsChecked = new bool?(false);
                    }
                }
            }
            else
            {
                bool? isChecked = Btn_OutTest.IsChecked;
                if (!isChecked.GetValueOrDefault() || toggleButton1 == null)
                    return;
                isChecked = toggleButton1.IsChecked;
                bool valueOrDefault = isChecked.GetValueOrDefault();
                for (int index = 0; index < Enum.GetValues(typeof(DISTATE)).Length; ++index)
                {
                    if (toggleButton1.Name == $"Btn_DI{index}")
                    {
                        local.DISET[index].value = valueOrDefault;
                        break;
                    }
                }
            }
        }

        private void Btn_DI_Mask_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            ref Dio_Packet local = ref gClass.str.DioPacket[gClass.srmNum];
            Console.WriteLine("Btn DI Mask Click " + button.Name);
            for (int index = 0; index < btnMaskList_DI.Count; ++index)
            {
                if (button == btnMaskList_DI[index])
                {
                    local.DISET[index].mask = !local.DISET[index].mask;
                    bool mask = local.DISET[index].mask;
                    Mask_DI[index].Visibility = local.DISET[index].mask ? Visibility.Visible : Visibility.Collapsed;
                    string name = Enum.GetName(typeof(DISTATE), index);
                    cIniAccess.Write($"{AppDomain.CurrentDomain.BaseDirectory}\\SRM{gClass.srmNum.ToString()}\\DioInfo.ini", name, "MASK", mask.ToString());
                    break;
                }
            }
        }

        private void dioTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ((DispatcherObject)this).Dispatcher.Invoke((Action)(() =>
            {
                try
                {
                    ref Dio_Packet local = ref gClass.str.DioPacket[gClass.srmNum];
                    for (int index = 0; index < EliStatus_DI.Count; ++index)
                    {
                        Brush onBrush = Brushes.GreenYellow;
                        Brush offBrush = Brushes.Gray;
                        string strEnum = ((DISTATE)index).ToString();
                        if (strEnum == "EM_SW" || strEnum == "SF_PLUG")
                        {
                            onBrush = Brushes.Gray;
                            offBrush = Brushes.GreenYellow;
                        }

                        if (((long)local.DISTATUS & (long)(1 << index)) > 0L)
                            EliStatus_DI[index].Fill = (Brush)Brushes.GreenYellow;
                        else
                            EliStatus_DI[index].Fill = (Brush)Brushes.Gray;
                    }
                    for (int index = 0; index < EliStatus_DO.Count; ++index)
                    {
                        if (((long)local.DOSTATUS & (long)(1 << index)) > 0L)
                            EliStatus_DO[index].Fill = (Brush)Brushes.GreenYellow;
                        else
                            EliStatus_DO[index].Fill = (Brush)Brushes.Gray;
                    }
                    for (int index = 0; index < Enum.GetValues(typeof(DISTATE)).Length; ++index)
                    {
                        if (local.DISET[index].value)
                            EliList_DI[index].Fill = (Brush)Brushes.GreenYellow;
                        else
                            EliList_DI[index].Fill = (Brush)Brushes.Gray;
                    }
                    for (int index = 0; index < Enum.GetValues(typeof(DOSTATE)).Length; ++index)
                    {
                        if (local.DOSET[index].value)
                            EliList_DO[index].Fill = (Brush)Brushes.GreenYellow;
                        else
                            EliList_DO[index].Fill = (Brush)Brushes.Gray;
                    }
                    for (int index = 0; index < monitorList.Count; ++index)
                    {
                        if (monitorList[index].BitValue)
                        {
                            monitorList[index].lbl_value.Content = "True";
                            monitorList[index].lbl_value.Foreground = (Brush)Brushes.GreenYellow;
                            monitorList[index].lbl_value.FontSize = 15.0;
                        }
                        else
                        {
                            monitorList[index].lbl_value.Content = "False";
                            monitorList[index].lbl_value.Foreground = (Brush)Brushes.White;
                            monitorList[index].lbl_value.FontSize = 12.0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    cIniAccess.SaveExLog(0, "EXCEPTION - PageDIOTimer : " + ex.Message);
                }
            }));
        }


        private void Btn_Right_Click(object sender, RoutedEventArgs e)
        {
            int num1 = monitorList.Where<MonitorData>((Func<MonitorData, bool>)(x => x.typeStr == "INPUT")).Skip<MonitorData>(inputIndex).Count<MonitorData>();
            int num2 = monitorList.Where<MonitorData>((Func<MonitorData, bool>)(x => x.typeStr == "OUTPUT")).Skip<MonitorData>(outputIndex).Count<MonitorData>();
            int num3 = monitorList.Where<MonitorData>((Func<MonitorData, bool>)(x => x.typeStr == "STATE")).Skip<MonitorData>(stateIndex).Count<MonitorData>();
            if (num1 > 0)
            {
                if (num2 > 0)
                {
                    display_Change(0);
                    lbl_Left.Content = "IN";
                    lbl_Right.Content = "OUT";
                }
                else
                {
                    display_Change(1);
                    lbl_Left.Content = "IN";
                    lbl_Right.Content = "IN";
                }
            }
            else if (num2 > 0)
            {
                display_Change(2);
                lbl_Left.Content = "OUT";
                lbl_Right.Content = "OUT";
            }
            else if (num3 > 0)
            {
                display_Change(3);
                lbl_Left.Content = "STATE";
                lbl_Right.Content = "STATE";
            }
            else
            {
                display_Change(4);
                lbl_Left.Content = "8BIT IN";
                lbl_Right.Content = "8BIT OUT";
                dispPageNo = 0;
                inputIndex = 0;
                outputIndex = 0;
                stateIndex = 0;
            }
        }


        // I/O 모니터링 디스플레이 맵핑 Change 함수--------------------------------------
        private void display_Change(int pageType)
        {
            ((DispatcherObject)this).Dispatcher.Invoke((Action)(() =>
            {
                List<MonitorData> list1 = monitorList.Where<MonitorData>((Func<MonitorData, bool>)(x => x.typeStr == "INPUT")).Skip<MonitorData>(inputIndex).ToList<MonitorData>();
                List<MonitorData> list2 = monitorList.Where<MonitorData>((Func<MonitorData, bool>)(x => x.typeStr == "OUTPUT")).Skip<MonitorData>(outputIndex).ToList<MonitorData>();
                List<MonitorData> list3 = monitorList.Where<MonitorData>((Func<MonitorData, bool>)(x => x.typeStr == "STATE")).Skip<MonitorData>(stateIndex).ToList<MonitorData>();
                ++dispPageNo;
                lbl_ioSeq.Content = ("I/O Page " + dispPageNo.ToString());
                int count = grid_SrmIO.RowDefinitions.Count;
                grid_SrmIO.Children.Clear();
                Console.WriteLine("pageType  " + pageType.ToString());
                for (int index = 0; index < count; ++index)
                {
                    if (grid_SrmIO.ColumnDefinitions.Count < 4)
                    {
                        int num = (int)VarMessageBox.Show("CheckIndex", "SRM IO Column Count Error", VarMessageBoxButton.OK);
                        break;
                    }
                    switch (pageType)
                    {
                        case 0:
                            if (list1.Count > index)
                            {
                                Grid.SetRow((UIElement)list1[index].lbl_type, index);
                                Grid.SetColumn((UIElement)list1[index].lbl_type, 0);
                                Grid.SetRow((UIElement)list1[index].lbl_value, index);
                                Grid.SetColumn((UIElement)list1[index].lbl_value, 1);
                                SafeAddToGrid((UIElement)list1[index].lbl_type, grid_SrmIO);
                                SafeAddToGrid((UIElement)list1[index].lbl_value, grid_SrmIO);
                                ++inputIndex;
                                break;
                            }
                            break;
                        case 1:
                            if (list1.Count > index)
                            {
                                Grid.SetRow((UIElement)list1[index].lbl_type, index);
                                Grid.SetColumn((UIElement)list1[index].lbl_type, 0);
                                Grid.SetRow((UIElement)list1[index].lbl_value, index);
                                Grid.SetColumn((UIElement)list1[index].lbl_value, 1);
                                SafeAddToGrid((UIElement)list1[index].lbl_type, grid_SrmIO);
                                SafeAddToGrid((UIElement)list1[index].lbl_value, grid_SrmIO);
                                ++inputIndex;
                                break;
                            }
                            break;
                        case 2:
                            if (list2.Count > index)
                            {
                                Grid.SetRow((UIElement)list2[index].lbl_type, index);
                                Grid.SetColumn((UIElement)list2[index].lbl_type, 0);
                                Grid.SetRow((UIElement)list2[index].lbl_value, index);
                                Grid.SetColumn((UIElement)list2[index].lbl_value, 1);
                                SafeAddToGrid((UIElement)list2[index].lbl_type, grid_SrmIO);
                                SafeAddToGrid((UIElement)list2[index].lbl_value, grid_SrmIO);
                                ++outputIndex;
                                break;
                            }
                            break;
                        case 3:
                            if (list3.Count > index)
                            {
                                Grid.SetRow((UIElement)list3[index].lbl_type, index);
                                Grid.SetColumn((UIElement)list3[index].lbl_type, 0);
                                Grid.SetRow((UIElement)list3[index].lbl_value, index);
                                Grid.SetColumn((UIElement)list3[index].lbl_value, 1);
                                SafeAddToGrid((UIElement)list3[index].lbl_type, grid_SrmIO);
                                SafeAddToGrid((UIElement)list3[index].lbl_value, grid_SrmIO);
                                ++stateIndex;
                                break;
                            }
                            break;
                        case 4:
                            List<MonitorData> list4 = monitorList.Where<MonitorData>((Func<MonitorData, bool>)(x => x.typeStr == "8BIT_IN")).ToList<MonitorData>();
                            if (list4.Count > index)
                            {
                                Grid.SetRow((UIElement)list4[index].lbl_type, index);
                                Grid.SetColumn((UIElement)list4[index].lbl_type, 0);
                                Grid.SetRow((UIElement)list4[index].lbl_value, index);
                                Grid.SetColumn((UIElement)list4[index].lbl_value, 1);
                                SafeAddToGrid((UIElement)list4[index].lbl_type, grid_SrmIO);
                                SafeAddToGrid((UIElement)list4[index].lbl_value, grid_SrmIO);
                                break;
                            }
                            break;
                    }
                }
                for (int index = 0; index < grid_SrmIO.RowDefinitions.Count; ++index)
                {
                    if (grid_SrmIO.ColumnDefinitions.Count < 4)
                    {
                        int num = (int)VarMessageBox.Show("CheckIndex", "SRM IO Column Count Error", VarMessageBoxButton.OK);
                        break;
                    }
                    switch (pageType)
                    {
                        case 0:
                            if (list2.Count > index)
                            {
                                Grid.SetRow((UIElement)list2[index].lbl_type, index);
                                Grid.SetColumn((UIElement)list2[index].lbl_type, 2);
                                Grid.SetRow((UIElement)list2[index].lbl_value, index);
                                Grid.SetColumn((UIElement)list2[index].lbl_value, 3);
                                SafeAddToGrid((UIElement)list2[index].lbl_type, grid_SrmIO);
                                SafeAddToGrid((UIElement)list2[index].lbl_value, grid_SrmIO);
                                ++outputIndex;
                                break;
                            }
                            break;
                        case 1:
                            if (list1.Count > index + count)
                            {
                                Grid.SetRow((UIElement)list1[index + count].lbl_type, index);
                                Grid.SetColumn((UIElement)list1[index + count].lbl_type, 2);
                                Grid.SetRow((UIElement)list1[index + count].lbl_value, index);
                                Grid.SetColumn((UIElement)list1[index + count].lbl_value, 3);
                                SafeAddToGrid((UIElement)list1[index + count].lbl_type, grid_SrmIO);
                                SafeAddToGrid((UIElement)list1[index + count].lbl_value, grid_SrmIO);
                                ++inputIndex;
                                break;
                            }
                            break;
                        case 2:
                            if (list2.Count > index + count)
                            {
                                Grid.SetRow((UIElement)list2[index + count].lbl_type, index);
                                Grid.SetColumn((UIElement)list2[index + count].lbl_type, 2);
                                Grid.SetRow((UIElement)list2[index + count].lbl_value, index);
                                Grid.SetColumn((UIElement)list2[index + count].lbl_value, 3);
                                SafeAddToGrid((UIElement)list2[index + count].lbl_type, grid_SrmIO);
                                SafeAddToGrid((UIElement)list2[index + count].lbl_value, grid_SrmIO);
                                ++outputIndex;
                                break;
                            }
                            break;
                        case 3:
                            if (list3.Count > index + count)
                            {
                                Grid.SetRow((UIElement)list3[index + count].lbl_type, index);
                                Grid.SetColumn((UIElement)list3[index + count].lbl_type, 2);
                                Grid.SetRow((UIElement)list3[index + count].lbl_value, index);
                                Grid.SetColumn((UIElement)list3[index + count].lbl_value, 3);
                                SafeAddToGrid((UIElement)list3[index + count].lbl_type, grid_SrmIO);
                                SafeAddToGrid((UIElement)list3[index + count].lbl_value, grid_SrmIO);
                                ++stateIndex;
                                break;
                            }
                            break;
                        case 4:
                            List<MonitorData> list5 = monitorList.Where<MonitorData>((Func<MonitorData, bool>)(x => x.typeStr == "8BIT_OUT")).ToList<MonitorData>();
                            if (list5.Count > index)
                            {
                                Grid.SetRow((UIElement)list5[index].lbl_type, index);
                                Grid.SetColumn((UIElement)list5[index].lbl_type, 2);
                                Grid.SetRow((UIElement)list5[index].lbl_value, index);
                                Grid.SetColumn((UIElement)list5[index].lbl_value, 3);
                                SafeAddToGrid((UIElement)list5[index].lbl_type, grid_SrmIO);
                                SafeAddToGrid((UIElement)list5[index].lbl_value, grid_SrmIO);
                                break;
                            }
                            break;
                    }
                }
            }));
        }

        private void SafeAddToGrid(UIElement element, Grid grid)
        {
            if (element == null)
                return;
            if (VisualTreeHelper.GetParent((DependencyObject)element) is Panel parent)
                parent.Children.Remove(element);
            grid.Children.Add(element);
        }

        public void SetPageMode(bool isAdmin)
        {
            // SET과 TEST 버튼은 관리자 모드에서만 활성화
            Btn_SetDIO.IsEnabled = isAdmin;
            Btn_OutTest.IsEnabled = isAdmin;
        }
    }
}
