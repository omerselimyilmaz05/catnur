namespace CatNur.Core;

/// <summary>
/// Central configuration for all physics constants, timing values, and pet parameters.
/// Avoids magic numbers scattered across the codebase.
/// </summary>
public static class PetConfiguration
{
    // ── Pet Dimensions ─────────────────────────────────────────
    public const double PetWidth = 100;
    public const double PetHeight = 100;

    // ── Physics ────────────────────────────────────────────────
    /// <summary>Gravity acceleration in pixels/second² (tuned for crisp, natural 2D fall).</summary>
    public const double Gravity = 2400.0;

    /// <summary>Terminal fall velocity in pixels/second to prevent tunneling.</summary>
    public const double MaxFallSpeed = 1800.0;

    /// <summary>Air drag multiplier applied per normalized frame (at 60fps).</summary>
    public const double AirDrag = 0.98;

    /// <summary>Ground friction multiplier applied on ground contact.</summary>
    public const double GroundFriction = 0.85;

    /// <summary>Bounce coefficient (0 = no bounce, 1 = perfectly elastic).</summary>
    public const double Restitution = 0.35;

    /// <summary>Below this vertical speed (px/s), stop bouncing and settle.</summary>
    public const double MinBounceVelocity = 150.0;

    // ── Throw Mechanics ────────────────────────────────────────
    /// <summary>Maximum throw speed in pixels/second (balanced middle-ground).</summary>
    public const double MaxThrowSpeed = 1500.0;

    /// <summary>Minimum throw speed threshold; below this, gentle drop occurs.</summary>
    public const double MinThrowSpeed = 110.0;

    /// <summary>Damping multiplier applied to raw mouse throw velocity.</summary>
    public const double ThrowDamping = 0.88;

    /// <summary>Duration of mouse history buffer for throw calculation (seconds).</summary>
    public const double MouseHistoryDuration = 0.11;

    // ── Game Loop ──────────────────────────────────────────────
    /// <summary>Fixed physics timestep (seconds). 1/60 = ~16.67ms.</summary>
    public const double FixedTimeStep = 1.0 / 60.0;

    /// <summary>Maximum frame time cap to prevent spiral of death (seconds).</summary>
    public const double MaxFrameTime = 0.25;

    // ── Foreground Window ──────────────────────────────────────
    /// <summary>How often to poll window surfaces in Z-order for occlusion (milliseconds).</summary>
    public const int ForegroundWindowPollMs = 60;

    // ── FSM Timing (future) ────────────────────────────────────
    /// <summary>Minimum idle time before wandering (seconds).</summary>
    public const double IdleMinDuration = 2.0;

    /// <summary>Maximum idle time before wandering (seconds).</summary>
    public const double IdleMaxDuration = 6.0;

    /// <summary>Wander speed in pixels/second.</summary>
    public const double WanderSpeed = 60.0;

    /// <summary>Maximum wander distance in pixels.</summary>
    public const double WanderMaxDistance = 300.0;
}
