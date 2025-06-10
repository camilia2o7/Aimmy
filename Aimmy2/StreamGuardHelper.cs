using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Aimmy2
{
    public static class StreamGuardHelper
    {
        const uint WDA_NONE = 0;
        const uint WDA_EXCLUDEFROMCAPTURE = 0x11;
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WS_EX_APPWINDOW = 0x00040000;

        private static bool _isEnabled = false;
        private static HashSet<IntPtr> _protectedWindows = new();

        [DllImport("user32.dll")]
        private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private static void ApplyToWindow(Window window, bool enable)
        {
            if (window == null) return;

            var hWnd = new WindowInteropHelper(window).Handle;
            if (hWnd == IntPtr.Zero) return;

            if (enable)
            {
                if (_protectedWindows.Contains(hWnd)) return;
                _protectedWindows.Add(hWnd);
            }
            else
            {
                _protectedWindows.Remove(hWnd);
            }

            SetWindowDisplayAffinity(hWnd, enable ? WDA_EXCLUDEFROMCAPTURE : WDA_NONE);
            window.ShowInTaskbar = !enable;

            var extendedStyle = GetWindowLong(hWnd, GWL_EXSTYLE);

            if (enable)
                SetWindowLong(hWnd, GWL_EXSTYLE, (extendedStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);
            else
                SetWindowLong(hWnd, GWL_EXSTYLE, (extendedStyle | WS_EX_APPWINDOW) & ~WS_EX_TOOLWINDOW);
        }

        public static void ApplyStreamGuardToAllWindows(bool enable)
        {
            _isEnabled = enable;

            foreach (Window window in Application.Current.Windows)
                ApplyToWindow(window, enable);

            if (enable)
            {
                Application.Current.Activated -= OnAppActivated;
                Application.Current.Activated += OnAppActivated;
            }
            else
            {
                Application.Current.Activated -= OnAppActivated;
                _protectedWindows.Clear();
            }
        }

        private static void OnAppActivated(object? sender, EventArgs e)
        {
            if (!_isEnabled) return;

            foreach (Window window in Application.Current.Windows)
            {
                var hWnd = new WindowInteropHelper(window).Handle;
                if (!_protectedWindows.Contains(hWnd))
                {
                    ApplyToWindow(window, true);
                }
            }
        }
    }
}
