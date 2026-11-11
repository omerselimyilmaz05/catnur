namespace CatNur.Views;

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

/// <summary>
/// Visual representation of the pet with dynamic multi-frame sprite animation engine.
/// Guarantees that active action animations play every frame to completion before the next animation begins.
/// </summary>
public partial class PetView : UserControl
{
    private class SpriteAnimation
    {
        public BitmapImage[] Frames { get; }
        public int Fps { get; }
        public TimeSpan Interval { get; }

        public SpriteAnimation(BitmapImage[] frames, int fps)
        {
            Frames = frames;
            Fps = Math.Max(1, fps);
            Interval = TimeSpan.FromMilliseconds(1000.0 / Fps);
        }
    }

    private static readonly HashSet<string> OneShotAnimationNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "otururken_esneme",
        "otururken_pati_yalama",
        "sag_kulak_kasima",
        "sol_kulak_kasima",
        "yer_eseleme",
        "tisslama",
        "uyurken_esneme_v1",
        "uyurken_el_yalama_v1",
        "miyavlama",
        "meow",
        "sit_yawn",
        "groom",
        "scratch_ear_right",
        "scratch_ear_left",
        "scratch_ground",
        "hiss"
    };

    private readonly Dictionary<string, SpriteAnimation> _animations = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _animTimer;

    private string _currentSpriteState = string.Empty;
    private SpriteAnimation? _currentAnimation;
    private int _currentFrameIndex;
    private Action? _onAnimationCompleted;
    private bool _isOneShot;
    private double _oneShotDurationSeconds = 0;
    private double _oneShotElapsedSeconds = 0;
    private bool _facingRight = true;

    /// <summary>True while a non-interruptible action animation (yawn, scratch, hiss, etc.) is currently playing.</summary>
    public bool IsActionAnimationPlaying => _isOneShot;

    public string CurrentSkin { get; set; } = "WhiteCat";

    public PetView()
    {
        InitializeComponent();

        _animTimer = new DispatcherTimer(DispatcherPriority.Render);
        _animTimer.Tick += OnAnimTimerTick;

        LoadAnimations(CurrentSkin);
        SetSpriteState("oturma");
    }

    /// <summary>
    /// Loads all animation frame sequences dynamically from all subdirectories in Assets/Images/{skinName}/.
    /// </summary>
    public void LoadAnimations(string skinName)
    {
        _animations.Clear();
        string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", skinName);

        if (!Directory.Exists(baseDir))
            return;

        // Default FPS map for known behaviors (Turkish & English)
        var defaultFps = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "idle", 8 }, { "oturma", 8 }, { "sit", 8 },
            { "walk", 10 }, { "yurume", 10 },
            { "run", 12 }, { "kosma", 12 },
            { "jump", 8 }, { "ziplama", 8 },
            { "fall", 8 }, { "dusme", 8 },
            { "drag", 10 }, { "tutulma", 10 },
            { "hiss", 4 }, { "tisslama", 4 },
            { "scratch_ground", 10 }, { "yer_eseleme", 10 },
            { "groom_paw", 8 }, { "groom", 8 }, { "pati_yalama", 8 }, { "otururken_pati_yalama", 8 },
            { "scratch_ear_right", 8 }, { "sag_kulak_kasima", 8 },
            { "scratch_ear_left", 8 }, { "sol_kulak_kasima", 8 },
            { "sit_yawn", 6 }, { "otururken_esneme", 6 },
            { "sit_to_stand", 8 }, { "ayaga_kalkma", 8 },
            { "sleep_sitting", 1 }, { "otururken_uyuma", 1 },
            { "sleep", 1 }, { "uyuma_v1", 1 }, { "uyuma_v2", 1 }, { "uyuma_v3", 1 }, { "uyuma_v4", 1 },
            { "sleep_v1_stretch", 6 }, { "uyurken_esneme_v1", 6 },
            { "sleep_v1_groom", 8 }, { "uyurken_el_yalama_v1", 8 },
            { "sleep_v2_stretch", 6 }, { "uyurken_esneme_v2", 6 },
            { "muzik_dans", 8 }, { "catjam", 8 },
            { "ziplama_hazirlik", 10 },
            { "miyavlama", 6 }, { "meow", 6 },
            { "duvardan_kayma", 8 }, { "wall_slide", 8 },
        };

        // Scan all subdirectories in the skin folder
        foreach (string subDir in Directory.GetDirectories(baseDir))
        {
            string animName = Path.GetFileName(subDir);
            var frames = new List<BitmapImage>();

            // Load frame_0.png, frame_1.png, ...
            int frameIdx = 0;
            while (true)
            {
                string framePath = Path.Combine(subDir, $"frame_{frameIdx}.png");
                if (!File.Exists(framePath))
                    break;

                var bmp = LoadBitmap(framePath);
                if (bmp != null)
                    frames.Add(bmp);

                frameIdx++;
            }

            if (frames.Count > 0)
            {
                int fps = defaultFps.TryGetValue(animName, out int f) ? f : 8;
                _animations[animName] = new SpriteAnimation(frames.ToArray(), fps);
                Console.WriteLine($"[PetView] Loaded animation '{animName}' with {frames.Count} frames ({fps} FPS)");
            }
        }

        // Turkish <-> English Alias pairs
        var aliasPairs = new (string Key1, string Key2)[]
        {
            ("oturma", "idle"),
            ("oturma", "sit"),
            ("yurume", "walk"),
            ("dusme", "fall"),
            ("ziplama", "jump"),
            ("tutulma", "drag"),
            ("uyuma_v1", "sleep"),
            ("otururken_pati_yalama", "groom"),
            ("otururken_pati_yalama", "groom_paw"),
            ("otururken_pati_yalama", "pati_yalama"),
            ("sag_kulak_kasima", "scratch_ear_right"),
            ("sol_kulak_kasima", "scratch_ear_left"),
            ("tisslama", "hiss"),
            ("miyavlama", "meow"),
            ("yer_eseleme", "scratch_ground"),
            ("ayaga_kalkma", "sit_to_stand"),
            ("otururken_uyuma", "sleep_sitting"),
            ("otururken_esneme", "sit_yawn"),
            ("uyurken_esneme_v1", "sleep_v1_stretch"),
            ("uyurken_el_yalama_v1", "sleep_v1_groom"),
            ("uyurken_esneme_v2", "sleep_v2_stretch"),
            ("muzik_dans", "catjam"),
        };

        foreach (var (k1, k2) in aliasPairs)
        {
            if (_animations.TryGetValue(k1, out var anim) && !_animations.ContainsKey(k2))
                _animations[k2] = anim;
            else if (_animations.TryGetValue(k2, out var anim2) && !_animations.ContainsKey(k1))
                _animations[k1] = anim2;
        }
    }

    private static BitmapImage? LoadBitmap(string path)
    {
        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.UriSource = new Uri(path, UriKind.Absolute);
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PetView] Error loading bitmap '{path}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Switches the active animation state.
    /// If an action animation is currently playing, prevents interruption until its full cycle completes
    /// (unless emergency drag/fall occurs).
    /// </summary>
    public void SetSpriteState(string stateName)
    {
        // Allow emergency override only on drag or active jump/fall physics
        if (_isOneShot)
        {
            if (string.Equals(stateName, "tutulma", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stateName, "drag", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stateName, "ziplama", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stateName, "dusme", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stateName, "ziplama_hazirlik", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stateName, "duvardan_kayma", StringComparison.OrdinalIgnoreCase))
            {
                _isOneShot = false;
                _onAnimationCompleted = null;
            }
            else
            {
                // Protect current action animation from premature cutoff
                return;
            }
        }

        if (string.Equals(_currentSpriteState, stateName, StringComparison.OrdinalIgnoreCase))
            return;

        if (!_animations.TryGetValue(stateName, out var anim))
        {
            if (!_animations.TryGetValue("oturma", out anim) && !_animations.TryGetValue("idle", out anim))
                return;
        }

        // If this animation is a one-shot action, treat it as non-interruptible sequence
        if (OneShotAnimationNames.Contains(stateName))
        {
            PlayAnimationOnce(stateName);
            return;
        }

        _onAnimationCompleted = null;
        _currentSpriteState = stateName;
        _currentAnimation = anim;
        _currentFrameIndex = 0;

        UpdateSpriteAlignment(stateName);

        if (anim.Frames.Length > 0)
        {
            PetSprite.Source = anim.Frames[0];
        }

        if (anim.Frames.Length > 1)
        {
            _animTimer.Interval = anim.Interval;
            if (!_animTimer.IsEnabled)
                _animTimer.Start();
        }
        else
        {
            _animTimer.Stop();
        }
    }

    /// <summary>
    /// Plays an animation for a specified duration (e.g. 2.0s for hissing), looping frames until the duration completes.
    /// Non-interruptible by other states.
    /// </summary>
    public void PlayAnimationForDuration(string stateName, TimeSpan duration, Action? onCompleted = null)
    {
        if (!_animations.TryGetValue(stateName, out var anim))
        {
            onCompleted?.Invoke();
            return;
        }

        _isOneShot = true;
        _oneShotDurationSeconds = duration.TotalSeconds;
        _oneShotElapsedSeconds = 0;
        _onAnimationCompleted = onCompleted;
        _currentSpriteState = stateName;
        _currentAnimation = anim;
        _currentFrameIndex = 0;

        UpdateSpriteAlignment(stateName);

        if (anim.Frames.Length > 0)
        {
            PetSprite.Source = anim.Frames[0];
        }

        _animTimer.Interval = anim.Frames.Length > 1 ? anim.Interval : TimeSpan.FromMilliseconds(100);
        if (!_animTimer.IsEnabled)
            _animTimer.Start();
    }

    /// <summary>
    /// Plays an animation once to full completion (frame 0 to frame N-1) and then calls onCompleted.
    /// Never cuts off in the middle.
    /// </summary>
    public void PlayAnimationOnce(string stateName, Action? onCompleted = null)
    {
        if (!_animations.TryGetValue(stateName, out var anim))
        {
            onCompleted?.Invoke();
            return;
        }

        _isOneShot = true;
        _oneShotDurationSeconds = 0;
        _oneShotElapsedSeconds = 0;
        _onAnimationCompleted = onCompleted;
        _currentSpriteState = stateName;
        _currentAnimation = anim;
        _currentFrameIndex = 0;

        UpdateSpriteAlignment(stateName);

        if (anim.Frames.Length > 0)
        {
            PetSprite.Source = anim.Frames[0];
        }

        if (anim.Frames.Length > 1)
        {
            _animTimer.Interval = anim.Interval;
            if (!_animTimer.IsEnabled)
                _animTimer.Start();
        }
        else
        {
            _animTimer.Stop();
            _isOneShot = false;
            onCompleted?.Invoke();
        }
    }

    private void OnAnimTimerTick(object? sender, EventArgs e)
    {
        if (_currentAnimation == null)
            return;

        if (_isOneShot)
        {
            if (_oneShotDurationSeconds > 0)
            {
                _oneShotElapsedSeconds += _animTimer.Interval.TotalSeconds;
                if (_currentAnimation.Frames.Length > 1)
                {
                    _currentFrameIndex = (_currentFrameIndex + 1) % _currentAnimation.Frames.Length;
                    PetSprite.Source = _currentAnimation.Frames[_currentFrameIndex];
                }

                if (_oneShotElapsedSeconds >= _oneShotDurationSeconds)
                {
                    _animTimer.Stop();
                    var callback = _onAnimationCompleted;
                    _onAnimationCompleted = null;
                    _isOneShot = false;
                    _oneShotDurationSeconds = 0;
                    _oneShotElapsedSeconds = 0;
                    _currentSpriteState = string.Empty;
                    callback?.Invoke();
                    return;
                }
                return;
            }
            else
            {
                if (_currentAnimation.Frames.Length <= 1)
                {
                    _animTimer.Stop();
                    var callback = _onAnimationCompleted;
                    _onAnimationCompleted = null;
                    _isOneShot = false;
                    _currentSpriteState = string.Empty;
                    callback?.Invoke();
                    return;
                }

                _currentFrameIndex++;
                if (_currentFrameIndex >= _currentAnimation.Frames.Length)
                {
                    // Action animation has fully completed every frame!
                    _animTimer.Stop();
                    var callback = _onAnimationCompleted;
                    _onAnimationCompleted = null;
                    _isOneShot = false;
                    _currentSpriteState = string.Empty; // Allow next state transition cleanly
                    callback?.Invoke();
                    return;
                }
                PetSprite.Source = _currentAnimation.Frames[_currentFrameIndex];
                return;
            }
        }
        else
        {
            if (_currentAnimation.Frames.Length > 1)
            {
                _currentFrameIndex = (_currentFrameIndex + 1) % _currentAnimation.Frames.Length;
                PetSprite.Source = _currentAnimation.Frames[_currentFrameIndex];
            }
        }
    }

    /// <summary>
    /// Sets the visual facing direction.
    /// Default sprite faces right: ScaleX = 1.0 (facing right), -1.0 (facing left).
    /// </summary>
    public void SetFacingDirection(bool facingRight)
    {
        if (_facingRight == facingRight)
            return;

        _facingRight = facingRight;
        BounceScale.ScaleX = facingRight ? 1.0 : -1.0;
    }

    /// <summary>
    /// Triggers a cute squash & stretch scale bounce effect (e.g. on click / jump).
    /// </summary>
    public void PlayBounceEffect()
    {
        double currentSign = _facingRight ? 1.0 : -1.0;

        var animY = new DoubleAnimation
        {
            From = 1.0,
            To = 0.82,
            Duration = TimeSpan.FromMilliseconds(120),
            AutoReverse = true,
            FillBehavior = FillBehavior.Stop,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        var animX = new DoubleAnimation
        {
            From = 1.0 * currentSign,
            To = 1.15 * currentSign,
            Duration = TimeSpan.FromMilliseconds(120),
            AutoReverse = true,
            FillBehavior = FillBehavior.Stop,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        BounceScale.BeginAnimation(ScaleTransform.ScaleYProperty, animY);
        BounceScale.BeginAnimation(ScaleTransform.ScaleXProperty, animX);
    }

    /// <summary>
    /// Dynamically aligns the sprite margin so that tall/vertical sprites like duvardan_kayma
    /// are never clipped by the window boundary, while ground sprites remain flush with the surface.
    /// </summary>
    private void UpdateSpriteAlignment(string stateName)
    {
        if (string.Equals(stateName, "duvardan_kayma", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(stateName, "wall_slide", StringComparison.OrdinalIgnoreCase))
        {
            PetSprite.Margin = new Thickness(0, 0, 0, 0); // No bottom clip: entire tail is 100% visible!
        }
        else
        {
            PetSprite.Margin = new Thickness(0, 0, 0, -21); // Ground paws surface alignment
        }
    }
}
