namespace CatNur.Interaction;

using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CatNur.Core;
using CatNur.OS;
using CatNur.Physics;
using Forms = System.Windows.Forms;

/// <summary>
/// Handles drag-and-throw mechanics for the pet with movement threshold protection.
/// Clicking without moving triggers pure click actions (meowing) without entering drag/tutulma mode.
/// Moving the mouse beyond the drag threshold (6 DIPs) activates drag mode and 1:1 window tracking.
///
/// Tracking accumulates small INCREMENTAL physical-pixel deltas onto the window's existing
/// WPF (DIP) position each move, converting each delta with whichever monitor the cursor is
/// currently over. This avoids two failure modes we hit before:
///  - A single fixed DIP offset captured once at drag-start, reused after the cursor crosses
///    onto a monitor with a different DPI, silently corrupts (two different-DPI monitors don't
///    share one continuous DIP space).
///  - Driving the window purely via raw SetWindowPos on every MouseMove: for a layered
///    (AllowsTransparency) topmost window, that forces a synchronous DWM composition update on
///    every single call, and crossing a DPI boundary while flooding it at mouse-move frequency
///    stalled the UI thread hard enough to freeze the whole app, tray icon included.
/// Using WPF's own Left/Top setter (its normal, cheap, well-tested code path) for the actual
/// move — just fed a carefully-converted delta instead of a stale absolute offset — keeps drag
/// tracking smooth without hammering DWM, and any DPI mismatch is at most one frame's worth of
/// a few pixels instead of a monitor-widths-wide jump.
/// </summary>
public class DragController
{
    private readonly MouseTracker _mouseTracker;
    private readonly PetBody _body;
    private readonly ScreenService _screenService;

    private NativeMethods.POINT _initialGrabPhysical;
    private NativeMethods.POINT _lastPhysical;
    private double _thresholdDpiScale = 1.0;
    private bool _isMouseDown;
    private bool _isDragging;
    private const double DragDistanceThreshold = 6.0; // DIPs moved before entering drag mode

    /// <summary>True while the user is actively dragging the pet.</summary>
    public bool IsDragging => _isDragging;

    public DragController(PetBody body, ScreenService screenService)
    {
        _body = body;
        _screenService = screenService;
        _mouseTracker = new MouseTracker(PetConfiguration.MouseHistoryDuration);
    }

    /// <summary>
    /// Call when the user presses the mouse button on the pet.
    /// </summary>
    public void OnMouseDown(Window petWindow, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        _isMouseDown = true;
        _isDragging = false;

        NativeMethods.GetCursorPos(out _initialGrabPhysical);
        _lastPhysical = _initialGrabPhysical;

        // Threshold check happens before any monitor crossing, so the window's current DPI is valid here.
        _thresholdDpiScale = VisualTreeHelper.GetDpi(petWindow).DpiScaleX;

        _mouseTracker.Clear();
        _mouseTracker.RecordSample(new Point(petWindow.Left, petWindow.Top));

        petWindow.CaptureMouse();
    }

    /// <summary>
    /// Call on mouse move events.
    /// 1:1 direct tracking: window follows cursor exactly, accumulating small deltas.
    /// </summary>
    public void OnMouseMove(Window petWindow, MouseEventArgs e)
    {
        if (!_isMouseDown)
            return;

        NativeMethods.GetCursorPos(out var currentPhysical);

        // Check if movement threshold is reached
        if (!_isDragging)
        {
            double deltaX = currentPhysical.X - _initialGrabPhysical.X;
            double deltaY = currentPhysical.Y - _initialGrabPhysical.Y;
            double thresholdPhysical = DragDistanceThreshold * _thresholdDpiScale;

            if (deltaX * deltaX + deltaY * deltaY >= thresholdPhysical * thresholdPhysical)
            {
                _isDragging = true;
                _body.IsDragged = true;
                _body.Velocity = new Vector(0, 0);
                _body.IsGrounded = false;
                _lastPhysical = currentPhysical; // incremental tracking baseline starts now
                Console.WriteLine("[DragController] Mouse movement threshold reached -> Entered 'tutulma' drag mode!");
            }
            else
            {
                return; // Movement below threshold -> keep in click mode
            }
        }

        int deltaPhysX = currentPhysical.X - _lastPhysical.X;
        int deltaPhysY = currentPhysical.Y - _lastPhysical.Y;
        _lastPhysical = currentPhysical;

        if (deltaPhysX == 0 && deltaPhysY == 0)
            return;

        // Convert this frame's small delta using whichever monitor the cursor is over right now
        var screen = Forms.Screen.FromPoint(new System.Drawing.Point(currentPhysical.X, currentPhysical.Y));
        var (scaleX, scaleY) = _screenService.GetDpiScale(screen);

        var newPosition = new Point(
            petWindow.Left + deltaPhysX / scaleX,
            petWindow.Top + deltaPhysY / scaleY
        );

        _body.Position = newPosition;
        petWindow.Left = newPosition.X;
        petWindow.Top = newPosition.Y;

        // Record DIP sample for release velocity calculation
        _mouseTracker.RecordSample(newPosition);
    }

    /// <summary>
    /// Call when the user releases the mouse button.
    /// Calculates throw velocity and releases the pet into physics.
    /// </summary>
    public void OnMouseUp(Window petWindow, MouseButtonEventArgs e)
    {
        if (!_isMouseDown && !_isDragging)
            return;

        bool wasDragging = _isDragging;
        _isMouseDown = false;
        _isDragging = false;
        petWindow.ReleaseMouseCapture();

        if (wasDragging)
        {
            _body.IsDragged = false;
            _mouseTracker.RecordSample(new Point(petWindow.Left, petWindow.Top));

            // Calculate smoothed throw velocity from mouse history
            Vector throwVelocity = _mouseTracker.CalculateVelocity();
            throwVelocity *= PetConfiguration.ThrowDamping;

            if (throwVelocity.Length > PetConfiguration.MaxThrowSpeed)
            {
                throwVelocity.Normalize();
                throwVelocity *= PetConfiguration.MaxThrowSpeed;
            }

            if (throwVelocity.Length >= PetConfiguration.MinThrowSpeed)
            {
                _body.Velocity = throwVelocity;
            }
            else
            {
                _body.Velocity = new Vector(0, 0);
            }

            _mouseTracker.Clear();
            Console.WriteLine($"[DragController] Released drag with velocity: ({_body.Velocity.X:F1}, {_body.Velocity.Y:F1})");
        }
    }
}
