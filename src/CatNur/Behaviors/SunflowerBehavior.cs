namespace CatNur.Behaviors;

using System;
using System.Collections.Generic;
using System.Windows;
using CatNur.Core;
using CatNur.StateMachine.States;

/// <summary>
/// The pet randomly fetches sunflowers and places them near the taskbar.
/// Goes to screen edge → disappears briefly → returns with 🌻 → drops it at collection area.
/// </summary>
public class SunflowerBehavior : IBehavior
{
    public string Name => "SunflowerCollect";
    public bool IsActive { get; private set; }

    private readonly Random _random = new();
    private readonly List<SunflowerItem> _collectedSunflowers = new();

    private SunflowerPhase _phase;
    private double _phaseTimer;
    private double _fetchDirection; // +1 = go right, -1 = go left
    private const double FetchSpeed = 100.0;
    private const double DisappearDuration = 2.0; // Time "off screen" fetching
    private const double ActivationChance = 0.05; // 5% every check

    /// <summary>All collected sunflowers (read-only).</summary>
    public IReadOnlyList<SunflowerItem> CollectedSunflowers => _collectedSunflowers;

    /// <summary>Fired when a new sunflower is placed.</summary>
    public event Action<SunflowerItem>? SunflowerPlaced;

    private enum SunflowerPhase
    {
        GoingToEdge,
        Disappearing,
        Returning,
        Placing
    }

    public bool CanActivate(PetContext context)
    {
        // Only collect sunflowers during WanderState (never interrupt sitting or idle actions)
        if (context.Fsm.CurrentState is not WanderState)
            return false;
        if (!context.Body.IsGrounded)
            return false;

        return _random.NextDouble() < ActivationChance;
    }

    public void Activate(PetContext context)
    {
        IsActive = true;
        _phase = SunflowerPhase.GoingToEdge;
        _phaseTimer = 0;

        // Go to nearest screen edge
        var workArea = context.ScreenService.GetWorkArea();
        double distToLeft = context.Body.Position.X - workArea.Left;
        double distToRight = workArea.Right - context.Body.Right;
        _fetchDirection = distToLeft < distToRight ? -1 : 1;
    }

    public void Update(PetContext context, double deltaTime)
    {
        if (!IsActive) return;

        // Abort if dragged
        if (context.Body.IsDragged)
        {
            Deactivate(context);
            return;
        }

        switch (_phase)
        {
            case SunflowerPhase.GoingToEdge:
                UpdateGoingToEdge(context, deltaTime);
                break;
            case SunflowerPhase.Disappearing:
                UpdateDisappearing(context, deltaTime);
                break;
            case SunflowerPhase.Returning:
                UpdateReturning(context, deltaTime);
                break;
            case SunflowerPhase.Placing:
                PlaceSunflower(context);
                break;
        }
    }

    private void UpdateGoingToEdge(PetContext context, double deltaTime)
    {
        var workArea = context.ScreenService.GetWorkArea();
        var velocity = context.Body.Velocity;
        velocity.X = _fetchDirection * FetchSpeed;
        context.Body.Velocity = velocity;

        // Check if reached edge
        bool reachedEdge = (_fetchDirection < 0 && context.Body.Left <= workArea.Left + 5) ||
                           (_fetchDirection > 0 && context.Body.Right >= workArea.Right - 5);

        if (reachedEdge)
        {
            _phase = SunflowerPhase.Disappearing;
            _phaseTimer = 0;
            velocity.X = 0;
            context.Body.Velocity = velocity;
        }
    }

    private void UpdateDisappearing(PetContext context, double deltaTime)
    {
        _phaseTimer += deltaTime;
        if (_phaseTimer >= DisappearDuration)
        {
            // Come back from the same edge
            _phase = SunflowerPhase.Returning;
            _fetchDirection = -_fetchDirection; // Reverse direction
        }
    }

    private void UpdateReturning(PetContext context, double deltaTime)
    {
        var velocity = context.Body.Velocity;
        velocity.X = _fetchDirection * FetchSpeed;
        context.Body.Velocity = velocity;

        // Walk a bit into the screen, then place
        var workArea = context.ScreenService.GetWorkArea();
        double centerX = (workArea.Left + workArea.Right) / 2;
        bool pastCenter = (_fetchDirection > 0 && context.Body.Position.X >= centerX - 100) ||
                          (_fetchDirection < 0 && context.Body.Position.X <= centerX + 100);

        if (pastCenter)
        {
            _phase = SunflowerPhase.Placing;
        }
    }

    private void PlaceSunflower(PetContext context)
    {
        // Place sunflower at collection area (above taskbar)
        var workArea = context.ScreenService.GetWorkArea();
        int sunflowerIndex = _collectedSunflowers.Count;

        var sunflower = new SunflowerItem
        {
            Position = new Point(
                workArea.Left + 20 + sunflowerIndex * 25,
                workArea.Bottom - 30
            ),
            IsPlaced = true
        };

        _collectedSunflowers.Add(sunflower);
        SunflowerPlaced?.Invoke(sunflower);

        // Stop and deactivate
        var velocity = context.Body.Velocity;
        velocity.X = 0;
        context.Body.Velocity = velocity;

        Deactivate(context);
    }

    public void Deactivate(PetContext context)
    {
        IsActive = false;
        _phaseTimer = 0;

        var velocity = context.Body.Velocity;
        velocity.X = 0;
        context.Body.Velocity = velocity;
    }
}

/// <summary>
/// Represents a single collected sunflower.
/// </summary>
public class SunflowerItem
{
    public Point Position { get; set; }
    public bool IsPlaced { get; set; }
}
