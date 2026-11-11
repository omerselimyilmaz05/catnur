namespace CatNur.StateMachine;

using CatNur.Core;

/// <summary>
/// Interface for a pet state in the finite state machine.
/// Each state represents a distinct behavioral mode (Idle, Wander, Falling, etc.).
/// </summary>
public interface IPetState
{
    /// <summary>
    /// Display name for debugging.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Called when entering this state.
    /// Initialize state-specific timers, animations, etc.
    /// </summary>
    void Enter(PetContext context);

    /// <summary>
    /// Called every fixed physics step while this state is active.
    /// </summary>
    /// <param name="context">The pet context with all services.</param>
    /// <param name="deltaTime">Fixed delta time in seconds.</param>
    void Update(PetContext context, double deltaTime);

    /// <summary>
    /// Called when leaving this state.
    /// Clean up state-specific resources.
    /// </summary>
    void Exit(PetContext context);
}
