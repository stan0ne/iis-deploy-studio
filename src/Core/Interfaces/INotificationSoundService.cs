namespace IISDeploy.Core.Interfaces;

/// <summary>
/// Plays a short notification sound when a long-running user-initiated
/// operation (export, import, etc.) reaches a terminal state. Implementations
/// are platform-specific (WPF / Windows in the default shipping build).
/// </summary>
public interface INotificationSoundService
{
    /// <summary>Sound for a successful operation completion.</summary>
    void PlaySuccess();

    /// <summary>Sound for a failed operation completion.</summary>
    void PlayFailure();
}
