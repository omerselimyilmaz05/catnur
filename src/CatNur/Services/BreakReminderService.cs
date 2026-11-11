namespace CatNur.Services;

using System;
using System.Windows.Threading;

/// <summary>
/// Periodically reminds the user to drink water (every 45 minutes).
/// Shows small, non-intrusive notifications near the pet.
/// </summary>
public class BreakReminderService : IDisposable
{
    private readonly DispatcherTimer _timer;

    /// <summary>Time between reminders (45 minutes).</summary>
    public TimeSpan ReminderInterval { get; set; } = TimeSpan.FromMinutes(45);

    /// <summary>Fired when a reminder should be shown.</summary>
    public event Action<string>? ReminderTriggered;

    private const string WaterReminderMessage = "💧 Su içmeyi unutma!";

    public BreakReminderService()
    {
        _timer = new DispatcherTimer
        {
            Interval = ReminderInterval
        };
        _timer.Tick += OnTimerTick;
    }

    /// <summary>Starts the reminder timer.</summary>
    public void Start()
    {
        _timer.Interval = ReminderInterval;
        _timer.Start();
    }

    /// <summary>Stops the reminder timer.</summary>
    public void Stop()
    {
        _timer.Stop();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        ReminderTriggered?.Invoke(WaterReminderMessage);
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}
