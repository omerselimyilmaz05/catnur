namespace CatNur.OS;

using System;
using System.Windows;
using CatNur.Physics;
using Forms = System.Windows.Forms;

/// <summary>
/// Provides screen dimensions and boundaries.
/// Wraps <see cref="SystemParameters.WorkArea"/> (excludes taskbar).
/// </summary>
public class ScreenService
{
    /// <summary>
    /// Gets the usable work area of the primary screen (excludes taskbar).
    /// Values are in WPF logical units (DIPs).
    /// </summary>
    public Rect GetWorkArea()
    {
        return SystemParameters.WorkArea;
    }

    /// <summary>
    /// Gets whichever monitor the given window handle physically sits on right now.
    /// Falls back to the primary monitor if the handle is invalid or the lookup fails.
    /// </summary>
    public Forms.Screen GetScreenFromHandle(IntPtr hwnd)
    {
        try
        {
            if (hwnd != IntPtr.Zero)
                return Forms.Screen.FromHandle(hwnd);
        }
        catch
        {
            // fall through to primary
        }

        return Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0];
    }

    /// <summary>
    /// Finds the next monitor in the cycle after the given one (wraps around).
    /// Returns null if only one monitor is connected. Used for the tray icon's manual
    /// "switch monitor" action.
    /// </summary>
    public Forms.Screen? GetNextScreen(Forms.Screen current)
    {
        var all = Forms.Screen.AllScreens;
        if (all.Length < 2)
            return null;

        int idx = Array.FindIndex(all, s => s.DeviceName == current.DeviceName);
        if (idx < 0) idx = 0;

        return all[(idx + 1) % all.Length];
    }

    /// <summary>
    /// Gets the given monitor's own DPI scale factor (1.0 = 96 DPI / 100%), independent of
    /// whatever DPI context our window is currently in. Needed to correctly place the window
    /// via raw physical pixels when it hasn't physically arrived on that monitor yet — WPF's
    /// own DIP-to-physical conversion always uses the window's CURRENT (old) monitor's DPI
    /// until the window has actually moved, which silently mis-places it on a mixed-DPI setup.
    /// </summary>
    public (double ScaleX, double ScaleY) GetDpiScale(Forms.Screen screen)
    {
        try
        {
            var wa = screen.Bounds;
            var center = new NativeMethods.POINT
            {
                X = wa.Left + wa.Width / 2,
                Y = wa.Top + wa.Height / 2
            };
            IntPtr hMonitor = NativeMethods.MonitorFromPoint(center, NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (hMonitor != IntPtr.Zero &&
                NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY) == 0)
            {
                return (dpiX / 96.0, dpiY / 96.0);
            }
        }
        catch
        {
            // fall through to 100%
        }

        return (1.0, 1.0);
    }

    /// <summary>
    /// Gets the usable work area (excludes taskbar) of the given monitor, converted to WPF
    /// logical units (DIPs) using THAT monitor's own DPI scale.
    /// </summary>
    public Rect GetWorkAreaForScreen(Forms.Screen screen)
    {
        var wa = screen.WorkingArea;
        var (scaleX, scaleY) = GetDpiScale(screen);

        return new Rect(
            wa.Left / scaleX,
            wa.Top / scaleY,
            wa.Width / scaleX,
            wa.Height / scaleY);
    }

    /// <summary>
    /// Creates a ground surface representing the bottom of the given screen work area.
    /// This is always present as the "floor" for the pet, scoped to whichever monitor it's on.
    /// </summary>
    public GroundSurface GetScreenGround(Rect workArea)
    {
        return new GroundSurface(
            left: workArea.Left,
            right: workArea.Right,
            top: workArea.Bottom,
            type: SurfaceType.ScreenBottom
        );
    }
}
