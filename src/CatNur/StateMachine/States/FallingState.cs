namespace CatNur.StateMachine.States;

using System;
using CatNur.Core;
using CatNur.Physics;

/// <summary>
/// The pet is airborne (falling or in ballistic jump).
/// Transitions to Idle on successful landing, or to Recovery if a jump was missed.
/// </summary>
public class FallingState : IPetState
{
    public string Name => "Falling";

    /// <summary>The target window HWND if this fall was a deliberate jump.</summary>
    public IntPtr TrackedJumpTargetHwnd { get; set; } = IntPtr.Zero;

    /// <summary>Current attempt number (1, 2, or 3).</summary>
    public int JumpAttemptCount { get; set; } = 0;

    public void Enter(PetContext context)
    {
    }

    public void Update(PetContext context, double deltaTime)
    {
        // Check if dragged mid-air
        if (context.Body.IsDragged)
        {
            ClearJumpTracking();
            context.Fsm.TransitionTo(context.States.Dragged);
            return;
        }

        // Check if landed and fully grounded on a surface
        if (context.Body.IsGrounded)
        {
            if (TrackedJumpTargetHwnd != IntPtr.Zero)
            {
                // Check if the pet landed on a window platform
                bool landedOnWindow = context.Body.StandingHWnd != IntPtr.Zero ||
                                      context.Body.CurrentSurface?.Type == SurfaceType.WindowTop;

                if (landedOnWindow)
                {
                    // SUCCESS! Pet is firmly resting on a window platform
                    context.Body.PlatformSafetyTimer = 15.0;
                    ClearJumpTracking();
                    context.Fsm.TransitionTo(context.States.Idle);
                    return;
                }
                else
                {
                    // MISSED! Pet landed on the screen bottom (floor) instead of the window
                    if (JumpAttemptCount < 3)
                    {
                        IntPtr missedTarget = TrackedJumpTargetHwnd;
                        int attempt = JumpAttemptCount;
                        ClearJumpTracking();

                        context.States.Recovery.TargetHwnd = missedTarget;
                        context.States.Recovery.AttemptCount = attempt;
                        context.Fsm.TransitionTo(context.States.Recovery);
                        return;
                    }
                }
            }

            ClearJumpTracking();
            context.Fsm.TransitionTo(context.States.Idle);
        }
    }

    public void ClearJumpTracking()
    {
        TrackedJumpTargetHwnd = IntPtr.Zero;
        JumpAttemptCount = 0;
    }

    public void Exit(PetContext context)
    {
    }
}
