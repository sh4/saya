using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace saya.core
{
    public class ProcessLaunchTask : ILaunchTask
    {
        [DllImport("user32.dll")]
        private extern static IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private extern static bool SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        private extern static bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll")]
        private extern static bool ShowWindow(IntPtr hwnd, int cmdShow);

        [DllImport("user32.dll")]
        private static extern bool GetWindowPlacement(IntPtr hwnd, ref WindowPlacement placement);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, ref Rectangle rect);

        private struct Point
        {
            public int X;
            public int Y;
        }
        
        private struct Rectangle
        {
            public Point Position;
            public int Width;
            public int Height;
        }

        private class PointEqualityComparer : IEqualityComparer<Point>
        {
            public bool Equals(Point a, Point b)
            {
                return a.X == b.X && a.Y == b.Y;
            }

            public int GetHashCode(Point obj)
            {
                return obj.X ^ obj.Y;
            }
        }

        private class RectangleEqualityComparer : IEqualityComparer<Rectangle>
        {
            public bool Equals(Rectangle a, Rectangle b)
            {
                return a.Width == b.Width && 
                       a.Height == b.Height && 
                       a.Position.Equals(b.Position);
            }

            public int GetHashCode(Rectangle obj)
            {
                return obj.Width ^ obj.Height ^ obj.Position.GetHashCode();
            }
        }

        private struct WindowPlacement
        {
            public int Length;
            public int Flags;
            public int ShowCmd;
            public Point MinPosition;
            public Point MaxPosition;
            public Rectangle NormalPosition;
        }

        private const int SW_SHOWNORMAL = 1;
        private const int SW_SHOWMINIMIZED = 2;
        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;

        private native.ProcessInfo ProcessInfo { get; } = new native.ProcessInfo();

        private string m_FilePath;
        public string FilePath
        {
            get { return m_FilePath; }
            set
            {
                if (m_FilePath == value)
                {
                    return;
                }
                m_FilePath = value;
                Name = Path.GetFileNameWithoutExtension(m_FilePath);
                Description = Path.GetDirectoryName(m_FilePath);
            }
        }
        public string ExistProcessFilePath { get; set; }
        public string ExistProcessArgument { get; set; }

        public string Name { get; private set; }
        public string Description { get; private set; }

        public Task Launch()
        {
            if (SwitchExistsWindow())
            {
                return Task.CompletedTask;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = FilePath,
                UseShellExecute = true,
            };

            // プロセスによっては Dispose すると例外が発生する
            Process.Start(startInfo);

            return Task.CompletedTask;
        }

        private bool SwitchExistsWindow()
        {
            var processes = Process.GetProcesses();
            var switchProcess = processes.FirstOrDefault(x =>
            {
                try
                {
                    return x.MainWindowHandle != IntPtr.Zero
                        && IsWindowVisible(x.MainWindowHandle)
                        && EqualFilePath(x, ExistProcessFilePath)
                        && EqualArguments(x, ExistProcessArgument);
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    return false;
                }
            });

            if (switchProcess == null)
            {
                return false;
            }

            ForceSetForegroundWindow(switchProcess);
            return true;
        }

        private bool EqualFilePath(Process p, string filePath)
        {
            return p.MainModule.FileName.Equals(filePath, StringComparison.InvariantCultureIgnoreCase);
        }

        private bool EqualArguments(Process p, string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                return true;
            }
            var arguments = ProcessInfo.GetProcessArguments(p.Id);
            return arguments.Equals(args, StringComparison.InvariantCultureIgnoreCase);
        }

        private void ForceSetForegroundWindow(Process p)
        {
            var handle = p.MainWindowHandle;

            if (GetForegroundWindow() != handle)
            {
                var placement = new WindowPlacement();
                placement.Length = Marshal.SizeOf(placement);
                GetWindowPlacement(handle, ref placement);

                // Aerosnap が有効な状態で SW_RESTORE するとスナップが解除されるので、最小化されてるときだけ復元する
                if (!ResizeByAerosnap(handle, placement))
                {
                    ShowWindow(handle, SW_SHOW);
                    if (placement.ShowCmd == SW_SHOWMINIMIZED)
                    {
                        ShowWindow(handle, SW_RESTORE);
                    }
                }
                SetForegroundWindow(handle);
            }
        }

        // http://espresso3389.hatenablog.com/entry/2015/11/20/025612
        private bool ResizeByAerosnap(IntPtr hwnd, WindowPlacement placement)
        {
            if (placement.ShowCmd != SW_SHOWNORMAL)
            {
                return false;
            }
            var rc = new Rectangle();
            GetWindowRect(hwnd, ref rc);
            return placement.NormalPosition.Equals(rc);
        }
    }
}
