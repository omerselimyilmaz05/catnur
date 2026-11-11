namespace CatNur.Media;

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using Windows.Media.Control;

/// <summary>
/// Listens for active media sessions using WinRT
/// GlobalSystemMediaTransportControlsSessionManager.
/// Raises events when music starts/stops playing.
/// 
/// Wrapped in try-catch to prevent crashes if WinRT APIs are unavailable.
/// </summary>
public class MediaListener : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;

    /// <summary>Fired when media playback starts.</summary>
    public event Action? MediaStarted;

    /// <summary>Fired when media playback stops/pauses.</summary>
    public event Action? MediaStopped;

    /// <summary>True if media is currently playing.</summary>
    public bool IsMediaPlaying { get; private set; }

    /// <summary>True if the media listener initialized successfully.</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>
    /// Initializes the media session listener. Must be called from UI thread.
    /// Fails silently if WinRT APIs are unavailable.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;

            HookCurrentSession(_sessionManager.GetCurrentSession());

            IsAvailable = true;
            Debug.WriteLine("[MediaListener] Initialized successfully.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaListener] Failed to initialize: {ex.Message}");
            IsAvailable = false;
            // App continues without media features
        }
    }

    private void OnCurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args)
    {
        try
        {
            HookCurrentSession(sender.GetCurrentSession());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaListener] Session change error: {ex.Message}");
        }
    }

    private void HookCurrentSession(GlobalSystemMediaTransportControlsSession? session)
    {
        // Unhook old session
        if (_currentSession != null)
        {
            _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        }

        _currentSession = session;

        // Hook new session
        if (_currentSession != null)
        {
            _currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
            CheckPlaybackStatus();
        }
        else
        {
            UpdatePlayingState(false);
        }
    }

    private void OnPlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args)
    {
        CheckPlaybackStatus();
    }

    private void CheckPlaybackStatus()
    {
        try
        {
            if (_currentSession == null)
            {
                UpdatePlayingState(false);
                return;
            }

            var playbackInfo = _currentSession.GetPlaybackInfo();
            bool isPlaying = playbackInfo?.PlaybackStatus ==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            UpdatePlayingState(isPlaying);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaListener] Playback check error: {ex.Message}");
        }
    }

    private void UpdatePlayingState(bool isPlaying)
    {
        if (IsMediaPlaying == isPlaying)
            return;

        IsMediaPlaying = isPlaying;

        // Dispatch to UI thread
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            if (isPlaying)
                MediaStarted?.Invoke();
            else
                MediaStopped?.Invoke();
        });
    }

    public void Dispose()
    {
        try
        {
            if (_currentSession != null)
            {
                _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                _currentSession = null;
            }

            if (_sessionManager != null)
            {
                _sessionManager.CurrentSessionChanged -= OnCurrentSessionChanged;
                _sessionManager = null;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaListener] Dispose error: {ex.Message}");
        }
    }
}
