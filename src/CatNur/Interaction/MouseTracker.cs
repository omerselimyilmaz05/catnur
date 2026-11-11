namespace CatNur.Interaction;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;

/// <summary>
/// Tracks mouse position history over a rolling time window.
/// Used to calculate responsive throw velocity when the pet is released.
/// </summary>
public class MouseTracker
{
    /// <summary>
    /// A single mouse position sample with timestamp.
    /// </summary>
    private readonly struct MouseSample
    {
        public readonly Point Position;
        public readonly double TimeSeconds;

        public MouseSample(Point position, double timeSeconds)
        {
            Position = position;
            TimeSeconds = timeSeconds;
        }
    }

    private readonly Queue<MouseSample> _history = new();
    private readonly double _historyDuration;

    public MouseTracker(double historyDuration)
    {
        _historyDuration = historyDuration;
    }

    /// <summary>
    /// Records a mouse position sample with current timestamp.
    /// Automatically purges samples older than the history duration.
    /// </summary>
    public void RecordSample(Point screenPosition)
    {
        double now = GetCurrentTime();
        _history.Enqueue(new MouseSample(screenPosition, now));

        // Purge old samples beyond rolling window
        while (_history.Count > 1 && (now - _history.Peek().TimeSeconds) > _historyDuration)
        {
            _history.Dequeue();
        }
    }

    /// <summary>
    /// Calculates the weighted average release velocity from the mouse history.
    /// Emphasizes recent frames so fast flicks feel snappy and powerful,
    /// while static pauses (>120ms) result in a calm zero-velocity drop.
    /// </summary>
    public Vector CalculateVelocity()
    {
        if (_history.Count < 2)
            return new Vector(0, 0);

        double now = GetCurrentTime();
        var samples = _history.ToArray();
        var newest = samples[^1];

        // If the user held the mouse still for >90ms before releasing,
        // treat as gentle drop with zero throw velocity
        if ((now - newest.TimeSeconds) > 0.09)
            return new Vector(0, 0);

        double totalWeight = 0;
        double weightedVx = 0;
        double weightedVy = 0;

        // Compute weighted average of segment velocities (newer segments get higher weight)
        for (int i = 1; i < samples.Length; i++)
        {
            var p0 = samples[i - 1];
            var p1 = samples[i];
            double dt = p1.TimeSeconds - p0.TimeSeconds;

            if (dt <= 0.001) // Skip duplicate timestamp samples
                continue;

            double vx = (p1.Position.X - p0.Position.X) / dt;
            double vy = (p1.Position.Y - p0.Position.Y) / dt;

            // Progressive weight ramp towards most recent frames
            double weight = (double)i * i;
            weightedVx += vx * weight;
            weightedVy += vy * weight;
            totalWeight += weight;
        }

        if (totalWeight <= 0)
            return new Vector(0, 0);

        return new Vector(weightedVx / totalWeight, weightedVy / totalWeight);
    }

    /// <summary>
    /// Clears all recorded samples.
    /// </summary>
    public void Clear()
    {
        _history.Clear();
    }

    /// <summary>
    /// High-resolution timestamp in seconds.
    /// </summary>
    private static double GetCurrentTime()
    {
        return Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    }
}
