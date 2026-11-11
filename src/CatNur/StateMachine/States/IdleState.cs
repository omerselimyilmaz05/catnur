namespace CatNur.StateMachine.States;

using System;
using CatNur.Core;

/// <summary>
/// The pet sits still and waits. While sitting, it randomly performs idle micro-actions
/// (yawning, paw licking, scratching right/left ear), then transitions to Wander.
/// </summary>
public class IdleState : IPetState
{
    public string Name => "Idle";

    public string? ActiveMicroAction { get; private set; }

    private double _idleTimer;
    private double _idleDuration;
    private double _microActionTimer;
    private double _nextMicroActionTime;
    private double _microActionDuration;
    private readonly Random _random = new();

    public void Enter(PetContext context)
    {
        // Pick a random idle duration (3 to 7 seconds)
        _idleDuration = PetConfiguration.IdleMinDuration +
                        _random.NextDouble() * (PetConfiguration.IdleMaxDuration - PetConfiguration.IdleMinDuration);
        _idleTimer = 0;
        _microActionTimer = 0;
        ActiveMicroAction = null;

        // Schedule first micro action after 1.2 - 2.5 seconds
        _nextMicroActionTime = 1.2 + _random.NextDouble() * 1.5;

        // Stop horizontal movement
        var velocity = context.Body.Velocity;
        velocity.X = 0;
        context.Body.Velocity = velocity;
    }

    public void Update(PetContext context, double deltaTime)
    {
        // Check if dragged
        if (context.Body.IsDragged)
        {
            context.Fsm.TransitionTo(context.States.Dragged);
            return;
        }

        // Check if ground lost
        if (!context.Body.IsGrounded)
        {
            context.Fsm.TransitionTo(context.States.Falling);
            return;
        }

        // Force strictly 0 horizontal velocity while idle
        var velocity = context.Body.Velocity;
        if (velocity.X != 0)
        {
            velocity.X = 0;
            context.Body.Velocity = velocity;
        }

        _idleTimer += deltaTime;
        _microActionTimer += deltaTime;

        // Handle active micro-action timeout
        if (ActiveMicroAction != null)
        {
            if (_microActionTimer >= _microActionDuration && !context.Body.IsActionLocked)
            {
                ActiveMicroAction = null; // Return to sitting
                _nextMicroActionTime = _idleTimer + 2.5 + _random.NextDouble() * 2.5;
            }
        }
        else if (_idleTimer >= _nextMicroActionTime && !context.Body.IsActionLocked)
        {
            // Trigger a random sitting micro action with exact animation duration
            int choice = _random.Next(4);
            (ActiveMicroAction, _microActionDuration) = choice switch
            {
                0 => ("otururken_esneme", 1.35),      // 8 frames @ 6 FPS = 1.33s
                1 => ("otururken_pati_yalama", 1.15),  // 9 frames @ 8 FPS = 1.125s
                2 => ("sag_kulak_kasima", 1.40),       // 11 frames @ 8 FPS = 1.375s
                _ => ("sol_kulak_kasima", 1.40)        // 11 frames @ 8 FPS = 1.375s
            };

            _microActionTimer = 0;
            Console.WriteLine($"[IdleState] Triggered sitting micro-action: {ActiveMicroAction} ({_microActionDuration:F2}s)");
        }

        // Wait for idle duration, then wander (strictly blocked if any action or lock is active)
        if (_idleTimer >= _idleDuration && ActiveMicroAction == null && !context.Body.IsActionLocked)
        {
            context.Fsm.TransitionTo(context.States.Wander);
        }
    }

    public void Exit(PetContext context)
    {
        _idleTimer = 0;
        _microActionTimer = 0;
        ActiveMicroAction = null;
    }
}
