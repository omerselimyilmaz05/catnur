namespace CatNur.StateMachine.States;

using System;
using CatNur.Core;

/// <summary>
/// The pet walks in a random direction for a random distance, occasionally pausing to scratch the ground,
/// then returns to Idle. Checks for drag, boundary reversals, and ground loss.
/// </summary>
public class WanderState : IPetState
{
    public string Name => "Wander";

    public bool IsScratchingGround => _isScratching;
    public bool IsStartingWalk => _walkStartTimer > 0;
    public double WanderDirection => _wanderDirection;

    private double _wanderDirection; // +1 = right, -1 = left
    private double _walkStartTimer;
    private double _distanceTraveled;
    private double _targetDistance;
    private bool _hasAttemptedScratch;
    private double _scratchTriggerDistance;
    private bool _isScratching;
    private double _scratchTimer;
    private double _scratchDuration;
    private double _turnCooldown;
    private readonly Random _random = new();

    public void Enter(PetContext context)
    {
        // Pick random direction
        _wanderDirection = _random.NextDouble() > 0.5 ? 1.0 : -1.0;

        // Stand-up anticipation frame (200ms) before walking velocity begins
        _walkStartTimer = 0.20;

        // Pick random distance
        _targetDistance = 60 + _random.NextDouble() * PetConfiguration.WanderMaxDistance;
        _distanceTraveled = 0;

        _hasAttemptedScratch = false;
        _isScratching = false;
        _scratchTimer = 0;
        _turnCooldown = 0.5;
        _scratchTriggerDistance = _targetDistance * (0.3 + _random.NextDouble() * 0.4);

        // Stand still during initial stand-up frame
        var velocity = context.Body.Velocity;
        velocity.X = 0;
        context.Body.Velocity = velocity;
    }

    public void Update(PetContext context, double deltaTime)
    {
        if (_turnCooldown > 0)
            _turnCooldown -= deltaTime;

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

        // Handle stand-up anticipation before walking
        if (_walkStartTimer > 0)
        {
            _walkStartTimer -= deltaTime;
            var vel = context.Body.Velocity;
            vel.X = 0;
            context.Body.Velocity = vel;
            if (_walkStartTimer <= 0)
            {
                vel.X = _wanderDirection * PetConfiguration.WanderSpeed;
                context.Body.Velocity = vel;
            }
            return;
        }

        // Handle ground scratching pause
        if (_isScratching)
        {
            _scratchTimer += deltaTime;
            var vel = context.Body.Velocity;
            vel.X = 0;
            context.Body.Velocity = vel;

            if (_scratchTimer >= _scratchDuration && !context.Body.IsActionLocked)
            {
                _isScratching = false;
                // Resume walking only when animation is completely finished
                vel.X = _wanderDirection * PetConfiguration.WanderSpeed;
                context.Body.Velocity = vel;
            }
            return;
        }

        // If any action is locked, keep horizontal velocity strictly at zero
        if (context.Body.IsActionLocked)
        {
            var vel = context.Body.Velocity;
            vel.X = 0;
            context.Body.Velocity = vel;
            return;
        }

        // Check screen boundaries — reverse direction at edges (with turn cooldown)
        var workArea = context.GetCurrentMonitorWorkArea();
        if (_turnCooldown <= 0)
        {
            if (context.Body.Left <= workArea.Left + 5 && _wanderDirection < 0)
            {
                _wanderDirection = 1.0;
                _turnCooldown = 0.5;
            }
            else if (context.Body.Right >= workArea.Right - 5 && _wanderDirection > 0)
            {
                _wanderDirection = -1.0;
                _turnCooldown = 0.5;
            }

            // Platform edge safety check
            if (context.Body.StandingHWnd != IntPtr.Zero &&
                context.Body.CurrentSurface != null &&
                context.Body.CurrentSurface.Type == Physics.SurfaceType.WindowTop &&
                context.Body.PlatformSafetyTimer > 0)
            {
                double footLeft = context.Body.Left + 35.0;
                double footRight = context.Body.Right - 35.0;

                if (footLeft <= context.Body.CurrentSurface.Left + 15.0 && _wanderDirection < 0)
                {
                    _wanderDirection = 1.0;
                    _turnCooldown = 0.5;
                }
                else if (footRight >= context.Body.CurrentSurface.Right - 15.0 && _wanderDirection > 0)
                {
                    _wanderDirection = -1.0;
                    _turnCooldown = 0.5;
                }
            }
        }

        // Check for ground scratching event mid-walk (35% probability)
        if (!_hasAttemptedScratch && _distanceTraveled >= _scratchTriggerDistance)
        {
            _hasAttemptedScratch = true;
            if (_random.NextDouble() < 0.35)
            {
                _isScratching = true;
                _scratchTimer = 0;
                // 7 frames @ 10 FPS = 0.70s per full cycle
                _scratchDuration = _random.Next(2) == 0 ? 0.72 : 1.42;
                var vel = context.Body.Velocity;
                vel.X = 0;
                context.Body.Velocity = vel;
                Console.WriteLine($"[WanderState] Pausing to scratch ground (Direction: {(_wanderDirection > 0 ? "Right" : "Left")}, Duration: {_scratchDuration:F2}s)");
                return;
            }
        }

        // Maintain wander velocity
        var velocity = context.Body.Velocity;
        velocity.X = _wanderDirection * PetConfiguration.WanderSpeed;
        context.Body.Velocity = velocity;

        // Track distance
        _distanceTraveled += PetConfiguration.WanderSpeed * deltaTime;
        if (_distanceTraveled >= _targetDistance)
        {
            context.Fsm.TransitionTo(context.States.Idle);
        }
    }

    public void Exit(PetContext context)
    {
        _distanceTraveled = 0;
        _isScratching = false;
    }
}
