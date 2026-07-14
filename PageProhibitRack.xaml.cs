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
using System.Collections.ObjectModel;
using System.Windows.Controls.Primitives;

namespace gcp_Wpf
{
    /// <summary>
    /// PageProhibitRack.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PageProhibitRack : Page
    {
        //ObservableCollection<Rack> rackList = null;
        UniformGrid uniformGrid = new UniformGrid();

        int cntRow, cntBay, cntLev, cntStn;

        bool changed = false;                           // 설정 변경 상태 체크
        //Singletone
        singletonClass gClass;
        Rack preSelRack;


        public PageProhibitRack()
        {
            gClass = singletonClass.Instance;

            InitializeComponent();

            ProhRackSetting.Visibility = Visibility.Collapsed;
            ProhRackSave.Visibility = Visibility.Collapsed;

            for (int i = 0; i < 6; i++)
            {
                TabItem selectedTabItemByIndex = tabRowControl.Items[i] as TabItem;
                if(selectedTabItemByIndex != null)
                {
                    selectedTabItemByIndex.Visibility = Visibility.Visible;
                }
            }

            rackInitialize();

        }
        private void tabRowControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl)
            {
                TabItem selectedTab = e.AddedItems[0] as TabItem;
                if (selectedTab != null)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        TabItem selectedTabItemByIndex = tabRowControl.Items[i] as TabItem;
                        if (i < gClass.str.SrmInfo[gClass.srmNum].row)
                        {
                            selectedTabItemByIndex.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            selectedTabItemByIndex.Visibility = Visibility.Collapsed;
                        }
                        ((StackPanel)selectedTabItemByIndex.Content).Children.Clear();
                    }
                    Console.WriteLine("Current Tab Name " + selectedTab.Header.ToString());
                    RackTabClick(tabRowControl.SelectedIndex);
                }
            }
        }

        private void rackInitialize()
        {
            //uniformGrid.Children.Clear();

            int curSrm = gClass.srmNum;
            // -----------------------------Rack 구조 리셋-----------------------------------
            cntRow = gClass.str.SrmInfo[curSrm].row;
            cntBay = gClass.str.SrmInfo[curSrm].bay;
            cntLev = gClass.str.SrmInfo[curSrm].lev;
            cntStn = gClass.str.SrmInfo[curSrm].stn;


            // to do Test Row
            //gClass.str.SrmInfo[curSrm].row = 2;
            //cntLev = 10;
            //cntBay = 15;


            uniformGrid.Rows = cntLev;
            uniformGrid.Columns = cntBay;
            
            // UniformGrid에 회색 철제 구조물 배경 설정
            LinearGradientBrush steelGradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            steelGradient.GradientStops.Add(new GradientStop(Color.FromRgb(120, 120, 130), 0.0));  // 상단: 밝은 회색
            steelGradient.GradientStops.Add(new GradientStop(Color.FromRgb(100, 100, 110), 0.5));  // 중간: 중간 회색
            steelGradient.GradientStops.Add(new GradientStop(Color.FromRgb(80, 80, 90), 1.0));    // 하단: 어두운 회색
            uniformGrid.Background = steelGradient;
            
            for (int i = cntLev; i > 0; i--)
            {
                for (int j = 1; j <= cntBay; j++)
                {
                    Rack tmp = new Rack(j, i, false, 0) { ParentUniformGrid = uniformGrid };
                    //tmp.ButtonClick += RackClickHandler;      // 지상반에서 금지렉 설정 시 핸들러 추가
                    
                    // 렉 사이 간격을 위한 마진 설정 (철제 구조물이 보이도록)
                    tmp.Margin = new Thickness(2);
                    
                    uniformGrid.Children.Add(tmp);              // Add 순서대로 Row / Column 사이즈에 따라서 순서배치되는듯
                    
                }
            }

                //stack_row1.Children.Add(uniformGrid);


            if (tabRowControl.SelectedIndex == 0)
            {
                for (int i = 0; i < 6; i++)
                {
                    TabItem selectedTabItemByIndex = tabRowControl.Items[i] as TabItem;
                    if(i < cntRow)
                    {
                        selectedTabItemByIndex.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        selectedTabItemByIndex.Visibility = Visibility.Collapsed;
                    }
                    ((StackPanel)selectedTabItemByIndex.Content).Children.Clear();
                }
                RackTabClick(0);
            }
            else
            {
                tabRowControl.SelectedIndex = 0;
            }

        }

        private void RackTabClick(int row)
        {
            // 금지렉 리스트 만들기---------------메인으로이동---------------------------------
            
            // 금지렉 디스플레이---------------------------------------------------
            foreach (UIElement child in uniformGrid.Children)
            {
                Rack tmp = (Rack)child;
                tmp.OnCustomEvent(false);        // Set Proh
                int[][] prohList = gClass.str.SrmInfo[gClass.srmNum].prohDataList;
                int prohCnt = gClass.str.SrmInfo[gClass.srmNum].prohParseCnt;
                // prohParseCnt가 배열크기(500)를 넘거나 항목이 null/짧을 수 있어 IndexOutOfRange/NRE 방지 가드
                for (int i = 0; i < prohCnt && prohList != null && i < prohList.Length; i++)           // 금지 ROW 파싱데이터 카운트
                {
                    int[] entry = prohList[i];
                    if (entry == null || entry.Length < 3) continue;
                    if (row == (entry[0]-1) && tmp.bay == entry[1] && tmp.lev == entry[2])
                    {
                        Console.WriteLine("Find Proh Rack : "+ (row+1) + "/" + tmp.bay + "/"+ tmp.lev);
                        tmp.OnCustomEvent(true);        // Set Proh
                    }
                }
            }

            // row 범위 초과/캐스팅 실패/Content 비-StackPanel 시 예외 방지 가드
            TabItem selectedTabItemByIndex = (row >= 0 && row < tabRowControl.Items.Count) ? tabRowControl.Items[row] as TabItem : null;
            if (selectedTabItemByIndex?.Content is StackPanel sp)
            {
                sp.Children.Add(uniformGrid);
            }
        }


        private void RackClickHandler(object sender, RoutedEventArgs e)
        {
            // Call the parent method when the custom button is clicked
            var selectedRack = sender as Rack;
            if (selectedRack != null)
            {
                if(preSelRack != null)
                {
                    preSelRack.BorderBrush = null;          // 현재 선택된 랙 만 보더 표시
                }
                preSelRack = selectedRack;
                //selectedRack.ro
                // Access members of SpecificSenderClass using specificSender variable
            }
        }
        //private void YourMouseDownEventHandler(object sender, MouseButtonEventArgs e)
        //{
        //    UIElement clickedElement = (UIElement)sender;

        //    if (Keyboard.Modifiers == ModifierKeys.Control)
        //    {
        //        // Multi-select behavior when holding the Control key
        //        if (selectedElements.Contains(clickedElement))
        //            selectedElements.Remove(clickedElement);
        //        else
        //            selectedElements.Add(clickedElement);
        //    }
        //    else
        //    {
        //        // Single-select behavior
        //        selectedElements.Clear();
        //        selectedElements.Add(clickedElement);
        //    }

        //    // Update the visual state or perform any necessary operations based on the selection
        //    UpdateSelectionVisuals();
        //}
    }


    /// <summary>
    /// 자식이 레이아웃 측정(Measure)에 기여하지 않고, 배치(Arrange) 시 부모 영역 전체를 받도록 하는 패널.
    /// 금지렉 X 표시가 셀 크기를 키우지 않도록 사용.
    /// </summary>
    internal class NoMeasureOverlayPanel : System.Windows.Controls.Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (UIElement child in InternalChildren)
                child.Measure(new Size(0, 0));
            return new Size(0, 0);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (UIElement child in InternalChildren)
                child.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            return finalSize;
        }
    }


    public class Rack : Button
    {

        public UniformGrid ParentUniformGrid { get; set; }

        public delegate void ButtonClickHandler(object sender, RoutedEventArgs e);
        public event ButtonClickHandler ButtonClick;
        public event EventHandler CustomEvent;
        public int row { get; set; }
        public int bay { get; set; }
        public int lev { get; set; }

        public bool select { get; set; }
        public bool proh { get; set; }
        public int spec { get; set; }

        private Grid xMarkCanvas;  // 금지렉 표시용 빨간색 X 표시 (Grid로 구현)

        //public String Bay1 { get; set; }
        public static readonly DependencyProperty CustomTextProperty =
        DependencyProperty.Register("CustomText", typeof(string), typeof(Rack), new PropertyMetadata(""));

        public Rack(int bay, int lev, bool proh=false, int spec=0 )
        {
            this.bay = bay;
            this.lev = lev;
            this.proh = proh;
            this.spec = spec;
            this.Foreground = Brushes.White;
            
            // Grid를 Content로 사용하여 렉 이미지와 box 이미지를 겹쳐서 표시
            Grid grid = new Grid();
            
            // Bay, Lev 텍스트 - 세련된 스타일
            TextBlock textBlock = new TextBlock 
            { 
                Text = bay + "," + lev, 
                HorizontalAlignment = HorizontalAlignment.Center, 
                VerticalAlignment = VerticalAlignment.Center, 
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                // 텍스트 그림자 효과로 가독성 향상
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 315,
                    ShadowDepth = 1,
                    Opacity = 0.8,
                    BlurRadius = 2
                }
            };
            grid.Children.Add(textBlock);
            
            // 빨간색 X 표시용 컨테이너: Measure 시 크기에 기여하지 않고, Arrange 시 셀 전체를 채우도록 함 (금지렉 있을 때 렉이 과도하게 커지는 현상 방지)
            var xMarkOverlay = new NoMeasureOverlayPanel
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                ClipToBounds = true
            };
            
            // 빨간색 X 표시 Grid (금지렉 표시용, 초기에는 숨김) - 박스 전체에 표시
            Grid xMarkGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(6),
                // 너무 옅어서 잘 안 보이던 문제 개선
                Opacity = 0.75  // 투명도 75%
            };
            
            // Viewbox로 감싸서 박스 전체에 X 표시
            Viewbox viewbox = new Viewbox
            {
                Stretch = Stretch.Fill,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            
            // X 표시를 위한 Path
            Path xPath = new Path
            {
                Stroke = Brushes.Red,
                // 선 두께보다는 대각선 길이로 존재감을 키움
                StrokeThickness = 10,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            
            // X 모양의 Path Geometry 생성
            // 셀 안쪽에서 균형 있게 보이도록 기본 좌표계 기준으로 그림
            PathGeometry pathGeometry = new PathGeometry();
            PathFigureCollection figures = new PathFigureCollection();
            
            // 첫 번째 대각선 (왼쪽 위 -> 오른쪽 아래)
            PathFigure figure1 = new PathFigure
            {
                StartPoint = new Point(0, 0),
                IsClosed = false
            };
            figure1.Segments.Add(new LineSegment(new Point(100, 100), true));
            figures.Add(figure1);
            
            // 두 번째 대각선 (오른쪽 위 -> 왼쪽 아래)
            PathFigure figure2 = new PathFigure
            {
                StartPoint = new Point(100, 0),
                IsClosed = false
            };
            figure2.Segments.Add(new LineSegment(new Point(0, 100), true));
            figures.Add(figure2);
            
            pathGeometry.Figures = figures;
            xPath.Data = pathGeometry;
            
            viewbox.Child = xPath;
            xMarkGrid.Children.Add(viewbox);
            xMarkCanvas = xMarkGrid;  // 변수명 호환성 유지
            xMarkOverlay.Children.Add(xMarkGrid);
            grid.Children.Add(xMarkOverlay);
            
            this.Content = grid;
            this.MinHeight = 50;
            this.MinWidth = 50;
            //this.Click += MyButton_Click;
            
            // 세련된 외부 박스 형태 - 그라데이션 배경과 둥근 모서리
            LinearGradientBrush rackGradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            rackGradient.GradientStops.Add(new GradientStop(Color.FromRgb(60, 60, 70), 0.0));   // 상단: 어두운 회색
            rackGradient.GradientStops.Add(new GradientStop(Color.FromRgb(40, 40, 50), 0.5));   // 중간: 더 어두운 회색
            rackGradient.GradientStops.Add(new GradientStop(Color.FromRgb(30, 30, 40), 1.0));   // 하단: 가장 어두운 회색
            
            this.Background = rackGradient;
            this.BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 110));  // 부드러운 테두리
            this.BorderThickness = new Thickness(1);
            
            // 둥근 모서리를 위한 Template 설정
            this.Template = CreateRackTemplate();
            
            // 그림자 효과로 입체감 추가
            this.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                Direction = 315,
                ShadowDepth = 2,
                Opacity = 0.3,
                BlurRadius = 3
            };
        }

        private ControlTemplate CreateRackTemplate()
        {
            // 둥근 모서리를 가진 세련된 Template 생성
            ControlTemplate template = new ControlTemplate(typeof(Button));
            
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));  // 둥근 모서리
            border.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            
            FrameworkElementFactory contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Stretch);
            
            border.AppendChild(contentPresenter);
            template.VisualTree = border;
            
            return template;
        }

        private void MyButton_Click(object sender, RoutedEventArgs e)
        {
            int index = -1;

            if (select)
            {
                this.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/rack.png")));
                this.BorderBrush = null;
                select = false;
            }
            else
            {
                this.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/rack_sel.png")));
                this.BorderBrush = Brushes.Red;
                select = true;
            }

            ButtonClick(sender, e);

            //MessageBox.Show("Button Item Index = " + index);
        }

        public virtual void OnCustomEvent(bool select)
        {
            if (select)
            {
                // 금지렉일 때: 빨간색 X 표시
                // 그라데이션 배경 유지
                LinearGradientBrush rackGradient = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1)
                };
                rackGradient.GradientStops.Add(new GradientStop(Color.FromRgb(60, 60, 70), 0.0));
                rackGradient.GradientStops.Add(new GradientStop(Color.FromRgb(40, 40, 50), 0.5));
                rackGradient.GradientStops.Add(new GradientStop(Color.FromRgb(30, 30, 40), 1.0));
                
                this.Background = rackGradient;
                this.BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 110));
                this.BorderThickness = new Thickness(1);
                
                if (xMarkCanvas != null)
                {
                    // 빨간색 X 표시
                    xMarkCanvas.Visibility = Visibility.Visible;
                }
            }
            else
            {
                // 일반 렉일 때
                // 그라데이션 배경 유지
                LinearGradientBrush rackGradient = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1)
                };
                rackGradient.GradientStops.Add(new GradientStop(Color.FromRgb(60, 60, 70), 0.0));
                rackGradient.GradientStops.Add(new GradientStop(Color.FromRgb(40, 40, 50), 0.5));
                rackGradient.GradientStops.Add(new GradientStop(Color.FromRgb(30, 30, 40), 1.0));
                
                this.Background = rackGradient;
                this.BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 110));
                this.BorderThickness = new Thickness(1);
                
                if (xMarkCanvas != null)
                {
                    xMarkCanvas.Visibility = Visibility.Collapsed;
                }
            }
            // 이벤트가 null이 아닌 경우에만 호출
            //CustomEvent?.Invoke(this, EventArgs.Empty);
        }
    }
}

        //private void Combo_row_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{

        //}

        //private void Btn_UnCheck_Click(object sender, RoutedEventArgs e)
        //{
        //    for (int i = 0; i < uniformGrid.Children.Count; i++)
        //    {
        //        var tmp = uniformGrid.Children[i] as Rack;
        //        if (tmp != null)
        //        {
        //            if (tmp.select)
        //            {
        //                tmp.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/rack.png")));
        //                tmp.BorderBrush = null;
        //                tmp.select = false;
        //            }
        //        }
        //    }
        //}

        //private void Btn_SelBayS_Click(object sender, RoutedEventArgs e)
        //{
        //    for (int i = 0; i < uniformGrid.Children.Count; i++)
        //    {
        //        var tmp = uniformGrid.Children[i] as Rack;
        //        if (tmp != null)
        //        {
        //            if(tmp.bay == preSelRack.bay)
        //            {
        //                tmp.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/rack_sel.png")));
        //                tmp.select = true;
        //            }
        //        }
        //    }
        //}

        //private void Btn_SelLevS_Click(object sender, RoutedEventArgs e)
        //{
        //    for (int i = 0; i < uniformGrid.Children.Count; i++)
        //    {
        //        var tmp = uniformGrid.Children[i] as Rack;
        //        if (tmp != null)
        //        {
        //            if (tmp.lev == preSelRack.lev)
        //            {
        //                tmp.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/rack_sel.png")));
        //                tmp.select = true;
        //            }
        //        }
        //    }
        //}

        //private void Btn_Proh_Click(object sender, RoutedEventArgs e)
        //{
        //    for (int i = 0; i < uniformGrid.Children.Count; i++)
        //    {
        //        var tmp = uniformGrid.Children[i] as Rack;
        //        if (tmp != null)
        //        {
        //            if (tmp.select)
        //            {
        //                if (tmp.proh)
        //                {
        //                    tmp.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/rack.png")));
        //                    tmp.BorderBrush = null;
        //                    tmp.select = false;
        //                    tmp.spec = 0;
        //                    tmp.proh = false;
        //                }
        //                else
        //                {
        //                    tmp.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/rack_proh.png")));
        //                    tmp.BorderBrush = null;
        //                    tmp.select = false;
        //                    tmp.spec = 0;
        //                    tmp.proh = true;
        //                }
        //            }
        //        }
        //    }
        //}

        //private void Btn_Spec_Click(object sender, RoutedEventArgs e)
        //{
        //    for (int i = 0; i < uniformGrid.Children.Count; i++)
        //    {
        //        var tmp = uniformGrid.Children[i] as Rack;
        //        if (tmp != null)
        //        {
        //            if (tmp.select)
        //            {
        //                if (tmp.spec > 0)
        //                {
        //                    tmp.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/rack.png")));
        //                    tmp.BorderBrush = null;
        //                    tmp.select = false;
        //                    tmp.spec = 0;
        //                    tmp.proh = false;
        //                }
        //                else
        //                {
        //                    tmp.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/rack_spec.png")));
        //                    tmp.BorderBrush = null;
        //                    tmp.select = false;
        //                    tmp.proh = false;
        //                    tmp.spec = 1;
        //                }
        //            }
        //        }
        //    }
        //}

        //private void Btn_Save_Click(object sender, RoutedEventArgs e)
        //{
        //    // 통신 전송 및 결과 확인하여 현재 상태 표시 갱신 
        //}

        //private void Btn_Cancel_Click(object sender, RoutedEventArgs e)
        //{
        //    for (int i = 0; i < uniformGrid.Children.Count; i++)
        //    {
        //        var tmp = uniformGrid.Children[i] as Rack;
        //        if (tmp != null)
        //        {
        //            tmp.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/gcp_Wpf;component/Resources/rack.png")));
        //            tmp.BorderBrush = null;
        //            tmp.select = false;
        //            tmp.spec = 0;
        //            tmp.proh = false;
        //        }
        //    }
        //}