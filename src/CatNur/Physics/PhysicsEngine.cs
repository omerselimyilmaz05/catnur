namespace CatNur.Physics;

using System;
using System.Collections.Generic;
using System.Windows;
using CatNur.Core;

/// <summary>
/// Simple 2D physics engine using semi-implicit Euler integration.
/// Applies gravity, air drag, and delegates collision resolution to <see cref="CollisionDetector"/>.
/// </summary>
public class PhysicsEngine
{
    private readonly CollisionDetector _collisionDetector;

    public PhysicsEngine(CollisionDetector collisionDetector)
    {
        _collisionDetector = collisionDetector;
    }

    /// <summary>
    /// Advances the physics simulation by one fixed timestep.
    /// </summary>
    /// <param name="body">The pet's physical body.</param>
    /// <param name="dt">Delta time in seconds (should be <see cref="PetConfiguration.FixedTimeStep"/>).</param>
    /// <param name="surfaces">Available ground surfaces.</param>
    /// <param name="screenBounds">Usable screen area (work area excluding taskbar).</param>
    public void Update(PetBody body, double dt, IReadOnlyList<GroundSurface>? surfaces, Rect screenBounds)
    {
        // If dragged, physics engine does not integrate position
        if (body.IsDragged)
            return;

        // ── Active Tracked Window Platform ─────────────────────
        // If the pet is grounded on an active 60 FPS tracked window, lock its vertical position to the platform.
        // This ensures moving the window upwards (or in any direction) never drops the pet due to stale periodic collision scans.
        if (body.StandingHWnd != IntPtr.Zero && body.IsGrounded && body.CurrentSurface != null && body.CurrentSurface.Type == SurfaceType.WindowTop)
        {
            var velocity = body.Velocity;
            velocity.Y = 0; // Vertical velocity locked to platform

            double dragFactor = Math.Pow(PetConfiguration.AirDrag, dt * 60.0);
            velocity.X *= dragFactor;
            body.Velocity = velocity;

            var position = body.Position;
            position.X += velocity.X * dt;
            position.Y = body.CurrentSurface.Top - body.Height;
            body.Position = position;

            // Enforce screen boundaries
            _collisionDetector.ResolveScreenBoundaries(body, screenBounds);
            return;
        }

        // ── Free / Airborne Physics Simulation ─────────────────
        // Semi-implicit Euler integration
        var vel = body.Velocity;

        // Wall Slide / Clinging Detection (Screen outer boundaries in mid-air)
        bool isTouchingLeftWall = (body.Left <= screenBounds.Left + 5.0 && body.Bottom < screenBounds.Bottom - 15.0);
        bool isTouchingRightWall = (body.Right >= screenBounds.Right - 5.0 && body.Bottom < screenBounds.Bottom - 15.0);
        int wallSide = isTouchingLeftWall ? -1 : (isTouchingRightWall ? 1 : 0);

        // Apply Wall Slide friction when against screen edges in mid-air (never during active jump liftoff)
        if (wallSide != 0 && body.JumpLaunchIgnoreTimer <= 0 && (vel.Y > 0 || body.IsWallSliding))
        {
            body.IsWallSliding = true;
            body.WallSide = wallSide;

            // Gentle wall slide speed (150 px/s instead of 1800 px/s terminal fall speed)
            const double wallSlideSpeed = 150.0;
            vel.Y = Math.Min(vel.Y + 200.0 * dt, wallSlideSpeed);
            vel.X = 0; // Hold flush against the screen wall

            // Snap X position so paws touch the screen border perfectly with zero gap!
            var currentPos = body.Position;
            currentPos.X = (wallSide == 1) ? (screenBounds.Right - body.Width + 28.0) : (screenBounds.Left - 28.0);
            body.Position = currentPos;
        }
        else
        {
            body.IsWallSliding = false;
            body.WallSide = 0;

            // Apply standard gravity
            vel.Y += PetConfiguration.Gravity * dt;

            // Clamp downward velocity to terminal fall speed
            if (vel.Y > PetConfiguration.MaxFallSpeed)
                vel.Y = PetConfiguration.MaxFallSpeed;
        }

        // Apply air drag
        double drag = Math.Pow(PetConfiguration.AirDrag, dt * 60.0);
        vel.X *= drag;
        body.Velocity = vel;

        // Integrate position
        double previousBottom = body.Bottom;

        var pos = body.Position;
        pos.X += vel.X * dt;
        pos.Y += vel.Y * dt;
        body.Position = pos;

        // Reset grounded flag — collision detector will set it if appropriate
        body.IsGrounded = false;

        // ── Resolve collisions ─────────────────────────────────
        _collisionDetector.ResolveCollisions(body, previousBottom, surfaces ?? Array.Empty<GroundSurface>(), screenBounds);
    }
}
