using Flow.Application.Abstractions;

namespace Flow.Application.Services;

/// <summary>
/// Default thread-safe implementation of <see cref="IHudStatusNotifier"/> for broadcasting HUD status state changes.
/// </summary>
public class HudStatusNotifier : IHudStatusNotifier
{
    public event EventHandler<HudStatusChangedEventArgs>? StatusChanged;

    public HudStatusState CurrentState { get; private set; } = HudStatusState.Hidden;

    public void ShowTranslating()
    {
        CurrentState = HudStatusState.Translating;
        OnStatusChanged(new HudStatusChangedEventArgs(HudStatusState.Translating));
    }

    public void ShowSuccess(string? message = null)
    {
        CurrentState = HudStatusState.Success;
        OnStatusChanged(new HudStatusChangedEventArgs(HudStatusState.Success, message));
    }

    public void ShowError(string? errorMessage = null)
    {
        CurrentState = HudStatusState.Error;
        OnStatusChanged(new HudStatusChangedEventArgs(HudStatusState.Error, errorMessage));
    }

    public void Hide()
    {
        CurrentState = HudStatusState.Hidden;
        OnStatusChanged(new HudStatusChangedEventArgs(HudStatusState.Hidden));
    }

    protected virtual void OnStatusChanged(HudStatusChangedEventArgs e)
    {
        StatusChanged?.Invoke(this, e);
    }
}
