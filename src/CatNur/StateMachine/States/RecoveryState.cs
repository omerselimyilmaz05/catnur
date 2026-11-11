namespace CatNur.StateMachine.States;

using System;
using CatNur.Core;

/// <summary>
/// Landing pause after jumping or falling.
/// Settles on four paws in 'oturma' pose for 0.5 seconds, then resumes calm wandering/idling.
/// </summary>
public class RecoveryState : IPetState
{
    public string Name => "Recovery";

    private double _timer;
    private const double RecoveryDuration = 0.5;

    public IntPtr TargetHwnd { get; set; } = IntPtr.Zero;
    public int AttemptCount { get; set; } = 0;

    public void Enter(PetContext context)
    {
        _timer = 0;
        var velocity = context.Body.Velocity;
        velocity.X = 0;
        velocity.Y = 0;
        context.Body.Velocity = velocity;
    }

    public void Update(PetContext context, double deltaTime)
    {
        if (context.Body.IsDragged)
        {
            Reset();
            context.Fsm.TransitionTo(context.States.Dragged);
            return;
        }

        if (!context.Body.IsGrounded)
        {
            Reset();
            context.Fsm.TransitionTo(context.States.Falling);
            return;
        }

        _timer += deltaTime;
        if (_timer >= RecoveryDuration)
        {
            Reset();
            context.Fsm.TransitionTo(context.States.Idle);
        }
    }

    public void Reset()
    {
        TargetHwnd = IntPtr.Zero;
        AttemptCount = 0;
        _timer = 0;
    }

    public void Exit(PetContext context)
    {
        _timer = 0;
    }
}
