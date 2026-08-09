using Flow.Application.Models;

namespace Flow.Application.Abstractions;

/// <summary>
/// Repository abstraction for persisting and loading application configuration (settings.json).
/// </summary>
public interface ISettingsRepository
{
    AppSettings LoadSettings();
    Task<AppSettings> LoadSettingsAsync(CancellationToken ct = default);
    void SaveSettings(AppSettings settings);
    Task SaveSettingsAsync(AppSettings settings, CancellationToken ct = default);
}
