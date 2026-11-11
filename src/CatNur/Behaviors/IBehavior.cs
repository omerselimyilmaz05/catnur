namespace CatNur.Behaviors;

using CatNur.Core;

/// <summary>
/// Interface for overlay behaviors that can run alongside FSM states.
/// Behaviors represent transient activities like chasing the mouse, catjam, or carrying sunflowers.
/// Multiple behaviors can be active simultaneously.
/// </summary>
public interface IBehavior
{
    /// <summary>Display name for debugging.</summary>
    string Name { get; }

    /// <summary>Whether this behavior is currently running.</summary>
    bool IsActive { get; }

    /// <summary>
    /// Checks if this behavior can currently activate.
    /// Called periodically by BehaviorManager.
    /// </summary>
    bool CanActivate(PetContext context);

    /// <summary>Called when the behavior starts.</summary>
    void Activate(PetContext context);

    /// <summary>Called every fixed physics step while active.</summary>
    void Update(PetContext context, double deltaTime);

    /// <summary>Called when the behavior ends.</summary>
    void Deactivate(PetContext context);
}
