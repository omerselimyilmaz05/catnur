namespace CatNur.Core;

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using CatNur.Behaviors;
using CatNur.Interaction;
using CatNur.Media;
using CatNur.OS;
using CatNur.Physics;
using CatNur.Services;
using CatNur.StateMachine;
using CatNur.StateMachine.States;

/// <summary>
/// Central hub that owns all services and coordinates the update/render cycle.
/// Acts as the composition root — all subsystems are created and wired here.
/// </summary>
public class PetContext : IDisposable
{
    // ── Core ───────────────────────────────────────────────────
    public GameLoop GameLoop { get; }
    public PetBody Body { get; }

    // ── Physics ────────────────────────────────────────────────
    public PhysicsEngine PhysicsEngine { get; }
    public CollisionDetector CollisionDetector { get; }

    // ── OS ─────────────────────────────────────────────────────
    public ScreenService ScreenService { get; }
    public ForegroundWindowTracker WindowTracker { get; }
    public WindowSurfaceDetector WindowSurfaceDetector { get; }

    // ── Interaction ────────────────────────────────────────────
    public DragController DragController { get; }
    public PetInteractionController InteractionController { get; }

    // ── State Machine ──────────────────────────────────────────
    public FsmController Fsm { get; }
    public StateRegistry States { get; }

    // ── Behaviors ──────────────────────────────────────────────
    public BehaviorManager BehaviorManager { get; }
    public CatjamBehavior CatjamBehavior { get; }
    public SunflowerBehavior SunflowerBehavior { get; }
    public MouseChaseBehavior MouseChaseBehavior { get; }

    // ── Media ──────────────────────────────────────────────────
    public MediaListener MediaListener { get; }

    // ── Services ───────────────────────────────────────────────
    public SoundService SoundService { get; }
    public BreakReminderService BreakReminder { get; }
    public TimeBehaviorService TimeBehavior { get; }

    // ── Internal ───────────────────────────────────────────────
    private readonly List<GroundSurface> _surfaces = new();
    private Visual? _dpiSource;
    private IntPtr _ownHwnd;
    private int _windowPollCounter;
    private List<GroundSurface>? _cachedWindowSurfaces;

    /// <summary>Active candidate window platforms discovered in Z-order.</summary>
    public IReadOnlyList<GroundSurface>? CachedWindowSurfaces => _cachedWindowSurfaces;

    /// <summary>All active ground surfaces (screen floor + valid window tops).</summary>
    public IReadOnlyList<GroundSurface> Surfaces => _surfaces;

    public bool IsInitialized { get; private set; }

    public PetContext()
    {
        // OS services
        ScreenService = new ScreenService();
        WindowTracker = new ForegroundWindowTracker();
        WindowSurfaceDetector = new WindowSurfaceDetector();

        // Physics
        CollisionDetector = new CollisionDetector();
        PhysicsEngine = new PhysicsEngine(CollisionDetector);

        // Core
        GameLoop = new GameLoop();
        Body = new PetBody(PetConfiguration.PetWidth, PetConfiguration.PetHeight);

        // Interaction
        DragController = new DragController(Body, ScreenService);
        InteractionController = new PetInteractionController(this);

        // State machine
        States = new StateRegistry();
        Fsm = new FsmController(this);

        // Behaviors
        CatjamBehavior = new CatjamBehavior();
        SunflowerBehavior = new SunflowerBehavior();
        MouseChaseBehavior = new MouseChaseBehavior();
        BehaviorManager = new BehaviorManager();
        BehaviorManager.Register(MouseChaseBehavior);
        // SunflowerBehavior is disabled for now as requested
        // CatjamBehavior is NOT registered for auto-activation — it's triggered by MediaListener

        // Media
        MediaListener = new MediaListener();

        // Services
        SoundService = new SoundService();
        BreakReminder = new BreakReminderService();
        TimeBehavior = new TimeBehaviorService(this);
    }

    /// <summary>
    /// Initializes the pet context.
    /// </summary>
    public async void Initialize(Visual dpiSource, IntPtr ownWindowHandle)
    {
        if (IsInitialized) return;

        _dpiSource = dpiSource;
        _ownHwnd = ownWindowHandle;
        WindowTracker.SetOwnWindowHandle(ownWindowHandle);
        WindowSurfaceDetector.SetOwnWindowHandle(ownWindowHandle);

        // Start pet at center-top of primary monitor
        var primaryArea = SystemParameters.WorkArea;
        Body.Position = new Point(
            (primaryArea.Width - Body.Width) / 2.0 + primaryArea.Left,
            primaryArea.Top + 50.0
        );

        // Wire game loop
        GameLoop.OnUpdate += Update;

        // Wire new window detection
        WindowSurfaceDetector.NewWindowDetected += OnNewWindowDetected;

        // Start in Falling state
        Fsm.TransitionTo(States.Falling);

        // Initialize media listener (async, may fail silently)
        await MediaListener.InitializeAsync();
        if (MediaListener.IsAvailable)
        {
            MediaListener.MediaStarted += OnMediaStarted;
            MediaListener.MediaStopped += OnMediaStopped;
        }

        // Start services
        BreakReminder.Start();
        TimeBehavior.Start();

        IsInitialized = true;
    }

    private readonly Random _jumpRandom = new();

    /// <summary>
    /// Gets the usable work area (excludes taskbar) of whichever monitor the pet is currently on.
    /// All physics boundaries, jump targets, wandering, and sleeping should be scoped to this
    /// so the pet never bleeds its behavior across monitor edges.
    /// </summary>
    public Rect GetCurrentMonitorWorkArea()
    {
        return ScreenService.GetWorkAreaForScreen(ScreenService.GetScreenFromHandle(_ownHwnd));
    }

    /// <summary>
    /// Physically relocates the pet's window to a specific point on a specific monitor and
    /// resets it into a clean falling state.
    ///
    /// Moves the window via WPF's own Left/Top setter (its normal, well-tested code path) —
    /// NOT a raw SetWindowPos call, which fights with WPF's internal DPI-changed handling when
    /// crossing a DPI boundary and previously left the pet mis-placed or fighting its way back.
    /// The trick: WPF's Left/Top setter converts DIPs to physical pixels using the window's
    /// CURRENT (pre-move) DPI context, not the target monitor's. So instead of feeding it the
    /// DIP value that's correct for the TARGET monitor (which the window hasn't reached yet),
    /// we work out the target's actual PHYSICAL pixel position first (using the target's own
    /// DPI scale) and then re-express that same physical position in the window's CURRENT DPI's
    /// terms — so once WPF does its normal conversion, it lands exactly on the intended physical
    /// spot regardless of how different the two monitors' DPI scales are.
    /// </summary>
    private void TeleportPetTo(System.Windows.Forms.Screen targetScreen, Point dipPosition)
    {
        if (_ownHwnd != IntPtr.Zero && _dpiSource is Window mainWindow)
        {
            var (targetScaleX, targetScaleY) = ScreenService.GetDpiScale(targetScreen);
            double physX = dipPosition.X * targetScaleX;
            double physY = dipPosition.Y * targetScaleY;

            var currentDpi = VisualTreeHelper.GetDpi(mainWindow);
            mainWindow.Left = physX / currentDpi.DpiScaleX;
            mainWindow.Top = physY / currentDpi.DpiScaleY;

            Console.WriteLine($"[TeleportPetTo] target=({physX:F0},{physY:F0}) currentDpi={currentDpi.DpiScaleX} -> Window.Left/Top set to ({mainWindow.Left:F1},{mainWindow.Top:F1})");
        }
        else
        {
            Console.WriteLine($"[TeleportPetTo] SKIPPED native move — ownHwnd={_ownHwnd} dpiSourceIsWindow={_dpiSource is Window}");
        }

        Body.Position = dipPosition;
        Body.Velocity = new Vector(0, 0);
        Body.IsGrounded = false;
        Body.StandingHWnd = IntPtr.Zero;
        Body.CurrentSurface = null;
        Body.IsWallSliding = false;
        Body.IsDragged = false;
        Body.ParkourWallKickPending = false;
        Body.IsPreparingJump = false;

        Fsm.TransitionTo(States.Falling);
        SoundService.PlayRandomMeow();
    }

    /// <summary>
    /// Sends the pet to the top-center of the PRIMARY monitor, regardless of where it currently
    /// is. Universal "rescue" action for the tray icon — always predictable, even if the pet is
    /// stuck/lost on a disconnected or misbehaving monitor.
    /// </summary>
    public void ResetToPrimaryMonitor()
    {
        var primary = System.Windows.Forms.Screen.PrimaryScreen ?? ScreenService.GetScreenFromHandle(IntPtr.Zero);
        var workArea = ScreenService.GetWorkAreaForScreen(primary);
        var dipPosition = new Point(
            (workArea.Width - Body.Width) / 2.0 + workArea.Left,
            workArea.Top + 50.0
        );

        TeleportPetTo(primary, dipPosition);
    }

    /// <summary>
    /// Deliberately sends the pet across to the OTHER monitor (does nothing but return false if
    /// only one is connected), landing in that monitor's bottom-right corner. This is the only
    /// way the pet crosses monitors on its own — normal jumping/sleeping/wandering stay scoped
    /// to whichever monitor it's currently on, so it never drifts over involuntarily.
    /// </summary>
    public bool SwitchToOtherMonitor()
    {
        var current = ScreenService.GetScreenFromHandle(_ownHwnd);
        var next = ScreenService.GetNextScreen(current);
        Console.WriteLine($"[SwitchToOtherMonitor] current={current.DeviceName} next={(next == null ? "null" : next.DeviceName)}");
        if (next == null)
            return false;

        var workArea = ScreenService.GetWorkAreaForScreen(next);
        var dipPosition = new Point(
            workArea.Right - Body.Width - 20.0,
            workArea.Bottom - Body.Height
        );
        Console.WriteLine($"[SwitchToOtherMonitor] targetWorkArea={workArea} dipPosition={dipPosition}");

        TeleportPetTo(next, dipPosition);
        return true;
    }

    private void OnNewWindowDetected(GroundSurface newSurface)
    {
        if (newSurface == null || newSurface.Type != SurfaceType.WindowTop)
            return;

        // Never wake up or jump while in Sleep Mode!
        if (Fsm.CurrentState is StateMachine.States.SleepingState)
            return;

        if (Body.IsDragged || Body.IsPreparingJump)
            return;

        // Only allow reacting to a new window while standing on solid ground
        if (!Body.IsGrounded)
            return;

        // Don't jump if already resting on this exact window
        if (Body.StandingHWnd != IntPtr.Zero && Body.StandingHWnd == newSurface.HWnd)
            return;
        if (Body.CurrentSurface != null && Body.CurrentSurface.HWnd == newSurface.HWnd)
            return;

        // 90% chance to jump immediately to the new window
        if (_jumpRandom.NextDouble() <= 0.90)
        {
            // 10% Chance for Parkour Wall Bounce Jump!
            if (_jumpRandom.NextDouble() < 0.10)
            {
                var screenArea = GetCurrentMonitorWorkArea();
                double currentX = (Body.Left + Body.Right) / 2.0;
                double currentY = Body.Bottom;

                bool bounceLeft = (currentX < (screenArea.Left + screenArea.Right) / 2.0);
                double wallX = bounceLeft ? (screenArea.Left + 15.0) : (screenArea.Right - 15.0);
                double wallY = (screenArea.Top + screenArea.Bottom) / 2.0; // Always target vertical center of the screen!

                if (WindowSurfaceDetector.TryCalculatePointJumpVelocity(currentX, currentY, wallX, wallY, out var wallLaunchVel, 70.0))
                {
                    StartJumpPreparation(wallLaunchVel, IntPtr.Zero, 0, isParkour: true, parkourTarget: newSurface);
                    return;
                }
            }

            if (WindowSurfaceDetector.TryCalculateJumpVelocity(Body, newSurface, out var launchVelocity))
            {
                double ignoreY = newSurface.Top > Body.Bottom ? newSurface.Top - 5.0 : 0;
                StartJumpPreparation(launchVelocity, newSurface.HWnd, ignoreY);
            }
        }
    }

    public void StartJumpPreparation(Vector launchVelocity, IntPtr targetHwnd, double ignoreSurfacesAboveY, bool isParkour = false, GroundSurface? parkourTarget = null)
    {
        Body.IsPreparingJump = true;
        Body.JumpPreparationTimer = 0.35; // 0.35s anticipation crouch
        Body.PendingJumpVelocity = launchVelocity;
        Body.PendingJumpTargetHwnd = targetHwnd;
        Body.PendingIgnoreSurfacesAboveY = ignoreSurfacesAboveY;
        Body.JumpLaunchSourceHWnd = Body.StandingHWnd != IntPtr.Zero ? Body.StandingHWnd : (Body.CurrentSurface?.HWnd ?? IntPtr.Zero);

        Body.IsParkourWallJump = isParkour;
        Body.ParkourFinalTarget = parkourTarget;
        Body.ParkourWallKickPending = false;
        Body.ParkourWallGripTimer = 0;

        // Keep horizontal velocity strictly 0 so the pet remains firmly planted while crouching
        var v = Body.Velocity;
        v.X = 0;
        v.Y = 0;
        Body.Velocity = v;
        Console.WriteLine($"[JumpPreparation] Preparing jump towards {(launchVelocity.X >= 0 ? "Right" : "Left")}{(isParkour ? " [PARKOUR WALL-BOUNCE]" : "")}");
    }

    private void Update(double deltaTime)
    {
        // ── 60 FPS standing window tracking (high-speed window dragging) ──
        if (_dpiSource != null)
        {
            WindowSurfaceDetector.TrackStandingWindowPerFrame(_dpiSource, Body);
        }

        // ── Current monitor bounds (all physics/jumping/wandering stay scoped to this) ──
        var screenBounds = GetCurrentMonitorWorkArea();

        // ── Build surfaces ─────────────────────────────────────
        _surfaces.Clear();
        _surfaces.Add(ScreenService.GetScreenGround(screenBounds));

        // Poll window surfaces in Z-order periodically (occlusion & candidate discovery)
        _windowPollCounter++;
        int pollInterval = Math.Max(1, (int)(PetConfiguration.ForegroundWindowPollMs / 1000.0 / PetConfiguration.FixedTimeStep));

        if (_windowPollCounter >= pollInterval)
        {
            _windowPollCounter = 0;
            if (_dpiSource != null)
            {
                _cachedWindowSurfaces = WindowSurfaceDetector.GetWindowSurfaces(_dpiSource, Body, screenBounds);
            }
        }

        if (_cachedWindowSurfaces != null)
        {
            for (int i = 0; i < _cachedWindowSurfaces.Count; i++)
            {
                _surfaces.Add(_cachedWindowSurfaces[i]);
            }
        }

        // ── Enforce Zero Horizontal Velocity for Stationary States ──
        EnforceStationaryZeroVelocity();

        // ── Physics ────────────────────────────────────────────
        PhysicsEngine.Update(Body, deltaTime, _surfaces, screenBounds);

        // ── Takeoff Immunity & Platform Safety Timers ──────────
        if (Body.JumpLaunchIgnoreTimer > 0)
        {
            Body.JumpLaunchIgnoreTimer -= deltaTime;
        }

        if (Body.StandingHWnd != IntPtr.Zero && Body.PlatformSafetyTimer > 0)
        {
            Body.PlatformSafetyTimer -= deltaTime;
        }
        else if (Body.StandingHWnd == IntPtr.Zero)
        {
            Body.PlatformSafetyTimer = 0;
        }

        // ── Parkour Wall Grip & Kickoff ────────────────────────
        if (Body.ParkourWallKickPending)
        {
            Body.ParkourWallGripTimer -= deltaTime;
            var vel = Body.Velocity;
            vel.X = 0;
            vel.Y = 0;
            Body.Velocity = vel;

            if (Body.ParkourWallGripTimer <= 0)
            {
                Body.ParkourWallKickPending = false;
                Body.IsParkourWallJump = false;
                Body.IsWallSliding = false;
                int prevWallSide = Body.WallSide;
                Body.WallSide = 0;

                var target = Body.ParkourFinalTarget;
                Body.ParkourFinalTarget = null;

                // Nudge position slightly away from the wall to clear the wall boundary immediately
                var pos = Body.Position;
                if (prevWallSide == 1 || pos.X > screenBounds.Right - Body.Width - 10.0)
                {
                    pos.X = screenBounds.Right - Body.Width - 15.0;
                }
                else if (prevWallSide == -1 || pos.X < screenBounds.Left + 10.0)
                {
                    pos.X = screenBounds.Left + 15.0;
                }
                Body.Position = pos;

                if (target != null && WindowSurfaceDetector.TryCalculateJumpVelocity(Body, target, out var kickVelocity))
                {
                    Body.JumpLaunchIgnoreTimer = 0.6;
                    Body.Velocity = kickVelocity;
                    Body.IsGrounded = false;
                    Body.StandingHWnd = IntPtr.Zero;
                    Body.CurrentSurface = null;

                    States.Falling.TrackedJumpTargetHwnd = target.HWnd;
                    States.Falling.JumpAttemptCount = 1;

                    Fsm.TransitionTo(States.Falling);
                }
                else
                {
                    Body.Velocity = new Vector(prevWallSide == 1 ? -400.0 : 400.0, -300.0);
                    Fsm.TransitionTo(States.Falling);
                }
            }
            return;
        }

        // ── Wall Slide Auto Jump (Max 2.5s wall slide before kicking off) ──
        if (Body.IsWallSliding && !Body.ParkourWallKickPending && !Body.IsPreparingJump && !Body.IsDragged)
        {
            Body.WallSlideTimer += deltaTime;
            if (Body.WallSlideTimer >= 2.5)
            {
                Body.WallSlideTimer = 0;
                Body.IsWallSliding = false;
                int prevWallSide = Body.WallSide;
                Body.WallSide = 0;

                // Nudge position slightly away from the wall to clear the wall boundary
                var pos = Body.Position;
                if (prevWallSide == 1 || pos.X > screenBounds.Right - Body.Width - 10.0)
                {
                    pos.X = screenBounds.Right - Body.Width - 15.0;
                }
                else if (prevWallSide == -1 || pos.X < screenBounds.Left + 10.0)
                {
                    pos.X = screenBounds.Left + 15.0;
                }
                Body.Position = pos;

                // Find candidate window platform to jump onto
                GroundSurface? bestTarget = null;
                Vector bestLaunchVelocity = default;
                double bestDist = double.MaxValue;
                double currentX = (Body.Left + Body.Right) / 2.0;
                double currentY = Body.Bottom;

                var surfaces = _cachedWindowSurfaces;
                if (surfaces != null)
                {
                    for (int i = 0; i < surfaces.Count; i++)
                    {
                        var surface = surfaces[i];
                        if (surface.Type != SurfaceType.WindowTop) continue;

                        if (WindowSurfaceDetector.TryCalculateJumpVelocity(Body, surface, out var launchVelocity))
                        {
                            double deltaX = (surface.Left + surface.Right) / 2.0 - currentX;
                            double deltaY = currentY - surface.Top;
                            double dist = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                            if (dist < bestDist)
                            {
                                bestDist = dist;
                                bestTarget = surface;
                                bestLaunchVelocity = launchVelocity;
                            }
                        }
                    }
                }

                if (bestTarget != null)
                {
                    // Kick off towards the nearest window!
                    Body.JumpLaunchIgnoreTimer = 0.6;
                    Body.Velocity = bestLaunchVelocity;
                    Body.IsGrounded = false;
                    Body.StandingHWnd = IntPtr.Zero;
                    Body.CurrentSurface = null;

                    States.Falling.TrackedJumpTargetHwnd = bestTarget.HWnd;
                    States.Falling.JumpAttemptCount = 1;

                    Fsm.TransitionTo(States.Falling);
                }
                else
                {
                    // No window: jump inwards towards the floor!
                    double kickVx = (prevWallSide == 1) ? -450.0 : 450.0;
                    Body.Velocity = new Vector(kickVx, -300.0);
                    Body.IsGrounded = false;
                    Body.StandingHWnd = IntPtr.Zero;
                    Body.CurrentSurface = null;

                    Fsm.TransitionTo(States.Falling);
                }
            }
        }
        else
        {
            Body.WallSlideTimer = 0;
        }

        // ── Jump Preparation Timer ──────────────────────────────
        if (Body.IsPreparingJump)
        {
            Body.JumpPreparationTimer -= deltaTime;
            var vel = Body.Velocity;
            vel.X = 0;
            vel.Y = 0;
            Body.Velocity = vel;

            if (Body.JumpPreparationTimer <= 0)
            {
                Body.IsPreparingJump = false;

                Vector finalLaunchVelocity = Body.PendingJumpVelocity;
                IntPtr targetHwnd = Body.PendingJumpTargetHwnd;

                Body.IgnoreSurfacesAboveY = Body.PendingIgnoreSurfacesAboveY;
                Body.JumpLaunchIgnoreTimer = 0.6; // 0.6s immunity to clear the takeoff platform
                Body.Velocity = finalLaunchVelocity;
                Body.IsGrounded = false;
                Body.StandingHWnd = IntPtr.Zero;
                Body.CurrentSurface = null;

                States.Falling.TrackedJumpTargetHwnd = targetHwnd;
                States.Falling.JumpAttemptCount = 1;

                Fsm.TransitionTo(States.Falling);
            }
            return;
        }

        // ── State machine ──────────────────────────────────────
        Fsm.Update(deltaTime);

        // ── Autonomous Jumping ──────────────────────────────────
        UpdateAutonomousJump(deltaTime);

        // ── Behaviors ──────────────────────────────────────────
        BehaviorManager.Update(this, deltaTime);

        // ── Final Enforcement: Zero Velocity for Stationary States ──
        EnforceStationaryZeroVelocity();
    }

    private void EnforceStationaryZeroVelocity()
    {
        if (Fsm.CurrentState is StateMachine.States.IdleState ||
            Fsm.CurrentState is StateMachine.States.RecoveryState ||
            (Fsm.CurrentState is StateMachine.States.WanderState wander && wander.IsScratchingGround) ||
            (Fsm.CurrentState is StateMachine.States.SleepingState sleep && sleep.IsSettledInBed) ||
            Body.IsPreparingJump ||
            Body.ParkourWallKickPending ||
            CatjamBehavior.IsDancing)
        {
            var v = Body.Velocity;
            v.X = 0;
            Body.Velocity = v;
        }
    }

    private double _autonomousJumpTimer = 4.5;

    private void UpdateAutonomousJump(double deltaTime)
    {
        // Only evaluate when grounded, not dragged, not in recovery/falling/sleeping/preparing jump
        if (!Body.IsGrounded || Body.IsDragged || Body.IsPreparingJump || Body.ParkourWallKickPending || Fsm.CurrentState is StateMachine.States.SleepingState or StateMachine.States.RecoveryState or StateMachine.States.DraggedState or StateMachine.States.FallingState)
            return;

        _autonomousJumpTimer -= deltaTime;
        if (_autonomousJumpTimer <= 0)
        {
            if (TryPerformAutonomousJump())
            {
                // Successful jump -> 4.5 - 6.5s interval
                _autonomousJumpTimer = 4.5 + _jumpRandom.NextDouble() * 2.0;
            }
            else
            {
                // No valid target or skipped by probability -> retry in 2.5 - 4.0s
                _autonomousJumpTimer = 2.5 + _jumpRandom.NextDouble() * 1.5;
            }
        }
    }

    /// <summary>
    /// Evaluates candidate window platforms across the entire screen and launches a ballistic jump.
    /// Excludes the current standing platform by HWND, reference, and physical coordinates.
    /// </summary>
    public bool TryPerformAutonomousJump()
    {
        var surfaces = _cachedWindowSurfaces;
        if (surfaces == null || surfaces.Count == 0)
            return false;

        // 45% chance to jump when another candidate window exists
        if (_jumpRandom.NextDouble() > 0.45)
            return false;

        double currentX = (Body.Left + Body.Right) / 2.0;
        double currentY = Body.Bottom;

        GroundSurface? bestTarget = null;
        double bestDist = double.MaxValue;
        Vector bestLaunchVelocity = default;

        for (int i = 0; i < surfaces.Count; i++)
        {
            var surface = surfaces[i];
            if (surface.Type != SurfaceType.WindowTop)
                continue;

            // 1. Exclude the exact surface currently standing on (by Tag and HWND)
            if (Body.CurrentSurface != null)
            {
                if (Body.CurrentSurface.Tag == surface.Tag && Body.CurrentSurface.HWnd == surface.HWnd)
                    continue;

                if (Body.CurrentSurface.Tag == null && surface.Tag == null && Body.StandingHWnd == surface.HWnd)
                    continue;
            }
            else if (Body.StandingHWnd != IntPtr.Zero && Body.StandingHWnd == surface.HWnd && surface.Tag == null)
            {
                continue;
            }

            // 2. Exclude if physically already on this platform or overlapping horizontally near the same height
            if (Math.Abs(currentY - surface.Top) < 40.0 && surface.OverlapsHorizontally(Body.Left - 20.0, Body.Right + 20.0))
                continue;

            // 3. Exclude platforms that are horizontally too close (minimum 100px separation or 40px vertical delta)
            double deltaX = (surface.Left + surface.Right) / 2.0 - currentX;
            double deltaY = currentY - surface.Top;
            double dist = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            if (dist < 80.0 && Math.Abs(deltaY) < 40.0)
                continue;

            if (WindowSurfaceDetector.TryCalculateJumpVelocity(Body, surface, out var launchVelocity))
            {
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestTarget = surface;
                    bestLaunchVelocity = launchVelocity;
                }
            }
        }

        if (bestTarget == null)
            return false;

        Console.WriteLine($"[AutonomousJump] Preparing jump from HWND 0x{Body.StandingHWnd:X} to Target HWND 0x{bestTarget.HWnd:X} at ({bestTarget.Left:F0}, {bestTarget.Top:F0})");

        // 10% Chance for Parkour Wall Bounce Jump!
        if (_jumpRandom.NextDouble() < 0.10)
        {
            var screenArea = GetCurrentMonitorWorkArea();
            bool bounceLeft = (currentX < (screenArea.Left + screenArea.Right) / 2.0);
            double wallX = bounceLeft ? (screenArea.Left + 15.0) : (screenArea.Right - 15.0);
            double wallY = (screenArea.Top + screenArea.Bottom) / 2.0; // Always target vertical center of the screen!

            if (WindowSurfaceDetector.TryCalculatePointJumpVelocity(currentX, currentY, wallX, wallY, out var wallLaunchVel, 70.0))
            {
                StartJumpPreparation(wallLaunchVel, IntPtr.Zero, 0, isParkour: true, parkourTarget: bestTarget);
                return true;
            }
        }

        // Standard direct ballistic jump (90%)
        double ignoreY = bestTarget.Top > Body.Bottom ? bestTarget.Top - 5.0 : 0;
        StartJumpPreparation(bestLaunchVelocity, bestTarget.HWnd, ignoreY);
        return true;
    }

    // ── Media Callbacks ────────────────────────────────────────

    private void OnMediaStarted()
    {
        // Never wake up from sleep when music/media starts!
        if (Fsm.CurrentState is StateMachine.States.SleepingState)
            return;

        if (!CatjamBehavior.IsActive)
        {
            BehaviorManager.ForceActivate(CatjamBehavior, this);
        }
    }

    private void OnMediaStopped()
    {
        if (CatjamBehavior.IsActive)
        {
            CatjamBehavior.Deactivate(this);
        }
    }

    // ── Lifecycle ──────────────────────────────────────────────

    public void Start() => GameLoop.Start();

    public void Shutdown()
    {
        GameLoop.Stop();
        GameLoop.OnUpdate -= Update;
        WindowSurfaceDetector.NewWindowDetected -= OnNewWindowDetected;

        // Cleanup media
        if (MediaListener.IsAvailable)
        {
            MediaListener.MediaStarted -= OnMediaStarted;
            MediaListener.MediaStopped -= OnMediaStopped;
        }
        MediaListener.Dispose();

        // Cleanup services
        SoundService.Dispose();
        BreakReminder.Dispose();
        TimeBehavior.Dispose();

        // Deactivate all behaviors
        BehaviorManager.DeactivateAll(this);
    }

    public void Dispose() => Shutdown();
}

/// <summary>
/// Holds singleton instances of all FSM states.
/// </summary>
public class StateRegistry
{
    public IdleState Idle { get; } = new();
    public WanderState Wander { get; } = new();
    public FallingState Falling { get; } = new();
    public DraggedState Dragged { get; } = new();
    public SleepingState Sleeping { get; } = new();
    public RecoveryState Recovery { get; } = new();
}
