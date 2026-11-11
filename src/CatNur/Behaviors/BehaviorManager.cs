namespace CatNur.Behaviors;

using System.Collections.Generic;
using CatNur.Core;

/// <summary>
/// Manages overlay behaviors. Periodically checks if behaviors can activate,
/// updates active ones, and deactivates completed ones.
/// </summary>
public class BehaviorManager
{
    private readonly List<IBehavior> _registeredBehaviors = new();
    private readonly List<IBehavior> _activeBehaviors = new();
    private double _checkTimer;
    private const double CheckInterval = 2.0; // Check every 2 seconds

    /// <summary>Active behaviors (read-only view).</summary>
    public IReadOnlyList<IBehavior> ActiveBehaviors => _activeBehaviors;

    /// <summary>
    /// Registers a behavior for automatic activation checks.
    /// </summary>
    public void Register(IBehavior behavior)
    {
        _registeredBehaviors.Add(behavior);
    }

    /// <summary>
    /// Called every fixed physics step.
    /// </summary>
    public void Update(PetContext context, double deltaTime)
    {
        // Update active behaviors
        for (int i = _activeBehaviors.Count - 1; i >= 0; i--)
        {
            var behavior = _activeBehaviors[i];
            behavior.Update(context, deltaTime);

            // Remove if deactivated itself
            if (!behavior.IsActive)
            {
                _activeBehaviors.RemoveAt(i);
            }
        }

        // Periodically check for new activations
        _checkTimer += deltaTime;
        if (_checkTimer >= CheckInterval)
        {
            _checkTimer = 0;
            CheckActivations(context);
        }
    }

    /// <summary>
    /// Forces activation of a specific behavior.
    /// </summary>
    public void ForceActivate(IBehavior behavior, PetContext context)
    {
        if (context.Fsm.CurrentState is StateMachine.States.SleepingState) return;
        if (behavior.IsActive) return;
        behavior.Activate(context);
        if (behavior.IsActive && !_activeBehaviors.Contains(behavior))
        {
            _activeBehaviors.Add(behavior);
        }
    }

    /// <summary>
    /// Deactivates all active behaviors.
    /// </summary>
    public void DeactivateAll(PetContext context)
    {
        for (int i = _activeBehaviors.Count - 1; i >= 0; i--)
        {
            _activeBehaviors[i].Deactivate(context);
        }
        _activeBehaviors.Clear();
    }

    private void CheckActivations(PetContext context)
    {
        if (context.Fsm.CurrentState is StateMachine.States.SleepingState) return;
        foreach (var behavior in _registeredBehaviors)
        {
            if (!behavior.IsActive && behavior.CanActivate(context))
            {
                behavior.Activate(context);
                if (behavior.IsActive)
                {
                    _activeBehaviors.Add(behavior);
                }
            }
        }
    }
}
