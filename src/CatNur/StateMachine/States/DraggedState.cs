namespace CatNur.StateMachine.States;

using CatNur.Core;

/// <summary>
/// The pet is being dragged by the user.
/// Physics movement is suspended; position follows the mouse.
/// On release (mouse up), transitions to Falling.
/// </summary>
public class DraggedState : IPetState
{
    public string Name => "Dragged";

    public void Enter(PetContext context)
    {
        // DragController handles the actual mouse tracking
        // Body.IsDragged is already set by DragController.OnMouseDown
    }

    public void Update(PetContext context, double deltaTime)
    {
        // When the user releases the mouse, DragController sets IsDragged = false
        if (!context.Body.IsDragged)
        {
            context.Fsm.TransitionTo(context.States.Falling);
        }
    }

    public void Exit(PetContext context)
    {
        // Nothing to clean up — DragController handles mouse release
    }
}
