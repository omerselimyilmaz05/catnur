namespace CatNur.OS;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using CatNur.Core;
using CatNur.Physics;

/// <summary>
/// Discovers and tracks window platforms using Win32 EnumWindows in top-to-bottom Z-order.
/// Supports per-window 60 FPS tracking (riding moving windows at high speed),
/// Z-order occlusion checks (including maximized/fullscreen windows),
/// new window appearance detection, and universal ballistic jump calculations.
/// </summary>
public class WindowSurfaceDetector
{
    private IntPtr _ownWindowHandle;
    private readonly HashSet<IntPtr> _knownHwnds = new();
    private bool _isFirstScan = true;
    private Rect? _lastWorkArea;

    /// <summary>
    /// Event fired when a newly opened, valid window platform is detected.
    /// </summary>
    public event Action<GroundSurface>? NewWindowDetected;

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
    /// Represents an enumerated window in Z-order with its visual bounds and app identity.
    /// </summary>
    public readonly struct WindowInfo
    {
        public readonly IntPtr HWnd;
        public readonly Rect Bounds;
        public readonly bool IsDiscord;

        public WindowInfo(IntPtr hWnd, Rect bounds, bool isDiscord = false)
        {
            HWnd = hWnd;
            Bounds = bounds;
            IsDiscord = isDiscord;
        }
    }

    /// <summary>
    /// Enumerates ALL visible, valid top-level windows in top-to-bottom Z-order.
    /// Includes maximized and fullscreen windows so they properly occlude windows beneath them.
    /// </summary>
    public List<WindowInfo> EnumerateWindowsInZOrder(Visual dpiSource, Rect workArea)
    {
        var result = new List<WindowInfo>();
        var dpi = VisualTreeHelper.GetDpi(dpiSource);
        IntPtr shellWnd = NativeMethods.GetShellWindow();
        IntPtr desktopWnd = NativeMethods.GetDesktopWindow();
        uint currentPid = (uint)Environment.ProcessId;

        NativeMethods.EnumWindows((hWnd, lParam) =>
        {
            if (hWnd == IntPtr.Zero || hWnd == _ownWindowHandle || hWnd == shellWnd || hWnd == desktopWnd)
                return true; // continue enumeration

            // Exclude ALL windows belonging to CatNur's own process (MainWindow, SunflowerView, NotificationView, etc.)
            NativeMethods.GetWindowThreadProcessId(hWnd, out uint windowPid);
            if (windowPid == currentPid)
                return true;

            if (!NativeMethods.IsWindowVisible(hWnd) || NativeMethods.IsIconic(hWnd))
                return true;

            // Exclude ToolWindows (floating toolbars, overlays, notification balloons)
            int exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
            if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0)
                return true;

            // Exclude cloaked background UWP/system windows (Settings, Search, Cortana, etc.)
            int cloaked = 0;
            int hrCloak = NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_CLOAKED, out cloaked, sizeof(int));
            if (hrCloak == 0 && cloaked != 0)
                return true;

            // Real application windows have a title/text. Invisible background helper windows have empty titles.
            if (NativeMethods.GetWindowTextLength(hWnd) <= 0)
                return true;

            // Class name filter
            var sb = new StringBuilder(256);
            if (NativeMethods.GetClassName(hWnd, sb, sb.Capacity) > 0)
            {
                string className = sb.ToString();
                for (int i = 0; i < ExcludedClasses.Length; i++)
                {
                    if (string.Equals(className, ExcludedClasses[i], StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            // Get visual bounds via DWM (excludes drop shadows)
            NativeMethods.RECT rect;
            int hr = NativeMethods.DwmGetWindowAttribute(
                hWnd,
                NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
                out rect,
                Marshal.SizeOf<NativeMethods.RECT>());

            if (hr != 0)
            {
                if (!NativeMethods.GetWindowRect(hWnd, out rect))
                    return true;
            }

            double left = rect.Left / dpi.DpiScaleX;
            double top = rect.Top / dpi.DpiScaleY;
            double width = rect.Width / dpi.DpiScaleX;
            double height = rect.Height / dpi.DpiScaleY;

            // Ignore windows located outside the pet's current monitor
            if (left >= workArea.Right || (left + width) <= workArea.Left || top >= workArea.Bottom || (top + height) <= workArea.Top)
                return true;

            // Detect if this window belongs to Discord
            bool isDiscord = false;
            try
            {
                var proc = System.Diagnostics.Process.GetProcessById((int)windowPid);
                if (proc.ProcessName.Contains("Discord", StringComparison.OrdinalIgnoreCase))
                {
                    isDiscord = true;
                }
            }
            catch { }

            // Include real windows on the primary monitor
            result.Add(new WindowInfo(hWnd, new Rect(left, top, width, height), isDiscord));
            return true;
        }, IntPtr.Zero);

        return result;
    }

    private double _previousWindowVelY;

    /// <summary>
    /// Performs a lightweight, 60 FPS per-frame tracking of the pet's current standing window.
    /// Follows horizontal and vertical window movements synchronously with zero lag.
    /// Supports dynamic platform inertia:
    /// - Fast downward pull (>400 px/s) drops platform from under feet.
    /// - Fast upward flick with abrupt stop flings the cat into the air with momentum.
    /// - Smooth upward/downward dragging carries the cat with the window.
    /// </summary>
    public void TrackStandingWindowPerFrame(Visual dpiSource, PetBody body)
    {
        if (body.StandingHWnd == IntPtr.Zero || !body.IsGrounded || body.IsDragged)
        {
            _previousWindowVelY = 0;
            return;
        }

        IntPtr hWnd = body.StandingHWnd;

        // Verify window existence and visibility
        if (!NativeMethods.IsWindow(hWnd) || !NativeMethods.IsWindowVisible(hWnd) || NativeMethods.IsIconic(hWnd))
        {
            _previousWindowVelY = 0;
            body.StandingHWnd = IntPtr.Zero;
            body.CurrentSurface = null;
            body.IsGrounded = false;
            return;
        }

        // Get updated visual bounds in DIPs
        NativeMethods.RECT rect;
        int hr = NativeMethods.DwmGetWindowAttribute(
            hWnd,
            NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
            out rect,
            Marshal.SizeOf<NativeMethods.RECT>());

        if (hr != 0)
        {
            if (!NativeMethods.GetWindowRect(hWnd, out rect))
            {
                _previousWindowVelY = 0;
                body.StandingHWnd = IntPtr.Zero;
                body.CurrentSurface = null;
                body.IsGrounded = false;
                return;
            }
        }

        var dpi = VisualTreeHelper.GetDpi(dpiSource);
        double left = rect.Left / dpi.DpiScaleX;
        double top = rect.Top / dpi.DpiScaleY;
        double width = rect.Width / dpi.DpiScaleX;
        double height = rect.Height / dpi.DpiScaleY;
        double right = left + width;

        double footLeft = body.Left + 35.0;
        double footRight = body.Right - 35.0;

        // Check if pet's feet are still supported on top of this window
        if (footRight <= left || footLeft >= right)
        {
            // Cat walked off the edge -> trigger natural fall
            _previousWindowVelY = 0;
            body.StandingHWnd = IntPtr.Zero;
            body.CurrentSurface = null;
            body.IsGrounded = false;
            return;
        }

        // Compute surface coordinates accounting for Discord sub-platforms
        double surfaceLeft = left;
        double surfaceRight = right;
        double surfaceTop = top;
        string? currentTag = body.CurrentSurface?.Tag;

        if (currentTag != null)
        {
            if (currentTag == "Discord_VoicePanel")
            {
                surfaceLeft = left + 72.0;
                surfaceRight = left + 240.0;
                surfaceTop = top + height - 160.0;
            }
            else if (currentTag == "Discord_ChatInput")
            {
                double chatRight = IsDiscordMemberListOpen(new Rect(left, top, right - left, height)) ? (right - 260.0) : (right - 32.0);
                surfaceLeft = left + 328.0;
                surfaceRight = chatRight;
                surfaceTop = top + height - 72.0;
            }
        }

        // Synchronously follow window movement (deltaX and deltaY) with realistic inertia
        if (body.CurrentSurface != null)
        {
            double oldTop = body.CurrentSurface.Top;
            double deltaY = surfaceTop - oldTop;
            double deltaX = surfaceLeft - body.CurrentSurface.Left;

            double winVelY = deltaY * 60.0; // Vertical window velocity in px/s
            double winVelX = deltaX * 60.0; // Horizontal window velocity in px/s

            // 1. FAST DOWNWARD DROP:
            // If the window is pulled downwards faster than gravity (> 400 px/s),
            // the platform drops out from under the cat, leaving it to fall naturally with gravity!
            if (winVelY > 400.0)
            {
                _previousWindowVelY = 0;
                body.StandingHWnd = IntPtr.Zero;
                body.CurrentSurface = null;
                body.IsGrounded = false;
                body.Velocity = new Vector(body.Velocity.X + winVelX * 0.3, 50.0);
                return;
            }

            // 2. UPWARD INERTIAL LAUNCH (FLING ON ABRUPT STOP / DECELERATION):
            // If the window WAS moving fast upwards (< -350 px/s) and suddenly stopped or slowed down,
            // the cat's upward momentum launches it into the air!
            if (_previousWindowVelY < -350.0 && winVelY > _previousWindowVelY + 250.0)
            {
                double launchVy = Math.Clamp(_previousWindowVelY, -1400.0, -350.0);
                _previousWindowVelY = 0;

                body.StandingHWnd = IntPtr.Zero;
                body.CurrentSurface = null;
                body.IsGrounded = false;
                body.Velocity = new Vector(body.Velocity.X + winVelX * 0.4, launchVy);
                return;
            }

            _previousWindowVelY = winVelY;

            // 3. STEADY MOVEMENT (UPWARD & HORIZONTAL):
            // Move pet with window so it rides smoothly on top of this surface
            body.Position = new Point(body.Position.X + deltaX, surfaceTop - body.Height);

            // Update stored surface with latest bounds
            body.CurrentSurface = new GroundSurface(surfaceLeft, surfaceRight, surfaceTop, SurfaceType.WindowTop, hWnd, top + height, currentTag);
        }
        else
        {
            _previousWindowVelY = 0;
            body.Position = new Point(body.Position.X, surfaceTop - body.Height);
            body.CurrentSurface = new GroundSurface(surfaceLeft, surfaceRight, surfaceTop, SurfaceType.WindowTop, hWnd, top + height, currentTag);
        }
    }

    /// <summary>
    /// Gets all candidate non-occluded window platforms in Z-order.
    /// Also detects newly opened windows and validates the standing platform.
    /// </summary>
    public List<GroundSurface> GetWindowSurfaces(Visual dpiSource, PetBody body, Rect workArea)
    {
        var surfaces = new List<GroundSurface>();
        var windows = EnumerateWindowsInZOrder(dpiSource, workArea);

        // If the pet just switched monitors, treat this scan as a fresh baseline instead of
        // reporting every already-open window on the new monitor as "newly opened" (which would
        // otherwise trigger a surprise involuntary jump right after the user placed it there).
        bool monitorChanged = _lastWorkArea == null || _lastWorkArea.Value != workArea;
        _lastWorkArea = workArea;

        var currentHwnds = new HashSet<IntPtr>();
        GroundSurface? newlyOpenedTarget = null;

        bool currentPlatformStillValid = false;

        // Process windows from top of Z-order to bottom
        for (int i = 0; i < windows.Count; i++)
        {
            var win = windows[i];
            var bounds = win.Bounds;
            currentHwnds.Add(win.HWnd);

            // Compute visible, non-occluded horizontal segments of this window's top landing edge
            var segments = new List<(double Left, double Right)> { (bounds.Left, bounds.Right) };

            for (int j = 0; j < i; j++)
            {
                var higherWin = windows[j].Bounds;

                // Check if higher window vertically covers the top edge of this window
                if (higherWin.Top <= bounds.Top + 10.0 && higherWin.Bottom >= bounds.Top - 5.0)
                {
                    var newSegments = new List<(double Left, double Right)>();
                    for (int k = 0; k < segments.Count; k++)
                    {
                        var seg = segments[k];
                        // No horizontal overlap
                        if (higherWin.Right <= seg.Left || higherWin.Left >= seg.Right)
                        {
                            newSegments.Add(seg);
                        }
                        else
                        {
                            // Overlap! Keep visible left part
                            if (higherWin.Left > seg.Left)
                            {
                                newSegments.Add((seg.Left, higherWin.Left));
                            }
                            // Keep visible right part
                            if (higherWin.Right < seg.Right)
                            {
                                newSegments.Add((higherWin.Right, seg.Right));
                            }
                        }
                    }
                    segments = newSegments;
                }
            }

            // Filter segments by minimum walkable width (60px) and add walkable surfaces within primary work area
            if (bounds.Top >= 25 && bounds.Top <= workArea.Bottom - 40 && bounds.Height >= 40 &&
                bounds.Left < workArea.Right && bounds.Right > workArea.Left)
            {
                for (int s = 0; s < segments.Count; s++)
                {
                    var seg = segments[s];
                    double validLeft = Math.Max(seg.Left, workArea.Left);
                    double validRight = Math.Min(seg.Right, workArea.Right);

                    if ((validRight - validLeft) >= 60.0)
                    {
                        var surface = new GroundSurface(
                            left: validLeft,
                            right: validRight,
                            top: bounds.Top,
                            type: SurfaceType.WindowTop,
                            hWnd: win.HWnd,
                            bottom: bounds.Bottom
                        );

                        surfaces.Add(surface);

                        // Detect newly opened window (not seen in previous scan)
                        if (!_isFirstScan && !monitorChanged && !_knownHwnds.Contains(win.HWnd))
                        {
                            if (newlyOpenedTarget == null)
                            {
                                newlyOpenedTarget = surface;
                            }
                        }
                    }
                }
            }

            // ── Discord Sub-Surfaces (Voice Panel and Chat Input) ──
            if (win.IsDiscord && bounds.Width >= 600 && bounds.Height >= 400 &&
                bounds.Left < workArea.Right && bounds.Right > workArea.Left)
            {
                var discordSubSurfaces = new List<(double Left, double Right, double Top, string Tag)>();

                // 1. Sol Alt Ses & Aktivite Paneli (Bottom-Left Voice/Activity Box - fixed on left sidebar)
                discordSubSurfaces.Add((bounds.Left + 72.0, bounds.Left + 240.0, bounds.Bottom - 160.0, "Discord_VoicePanel"));

                // 2. Sohbet Mesaj Yazma Barı (Chat Message Input Box - ONLY on Chat Screen)
                bool inCallView = IsDiscordInCallView(bounds);
                if (!inCallView)
                {
                    double chatRight = IsDiscordMemberListOpen(bounds) ? (bounds.Right - 260.0) : (bounds.Right - 32.0);
                    discordSubSurfaces.Add((bounds.Left + 328.0, chatRight, bounds.Bottom - 72.0, "Discord_ChatInput"));
                }

                for (int d = 0; d < discordSubSurfaces.Count; d++)
                {
                    var dSub = discordSubSurfaces[d];
                    double validSubLeft = Math.Max(dSub.Left, workArea.Left);
                    double validSubRight = Math.Min(dSub.Right, workArea.Right);

                    if (dSub.Top >= 25 && dSub.Top <= workArea.Bottom - 40 && (validSubRight - validSubLeft) >= 60.0)
                    {
                        // Check if covered by any higher window in Z-order
                        bool isOccluded = false;
                        for (int j = 0; j < i; j++)
                        {
                            var higherWin = windows[j].Bounds;
                            if (higherWin.Left < validSubRight && higherWin.Right > validSubLeft &&
                                higherWin.Top <= dSub.Top + 10.0 && higherWin.Bottom >= dSub.Top - 5.0)
                            {
                                isOccluded = true;
                                break;
                            }
                        }

                        if (!isOccluded)
                        {
                            surfaces.Add(new GroundSurface(
                                left: validSubLeft,
                                right: validSubRight,
                                top: dSub.Top,
                                type: SurfaceType.WindowTop,
                                hWnd: win.HWnd,
                                bottom: bounds.Bottom,
                                tag: dSub.Tag
                            ));
                        }
                    }
                }
            }

            // Check if the window the pet is standing on is still valid and NOT covered under the pet's feet
            if (body.StandingHWnd != IntPtr.Zero && body.StandingHWnd == win.HWnd)
            {
                double winFootLeft = body.Left + 35.0;
                double winFootRight = body.Right - 35.0;

                // Check standing surface bounds (either top window edge or specific Discord sub-surface)
                double standLeft = body.CurrentSurface?.Left ?? bounds.Left;
                double standRight = body.CurrentSurface?.Right ?? bounds.Right;
                double standTop = body.CurrentSurface?.Top ?? bounds.Top;

                // Pet feet must be within the standing surface bounds
                if (winFootRight > standLeft && winFootLeft < standRight)
                {
                    // Check if any higher window in Z-order physically covers the pet's feet
                    bool coveredByHigherWindow = false;
                    for (int j = 0; j < i; j++)
                    {
                        var higherWin = windows[j].Bounds;
                        if (higherWin.Left < winFootRight && higherWin.Right > winFootLeft)
                        {
                            if (higherWin.Top <= standTop + 10.0 && higherWin.Bottom >= standTop - 5.0)
                            {
                                coveredByHigherWindow = true;
                                break;
                            }
                        }
                    }

                    if (!coveredByHigherWindow)
                    {
                        currentPlatformStillValid = true;
                    }
                }
            }
        }

        // If the pet was standing on a window, but that window is no longer valid,
        // was closed, minimized, moved away from under the pet, or covered by a higher window:
        if (body.StandingHWnd != IntPtr.Zero && !currentPlatformStillValid && body.IsGrounded)
        {
            body.StandingHWnd = IntPtr.Zero;
            body.CurrentSurface = null;
            body.IsGrounded = false; // Trigger natural gravity fall
        }

        // Update known HWNDs
        _knownHwnds.Clear();
        foreach (var h in currentHwnds)
        {
            _knownHwnds.Add(h);
        }
        _isFirstScan = false;

        // Notify if a new window was opened
        if (newlyOpenedTarget != null)
        {
            NewWindowDetected?.Invoke(newlyOpenedTarget);
        }

        return surfaces;
    }

    private static readonly Random _jumpTargetRandom = new();

    /// <summary>
    /// Calculates universal ballistic jump velocity (Vx, Vy) from any point to any target point.
    /// </summary>
    public static bool TryCalculatePointJumpVelocity(double currentX, double currentY, double targetX, double targetY, out Vector launchVelocity, double apexExtra = 60.0)
    {
        launchVelocity = default;
        double deltaX = targetX - currentX;
        double deltaY = currentY - targetY;
        double g = PetConfiguration.Gravity;

        double vy;
        double tUp;
        double tDown;

        if (deltaY > 0)
        {
            double hPeak = deltaY + apexExtra;
            vy = -Math.Sqrt(2.0 * g * hPeak);
            tUp = Math.Abs(vy) / g;
            tDown = Math.Sqrt(2.0 * apexExtra / g);
        }
        else
        {
            double hHop = apexExtra;
            vy = -Math.Sqrt(2.0 * g * hHop);
            tUp = Math.Abs(vy) / g;
            tDown = Math.Sqrt(2.0 * (hHop - deltaY) / g);
        }

        double tTotal = tUp + tDown;
        if (tTotal <= 0.05)
            return false;

        double k = -60.0 * Math.Log(PetConfiguration.AirDrag);
        double effectiveTime = (1.0 - Math.Exp(-k * tTotal)) / k;
        double vx = (effectiveTime > 0.01) ? (deltaX / effectiveTime) : (deltaX / tTotal);

        if (Math.Abs(vx) > 1700.0)
            vx = Math.Sign(vx) * 1700.0;
        if (Math.Abs(vy) > 2300.0)
            vy = -2300.0;

        launchVelocity = new Vector(vx, vy);
        return true;
    }

    /// <summary>
    /// Calculates universal ballistic jump velocity (Vx, Vy) from the pet's current position
    /// to land gracefully on a random safe spot along the top edge of the target window.
    /// </summary>
    public static bool TryCalculateJumpVelocity(PetBody body, GroundSurface targetSurface, out Vector launchVelocity, double? specificTargetX = null)
    {
        launchVelocity = default;

        double currentY = body.Bottom;
        double currentX = (body.Left + body.Right) / 2.0;

        // Dynamically pick a random safe landing spot anywhere along the window surface
        double minX = targetSurface.Left + 45.0;
        double maxX = targetSurface.Right - 45.0;
        double targetX;

        if (specificTargetX.HasValue)
        {
            targetX = specificTargetX.Value;
        }
        else if (maxX > minX)
        {
            targetX = minX + _jumpTargetRandom.NextDouble() * (maxX - minX);
        }
        else
        {
            targetX = (targetSurface.Left + targetSurface.Right) / 2.0;
        }

        return TryCalculatePointJumpVelocity(currentX, currentY, targetX, targetSurface.Top, out launchVelocity, 60.0);
    }

    /// <summary>
    /// Checks whether Discord is currently displaying the Call Screen (deep black background)
    /// or the Text Chat Screen (dark slate grey background) by sampling the main content background color.
    /// </summary>
    private static bool IsDiscordInCallView(Rect bounds)
    {
        try
        {
            IntPtr hdc = NativeMethods.GetDC(IntPtr.Zero);
            if (hdc == IntPtr.Zero) return false;

            int sampleX = (int)(bounds.Left + 450.0);
            int sampleY = (int)(bounds.Top + 140.0);

            uint pixel = NativeMethods.GetPixel(hdc, sampleX, sampleY);
            NativeMethods.ReleaseDC(IntPtr.Zero, hdc);

            int r = (int)(pixel & 0xFF);
            int g = (int)((pixel >> 8) & 0xFF);
            int b = (int)((pixel >> 16) & 0xFF);

            // In Discord Call View, the main area background is deep pitch black (RGB <= 28, e.g. #000000 or #111214)
            // In Discord Chat View, the main area background is dark slate grey (RGB ~ 49, 51, 56)
            return (r <= 28 && g <= 28 && b <= 28);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks whether Discord's right member/friends list is open.
    /// When open, the chat input box stops ~260px before the right edge.
    /// When closed, it extends to ~32px before the right edge.
    /// </summary>
    private static bool IsDiscordMemberListOpen(Rect bounds)
    {
        try
        {
            IntPtr hdc = NativeMethods.GetDC(IntPtr.Zero);
            if (hdc == IntPtr.Zero) return true;

            int sampleX = (int)(bounds.Right - 80.0);
            int sampleY = (int)(bounds.Bottom - 60.0);

            uint pixel = NativeMethods.GetPixel(hdc, sampleX, sampleY);
            NativeMethods.ReleaseDC(IntPtr.Zero, hdc);

            int r = (int)(pixel & 0xFF);
            int g = (int)((pixel >> 8) & 0xFF);
            int b = (int)((pixel >> 16) & 0xFF);

            // Member list panel is dark background (RGB <= 46).
            // Input container when stretched fully is lighter (RGB ~ 56).
            return (r <= 46 && g <= 46 && b <= 46);
        }
        catch
        {
            return true;
        }
    }
}
