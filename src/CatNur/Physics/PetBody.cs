namespace CatNur.Physics;

using System;
using System.Windows;

/// <summary>
/// Represents the pet's physical body: position, velocity, dimensions, and state flags.
/// Pure data object — stores current physical state and active platform handle.
/// </summary>
public class PetBody
{
    public Point Position { get; set; }
    public Vector Velocity { get; set; }

    public double Width { get; }
    public double Height { get; }

    /// <summary>True when the pet is resting on a surface.</summary>
    public bool IsGrounded { get; set; }

    /// <summary>True when the user is dragging the pet.</summary>
    public bool IsDragged { get; set; }

    /// <summary>The specific window handle (HWND) the pet is standing on, or IntPtr.Zero for screen floor / in air.</summary>
    public IntPtr StandingHWnd { get; set; } = IntPtr.Zero;

    /// <summary>The active surface the pet is currently resting on.</summary>
    public GroundSurface? CurrentSurface { get; set; }

    /// <summary>Initial 15-second grace period on a new window where pet turns around at edges instead of falling.</summary>
    public double PlatformSafetyTimer { get; set; } = 0;

    /// <summary>When jumping downward to a lower platform, ignore surfaces above this Y coordinate to allow passage.</summary>
    public double IgnoreSurfacesAboveY { get; set; } = 0;

    /// <summary>The HWND of the takeoff platform to ignore during jump liftoff so the pet doesn't land back on it immediately.</summary>
    public IntPtr JumpLaunchSourceHWnd { get; set; } = IntPtr.Zero;
    public double JumpLaunchIgnoreTimer { get; set; } = 0;

    /// <summary>True during the 0.35-second anticipation crouch before launching into a jump.</summary>
    public bool IsPreparingJump { get; set; } = false;
    public double JumpPreparationTimer { get; set; } = 0;
    public Vector PendingJumpVelocity { get; set; }
    public IntPtr PendingJumpTargetHwnd { get; set; } = IntPtr.Zero;
    public double PendingIgnoreSurfacesAboveY { get; set; } = 0;

    /// <summary>True while an action animation is visually playing. Prevents any state or physics from moving the pet horizontally.</summary>
    public bool IsActionLocked { get; set; } = false;

    /// <summary>True while the pet is touching a wall and slowly sliding down.</summary>
    public bool IsWallSliding { get; set; } = false;

    /// <summary>-1 if wall is on the left, +1 if wall is on the right, 0 if not against a wall.</summary>
    public int WallSide { get; set; } = 0;

    /// <summary>Parkour 2-stage wall bounce jump properties.</summary>
    public bool IsParkourWallJump { get; set; } = false;
    public GroundSurface? ParkourFinalTarget { get; set; }
    public double ParkourWallGripTimer { get; set; } = 0;
    public bool ParkourWallKickPending { get; set; } = false;

    /// <summary>Time spent sliding on the wall in seconds. After 2.0s, triggers autonomous wall jump to a window or ground.</summary>
    public double WallSlideTimer { get; set; } = 0;

    // ── Computed bounds ────────────────────────────────────────

    public double Left => Position.X;
    public double Top => Position.Y;
    public double Right => Position.X + Width;
    public double Bottom => Position.Y + Height;

    /// <summary>The bounding rectangle of the pet in screen coordinates.</summary>
    public Rect Bounds => new(Position.X, Position.Y, Width, Height);

    public PetBody(double width, double height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Applies a velocity impulse (adds to current velocity).
    /// Used for throw mechanics and knockback effects.
    /// </summary>
    public void ApplyImpulse(Vector impulse)
    {
        Velocity += impulse;
    }
}
