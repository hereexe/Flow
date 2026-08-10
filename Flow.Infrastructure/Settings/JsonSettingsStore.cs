using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flow.Application.Abstractions;
using Flow.Application.Models;

namespace Flow.Infrastructure.Settings;

public class JsonSettingsStore : ISettingsStore, ISettingsRepository
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonSettingsStore(string? customPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            _filePath = customPath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "Flow");
            _filePath = Path.Combine(folder, "settings.json");
        }
    }

    public AppSettings Load()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    var defaultSettings = new AppSettings();
                    SaveInternal(defaultSettings);
                    return defaultSettings;
                }

                var json = File.ReadAllText(_filePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings == null || !settings.Validate(out _))
                {
                    var fallbackSettings = new AppSettings();
                    SaveInternal(fallbackSettings);
                    return fallbackSettings;
                }

                return settings;
            }
            catch
            {
                var fallbackSettings = new AppSettings();
                try
                {
                    SaveInternal(fallbackSettings);
                }
                catch
                {
                    // Ignore write failures on corrupt recovery fallback
                }
                return fallbackSettings;
            }
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (_lock)
        {
            SaveInternal(settings);
        }
    }

    public AppSettings LoadSettings() => Load();

    public Task<AppSettings> LoadSettingsAsync(CancellationToken ct = default)
    {
        return Task.Run(Load, ct);
    }

    public void SaveSettings(AppSettings settings) => Save(settings);

    public Task SaveSettingsAsync(AppSettings settings, CancellationToken ct = default)
    {
        return Task.Run(() => Save(settings), ct);
    }

    private void SaveInternal(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_filePath, json);
    }
}
