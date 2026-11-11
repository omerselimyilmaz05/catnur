namespace CatNur.StateMachine;

using System;
using System.Diagnostics;
using CatNur.Core;

/// <summary>
/// Manages state transitions for the pet FSM.
/// Only one state is active at a time. Provides clean enter/exit lifecycle.
/// </summary>
public class FsmController
{
    private IPetState? _currentState;
    private readonly PetContext _context;

    /// <summary>The currently active state.</summary>
    public IPetState? CurrentState => _currentState;

    /// <summary>Name of the current state (for debugging).</summary>
    public string CurrentStateName => _currentState?.Name ?? "None";

    /// <summary>Fired when the state changes. Parameters: (oldState, newState).</summary>
    public event Action<IPetState?, IPetState>? StateChanged;

    public FsmController(PetContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Transitions to a new state. Calls Exit on the current state and Enter on the new one.
    /// Does nothing if already in the target state.
    /// </summary>
    public void TransitionTo(IPetState newState)
    {
        if (_currentState == newState)
            return;

        var oldState = _currentState;

        Debug.WriteLine($"[FSM] {oldState?.Name ?? "None"} → {newState.Name}");

        _currentState?.Exit(_context);
        _currentState = newState;
        _currentState.Enter(_context);

        StateChanged?.Invoke(oldState, newState);
    }

    /// <summary>
    /// Updates the current state. Called every fixed physics step.
    /// </summary>
    public void Update(double deltaTime)
    {
        _currentState?.Update(_context, deltaTime);
    }
}
