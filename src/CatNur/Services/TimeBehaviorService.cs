namespace CatNur.Services;

using System;
using System.Windows.Threading;
using CatNur.Core;
using CatNur.StateMachine.States;

/// <summary>
/// Monitors the system clock and applies time-based behaviors.
/// After 02:00, the pet behaves more sleepily (slower wander, more frequent naps).
/// Uses a low-frequency timer (checks every 5 minutes, not every frame).
/// </summary>
public class TimeBehaviorService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly PetContext _context;

    /// <summary>Whether it's currently "night mode" (past 02:00).</summary>
    public bool IsNightMode { get; private set; }

    /// <summary>Fired when night mode state changes.</summary>
    public event Action<bool>? NightModeChanged;

    /// <summary>Start hour for night mode (24h format).</summary>
    public int NightStartHour { get; set; } = 2;

    /// <summary>End hour for night mode (24h format).</summary>
    public int NightEndHour { get; set; } = 6;

    public TimeBehaviorService(PetContext context)
    {
        _context = context;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(5) // Low frequency check
        };
        _timer.Tick += OnTimerTick;
    }

    /// <summary>Starts monitoring the system clock.</summary>
    public void Start()
    {
        CheckTimeOfDay(); // Initial check
        _timer.Start();
    }

    /// <summary>Stops monitoring.</summary>
    public void Stop()
    {
        _timer.Stop();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        CheckTimeOfDay();
    }

    private void CheckTimeOfDay()
    {
        int hour = DateTime.Now.Hour;
        bool shouldBeNight = hour >= NightStartHour && hour < NightEndHour;

        if (shouldBeNight != IsNightMode)
        {
            IsNightMode = shouldBeNight;
            NightModeChanged?.Invoke(IsNightMode);

            if (IsNightMode)
            {
                ApplyNightBehavior();
            }
        }
    }

    private void ApplyNightBehavior()
    {
        // If idle or wandering, transition to sleep
        if (_context.Fsm.CurrentState is IdleState or WanderState)
        {
            _context.Fsm.TransitionTo(_context.States.Sleeping);
        }
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}
