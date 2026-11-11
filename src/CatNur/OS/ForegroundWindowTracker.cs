namespace CatNur.OS;

using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;

/// <summary>
/// Tracks the foreground (active) window and provides its screen bounds.
/// Uses DwmGetWindowAttribute for accurate visual bounds (excludes drop shadows).
/// Filters out desktop, shell, taskbar, and invalid/fullscreen system windows.
/// </summary>
public class ForegroundWindowTracker
{
    private IntPtr _ownWindowHandle;

    // Known desktop, shell, and system class names to exclude
    private static readonly string[] ExcludedClasses =
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "Windows.UI.Core.CoreWindow",
        "ImmersiveLauncher",
        "ApplicationFrameWindow_Ghost",
        "DV2ControlHost"
    };

    public void SetOwnWindowHandle(IntPtr handle)
    {
        _ownWindowHandle = handle;
    }

    /// <summary>
    /// Gets the bounding rectangle of the current foreground window in WPF logical units (DIPs).
    /// Returns null if no valid window exists, if it's the desktop, or if it's our own window.
    /// </summary>
    public Rect? GetForegroundWindowBounds(Visual wpfWindow)
    {
        try
        {
            IntPtr hWnd = NativeMethods.GetForegroundWindow();

            if (hWnd == IntPtr.Zero)
                return null;
            if (hWnd == _ownWindowHandle)
                return null;

            // Exclude shell and desktop window handles
            IntPtr shellWnd = NativeMethods.GetShellWindow();
            IntPtr desktopWnd = NativeMethods.GetDesktopWindow();
            if (hWnd == shellWnd || hWnd == desktopWnd)
                return null;

            if (!NativeMethods.IsWindowVisible(hWnd))
                return null;
            if (NativeMethods.IsIconic(hWnd))
                return null;

            // Check window class name against excluded system/desktop classes
            var sb = new StringBuilder(256);
            if (NativeMethods.GetClassName(hWnd, sb, sb.Capacity) > 0)
            {
                string className = sb.ToString();
                for (int i = 0; i < ExcludedClasses.Length; i++)
                {
                    if (string.Equals(className, ExcludedClasses[i], StringComparison.OrdinalIgnoreCase))
                        return null;
                }
            }

            // Get actual visual bounds (excludes invisible drop shadow borders)
            NativeMethods.RECT rect;
            int hr = NativeMethods.DwmGetWindowAttribute(
                hWnd,
                NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
                out rect,
                Marshal.SizeOf<NativeMethods.RECT>());

            if (hr != 0)
            {
                if (!NativeMethods.GetWindowRect(hWnd, out rect))
                    return null;
            }

            // Convert from physical pixels to WPF logical units (DIPs)
            var dpi = VisualTreeHelper.GetDpi(wpfWindow);
            double left = rect.Left / dpi.DpiScaleX;
            double top = rect.Top / dpi.DpiScaleY;
            double width = rect.Width / dpi.DpiScaleX;
            double height = rect.Height / dpi.DpiScaleY;

            // Sanity check dimensions:
            var workArea = SystemParameters.WorkArea;
            if (width < 80 || height < 40)
                return null;

            // If the window top is off-screen (<= 10px) or below the work area,
            // it cannot serve as a valid walkable platform
            if (top <= 10 || top >= workArea.Bottom - 40)
                return null;

            // If the window covers the entire work area (full screen window/overlay),
            // it doesn't have a walkable top edge
            if (width >= workArea.Width - 10 && height >= workArea.Height - 10 && top <= 30)
                return null;

            return new Rect(left, top, width, height);
        }
        catch
        {
            return null;
        }
    }
}
