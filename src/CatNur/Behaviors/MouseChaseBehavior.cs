namespace CatNur.Behaviors;

using System;
using System.Windows;
using CatNur.Core;
using CatNur.OS;
using CatNur.StateMachine.States;

/// <summary>
/// When the pet is idle and the mouse is nearby and moving fast,
/// the pet randomly decides to chase it.
/// </summary>
public class MouseChaseBehavior : IBehavior
{
    public string Name => "MouseChase";
    public bool IsActive { get; private set; }

    private double _chaseTimer;
    private const double MaxChaseDuration = 8.0; // Max chase time in seconds
    private const double MinMouseSpeed = 200.0; // Minimum mouse speed to trigger chase
    private const double MaxChaseDistance = 600.0; // Give up if mouse too far
    private const double ChaseSpeed = 150.0; // Chase speed in px/s
    private const double ActivationChance = 0.15; // 15% chance when conditions met

    private readonly Random _random = new();

    // Mouse tracking
    private Point _lastMousePosition;
    private double _lastMouseTime;
    private double _mouseSpeed;

    public bool CanActivate(PetContext context)
    {
        // Only chase when in WanderState and grounded (never interrupt sitting or idle actions)
        if (context.Fsm.CurrentState is not WanderState)
            return false;
        if (!context.Body.IsGrounded)
            return false;

        // Check mouse speed
        UpdateMouseSpeed();

        if (_mouseSpeed < MinMouseSpeed)
            return false;

        // Random chance
        return _random.NextDouble() < ActivationChance;
    }

    public void Activate(PetContext context)
    {
        IsActive = true;
        _chaseTimer = 0;
    }

    public void Update(PetContext context, double deltaTime)
    {
        if (!IsActive) return;

        _chaseTimer += deltaTime;

        // Timeout
        if (_chaseTimer >= MaxChaseDuration)
        {
            Deactivate(context);
            return;
        }

        // If dragged or sleeping, stop chasing
        if (context.Body.IsDragged || context.Fsm.CurrentState is SleepingState)
        {
            Deactivate(context);
            return;
        }

        // Get current mouse position and compare with cat center
        var mousePos = GetMouseScreenPosition();
        double catCenterX = context.Body.Position.X + context.Body.Width / 2.0;
        double dx = mousePos.X - catCenterX;

        // Give up if mouse is too far
        if (Math.Abs(dx) > MaxChaseDistance)
        {
            Deactivate(context);
            return;
        }

        // Deadzone: If already at mouse position, don't jitter back and forth
        if (Math.Abs(dx) < 30.0)
        {
            if (context.Body.IsGrounded)
            {
                var vel = context.Body.Velocity;
                vel.X = 0;
                context.Body.Velocity = vel;
            }
            return;
        }

        // Move toward mouse smoothly
        if (context.Body.IsGrounded)
        {
            double direction = dx > 0 ? 1.0 : -1.0;
            var velocity = context.Body.Velocity;
            velocity.X = direction * ChaseSpeed;
            context.Body.Velocity = velocity;
        }
    }

    public void Deactivate(PetContext context)
    {
        IsActive = false;
        _chaseTimer = 0;

        // Stop horizontal movement
        if (context.Body.IsGrounded)
        {
            var velocity = context.Body.Velocity;
            velocity.X = 0;
            context.Body.Velocity = velocity;
        }
    }

    private void UpdateMouseSpeed()
    {
        var currentPos = GetMouseScreenPosition();
        double now = Environment.TickCount64 / 1000.0;
        double dt = now - _lastMouseTime;

        if (dt > 0.01)
        {
            double dx = currentPos.X - _lastMousePosition.X;
            double dy = currentPos.Y - _lastMousePosition.Y;
            _mouseSpeed = Math.Sqrt(dx * dx + dy * dy) / dt;
        }

        _lastMousePosition = currentPos;
        _lastMouseTime = now;
    }

    private static Point GetMouseScreenPosition()
    {
        // Use Win32 API directly (WPF doesn't have a simple static mouse-position accessor)
        NativeMethods.GetCursorPos(out var point);
        return new Point(point.X, point.Y);
    }
}

