using System.IO;
using System.Media;
using IISDeploy.Core.Interfaces;

namespace IISDeploy.UI.Services;

/// <summary>
/// WPF (Windows) implementation of <see cref="INotificationSoundService"/>.
/// Plays short .wav files from %WINDIR%\Media using <see cref="SoundPlayer"/>.
/// Default mapping:
///   success -> Windows Background.wav
///   failure -> Windows Exclamation.wav
/// Both filenames are platform-provided; if either is missing on the host
/// (minimal Windows install, Server Core, etc.) the service logs a warning
/// and returns silently — it never throws.
/// </summary>
public class WpfNotificationSoundService : INotificationSoundService
{
    public const string DefaultWindowsMediaFolder = @"%WINDIR%\Media";
    public const string DefaultSuccessSoundFileName = "Windows Background.wav";
    public const string DefaultFailureSoundFileName = "Windows Exclamation.wav";

    private readonly ILoggingService _logger;
    private readonly string _successSoundPath;
    private readonly string _failureSoundPath;

    /// <summary>Production ctor — uses the platform default Windows Media paths.</summary>
    public WpfNotificationSoundService(ILoggingService logger)
        : this(logger, successSoundPath: null, failureSoundPath: null)
    {
    }

    /// <summary>
    /// Test-friendly ctor — accepts explicit absolute paths. Any null argument
    /// falls back to the default Windows Media path for that slot.
    /// </summary>
    public WpfNotificationSoundService(
        ILoggingService logger,
        string? successSoundPath,
        string? failureSoundPath)
    {
        _logger = logger;
        _successSoundPath = successSoundPath ?? ResolveDefaultPath(DefaultSuccessSoundFileName);
        _failureSoundPath = failureSoundPath ?? ResolveDefaultPath(DefaultFailureSoundFileName);
    }

    public void PlaySuccess() => Play(_successSoundPath, "success");

    public void PlayFailure() => Play(_failureSoundPath, "failure");

    private static string ResolveDefaultPath(string fileName) =>
        Path.Combine(
            Environment.ExpandEnvironmentVariables(DefaultWindowsMediaFolder),
            fileName);

    private void Play(string path, string kind)
    {
        try
        {
            if (!File.Exists(path))
            {
                _logger.Warning(
                    "Notification {Kind} sound not found at {Path}; skipping playback.",
                    kind, path);
                return;
            }

            using var player = new SoundPlayer(path);
            player.Play();
        }
        catch (Exception ex)
        {
            _logger.Warning(
                "Failed to play {Kind} notification sound at {Path}: {ErrorMessage}",
                kind, path, ex.Message);
        }
    }
}
