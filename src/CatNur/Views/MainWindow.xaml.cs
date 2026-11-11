namespace CatNur.Views;

using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using CatNur.Core;

/// <summary>
/// Main pet window. Transparent, chromeless, topmost.
/// Moves around the screen driven by physics.
/// </summary>
public partial class MainWindow : Window
{
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;

    private PetContext? _context;
    private NotificationView? _notificationView;
    private OS.TrayIconService? _trayIconService;
    private int _renderLogCount;
    private readonly System.Windows.Threading.DispatcherTimer _singleClickTimer = new();

    public MainWindow()
    {
        InitializeComponent();
        Width = PetConfiguration.PetWidth;
        Height = PetConfiguration.PetHeight;
    }

    // ── Window Lifecycle ───────────────────────────────────────

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        IntPtr hwnd = helper.Handle;

        // Hook WndProc to intercept WM_MOUSEACTIVATE so clicking pet never steals focus
        var source = HwndSource.FromHwnd(hwnd);
        source?.AddHook(WndProc);

        // Enforce permanent native Topmost & NoActivate styles so pet is never buried behind other windows
        int exStyle = OS.NativeMethods.GetWindowLong(hwnd, OS.NativeMethods.GWL_EXSTYLE);
        OS.NativeMethods.SetWindowLong(hwnd, OS.NativeMethods.GWL_EXSTYLE, exStyle | OS.NativeMethods.WS_EX_NOACTIVATE | OS.NativeMethods.WS_EX_TOOLWINDOW | OS.NativeMethods.WS_EX_TOPMOST);
        OS.NativeMethods.SetWindowPos(hwnd, OS.NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, OS.NativeMethods.SWP_NOMOVE | OS.NativeMethods.SWP_NOSIZE | OS.NativeMethods.SWP_NOACTIVATE | OS.NativeMethods.SWP_SHOWWINDOW);

        // Setup 300ms single vs double click evaluation timer
        _singleClickTimer.Interval = TimeSpan.FromMilliseconds(300);
        _singleClickTimer.Tick += (s, ev) =>
        {
            _singleClickTimer.Stop();
            Console.WriteLine("[MainWindow] Single-click confirmed (300ms window elapsed) -> Triggering 'miyavlama' animation & sound!");

            // If sleeping, wake up to Idle!
            if (_context?.Fsm.CurrentState is StateMachine.States.SleepingState)
            {
                _context.Fsm.TransitionTo(_context.States.Idle);
            }

            PetVisual.PlayAnimationOnce("miyavlama");
            _context?.SoundService.PlayRandomMeow();
            PetVisual.PlayBounceEffect();
        };

        var workArea = SystemParameters.WorkArea;
        Console.WriteLine($"[MainWindow] Loaded!");
        Console.WriteLine($"[MainWindow] Primary Screen WorkArea: Left={workArea.Left}, Top={workArea.Top}, Width={workArea.Width}, Height={workArea.Height}");
        Console.WriteLine($"[MainWindow] Initial Window: Left={Left}, Top={Top}, Width={Width}, Height={Height}, IsVisible={IsVisible}, Topmost={Topmost}");

        // Create notification companion window
        _notificationView = new NotificationView();

        // Initialize PetContext
        _context = new PetContext();
        _context.Initialize(dpiSource: this, ownWindowHandle: hwnd);

        // Initialize System Tray Icon (NotifyIcon) in "Gizli Simgeleri Göster" area
        _trayIconService = new OS.TrayIconService(_context);

        // Wire break reminder
        _context.BreakReminder.ReminderTriggered += message =>
        {
            _notificationView?.ShowMessage(message, _context.Body.Position);
        };

        // Hook render
        _context.GameLoop.OnRender += SyncWindowPosition;

        // Ensure auto-start on Windows login is registered in Registry
        Services.StartupService.EnsureAutoStartRegistered();

        // Start
        _context.Start();
        Console.WriteLine($"[MainWindow] GameLoop started. Pet initial position: ({_context.Body.Position.X:F1}, {_context.Body.Position.Y:F1})");
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _trayIconService?.Dispose();
        _trayIconService = null;

        if (_context != null)
        {
            _context.GameLoop.OnRender -= SyncWindowPosition;
            _context.Dispose();
            _context = null;
        }

        _notificationView?.Close();
    }

    // ── Render Sync ────────────────────────────────────────────

    private void SyncWindowPosition()
    {
        if (_context == null) return;

        Left = _context.Body.Position.X;
        Top = _context.Body.Position.Y;

        // Keep Topmost Z-order alive every 60 frames (~1s) so newly focused windows never bury the pet
        _renderLogCount++;
        if (_renderLogCount % 60 == 0)
        {
            var helper = new WindowInteropHelper(this);
            if (helper.Handle != IntPtr.Zero)
            {
                OS.NativeMethods.SetWindowPos(helper.Handle, OS.NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, OS.NativeMethods.SWP_NOMOVE | OS.NativeMethods.SWP_NOSIZE | OS.NativeMethods.SWP_NOACTIVATE);
            }
        }

        // Log the first 5 frames and every 120 frames (~2s)
        if (_renderLogCount <= 5 || _renderLogCount % 120 == 0)
        {
            Console.WriteLine($"[MainWindow Render #{_renderLogCount}] Pos: ({Left:F1}, {Top:F1}), Vel: ({_context.Body.Velocity.X:F1}, {_context.Body.Velocity.Y:F1}), Grounded: {_context.Body.IsGrounded}, State: {_context.Fsm.CurrentStateName}");
        }

        // Action animation locking: force zero horizontal velocity while stationary action animations (yawn, scratch, hiss) are playing
        if (PetVisual.IsActionAnimationPlaying && !_context.Body.IsPreparingJump && _context.Fsm.CurrentState is not StateMachine.States.FallingState)
        {
            _context.Body.IsActionLocked = true;
            var vel = _context.Body.Velocity;
            vel.X = 0;
            _context.Body.Velocity = vel;
        }
        else
        {
            _context.Body.IsActionLocked = false;
        }

        // Facing direction
        if (_context.Body.Velocity.X > 5)
            PetVisual.SetFacingDirection(facingRight: true);
        else if (_context.Body.Velocity.X < -5)
            PetVisual.SetFacingDirection(facingRight: false);

        // Update active sprite state based on FSM and behaviors
        if (_context.Body.IsDragged || _context.Fsm.CurrentState is StateMachine.States.DraggedState)
        {
            PetVisual.SetSpriteState("tutulma");
            PetVisual.Opacity = 1.0;
        }
        else if (_context.Body.IsPreparingJump)
        {
            PetVisual.SetSpriteState("ziplama_hazirlik");
            PetVisual.Opacity = 1.0;
        }
        else if (_context.Fsm.CurrentState is StateMachine.States.RecoveryState)
        {
            PetVisual.SetSpriteState("oturma");
            PetVisual.Opacity = 1.0;
        }
        else if (_context.Fsm.CurrentState is StateMachine.States.SleepingState sleepState)
        {
            if (sleepState.IsSettledInBed)
            {
                // Dynamic sleep posture (v1/v3/v4) and v1 sleep micro-actions (esneme / el yalama)
                string sleepSprite = sleepState.ActiveSleepMicroAction ?? sleepState.CurrentSleepVariant;
                PetVisual.SetSpriteState(sleepSprite);
                PetVisual.Opacity = 0.90;
            }
            else
            {
                // Walking to bed
                PetVisual.SetSpriteState("yurume");
                PetVisual.Opacity = 1.0;
            }
        }
        else if (_context.CatjamBehavior.IsDancing)
        {
            PetVisual.SetSpriteState("muzik_dans");
            PetVisual.Opacity = 1.0;
        }
        else if (_context.Body.IsWallSliding)
        {
            PetVisual.SetSpriteState("duvardan_kayma");
            // If wall is on the right (WallSide == 1), paws face right; if left (WallSide == -1), paws face left
            PetVisual.SetFacingDirection(_context.Body.WallSide == 1);
            PetVisual.Opacity = 1.0;
        }
        else if (_context.Fsm.CurrentState is StateMachine.States.FallingState)
        {
            // Upward ballistic jump -> ziplama, downward / apex -> dusme
            if (_context.Body.Velocity.Y < -30)
                PetVisual.SetSpriteState("ziplama");
            else
                PetVisual.SetSpriteState("dusme");

            PetVisual.Opacity = 1.0;
        }
        else if (_context.Fsm.CurrentState is StateMachine.States.WanderState wanderState)
        {
            // Face the intended walking direction immediately (even during initial stand-up frame)
            PetVisual.SetFacingDirection(wanderState.WanderDirection > 0);

            // Stand-up transition / ground scratching pause / normal walking
            if (wanderState.IsStartingWalk)
                PetVisual.SetSpriteState("yurume_baslangic");
            else if (wanderState.IsScratchingGround)
                PetVisual.SetSpriteState("yer_eseleme");
            else
                PetVisual.SetSpriteState("yurume");

            PetVisual.Opacity = 1.0;
        }
        else if (_context.MouseChaseBehavior.IsActive)
        {
            PetVisual.SetSpriteState("yurume");
            PetVisual.Opacity = 1.0;
        }
        else if (_context.Fsm.CurrentState is StateMachine.States.IdleState idleState)
        {
            // Sitting idle or micro-actions (esneme, pati yalama, sag/sol kulak kasima)
            string idleSprite = idleState.ActiveMicroAction ?? "oturma";
            PetVisual.SetSpriteState(idleSprite);
            PetVisual.Opacity = 1.0;
        }
        else
        {
            PetVisual.SetSpriteState("oturma");
            PetVisual.Opacity = 1.0;
        }
    }

    // ── Mouse Events ───────────────────────────────────────────

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Console.WriteLine($"[MainWindow] MouseLeftButtonDown at ({Left:F1}, {Top:F1}), ClickCount: {e.ClickCount}");

        // Delegate to drag controller so initial grab offset is captured
        _context?.DragController.OnMouseDown(this, e);

        if (e.ClickCount >= 2 || _singleClickTimer.IsEnabled)
        {
            // Second click received within 300ms -> Cancel pending single-click
            _singleClickTimer.Stop();

            // If sleeping, wake up to Idle!
            if (_context?.Fsm.CurrentState is StateMachine.States.SleepingState)
            {
                _context.Fsm.TransitionTo(_context.States.Idle);
            }

            // Double click: Play hiss (tisslama) animation for 2.0 seconds and hiss sound
            Console.WriteLine("[MainWindow] Double-click confirmed -> Triggering 'tisslama' for 2.0s!");
            PetVisual.PlayAnimationForDuration("tisslama", TimeSpan.FromSeconds(2.0));
            _context?.SoundService.Play("tisslama");
            PetVisual.PlayBounceEffect();
            return;
        }

        // Start 300ms evaluation window for single click
        _singleClickTimer.Stop();
        _singleClickTimer.Start();
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (_context?.DragController.IsDragging == true)
        {
            // If dragging threshold exceeded, cancel pending single-click
            _singleClickTimer.Stop();
        }
        _context?.DragController.OnMouseMove(this, e);
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        Console.WriteLine($"[MainWindow] MouseLeftButtonUp at ({Left:F1}, {Top:F1})");
        _context?.DragController.OnMouseUp(this, e);
    }

    private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        Console.WriteLine($"[MainWindow] MouseRightButtonDown at ({Left:F1}, {Top:F1})");
        _context?.InteractionController.OnMouseRightButtonDown();
    }

    // ── WndProc ────────────────────────────────────────────────

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_MOUSEACTIVATE)
        {
            handled = true;
            return new IntPtr(MA_NOACTIVATE);
        }
        return IntPtr.Zero;
    }
}
