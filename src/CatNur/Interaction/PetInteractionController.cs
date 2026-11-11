namespace CatNur.Interaction;

using System;
using System.Windows;
using System.Windows.Input;
using CatNur.Core;
using CatNur.StateMachine.States;

/// <summary>
/// Handles click interactions on the pet.
/// Single click: bounce effect. Double click: petting. Right click: sleep.
/// </summary>
public class PetInteractionController
{
    private readonly PetContext _context;
    private DateTime _lastClickTime = DateTime.MinValue;
    private const double DoubleClickThresholdMs = 400;

    public PetInteractionController(PetContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Handles left mouse button down. Distinguishes single/double click
    /// and delegates to DragController.
    /// </summary>
    public void OnMouseLeftButtonDown(Window petWindow, MouseButtonEventArgs e)
    {
        var now = DateTime.UtcNow;
        var timeSinceLastClick = (now - _lastClickTime).TotalMilliseconds;

        if (timeSinceLastClick < DoubleClickThresholdMs)
        {
            // Double click — petting
            OnDoubleClick();
            _lastClickTime = DateTime.MinValue; // Reset to avoid triple-click
        }
        else
        {
            // Potential single click — also starts drag
            _lastClickTime = now;
            OnSingleClick();
        }

        // Always delegate to drag controller
        _context.DragController.OnMouseDown(petWindow, e);
    }

    /// <summary>
    /// Handles right mouse button down — puts the pet to sleep.
    /// </summary>
    public void OnMouseRightButtonDown()
    {
        // Wake up if sleeping, otherwise go to sleep
        if (_context.Fsm.CurrentState is SleepingState)
        {
            _context.Fsm.TransitionTo(_context.States.Idle);
        }
        else
        {
            _context.Fsm.TransitionTo(_context.States.Sleeping);
        }
    }

    /// <summary>
    /// Single click response: quick visual bounce and wake up.
    /// </summary>
    private void OnSingleClick()
    {
        // Wake from sleep on click
        if (_context.Fsm.CurrentState is SleepingState)
        {
            _context.Fsm.TransitionTo(_context.States.Idle);
        }

        // Bounce impulse
        if (_context.Body.IsGrounded)
        {
            _context.Body.ApplyImpulse(new Vector(0, -120)); // Small upward hop
        }
    }

    /// <summary>
    /// Double click response: petting interaction bounce.
    /// </summary>
    private void OnDoubleClick()
    {
        // Slightly bigger upward bounce for petting
        if (_context.Body.IsGrounded)
        {
            _context.Body.ApplyImpulse(new Vector(0, -200));
        }
    }
}
