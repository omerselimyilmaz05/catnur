namespace CatNur.Core;

using System;
using System.Diagnostics;
using System.Windows.Media;

/// <summary>
/// Main game loop driven by <see cref="CompositionTarget.Rendering"/>.
/// Uses a fixed timestep accumulator for deterministic physics
/// while rendering at the monitor's refresh rate.
/// </summary>
public class GameLoop
{
    private readonly Stopwatch _stopwatch = new();
    private TimeSpan _lastRenderTime;
    private double _accumulator;

    /// <summary>
    /// Fired once per fixed physics step (1/60s).
    /// Parameter is the fixed delta time in seconds.
    /// </summary>
    public event Action<double>? OnUpdate;

    /// <summary>
    /// Fired once per render frame (vsync).
    /// Used to sync visual positions.
    /// </summary>
    public event Action? OnRender;

    /// <summary>True while the game loop is running.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Starts the game loop. Hooks into <see cref="CompositionTarget.Rendering"/>.
    /// </summary>
    public void Start()
    {
        if (IsRunning) return;

        IsRunning = true;
        _accumulator = 0;
        _stopwatch.Restart();
        _lastRenderTime = _stopwatch.Elapsed;
        CompositionTarget.Rendering += OnCompositionTargetRendering;
    }

    /// <summary>
    /// Stops the game loop and unhooks from rendering.
    /// </summary>
    public void Stop()
    {
        if (!IsRunning) return;

        IsRunning = false;
        CompositionTarget.Rendering -= OnCompositionTargetRendering;
        _stopwatch.Stop();
    }

    private void OnCompositionTargetRendering(object? sender, EventArgs e)
    {
        var currentTime = _stopwatch.Elapsed;
        double frameTime = (currentTime - _lastRenderTime).TotalSeconds;
        _lastRenderTime = currentTime;

        // Cap frame time to prevent spiral of death
        // (e.g. when window is dragged or system stalls)
        if (frameTime > PetConfiguration.MaxFrameTime)
            frameTime = PetConfiguration.MaxFrameTime;

        _accumulator += frameTime;

        // Fixed timestep updates — deterministic regardless of monitor refresh rate
        while (_accumulator >= PetConfiguration.FixedTimeStep)
        {
            OnUpdate?.Invoke(PetConfiguration.FixedTimeStep);
            _accumulator -= PetConfiguration.FixedTimeStep;
        }

        // Render pass — sync visuals with physics state
        OnRender?.Invoke();
    }
}
