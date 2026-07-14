using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using LogAnalyzer.Models;
using LogAnalyzer.Services;

namespace LogAnalyzer
{
    public partial class MainWindow : Window
    {
        private List<TxRxPair> _pairs = new List<TxRxPair>();
        private int _currentIndex = -1;

        public MainWindow()
        {
            InitializeComponent();
            UpdateNavigationState();
        }

        private async void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "로그 파일 (*.log)|*.log|텍스트 (*.txt)|*.txt|모든 파일 (*.*)|*.*",
                Title = "로그 파일 선택"
            };
            if (dlg.ShowDialog() != true) return;

            string filePath = dlg.FileName;
            BtnOpenFile.IsEnabled = false;
            try
            {
                // 파일 Read + 파싱을 스레드 풀에서 실행
                var (text, pairs) = await Task.Run(() =>
                {
                    string content = File.ReadAllText(filePath);
                    var entries = LogParser.ParseLines(content);
                    var list = LogParser.BuildTxRxPairs(entries);
                    return (content, list);
                }).ConfigureAwait(true);

                // UI 스레드에서만 컨트롤 갱신
                TxtInput.Text = text;
                _pairs = pairs;
                _currentIndex = _pairs.Count > 0 ? 0 : -1;
                ShowCurrentPair();
                UpdateNavigationState();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("파일을 읽는 중 오류: " + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                BtnOpenFile.IsEnabled = true;
            }
        }

        private void BtnParsePaste_Click(object sender, RoutedEventArgs e)
        {
            ParseAndShow(TxtInput.Text);
        }

        private void ParseAndShow(string text)
        {
            var entries = LogParser.ParseLines(text);
            _pairs = LogParser.BuildTxRxPairs(entries);
            _currentIndex = _pairs.Count > 0 ? 0 : -1;
            ShowCurrentPair();
            UpdateNavigationState();
        }

        private void ShowCurrentPair()
        {
            if (_currentIndex < 0 || _currentIndex >= _pairs.Count)
            {
                TxtTx.Text = "";
                TxtRx.Text = "";
                TxtIndex.Text = "0 / 0 쌍";
                FillTxDetail(null);
                FillRxDetail(null);
                return;
            }

            var pair = _pairs[_currentIndex];
            TxtTx.Text = pair.Tx.RawLine;
            TxtRx.Text = pair.Rx != null ? pair.Rx.RawLine : "(응답 없음)";
            TxtIndex.Text = $"{_currentIndex + 1} / {_pairs.Count} 쌍";

            var txPacket = PacketParser.ParseFromLogLine(pair.Tx.RawLine);
            FillTxDetail(txPacket);
            FillRxDetail(pair.Rx != null ? PacketParser.ParseFromLogLine(pair.Rx.RawLine) : null);
        }

        /// <summary>udpClientClass 변수명/주석 기준 TX 상세 에디트 채우기</summary>
        private void FillTxDetail(ParsedPacket? p)
        {
            if (p == null || !p.IsValid)
            {
                string err = p?.ErrorMessage ?? "";
                Tx_Syn.Text = err;
                Tx_SrcType.Text = Tx_SrcId.Text = Tx_DstType.Text = Tx_DstId.Text = Tx_SeqNum.Text = "";
                Tx_ByPass1.Text = Tx_ByPass2.Text = Tx_Cmd1.Text = Tx_Len.Text = Tx_Cmd2.Text = Tx_Data.Text = Tx_DataInterpret.Text = err;
                return;
            }
            Tx_Syn.Text = p.Syn;
            Tx_SrcType.Text = $"{p.SrcType:X2} (0x00=지상반)";
            Tx_SrcId.Text = $"{p.SrcId}";
            Tx_DstType.Text = $"{p.DstType:X2} (0x60=SRM)";
            Tx_DstId.Text = $"{p.DstId} (SRM호기)";
            Tx_SeqNum.Text = $"{p.SeqNum}";
            Tx_ByPass1.Text = $"{p.ByPass1:X2}";
            Tx_ByPass2.Text = $"{p.ByPass2:X2}";
            Tx_Cmd1.Text = $"{p.Cmd1:X2}";
            Tx_Len.Text = $"{p.Len} (CMD2+Data 길이)";
            Tx_Cmd2.Text = $"{p.Cmd2:X2}";
            Tx_Data.Text = p.Data != null && p.Data.Length > 0
                ? string.Join(" ", p.Data.Select(b => b.ToString("X2")))
                : "(없음)";
            Tx_DataInterpret.Text = p.Data != null && p.Data.Length > 0
                ? DataInterpreter.Interpret(p.Cmd2, p.Data, isTx: true)
                : "";
        }

        /// <summary>udpClientClass 변수명/주석 기준 RX 상세 에디트 채우기</summary>
        private void FillRxDetail(ParsedPacket? p)
        {
            if (p == null || !p.IsValid)
            {
                Rx_Syn.Text = p == null ? "(응답 없음)" : p.ErrorMessage;
                Rx_SrcType.Text = Rx_SrcId.Text = Rx_DstType.Text = Rx_DstId.Text = Rx_SeqNum.Text = "";
                Rx_ByPass1.Text = Rx_ByPass2.Text = Rx_Cmd1.Text = Rx_Len.Text = Rx_Cmd2.Text = Rx_Data.Text = Rx_DataInterpret.Text = "";
                return;
            }
            Rx_Syn.Text = p.Syn;
            Rx_SrcType.Text = $"{p.SrcType:X2} (0x60=SRM)";
            Rx_SrcId.Text = $"{p.SrcId} (SRM호기)";
            Rx_DstType.Text = $"{p.DstType:X2} (0x00=지상반)";
            Rx_DstId.Text = $"{p.DstId}";
            Rx_SeqNum.Text = $"{p.SeqNum}";
            Rx_ByPass1.Text = $"{p.ByPass1:X2}";
            Rx_ByPass2.Text = $"{p.ByPass2:X2}";
            Rx_Cmd1.Text = $"{p.Cmd1:X2} (TX CMD1+0x80)";
            Rx_Len.Text = $"{p.Len} (CMD2+Data 길이)";
            Rx_Cmd2.Text = $"{p.Cmd2:X2}";
            Rx_Data.Text = p.Data != null && p.Data.Length > 0
                ? string.Join(" ", p.Data.Select(b => b.ToString("X2")))
                : "(없음)";
            Rx_DataInterpret.Text = p.Data != null && p.Data.Length > 0
                ? DataInterpreter.Interpret(p.Cmd2, p.Data, isTx: false)
                : "";
        }

        private void UpdateNavigationState()
        {
            BtnPrev.IsEnabled = _pairs.Count > 0 && _currentIndex > 0;
            BtnNext.IsEnabled = _pairs.Count > 0 && _currentIndex < _pairs.Count - 1;
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex <= 0) return;
            _currentIndex--;
            ShowCurrentPair();
            UpdateNavigationState();
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex >= _pairs.Count - 1) return;
            _currentIndex++;
            ShowCurrentPair();
            UpdateNavigationState();
        }
    }
}
