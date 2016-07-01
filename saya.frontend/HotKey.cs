using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace saya.frontend
{
    // see also: http://stackoverflow.com/questions/48935/how-can-i-register-a-global-hot-key-to-say-ctrlshiftletter-using-wpf-and-ne
    public class HotKey : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hwnd, int id, int modifiers, int virtualKey);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

        private class HotKeyContext
        {
            public int Id { get; set; }
            public KeyGesture Gesture { get; set; }
            public Action<KeyGesture> Action { get; set; }
        }

        private static Dictionary<int, List<HotKeyContext>> s_HotKeyGestures = new Dictionary<int, List<HotKeyContext>>();
        private static ThreadMessageEventHandler s_HotKeyFilter;

        private const int WM_HOTKEY = 0x312;

        private HotKeyContext Context;

        public HotKey(KeyGesture gesture, Action<KeyGesture> action)
        {
            var virtualKey = KeyInterop.VirtualKeyFromKey(gesture.Key);
            var modifiers =  (int)gesture.Modifiers;

            Context = new HotKeyContext
            {
                // An application must specify an id value in the range 0x0000 through 0xBEFF.
                // https://msdn.microsoft.com/ja-jp/library/windows/desktop/ms646309(v=vs.85).aspx
                Id = (virtualKey & 0xff) | ((modifiers & 0xf) << 8), // [Modifiers:4bit][VK:8bit]
                Gesture = gesture,
                Action = action
            };

            List<HotKeyContext> contexts;
            if (s_HotKeyGestures.TryGetValue(Context.Id, out contexts))
            {
                contexts.Add(Context);
            }
            else
            {
                if (!RegisterHotKey(IntPtr.Zero, Context.Id, modifiers, virtualKey))
                {
                    throw new System.ComponentModel.Win32Exception();
                }
                s_HotKeyGestures.Add(Context.Id, new List<HotKeyContext> { Context });
            }

            if (s_HotKeyFilter == null)
            {
                s_HotKeyFilter = new ThreadMessageEventHandler(WindowMessageProc);
                ComponentDispatcher.ThreadFilterMessage += s_HotKeyFilter;
            }
        }

        private void WindowMessageProc(ref MSG msg, ref bool handled)
        {
            if (msg.message != WM_HOTKEY)
            {
                return;
            }

            {
                int hotKeyId = msg.wParam.ToInt32();
                List<HotKeyContext> contexts;
                if (s_HotKeyGestures.TryGetValue(hotKeyId, out contexts))
                {
                    foreach (var context in contexts)
                    {
                        context?.Action(context.Gesture);
                    }
                    handled = true;
                }
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // 重複する呼び出しを検出するには

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                List<HotKeyContext> contexts;
                if (s_HotKeyGestures.TryGetValue(Context.Id, out contexts))
                {
                    contexts.Remove(Context);
                    if (contexts.Count == 0)
                    {
                        UnregisterHotKey(IntPtr.Zero, Context.Id);
                        s_HotKeyGestures.Remove(Context.Id);
                    }
                }

                if (disposing)
                {
                    // TODO: マネージ状態を破棄します (マネージ オブジェクト)。
                    if (s_HotKeyFilter != null && s_HotKeyGestures.Count == 0)
                    {
                        ComponentDispatcher.ThreadFilterMessage -= s_HotKeyFilter;
                        s_HotKeyFilter = null;
                    }
                }

                // TODO: アンマネージ リソース (アンマネージ オブジェクト) を解放し、下のファイナライザーをオーバーライドします。
                // TODO: 大きなフィールドを null に設定します。

                disposedValue = true;
            }
        }

        // TODO: 上の Dispose(bool disposing) にアンマネージ リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします。
        ~HotKey() {
           // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
           Dispose(false);
        }

        // このコードは、破棄可能なパターンを正しく実装できるように追加されました。
        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
            Dispose(true);
            // TODO: 上のファイナライザーがオーバーライドされる場合は、次の行のコメントを解除してください。
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
