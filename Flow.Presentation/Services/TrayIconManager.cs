using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Flow.Application.Abstractions;
using Flow.Presentation.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Flow.Presentation.Services;

public class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly IHudStatusNotifier _statusNotifier;
    private readonly IServiceProvider _serviceProvider;
    private SettingsWindow? _settingsWindow;
    private bool _disposed;

    public TrayIconManager(IHudStatusNotifier statusNotifier, IServiceProvider serviceProvider)
    {
        _statusNotifier = statusNotifier;
        _serviceProvider = serviceProvider;

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "Flow - Ready"
        };

        InitializeContextMenu();

        _statusNotifier.StatusChanged += OnStatusChanged;
    }

    private void InitializeContextMenu()
    {
        var contextMenu = new ContextMenuStrip();
        
        var settingsMenuItem = new ToolStripMenuItem("Settings");
        settingsMenuItem.Click += (s, e) => OpenSettingsWindow();

        var exitMenuItem = new ToolStripMenuItem("Exit");
        exitMenuItem.Click += (s, e) => 
        {
            System.Windows.Application.Current?.Shutdown();
        };
        
        contextMenu.Items.Add(settingsMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitMenuItem);
        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    private void OpenSettingsWindow()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            if (_settingsWindow != null)
            {
                _settingsWindow.Activate();
                return;
            }

            var viewModel = _serviceProvider.GetRequiredService<SettingsViewModel>();
            _settingsWindow = new SettingsWindow(viewModel);
            _settingsWindow.Closed += (s, e) => _settingsWindow = null;
            _settingsWindow.Show();
        });
    }

    private void OnStatusChanged(object? sender, HudStatusChangedEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            switch (e.State)
            {
                case HudStatusState.Translating:
                    SetTranslatingState();
                    break;
                case HudStatusState.Error:
                    SetErrorState(e.Message);
                    break;
                case HudStatusState.Success:
                case HudStatusState.Hidden:
                case HudStatusState.Canceled:
                default:
                    SetReadyState();
                    break;
            }
        });
    }

    private void SetReadyState()
    {
        _notifyIcon.Icon = SystemIcons.Application;
        _notifyIcon.Text = "Flow - Ready";
    }

    private void SetTranslatingState()
    {
        _notifyIcon.Icon = SystemIcons.Information;
        _notifyIcon.Text = "Flow - Translating...";
    }

    private void SetErrorState(string? message)
    {
        _notifyIcon.Icon = SystemIcons.Error;
        var tooltip = "Flow - Error";
        if (!string.IsNullOrWhiteSpace(message))
        {
            tooltip += $"\n{message}";
            // Truncate tooltip if it's too long for NotifyIcon (max 63 chars in some Windows versions, max 127 in newer)
            if (tooltip.Length > 120) 
            {
                tooltip = tooltip.Substring(0, 117) + "...";
            }
        }
        _notifyIcon.Text = tooltip;
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        if (_statusNotifier != null)
        {
            _statusNotifier.StatusChanged -= OnStatusChanged;
        }

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        _disposed = true;
    }
}

