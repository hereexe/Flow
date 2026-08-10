using System;
using System.Windows;
using Flow.Application.Abstractions;
using Flow.Application.Models;
using Flow.Application.Services;
using Flow.Infrastructure.Settings;
using Flow.Infrastructure.Translation;
using Flow.Infrastructure.Translation.Offline;
using Flow.Infrastructure.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Flow.Presentation;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private readonly IHost _host;

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

                services.AddSingleton<IHotkeyService, GlobalHotkeyService>();
                services.AddSingleton<IClipboardService, ClipboardAdapter>();

                // Register offline translation process manager & provider
                services.AddSingleton<OpusCatOptions>();
                services.AddHttpClient<OpusCatProcessManager>();
                services.AddSingleton<IOpusCatProcessManager>(sp => sp.GetRequiredService<OpusCatProcessManager>());

                services.AddHttpClient<OpusCatSidecarProvider>();
                services.AddSingleton<ITranslationProvider>(sp => sp.GetRequiredService<OpusCatSidecarProvider>());

                services.AddSingleton<ITranslationProviderFactory, TranslationProviderFactory>();

                // Register application services
                services.AddSingleton<IDirectionDetector, DirectionDetector>();
                services.AddScoped<ITranslationOrchestrator>(sp =>
                {
                    var settings = sp.GetRequiredService<AppSettings>();
                    var factory = sp.GetRequiredService<ITranslationProviderFactory>();
                    var provider = factory.GetActive(settings);
                    return new TranslationOrchestrator(
                        sp.GetRequiredService<IDirectionDetector>(),
                        provider,
                        sp.GetRequiredService<IClipboardService>(),
                        sp.GetRequiredService<IHudStatusNotifier>());
                });
            })
            .Build();
    }

    public static IServiceProvider Services => ((App)Current)._host.Services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        await _host.StartAsync();

        RegisterGlobalHotkey();
    }

    private void RegisterGlobalHotkey()
    {
        var settings = Services.GetService<AppSettings>();
        var hotkeyService = Services.GetService<IHotkeyService>();
        if (settings != null && hotkeyService != null)
        {
            var combo = settings.HotkeyCombination;
            bool success = hotkeyService.Register(combo, async () =>
            {
                using var scope = Services.CreateScope();
                var orchestrator = scope.ServiceProvider.GetService<ITranslationOrchestrator>();
                if (orchestrator != null)
                {
                    await orchestrator.ExecuteTranslationAsync();
                }
            });

            if (!success)
            {
                var hud = Services.GetService<IHudStatusNotifier>();
                hud?.ShowError($"Hotkey conflict: '{combo}' is already registered by another application.");
            }
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        var hotkeyService = Services.GetService<IHotkeyService>();
        hotkeyService?.Unregister();

        using (_host)
        {
            await _host.StopAsync();
        }
        base.OnExit(e);
    }
}
