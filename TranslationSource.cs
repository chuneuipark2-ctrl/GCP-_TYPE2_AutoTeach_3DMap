using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Collections.Generic;
using System.Threading;

namespace gcp_Wpf
{ 
    class TranslationSource : INotifyPropertyChanged
    {
        private const char KeySeparator = '_';
        private static readonly TranslationSource instance = new TranslationSource();

        public static TranslationSource Instance
        {
            get { return instance; }
        }

        private readonly ResourceManager resManager = Properties.Resources.ResourceManager;
        private CultureInfo currentCulture = null;

        public string this[string key]
        {
            
            get
            {
                if (key.IndexOf(KeySeparator) == -1)
                {
                    var res = this.resManager.GetString(key, this.currentCulture);
                    return !string.IsNullOrWhiteSpace(res) ? res : $"[{key}]";
                }
                else
                {
                    var keys = key.Split(KeySeparator);
                    var results = new List<string>();
                    foreach (var k in keys)
                    {
                        var res = this.resManager.GetString(k, this.currentCulture);
                        results.Add(!string.IsNullOrWhiteSpace(res) ? res : $"[{k}]");
                    }

                    return string.Join(" ", results);
                }
            }
        }

        /// <summary>기본 문화(앱 리소스에 문화 미지정 시 사용). null이면 한국어(ko).</summary>
        public CultureInfo CurrentCulture
        {
            get { return this.currentCulture; }
            set
            {
                if (this.currentCulture != value)
                {
                    this.currentCulture = value;
                    // MessageBox 등 시스템 다이얼로그 버튼(확인/취소/예/아니오)이 선택 언어로 나오도록 스레드 문화 동기화
                    CultureInfo threadCulture = value ?? CultureInfo.GetCultureInfo("ko");
                    Thread.CurrentThread.CurrentUICulture = threadCulture;
                    Thread.CurrentThread.CurrentCulture = threadCulture;
                    var @event = this.PropertyChanged;
                    if (@event != null)
                    {
                        @event.Invoke(this, new PropertyChangedEventArgs(string.Empty));
                    }
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}