using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flow.Application.Abstractions;
using Flow.Application.Models;

namespace Flow.Infrastructure.Settings;

public class JsonSettingsStore : ISettingsStore
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
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
                return settings ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_lock)
        {
            SaveInternal(settings);
        }
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
