using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace gcp_Wpf
{
    /// <summary>
    /// PageStationSet.xaml에 대한 상호 작용 논리
    /// </summary>
    /// 
    public class StnData
    {
        public int stNum { get; set; }
        public string stnType { get; set; }
        public string goodsType { get; set; }
        public int intNum { get; set; }
    }
    public partial class PageStationSet : Page
    {
        ObservableCollection<StnData> stnList = new ObservableCollection<StnData>();

        //Singletone
        singletonClass gClass;
        public PageStationSet()
        {
            InitializeComponent();
            gClass = singletonClass.Instance;

            //stnList.Add(new StnData { stNum = 1, stnType = "2023-03-31 15:02:26", goodsType = "06", intNum = 1 });
            //stnList.Add(new StnData { stNum = 2, stnType = "2023-03-31 15:02:26", goodsType = "06", intNum = 2 });
            //stnList.Add(new StnData { stNum = 3, stnType = "2023-03-31 15:02:26", goodsType = "06", intNum = 3 });
            //stnList.Add(new StnData { stNum = 4, stnType = "2023-03-31 15:02:26", goodsType = "06", intNum = 4 });
            //stnList.Add(new StnData { stNum = 5, stnType = "2023-03-31 15:02:26", goodsType = "06", intNum = 5 });

            stnList.Add(new StnData { stNum = 5, stnType = "2023-03-31 15:02:26", goodsType = "06", intNum = 5 });

            StationList.ItemsSource = stnList;

            // 언어 변경 이벤트 구독
            TranslationSource.Instance.PropertyChanged += TranslationSource_PropertyChanged;
        }

        public void resetStationList()
        {
            stnList.Clear();
            string tmpStnType = "";
            string tmpGoodType = "";
            for(int i = 0; i < gClass.str.SrmInfo[gClass.srmNum].stn; i++)
            {
                tmpStnType = "";
                tmpGoodType = "";
                switch (gClass.str.SrmInfo[gClass.srmNum].SrmStation[i].stnType) 
                {
                    case 0:
                        tmpStnType = cConstDefine.tr("타입없음");
                        break;
                    case 1:
                        tmpStnType = cConstDefine.tr("입고");
                        break;
                    case 2:
                        tmpStnType = cConstDefine.tr("출고");
                        break;
                    case 3:
                        tmpStnType = cConstDefine.tr("입출고");
                        break;
                    case 4:
                        tmpStnType = cConstDefine.tr("가상스테이션");
                        break;
                }

                if (gClass.str.SrmInfo[gClass.srmNum].SrmStation[i].goodType == 0)
                {
                    tmpGoodType += cConstDefine.tr("모든 화물 허용 안함");
                }
                else
                {
                    if ((gClass.str.SrmInfo[gClass.srmNum].SrmStation[i].goodType & 0x01) > 0)
                    {
                        tmpGoodType += cConstDefine.tr("1종") + ", ";
                    }
                    if ((gClass.str.SrmInfo[gClass.srmNum].SrmStation[i].goodType & 0x02) > 0)
                    {
                        tmpGoodType += cConstDefine.tr("2종") + ", ";
                    }
                    if ((gClass.str.SrmInfo[gClass.srmNum].SrmStation[i].goodType & 0x04) > 0)
                    {
                        tmpGoodType += cConstDefine.tr("3종") + ", ";
                    }
                    if ((gClass.str.SrmInfo[gClass.srmNum].SrmStation[i].goodType & 0x08) > 0)
                    {
                        tmpGoodType += cConstDefine.tr("4종") + ", ";
                    }
                    if ((gClass.str.SrmInfo[gClass.srmNum].SrmStation[i].goodType & 0x10) > 0)
                    {
                        tmpGoodType += cConstDefine.tr("5종") + ", ";
                    }
                    if ((gClass.str.SrmInfo[gClass.srmNum].SrmStation[i].goodType & 0x20) > 0)
                    {
                        tmpGoodType += cConstDefine.tr("6종") + ", ";
                    }
                    if ((gClass.str.SrmInfo[gClass.srmNum].SrmStation[i].goodType & 0x40) > 0)
                    {
                        tmpGoodType += cConstDefine.tr("7종") + ", ";
                    }
                    if ((gClass.str.SrmInfo[gClass.srmNum].SrmStation[i].goodType & 0x80) > 0)
                    {
                        tmpGoodType += cConstDefine.tr("8종");
                    }
                }

                stnList.Add(new StnData { stNum = i+1, stnType = tmpStnType, goodsType = tmpGoodType, intNum = gClass.str.SrmInfo[gClass.srmNum].SrmStation[i].intNum });
            }
        }


        private void TranslationSource_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 언어 변경 시 스테이션 리스트 텍스트 업데이트
            Dispatcher.Invoke(() =>
            {
                // 현재 페이지가 표시 중일 때만 업데이트
                if (this.IsVisible)
                {
                    resetStationList();
                }
            });
        }
    }
}
