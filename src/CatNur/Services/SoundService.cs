namespace CatNur.Services;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;

/// <summary>
/// Manages playback of sound effects (mp3/wav) using preloaded System.Windows.Media.MediaPlayer instances.
/// Preloads all audio files at startup so media streams are fully opened, volumes are locked,
/// and there are zero audio volume race conditions or mixer desyncs.
/// </summary>
public class SoundService : IDisposable
{
    private readonly string _soundsDirectory;
    private readonly Random _random = new();
    private readonly ConcurrentDictionary<string, MediaPlayer> _players = new(StringComparer.OrdinalIgnoreCase);
    private MediaPlayer? _loopingPlayer;

    /// <summary>Global mute flag for sound effects.</summary>
    public bool IsMuted { get; set; } = false;

    /// <summary>Volume for meow sounds (0.0 to 1.0). Set to 0.20 (20%) for soft, pleasant volume.</summary>
    public double MeowVolume { get; set; } = 0.20;

    public SoundService()
    {
        _soundsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Sounds");
        PreloadAllSounds();
    }

    /// <summary>
    /// Preloads all sound files at application startup so media streams are fully opened
    /// and target volumes are locked in before the user ever clicks.
    /// </summary>
    public void PreloadAllSounds()
    {
        try
        {
            for (int i = 1; i <= 5; i++)
            {
                GetOrInitPlayer($"miyav_{i}");
            }
            GetOrInitPlayer("tisslama");
            Console.WriteLine("[SoundService] All sound effects preloaded and volume-locked at startup!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SoundService] Error during sound preloading: {ex.Message}");
        }
    }

    private MediaPlayer? GetOrInitPlayer(string soundName)
    {
        return _players.GetOrAdd(soundName, name =>
        {
            string mp3Path = Path.Combine(_soundsDirectory, $"{name}.mp3");
            string wavPath = Path.Combine(_soundsDirectory, $"{name}.wav");
            string filePath = File.Exists(mp3Path) ? mp3Path : (File.Exists(wavPath) ? wavPath : string.Empty);

            if (string.IsNullOrEmpty(filePath))
            {
                Console.WriteLine($"[SoundService] Sound file not found: {name}");
                return null!;
            }

            double targetVolume = 0.40;
            if (name.StartsWith("miyav", StringComparison.OrdinalIgnoreCase))
            {
                targetVolume = MeowVolume; // 20%
            }
            else if (name.StartsWith("tis", StringComparison.OrdinalIgnoreCase) || name.StartsWith("hiss", StringComparison.OrdinalIgnoreCase))
            {
                targetVolume = 0.15; // 15% soft hiss
            }

            var player = new MediaPlayer();
            player.Volume = targetVolume;
            player.MediaOpened += (s, e) =>
            {
                player.Volume = targetVolume;
            };
            player.Open(new Uri(filePath, UriKind.Absolute));
            return player;
        });
    }

    /// <summary>
    /// Plays a random meow sound from the 5 imported meow audio files (miyav_1 .. miyav_5).
    /// </summary>
    public void PlayRandomMeow()
    {
        int index = _random.Next(1, 6); // 1 to 5
        Play($"miyav_{index}");
    }

    /// <summary>
    /// Plays an audio file in a continuous loop (e.g. "uyuma" while sleeping).
    /// </summary>
    public void PlayLooping(string soundName)
    {
        if (IsMuted)
            return;

        try
        {
            StopLooping();

            string mp3Path = Path.Combine(_soundsDirectory, $"{soundName}.mp3");
            string wavPath = Path.Combine(_soundsDirectory, $"{soundName}.wav");
            string filePath = File.Exists(mp3Path) ? mp3Path : (File.Exists(wavPath) ? wavPath : string.Empty);

            if (string.IsNullOrEmpty(filePath))
            {
                Console.WriteLine($"[SoundService] Looping sound file not found: {soundName}");
                return;
            }

            _loopingPlayer = new MediaPlayer();
            _loopingPlayer.Volume = 0.40;
            _loopingPlayer.MediaOpened += (s, e) =>
            {
                if (_loopingPlayer != null)
                    _loopingPlayer.Volume = 0.40;
            };
            _loopingPlayer.MediaEnded += (s, e) =>
            {
                if (_loopingPlayer != null)
                {
                    _loopingPlayer.Position = TimeSpan.Zero;
                    _loopingPlayer.Play();
                }
            };
            _loopingPlayer.Open(new Uri(filePath, UriKind.Absolute));
            _loopingPlayer.Play();
            Console.WriteLine($"[SoundService] Started looping audio: {soundName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SoundService] Error playing looping sound '{soundName}': {ex.Message}");
        }
    }

    /// <summary>
    /// Stops the active looping audio (e.g. when waking up from sleep).
    /// </summary>
    public void StopLooping()
    {
        try
        {
            if (_loopingPlayer != null)
            {
                _loopingPlayer.Stop();
                _loopingPlayer.Close();
                _loopingPlayer = null;
                Console.WriteLine("[SoundService] Stopped looping audio.");
            }
        }
        catch { }
    }

    /// <summary>
    /// Plays a one-shot sound effect by name (e.g. "miyav_1", "miyav_2") with guaranteed preloaded volume.
    /// </summary>
    public void Play(string soundName)
    {
        if (IsMuted)
            return;

        try
        {
            var player = GetOrInitPlayer(soundName);
            if (player == null)
                return;

            double targetVolume = soundName.StartsWith("miyav", StringComparison.OrdinalIgnoreCase) ? MeowVolume : 0.40;
            if (soundName.StartsWith("tis", StringComparison.OrdinalIgnoreCase) || soundName.StartsWith("hiss", StringComparison.OrdinalIgnoreCase))
            {
                targetVolume = 0.15;
            }

            player.Stop();
            player.Volume = targetVolume;
            player.Position = TimeSpan.Zero;
            player.Play();
            Console.WriteLine($"[SoundService] Playing sound: {soundName} (Strict Volume: {targetVolume:P0})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SoundService] Error playing sound '{soundName}': {ex.Message}");
        }
    }

    public void Dispose()
    {
        StopLooping();
        foreach (var player in _players.Values)
        {
            try { player?.Close(); } catch { }
        }
        _players.Clear();
    }
}
