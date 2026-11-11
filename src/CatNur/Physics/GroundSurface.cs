namespace CatNur.Physics;

using System;

/// <summary>
/// Type of ground surface the pet can stand on.
/// </summary>
public enum SurfaceType
{
    /// <summary>The bottom edge of the screen / work area.</summary>
    ScreenBottom,

    /// <summary>The top edge of a visible window.</summary>
    WindowTop
}

/// <summary>
/// Represents a horizontal surface the pet can land on.
/// Defined by its horizontal span (Left..Right), vertical position (Top),
/// and associated window handle (if any).
/// </summary>
public class GroundSurface
{
    /// <summary>Left edge of the surface in screen coordinates (DIPs).</summary>
    public double Left { get; }

    /// <summary>Right edge of the surface in screen coordinates (DIPs).</summary>
    public double Right { get; }

    /// <summary>The Y coordinate of the walking surface (where the pet's bottom rests).</summary>
    public double Top { get; }

    /// <summary>The bottom Y coordinate of the window (if applicable).</summary>
    public double Bottom { get; }

    /// <summary>What kind of surface this is.</summary>
    public SurfaceType Type { get; }

    /// <summary>The window handle (HWND) this platform belongs to, or IntPtr.Zero for screen floor.</summary>
    public IntPtr HWnd { get; }

    /// <summary>Optional sub-surface tag (e.g. "Discord_VoicePanel", "Discord_CallTile_1", "Discord_ChatInput").</summary>
    public string? Tag { get; }

    public GroundSurface(double left, double right, double top, SurfaceType type, IntPtr hWnd = default, double bottom = 0, string? tag = null)
    {
        Left = left;
        Right = right;
        Top = top;
        Bottom = bottom;
        Type = type;
        HWnd = hWnd;
        Tag = tag;
    }

    /// <summary>
    /// Checks whether the pet's feet (center of gravity) are supported by this surface.
    /// Uses a 35px foot margin on the 100px bounding box so the pet falls when stepping off edges.
    /// </summary>
    public bool OverlapsHorizontally(double bodyLeft, double bodyRight)
    {
        if (Type == SurfaceType.ScreenBottom)
        {
            return bodyRight > Left && bodyLeft < Right;
        }

        // Window platforms: center of mass / feet must be supported
        double footLeft = bodyLeft + 35.0;
        double footRight = bodyRight - 35.0;
        return footRight > Left && footLeft < Right;
    }
}
