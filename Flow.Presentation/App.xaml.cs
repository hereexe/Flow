using System;
using System.Windows;
using Flow.Application.Abstractions;
using Flow.Infrastructure.Settings;
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
                // Register infrastructure stores
                services.AddSingleton<ISettingsStore, JsonSettingsStore>();
                services.AddSingleton(sp => sp.GetRequiredService<ISettingsStore>().Load());
            })
            .Build();
    }

    public static IServiceProvider Services => ((App)Current)._host.Services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        await _host.StartAsync();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        using (_host)
        {
            await _host.StopAsync();
        }
        base.OnExit(e);
    }
}
