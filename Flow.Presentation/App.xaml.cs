using System;
using System.Windows;
using Flow.Application.Abstractions;
using Flow.Application.Models;
using Flow.Application.Services;
using Flow.Infrastructure.Settings;
using Flow.Infrastructure.Translation;
using Flow.Infrastructure.Translation.Online;
using Flow.Infrastructure.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Flow.Presentation.Services;
using Flow.Presentation.Settings;

namespace Flow.Presentation;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private readonly IHost _host;
    private TrayIconManager? _trayIconManager;
    private HudWindowManager? _hudWindowManager;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Register infrastructure stores & secret storage
                services.AddSingleton<JsonSettingsStore>();
                services.AddSingleton<ISettingsStore>(sp => sp.GetRequiredService<JsonSettingsStore>());
                services.AddSingleton<ISettingsRepository>(sp => sp.GetRequiredService<JsonSettingsStore>());
                services.AddSingleton<ISecretStore, CredentialManagerSecretStore>();
                services.AddSingleton(sp => sp.GetRequiredService<ISettingsStore>().Load());
                services.AddSingleton<IStartupService, RegistryStartupService>();

                services.AddSingleton<IHotkeyService, GlobalHotkeyService>();
                services.AddSingleton<IClipboardService, ClipboardAdapter>();

                // Register online translation providers
                services.AddHttpClient<AzureTranslatorProvider>();
                services.AddSingleton<ITranslationProvider>(sp => sp.GetRequiredService<AzureTranslatorProvider>());

                services.AddHttpClient<DeepLProvider>();
                services.AddSingleton<ITranslationProvider>(sp => sp.GetRequiredService<DeepLProvider>());

                services.AddHttpClient<GoogleTranslateProvider>();
                services.AddSingleton<ITranslationProvider>(sp => sp.GetRequiredService<GoogleTranslateProvider>());

                services.AddSingleton<ITranslationProviderFactory, TranslationProviderFactory>();

                // Register application services
                services.AddSingleton<IHudStatusNotifier, HudStatusNotifier>();
                services.AddSingleton<TrayIconManager>();
                services.AddSingleton<HudWindowManager>();
                services.AddSingleton<IDirectionDetector, DirectionDetector>();
                services.AddTransient<ITranslationOrchestrator, TranslationOrchestrator>();

                // Register SettingsViewModel as transient (new instance per settings window open)
                services.AddTransient<SettingsViewModel>(sp => new SettingsViewModel(
                    sp.GetRequiredService<ISettingsStore>(),
                    sp.GetRequiredService<ISecretStore>(),
                    sp.GetRequiredService<IHotkeyService>(),
                    sp.GetRequiredService<IHudStatusNotifier>(),
                    sp.GetRequiredService<IStartupService>(),
                    CreateHotkeyPressedCallback()));
            })
            .Build();
    }

    public static IServiceProvider Services => ((App)Current)._host.Services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        await _host.StartAsync();

        Dispatcher.Invoke(() =>
        {
            // Initialize system tray icon and HUD window manager on UI thread
            _trayIconManager = Services.GetRequiredService<TrayIconManager>();
            _hudWindowManager = Services.GetRequiredService<HudWindowManager>();

            RegisterGlobalHotkey();
        });
    }

    /// <summary>
    /// Creates the callback action invoked when the global translation hotkey is pressed.
    /// This is extracted so it can be reused both at startup and by SettingsViewModel
    /// after re-registering the hotkey.
    /// </summary>
    private static Action CreateHotkeyPressedCallback()
    {
        return () =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(async () =>
            {
                using var scope = Services.CreateScope();
                var orchestrator = scope.ServiceProvider.GetService<ITranslationOrchestrator>();
                if (orchestrator != null)
                {
                    await orchestrator.ExecuteTranslationAsync();
                }
            });
        };
    }

    private void RegisterGlobalHotkey()
    {
        var settings = Services.GetService<AppSettings>();
        var hotkeyService = Services.GetService<IHotkeyService>();
        if (settings != null && hotkeyService != null)
        {
            var combo = settings.HotkeyCombination;
            bool success = hotkeyService.Register(combo, CreateHotkeyPressedCallback());

            if (!success)
            {
                var hud = Services.GetService<IHudStatusNotifier>();
                hud?.ShowError($"Hotkey conflict: '{combo}' is already registered by another application.");
            }
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _trayIconManager?.Dispose();
        _hudWindowManager?.Dispose();
        
        var hotkeyService = Services.GetService<IHotkeyService>();
        hotkeyService?.Unregister();

        using (_host)
        {
            await _host.StopAsync();
        }
        base.OnExit(e);
    }
}

