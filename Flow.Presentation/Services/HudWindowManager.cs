using System;
using System.Windows;
using System.Windows.Media;
using Flow.Application.Abstractions;
using Flow.Presentation.Views;

namespace Flow.Presentation.Services;

public class HudWindowManager : IDisposable
{
    private readonly IHudStatusNotifier _statusNotifier;
    private readonly ISettingsStore _settingsStore;
    private HudWindow? _hudWindow;
    private bool _disposed;

    public HudWindowManager(IHudStatusNotifier statusNotifier, ISettingsStore settingsStore)
    {
        _statusNotifier = statusNotifier;
        _settingsStore = settingsStore;
        _statusNotifier.StatusChanged += OnStatusChanged;
    }

    private void OnStatusChanged(object? sender, HudStatusChangedEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            if (_hudWindow == null)
            {
                _hudWindow = new HudWindow();
                // Ensure it closes when app closes
                System.Windows.Application.Current.MainWindow.Closed += (s, args) => _hudWindow.Close();
            }

            var settings = _settingsStore.Load();
            _hudWindow.ApplyThemeColors(settings.Theme);

            switch (e.State)
            {
                case HudStatusState.Translating:
                    _hudWindow.ShowTranslating();
                    break;
                case HudStatusState.Success:
                    _hudWindow.ShowSuccess(e.Message);
                    break;
                case HudStatusState.Error:
                    _hudWindow.ShowError(e.Message);
                    break;
                case HudStatusState.Hidden:
                case HudStatusState.Canceled:
                    _hudWindow.Hide();
                    break;
            }
        });
    }

    public void Dispose()
    {
        if (_disposed) return;

        if (_statusNotifier != null)
        {
            _statusNotifier.StatusChanged -= OnStatusChanged;
        }

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            _hudWindow?.Close();
        });

        _disposed = true;
    }
}
