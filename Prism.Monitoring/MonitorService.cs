using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Prism.Monitoring
{
    public class WindowChangedEventArgs : EventArgs
    {
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
    }

    public class MonitorService
    {
        public event EventHandler<WindowChangedEventArgs>? WindowChanged;
        
        private CancellationTokenSource? _cts;
        private IntPtr _lastHwnd = IntPtr.Zero;

        public void Start()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => MonitoringLoop(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
        }

        private async Task MonitoringLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var hwnd = Win32Hooks.GetForegroundWindow();
                if (hwnd != _lastHwnd && hwnd != IntPtr.Zero)
                {
                    _lastHwnd = hwnd;
                    Win32Hooks.GetWindowThreadProcessId(hwnd, out uint processId);
                    try
                    {
                        var process = Process.GetProcessById((int)processId);
                        var title = GetWindowTitle(hwnd);

                        WindowChanged?.Invoke(this, new WindowChangedEventArgs
                        {
                            ProcessName = process.ProcessName,
                            WindowTitle = title ?? string.Empty
                        });
                    }
                    catch
                    {
                        // Ignore
                    }
                }
                
                await Task.Delay(1000, token);
            }
        }

        private string? GetWindowTitle(IntPtr hwnd)
        {
            const int nChars = 256;
            StringBuilder Buff = new StringBuilder(nChars);
            if (Win32Hooks.GetWindowText(hwnd, Buff, nChars) > 0)
            {
                return Buff.ToString();
            }
            return null;
        }
    }
}
