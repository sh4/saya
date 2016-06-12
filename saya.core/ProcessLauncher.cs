using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace saya.core
{
    public class ProcessLaunchTask : ILaunchTask
    {
        [DllImport("User32.dll")]
        private extern static bool SetForegroundWindow(IntPtr hwnd);

        [DllImport("User32.dll")]
        private extern static bool IsWindowVisible(IntPtr hwnd);


        public string FilePath { get; private set; }

        public ProcessLaunchTask(string filePath)
        {
            FilePath = filePath;
        }

        public void Launch()
        {
            if (SwitchActiveWindow(FilePath))
            {
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = FilePath,
                UseShellExecute = false,
            };
            Process.Start(startInfo).Dispose();
        }

        private bool SwitchActiveWindow(string filePath)
        {
            var processes = Process.GetProcesses();
            var enableSwitchWindows = processes.Where(x =>
            {
                try
                {
                    return x.MainWindowHandle != IntPtr.Zero
                        && IsWindowVisible(x.MainWindowHandle)
                        && x.MainModule.FileName.Equals(filePath, StringComparison.InvariantCultureIgnoreCase);
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    return false;
                }
            });

            foreach (var w in enableSwitchWindows)
            {
                SetForegroundWindow(w.MainWindowHandle);
                return true;
            }

            return false;
        }
    }
}
