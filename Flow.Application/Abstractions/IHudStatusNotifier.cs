namespace Flow.Application.Abstractions;

public enum HudStatusState
{
    Hidden,
    Translating,
    Success,
    Error,
    Canceled
}

public record HudStatusChangedEventArgs(HudStatusState State, string? Message = null);

/// <summary>
/// Service abstraction for notifying the HUD UI overlay (positioned at the bottom-center of the screen)
/// about real-time translation progress, completion, errors, and auto-dismissal.
/// </summary>
public interface IHudStatusNotifier
{
    event EventHandler<HudStatusChangedEventArgs>? StatusChanged;
    HudStatusState CurrentState { get; }
    void ShowTranslating();
    void ShowSuccess(string? message = null);
    void ShowError(string? errorMessage = null);
    void Hide();
}
