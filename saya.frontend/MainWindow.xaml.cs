using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace saya.frontend
{

    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public class LaunchMessage {}
        public class ToggleMessage {}
        public class CloseMessage {}

        [DllImport("user32.dll")]
        private static extern long GetWindowLongPtr(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern long SetWindowLongPtr(IntPtr hwnd, int index, long newValue);

        private static readonly int GWL_STYLE = -20;
        private static readonly long WS_EX_TOOLWINDOW = 0x80;

        public MainWindow()
        {
            InitializeComponent();
            EnableExToolWindowStyle();

            // ウインドウ表示のトグル
            Messenger.Default.Register<ToggleMessage>(ToggleWindowVisibility);
            // ウインドウを非表示に
            Messenger.Default.Register<CloseMessage>(HiddenWindow);
            // テキストボックスをデフォルトフォーカスとする
            ContentRendered += (sender, e) => MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            // リスト項目の左クリックは、選択中の項目を起動
            CandidateList.MouseLeftButtonUp += (sender, e) => Messenger.Default.Send(new LaunchMessage());
            // ViewModel が IDisposable インターふぇすを実装していれば Dispose を試みる
            Closed += (sender, e) => (DataContext as IDisposable)?.Dispose();
        }

        // Alt+Tab の切り替え対象のウインドウとしてリストアップされないようにする
        private void EnableExToolWindowStyle()
        {
            var handle = new WindowInteropHelper(this).EnsureHandle();
            var style = GetWindowLongPtr(handle, GWL_STYLE);
            SetWindowLongPtr(handle, GWL_STYLE, style | WS_EX_TOOLWINDOW);
        }

        private void ToggleWindowVisibility(ToggleMessage message)
        {
            if (!IsVisible)
            {
                return;
            }
            
            // ウインドウを非表示にするとその時点で描画がストップするので、不透明度を 0.0 にして見た目は非表示にて描画は継続させる
            if (Math.Abs(1.0 - Opacity) < double.Epsilon)
            {
                HiddenWindow(null);
            }
            else
            {
                Opacity = 1.0;
                // ウインドウをアクティブ後、コマンドボックスにフォーカスを移す
                Activate();
                CommandTextBox.Focus();
            }
        }

        private void HiddenWindow(CloseMessage message)
        {
            Opacity = 0.0;
        }
    }
}
