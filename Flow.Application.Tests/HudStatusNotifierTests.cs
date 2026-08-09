using Flow.Application.Abstractions;
using Xunit;

namespace Flow.Application.Tests;

public class HudStatusNotifierTests
{
    private class FakeHudNotifier : IHudStatusNotifier
    {
        public event EventHandler<HudStatusChangedEventArgs>? StatusChanged;
        public HudStatusState CurrentState { get; private set; } = HudStatusState.Hidden;

        public void ShowTranslating()
        {
            CurrentState = HudStatusState.Translating;
            StatusChanged?.Invoke(this, new HudStatusChangedEventArgs(HudStatusState.Translating));
        }

        public void ShowSuccess(string? message = null)
        {
            CurrentState = HudStatusState.Success;
            StatusChanged?.Invoke(this, new HudStatusChangedEventArgs(HudStatusState.Success, message));
        }

        public void ShowError(string errorMessage)
        {
            CurrentState = HudStatusState.Error;
            StatusChanged?.Invoke(this, new HudStatusChangedEventArgs(HudStatusState.Error, errorMessage));
        }

        public void Hide()
        {
            CurrentState = HudStatusState.Hidden;
            StatusChanged?.Invoke(this, new HudStatusChangedEventArgs(HudStatusState.Hidden));
        }
    }

    [Fact]
    public void ShowTranslating_EmitsStatusChangedEvent_AndUpdatesCurrentState()
    {
        // Arrange
        var notifier = new FakeHudNotifier();
        HudStatusState? lastStateEmitted = null;

        notifier.StatusChanged += (sender, args) => lastStateEmitted = args.State;

        // Act
        notifier.ShowTranslating();

        // Assert
        Assert.Equal(HudStatusState.Translating, notifier.CurrentState);
        Assert.Equal(HudStatusState.Translating, lastStateEmitted);
    }

    [Fact]
    public void ShowError_EmitsErrorMessageInEvent()
    {
        // Arrange
        var notifier = new FakeHudNotifier();
        string? errorMessage = null;

        notifier.StatusChanged += (sender, args) => errorMessage = args.Message;

        // Act
        notifier.ShowError("API connection timeout");

        // Assert
        Assert.Equal(HudStatusState.Error, notifier.CurrentState);
        Assert.Equal("API connection timeout", errorMessage);
    }
}
