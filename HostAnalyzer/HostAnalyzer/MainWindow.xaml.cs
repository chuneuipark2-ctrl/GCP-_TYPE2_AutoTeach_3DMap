using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using HostAnalyzer.Models;
using HostAnalyzer.Services;

namespace HostAnalyzer
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
                Title = "Host 로그 파일 선택"
            };
            if (dlg.ShowDialog() != true) return;

            string filePath = dlg.FileName;
            BtnOpenFile.IsEnabled = false;
            try
            {
                var (text, pairs) = await Task.Run(() =>
                {
                    string content = File.ReadAllText(filePath);
                    var entries = HostLogParser.ParseLines(content);
                    var list = HostLogParser.BuildTxRxPairs(entries);
                    return (content, list);
                }).ConfigureAwait(true);

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
            var entries = HostLogParser.ParseLines(text);
            _pairs = HostLogParser.BuildTxRxPairs(entries);
            _currentIndex = _pairs.Count > 0 ? 0 : -1;
            ShowCurrentPair();
            UpdateNavigationState();
        }

        private void ShowCurrentPair()
        {
            if (_currentIndex < 0 || _currentIndex >= _pairs.Count)
            {
                TxtRx.Text = "";
                TxtTx.Text = "";
                TxtIndex.Text = "0 / 0 쌍";
                FillRxDetail(null);
                FillTxDetail(null);
                return;
            }

            var pair = _pairs[_currentIndex];
            TxtRx.Text = pair.Rx.RawLine;
            TxtTx.Text = pair.Tx != null ? pair.Tx.RawLine : "(응답 없음)";
            TxtIndex.Text = $"{_currentIndex + 1} / {_pairs.Count} 쌍";

            var rxPacket = HostPacketParser.ParseFromLogLine(pair.Rx.RawLine, isTx: false);
            FillRxDetail(rxPacket);
            FillTxDetail(pair.Tx != null ? HostPacketParser.ParseFromLogLine(pair.Tx.RawLine, isTx: true) : null);
        }

        private void FillRxDetail(HostParsedPacket? p)
        {
            if (p == null || !p.IsValid)
            {
                string err = p?.ErrorMessage ?? "";
                Rx_Syn.Text = err;
                Rx_EqId.Text = Rx_ReqType.Text = Rx_WordCount.Text = Rx_DataInterpret.Text = err;
                return;
            }
            Rx_Syn.Text = p.Syn;
            Rx_EqId.Text = $"{p.EqId} (SRM 호기)";
            Rx_ReqType.Text = $"{p.ReqType} (0=일반, 1=보조)";
            Rx_WordCount.Text = p.Words?.Length.ToString() ?? "0";
            Rx_DataInterpret.Text = HostDataInterpreter.Interpret(p);
        }

        private void FillTxDetail(HostParsedPacket? p)
        {
            if (p == null || !p.IsValid)
            {
                Tx_Syn.Text = p == null ? "(응답 없음)" : p.ErrorMessage;
                Tx_EqId.Text = Tx_ReqType.Text = Tx_WordCount.Text = Tx_DataInterpret.Text = "";
                return;
            }
            Tx_Syn.Text = p.Syn;
            Tx_EqId.Text = $"{p.EqId} (SRM 호기)";
            Tx_ReqType.Text = $"0x{p.ReqType:X2} (ReqType+0x80)";
            Tx_WordCount.Text = p.Words?.Length.ToString() ?? "0";
            Tx_DataInterpret.Text = HostDataInterpreter.Interpret(p);
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
