using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Drawing;

namespace saya.frontend
{
    public class ShellIcon : IDisposable
    {
        private core.native.ShellIconInfo IconInfo = new core.native.ShellIconInfo();

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public ShellIcon()
        {
        }
        
        public ImageSource GetIcon(string filePath)
        {
            IntPtr hIcon = IntPtr.Zero;
            try
            {
                hIcon = IconInfo.GetIcon(filePath);
                using (var icon = Icon.FromHandle(hIcon))
                {
                    var image = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        new System.Windows.Int32Rect(0, 0, icon.Width, icon.Height),
                        BitmapSizeOptions.FromEmptyOptions());
                    image.Freeze();
                    return image;
                }
            }
            finally
            {
                DestroyIcon(hIcon);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // 重複する呼び出しを検出するには

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージ状態を破棄します (マネージ オブジェクト)。
                }

                // TODO: アンマネージ リソース (アンマネージ オブジェクト) を解放し、下のファイナライザーをオーバーライドします。
                // TODO: 大きなフィールドを null に設定します。
                IconInfo.Dispose();
                IconInfo = null;

                disposedValue = true;
            }
        }

        // TODO: 上の Dispose(bool disposing) にアンマネージ リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします。
        ~ShellIcon() {
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
