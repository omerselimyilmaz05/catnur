namespace CatNur.Physics;

using System;
using System.Collections.Generic;
using System.Windows;
using CatNur.Core;

/// <summary>
/// Detects and resolves AABB collisions between the pet body and ground surfaces / screen boundaries.
/// Strictly enforces directional collision from above to prevent teleportation onto platforms.
/// </summary>
public class CollisionDetector
{
    /// <summary>
    /// Resolves all collisions: ground surfaces first, then screen boundaries.
    /// </summary>
    /// <param name="body">The pet body.</param>
    /// <param name="previousBottom">The Y coordinate of the pet's bottom before this step's movement.</param>
    /// <param name="surfaces">Available ground surfaces.</param>
    /// <param name="screenBounds">Screen work area.</param>
    public void ResolveCollisions(PetBody body, double previousBottom, IReadOnlyList<GroundSurface> surfaces, Rect screenBounds)
    {
        ResolveGroundCollisions(body, previousBottom, surfaces);
        ResolveScreenBoundaries(body, screenBounds);
    }

    /// <summary>
    /// Checks each ground surface for collision with the pet.
    /// The pet can only land on a surface when falling from above it.
    /// </summary>
    private void ResolveGroundCollisions(PetBody body, double previousBottom, IReadOnlyList<GroundSurface> surfaces)
    {
        // Only check when moving downward or resting
        if (body.Velocity.Y < 0)
            return;

        double currentBottom = body.Bottom;
        GroundSurface? bestSurface = null;
        double bestSurfaceTop = double.MaxValue;

        for (int i = 0; i < surfaces.Count; i++)
        {
            var surface = surfaces[i];

            // If performing an intentional downward jump, ignore platforms higher than the target
            if (body.IgnoreSurfacesAboveY > 0 && surface.Top < body.IgnoreSurfacesAboveY)
                continue;

            // While actively jumping off a platform, ignore the takeoff platform so the pet clears it cleanly
            if (body.JumpLaunchIgnoreTimer > 0 && body.JumpLaunchSourceHWnd != IntPtr.Zero && surface.HWnd == body.JumpLaunchSourceHWnd)
                continue;

            // Strict directional top landing check
            if (previousBottom <= surface.Top + 2.0 && currentBottom >= surface.Top - 2.0)
            {
                if (surface.OverlapsHorizontally(body.Left, body.Right))
                {
                    if (surface.Top < bestSurfaceTop)
                    {
                        bestSurfaceTop = surface.Top;
                        bestSurface = surface;
                    }
                }
            }
        }

        if (bestSurface != null)
        {
            LandOnSurface(body, bestSurface);
        }
    }

    /// <summary>
    /// Snaps the pet cleanly on top of a surface and resolves velocity/restitution.
    /// </summary>
    private void LandOnSurface(PetBody body, GroundSurface surface)
    {
        var position = body.Position;
        position.Y = surface.Top - body.Height;
        body.Position = position;

        body.CurrentSurface = surface;
        if (surface.Type == SurfaceType.WindowTop && surface.HWnd != IntPtr.Zero)
        {
            body.StandingHWnd = surface.HWnd;
            body.PlatformSafetyTimer = 15.0;
        }
        else
        {
            body.StandingHWnd = IntPtr.Zero;
            body.PlatformSafetyTimer = 0;
        }

        var velocity = body.Velocity;

        // Bounce or settle
        if (Math.Abs(velocity.Y) > PetConfiguration.MinBounceVelocity)
        {
            // Bounce: invert vertical velocity with energy loss
            velocity.Y = -velocity.Y * PetConfiguration.Restitution;
        }
        else
        {
            // Settle: stop vertical movement
            velocity.Y = 0;
            body.IsGrounded = true;
        }

        // Apply ground friction to horizontal movement
        velocity.X *= PetConfiguration.GroundFriction;

        // Stop tiny horizontal movement
        if (Math.Abs(velocity.X) < 1.0)
            velocity.X = 0;

        body.Velocity = velocity;
        body.IgnoreSurfacesAboveY = 0;
    }

    /// <summary>
    /// Enforces screen boundaries:
    /// - Bounces off left and right screen edges.
    /// - Sky Portal (Screen Wrap): If the pet falls below the taskbar/screen bottom,
    ///   it respawns from the sky directly above that same horizontal position!
    /// </summary>
    public void ResolveScreenBoundaries(PetBody body, Rect screenBounds)
    {
        var position = body.Position;
        var velocity = body.Velocity;

        // Left boundary
        if (position.X <= screenBounds.Left && velocity.X <= 50.0)
        {
            // If in mid-air above ground level, catch the wall with claws!
            if (position.Y + body.Height < screenBounds.Bottom - 15.0)
            {
                body.IsWallSliding = true;
                body.WallSide = -1;
                position.X = screenBounds.Left - 28.0;
                velocity.X = 0;
                velocity.Y = Math.Max(velocity.Y, 50.0);

                if (body.IsParkourWallJump && body.ParkourFinalTarget != null && !body.ParkourWallKickPending)
                {
                    body.ParkourWallKickPending = true;
                    body.ParkourWallGripTimer = 1.0; // 1.0 second wall hold before kicking off
                }
            }
            else
            {
                position.X = screenBounds.Left;
                velocity.X = Math.Abs(velocity.X) * PetConfiguration.Restitution;
            }
        }
        // Right boundary
        else if (position.X + body.Width >= screenBounds.Right && velocity.X >= -50.0)
        {
            // If in mid-air above ground level, catch the wall with claws!
            if (position.Y + body.Height < screenBounds.Bottom - 15.0)
            {
                body.IsWallSliding = true;
                body.WallSide = 1;
                position.X = screenBounds.Right - body.Width + 28.0;
                velocity.X = 0;
                velocity.Y = Math.Max(velocity.Y, 50.0);

                if (body.IsParkourWallJump && body.ParkourFinalTarget != null && !body.ParkourWallKickPending)
                {
                    body.ParkourWallKickPending = true;
                    body.ParkourWallGripTimer = 1.0; // 1.0 second wall hold before kicking off
                }
            }
            else
            {
                position.X = screenBounds.Right - body.Width;
                velocity.X = -Math.Abs(velocity.X) * PetConfiguration.Restitution;
            }
        }

        // Sky Portal / Screen Wrap:
        // If the pet falls below the bottom of the screen (dragged or thrown below the taskbar),
        // it drops down from the sky directly above the exact same horizontal position!
        if (!body.IsDragged && position.Y > screenBounds.Bottom + 10.0)
        {
            position.X = Math.Clamp(position.X, screenBounds.Left, screenBounds.Right - body.Width);
            position.Y = screenBounds.Top - body.Height - 10.0;
            velocity.Y = Math.Clamp(velocity.Y, 200.0, 600.0);

            body.IsGrounded = false;
            body.StandingHWnd = IntPtr.Zero;
            body.CurrentSurface = null;
            body.IgnoreSurfacesAboveY = 0;
        }

        body.Position = position;
        body.Velocity = velocity;
    }
}
