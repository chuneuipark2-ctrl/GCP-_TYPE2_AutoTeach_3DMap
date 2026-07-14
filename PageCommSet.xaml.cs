using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Globalization;
using gcp_Wpf.Properties;
using System.IO;
using System.Runtime.Serialization;
using System.Data;
using System.Formats.Asn1;
using gcp_Wpf.MenuWindow;

namespace gcp_Wpf
{
    /// <summary>
    /// PageCommSet.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PageCommSet : Page
    {
        singletonClass gClass;
        MainWindow pMain;

        public PageCommSet(MainWindow parent)
        {
            // SingleTone Test
            gClass = singletonClass.Instance;
            InitializeComponent();
            pMain = parent;
            SetPageMode(false);
            changeData();

            string progType;
#if DONGWON
            progType = "DONGWON Systems";
#else
            progType = "STANDARD";
#endif

            string filePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            // 파일 정보 가져오기
            FileInfo fileInfo = new FileInfo(filePath);

            // 생성일자 가져오기
            DateTime creationDate = fileInfo.CreationTime;

            // 수정일자 가져오기
            DateTime lastModifiedDate = fileInfo.LastWriteTime;

            // 결과 출력
            Console.WriteLine("파일 생성일: " + creationDate);
            Console.WriteLine("파일 수정일: " + lastModifiedDate);

            if(lastModifiedDate.ToString() != gClass.str.GcpInfo.buildDate)
            {
                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "GCPINFO", "BuildDate", lastModifiedDate.ToString());
            }
            lbl_buildDate.Content = lastModifiedDate.ToString("yyyy-MM-dd HH:mm:ss  -  " + progType);
        }

        public void changeData()                // 크레인 선택 변경 시 표시 데이터 초기화
        {
            Console.WriteLine("changeData Called " + gClass.str.GcpInfo.language);

            edit_srmIP.Text = gClass.str.SrmInfo[gClass.srmNum].srmIP;
            edit_srmPORT.Text = gClass.str.SrmInfo[gClass.srmNum].srmPORT.ToString();

            edit_wcsPORT.Text = gClass.str.SrmInfo[gClass.srmNum].hostPORT.ToString();
            edit_wcsTimeout.Text = gClass.str.SrmInfo[gClass.srmNum].hostTimeout.ToString();

            Btn_hbCheck.IsChecked = (gClass.str.SrmInfo[gClass.srmNum].heartBeatCheck != 0) ? true : false;
            edit_hbTimeout.Text = gClass.str.SrmInfo[gClass.srmNum].heartBeatTimeout.ToString();

            Btn_dioUse.IsChecked = (gClass.str.SrmInfo[gClass.srmNum].dioUse != 0) ? true : false;
            Btn_dioUse_Changed();

            Btn_sfUse.IsChecked = (gClass.str.SrmInfo[gClass.srmNum].sfUse != 0) ? true : false;


            edit_COMPORT.Text = gClass.str.SrmInfo[gClass.srmNum].comPORT;
            edit_BAUDRATE.Text = gClass.str.SrmInfo[gClass.srmNum].baudRate.ToString();
            edit_PARITY.Text = gClass.str.SrmInfo[gClass.srmNum].parity.ToString();
            edit_DATABIT.Text = gClass.str.SrmInfo[gClass.srmNum].dataBit.ToString();
            edit_STOPBIT.Text = gClass.str.SrmInfo[gClass.srmNum].stopBit.ToString();

            edit_DioIP.Text = gClass.str.SrmInfo[gClass.srmNum].dioIP.ToString();
            edit_DioID.Text = gClass.str.SrmInfo[gClass.srmNum].dioID.ToString();

            Btn_modemErrorCheck.IsChecked = (gClass.str.SrmInfo[gClass.srmNum].modemErrorCheck != 0) ? true : false;

            combo_SrmCount.SelectedIndex = gClass.str.GcpInfo.srmCount - 1;
            combo_dioType.SelectedIndex = gClass.str.SrmInfo[gClass.srmNum].dioType;

            SetPageMode(gClass.str.GcpInfo.isAdminMode);
        }

        public void SetPageMode(bool isAdmin)
        {
            // 페이지 전체를 비활성화/활성화
            this.IsEnabled = isAdmin;
        }

        private void Button_Click(object sender, RoutedEventArgs e)                         // 설정 정보 저장버튼 클릭 이벤트
        {
            //TranslationSource.Instance
            VarMessageBoxResult result = VarMessageBox.Show(cConstDefine.tr("저장"), cConstDefine.tr("저장문구"), VarMessageBoxButton.OKCancel);

            if (result == VarMessageBoxResult.OK)
            {
                // OK button clicked
                // INI 파일 설정 정보 공유 구조체로 초기화  (순서 중요)

                // 설정 값 내부 변수 저장

                gClass.str.SrmInfo[gClass.srmNum].srmIP = edit_srmIP.Text;
                gClass.str.SrmInfo[gClass.srmNum].srmPORT = int.Parse(edit_srmPORT.Text);

                gClass.str.SrmInfo[gClass.srmNum].hostPORT = int.Parse(edit_wcsPORT.Text);
                gClass.str.SrmInfo[gClass.srmNum].hostTimeout = uint.Parse(edit_wcsTimeout.Text);

                bool bChk = Btn_dioUse.IsChecked.GetValueOrDefault(false);
                gClass.str.SrmInfo[gClass.srmNum].dioUse = bChk? 1 : 0;

                bool bChkHb = Btn_hbCheck.IsChecked.GetValueOrDefault(false);
                gClass.str.SrmInfo[gClass.srmNum].heartBeatCheck = bChkHb ? 1 : 0;
                gClass.str.SrmInfo[gClass.srmNum].heartBeatTimeout = int.Parse(edit_hbTimeout.Text);

                bChk = Btn_sfUse.IsChecked.GetValueOrDefault(false);
                gClass.str.SrmInfo[gClass.srmNum].sfUse = bChk ? 1 : 0;

                gClass.str.SrmInfo[gClass.srmNum].dioIP = edit_DioIP.Text;
                gClass.str.SrmInfo[gClass.srmNum].dioID = int.Parse(edit_DioID.Text);

                gClass.str.SrmInfo[gClass.srmNum].comPORT = edit_COMPORT.Text;
                gClass.str.SrmInfo[gClass.srmNum].baudRate = int.Parse(edit_BAUDRATE.Text);
                gClass.str.SrmInfo[gClass.srmNum].parity = int.Parse(edit_PARITY.Text);
                gClass.str.SrmInfo[gClass.srmNum].dataBit = int.Parse(edit_DATABIT.Text);
                gClass.str.SrmInfo[gClass.srmNum].stopBit = int.Parse(edit_STOPBIT.Text);

                bool bChkModem = Btn_modemErrorCheck.IsChecked.GetValueOrDefault(false);
                gClass.str.SrmInfo[gClass.srmNum].modemErrorCheck = bChkModem ? 1 : 0;

                gClass.str.GcpInfo.srmCount = combo_SrmCount.SelectedIndex + 1;   // 최소 인덱스와 실제 데이터 카운트 차이 +1 
                gClass.str.SrmInfo[gClass.srmNum].dioType = combo_dioType.SelectedIndex;

                // 설정 값 INI 파일 저장
                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "SRMCOMM_" + gClass.srmNum, "SRMIP", gClass.str.SrmInfo[gClass.srmNum].srmIP);
                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "SRMCOMM_" + gClass.srmNum, "SRMPORT", gClass.str.SrmInfo[gClass.srmNum].srmPORT.ToString());
                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "SRMCOMM_" + gClass.srmNum, "MODEMERRORCHECK", gClass.str.SrmInfo[gClass.srmNum].modemErrorCheck.ToString());

                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "HOSTCOMM_" + gClass.srmNum, "HOSTPORT", gClass.str.SrmInfo[gClass.srmNum].hostPORT.ToString());
                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "HOSTCOMM_" + gClass.srmNum, "HOSTTIMEOUT", gClass.str.SrmInfo[gClass.srmNum].hostTimeout.ToString());
                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "HOSTCOMM_" + gClass.srmNum, "HEARTBEATCHECK", gClass.str.SrmInfo[gClass.srmNum].heartBeatCheck.ToString());
                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "HOSTCOMM_" + gClass.srmNum, "HEARTBEATTIMEOUT", gClass.str.SrmInfo[gClass.srmNum].heartBeatTimeout.ToString());

                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "DIOCOMM_" + gClass.srmNum, "DIOUSE", gClass.str.SrmInfo[gClass.srmNum].dioUse.ToString());
                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "DIOCOMM_" + gClass.srmNum, "SFUSE", gClass.str.SrmInfo[gClass.srmNum].sfUse.ToString());
                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "DIOCOMM_" + gClass.srmNum, "DIOTYPE", gClass.str.SrmInfo[gClass.srmNum].dioType.ToString());

                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "DIOCOMM_" + gClass.srmNum, "DIOIP", gClass.str.SrmInfo[gClass.srmNum].dioIP);
                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "DIOCOMM_" + gClass.srmNum, "DIOID", gClass.str.SrmInfo[gClass.srmNum].dioID.ToString());

                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "DIOCOMM_" + gClass.srmNum, "COMPORT", gClass.str.SrmInfo[gClass.srmNum].comPORT);
                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "DIOCOMM_" + gClass.srmNum, "BAUDRATE", gClass.str.SrmInfo[gClass.srmNum].baudRate.ToString());
                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "DIOCOMM_" + gClass.srmNum, "PARITY", gClass.str.SrmInfo[gClass.srmNum].parity.ToString());
                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "DIOCOMM_" + gClass.srmNum, "DATABIT", gClass.str.SrmInfo[gClass.srmNum].dataBit.ToString());
                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "DIOCOMM_" + gClass.srmNum, "STOPBIT", gClass.str.SrmInfo[gClass.srmNum].stopBit.ToString());

                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "GCPINFO", "LanguageIdx", gClass.str.GcpInfo.language.ToString());
                cIniAccess.Write(AppDomain.CurrentDomain.BaseDirectory + "\\Config\\Config.ini", "GCPINFO", "CraneCount", gClass.str.GcpInfo.srmCount.ToString()); 
                


                VarMessageBox.Show(cConstDefine.tr("저장"), cConstDefine.tr("저장완료재시작"), VarMessageBoxButton.OK);
            }
            else if (result == VarMessageBoxResult.Cancel)
            {
                // Cancel button clicked or dialog closed using the X button
            }
        }

        private void Click_OpenNumpad(object sender, MouseButtonEventArgs e)
        {
            // 관리자 모드가 아니면 넘버패드를 열지 않음
            if (!gClass.str.GcpInfo.isAdminMode)
            {
                e.Handled = true;
                return;
            }

            TextBox edit = sender as TextBox;
            if (edit == null || pMain == null)
            {
                Console.WriteLine("Click_OpenNumpad: edit or pMain is null");
                return;
            }

            // TextBox가 비활성화되어 있으면 넘버패드를 열지 않음
            if (!edit.IsEnabled)
            {
                Console.WriteLine("Click_OpenNumpad: edit is disabled");
                return;
            }

            try
            {
                Window parentWindow = Window.GetWindow(this);
                if (parentWindow != null && pMain.tmpNumPad != null)
                {
                    pMain.tmpNumPad.AttachTo(edit, parentWindow, pMain.PointToScreen(new Point(0, 0)), pMain.ActualWidth, pMain.ActualHeight);
                    Console.WriteLine("Click_OpenNumpad Success: " + edit.Name);
                }
                else
                {
                    Console.WriteLine("Click_OpenNumpad: parentWindow or tmpNumPad is null");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Click_OpenNumpad Error: " + ex.Message);
            }
        }

        private void Btn_dioUse_Changed()
        {
            switch (Btn_dioUse.IsChecked)
            {
                case false:
                    bdr_dioComm.IsEnabled = false;
                    bdr_dioIP.IsEnabled = false;
                    break;
                case true:
                    if (combo_dioType.SelectedIndex == 0)
                    {
                        bdr_dioComm.IsEnabled = false;
                        bdr_dioIP.IsEnabled = true;
                    }
                    else
                    {
                        bdr_dioComm.IsEnabled = true;
                        bdr_dioIP.IsEnabled = false;
                    }
                    break;
            }
        }


        private void Btn_dioUse_Checked(object sender, RoutedEventArgs e)
        {
            Btn_dioUse_Changed();
        }

        private void Btn_dioUse_Unchecked(object sender, RoutedEventArgs e)
        {
            Btn_dioUse_Changed();
        }
    }
}
