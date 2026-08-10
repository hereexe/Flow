using System;
using System.Windows;
using System.Windows.Media;
using Flow.Application.Abstractions;
using Flow.Presentation.Views;

namespace Flow.Presentation.Services;

public class HudWindowManager : IDisposable
{
    private readonly IHudStatusNotifier _statusNotifier;
    private HudWindow? _hudWindow;
    private bool _disposed;

    public HudWindowManager(IHudStatusNotifier statusNotifier)
    {
        _statusNotifier = statusNotifier;
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

            switch (e.State)
            {
                case HudStatusState.Translating:
                    _hudWindow.ShowStatus("Translating...", null, new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xCC, 0x00, 0x00, 0x00)));
                    break;
                case HudStatusState.Success:
                    _hudWindow.ShowStatus("Ready", TimeSpan.FromSeconds(1), new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xCC, 0x00, 0x55, 0x00)));
                    break;
                case HudStatusState.Error:
                    _hudWindow.ShowStatus(e.Message ?? "Error", TimeSpan.FromSeconds(4), new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xCC, 0x88, 0x00, 0x00)));
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
