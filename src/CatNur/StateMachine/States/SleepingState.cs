namespace CatNur.StateMachine.States;

using System;
using System.Windows;
using CatNur.Core;
using CatNur.Physics;

/// <summary>
/// The pet goes to sleep. First walks to a cozy spot (screen corner or window edge),
/// then picks randomly from 3 sleeping postures (uyuma_v1, uyuma_v3, uyuma_v4).
/// If uyuma_v1 is active, it periodically stretches or licks its hand while asleep.
/// </summary>
public class SleepingState : IPetState
{
    public string Name => "Sleeping";

    public bool IsSettledInBed => _reachedSleepSpot;
    public string CurrentSleepVariant { get; private set; } = "uyuma_v1";
    public string? ActiveSleepMicroAction { get; private set; }

    private bool _reachedSleepSpot;
    private Point _sleepTarget;
    private double _sleepTimer;
    private double _nextMicroActionTime;
    private double _microActionTimer;
    private double _microActionDuration;
    private readonly Random _random = new();

    public void Enter(PetContext context)
    {
        _reachedSleepSpot = false;
        ActiveSleepMicroAction = null;
        _sleepTimer = 0;
        _microActionTimer = 0;

        // Randomly pick one of the 3 sleep variants
        int choice = _random.Next(3);
        CurrentSleepVariant = choice switch
        {
            0 => "uyuma_v1",
            1 => "uyuma_v3",
            _ => "uyuma_v4"
        };
        Console.WriteLine($"[SleepingState] Selected sleep variant: {CurrentSleepVariant}");

        // Schedule first sleeping micro action if v1
        _nextMicroActionTime = 4.0 + _random.NextDouble() * 5.0;

        var body = context.Body;
        var workArea = context.GetCurrentMonitorWorkArea();

        if (body.StandingHWnd != IntPtr.Zero && body.CurrentSurface != null && body.CurrentSurface.Type == SurfaceType.WindowTop)
        {
            double targetX = Math.Max(body.CurrentSurface.Left + 10, body.CurrentSurface.Right - body.Width - 15);
            _sleepTarget = new Point(targetX, body.CurrentSurface.Top - body.Height);
        }
        else
        {
            _sleepTarget = new Point(
                workArea.Right - body.Width - 20,
                workArea.Bottom - body.Height
            );
        }

        // Start walking toward the sleep spot
        var velocity = body.Velocity;
        double direction = _sleepTarget.X > body.Position.X ? 1.0 : -1.0;
        velocity.X = direction * PetConfiguration.WanderSpeed;
        body.Velocity = velocity;
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
        if (!context.Body.IsGrounded && !context.Body.IsDragged)
        {
            context.Fsm.TransitionTo(context.States.Falling);
            return;
        }

        if (!_reachedSleepSpot)
        {
            // Move toward sleep spot
            double distance = _sleepTarget.X - context.Body.Position.X;
            if (Math.Abs(distance) < 6.0)
            {
                _reachedSleepSpot = true;
                var velocity = context.Body.Velocity;
                velocity.X = 0;
                context.Body.Velocity = velocity;

                // Start ambient sleeping sound
                context.SoundService.PlayLooping("uyuma");
            }
            else
            {
                double direction = distance > 0 ? 1.0 : -1.0;
                var velocity = context.Body.Velocity;
                velocity.X = direction * PetConfiguration.WanderSpeed;
                context.Body.Velocity = velocity;
            }
        }
        else
        {
            // Settled in bed
            _sleepTimer += deltaTime;
            _microActionTimer += deltaTime;

            // Handle micro-actions if in v1 sleep
            if (CurrentSleepVariant == "uyuma_v1")
            {
                if (ActiveSleepMicroAction != null)
                {
                    if (_microActionTimer >= _microActionDuration)
                    {
                        ActiveSleepMicroAction = null; // Return to main sleep pose
                        _nextMicroActionTime = _sleepTimer + 6.0 + _random.NextDouble() * 6.0;
                    }
                }
                else if (_sleepTimer >= _nextMicroActionTime)
                {
                    // Trigger sleep micro action (esneme or el yalama)
                    ActiveSleepMicroAction = _random.Next(2) == 0 ? "uyurken_esneme_v1" : "uyurken_el_yalama_v1";
                    _microActionTimer = 0;
                    _microActionDuration = 1.3 + _random.NextDouble() * 0.7;
                    Console.WriteLine($"[SleepingState] Triggered sleep micro-action: {ActiveSleepMicroAction}");
                }
            }
        }
    }

    public void Exit(PetContext context)
    {
        _reachedSleepSpot = false;
        ActiveSleepMicroAction = null;
        context.SoundService.StopLooping();
    }
}
