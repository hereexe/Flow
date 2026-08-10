using System;
using System.IO;
using System.Threading.Tasks;
using Flow.Application.Models;
using Flow.Domain;
using Flow.Infrastructure.Settings;
using Xunit;

namespace Flow.Application.Tests;

public class JsonSettingsStoreTests : IDisposable
{
    private readonly string _tempPath;

    public JsonSettingsStoreTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"flow_test_settings_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempPath))
        {
            try { File.Delete(_tempPath); } catch { }
        }
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_CreatesDefaultSettingsFileAndReturnsDefault()
    {
        // Arrange
        var store = new JsonSettingsStore(_tempPath);

        // Act
        var settings = store.Load();

        // Assert
        Assert.NotNull(settings);
        Assert.Equal("Ctrl+Shift+T", settings.Hotkey);
        Assert.Equal(Language.En, settings.PrimaryLanguage);
        Assert.Equal(Language.Ru, settings.SecondaryLanguage);
        Assert.True(File.Exists(_tempPath));
    }

    [Fact]
    public void SaveAndLoad_Roundtrip_PreservesModifiedSettings()
    {
        // Arrange
        var store = new JsonSettingsStore(_tempPath);
        var initial = new AppSettings
        {
            Hotkey = "Alt+F10",
            PrimaryLanguage = Language.Ru,
            SecondaryLanguage = Language.En,
            Mode = TranslationMode.Online,
            ActiveOnlineProvider = ProviderIdentifiers.DeepL
        };

        // Act
        store.Save(initial);
        var loaded = store.Load();

        // Assert
        Assert.Equal("Alt+F10", loaded.Hotkey);
        Assert.Equal(Language.Ru, loaded.PrimaryLanguage);
        Assert.Equal(Language.En, loaded.SecondaryLanguage);
        Assert.Equal(TranslationMode.Online, loaded.Mode);
        Assert.Equal(ProviderIdentifiers.DeepL, loaded.ActiveOnlineProvider);
    }

    [Fact]
    public void Load_WhenJsonIsCorrupted_ReturnsDefaultSettingsAndRecoversFile()
    {
        // Arrange
        File.WriteAllText(_tempPath, "{ invalid json content ... }");
        var store = new JsonSettingsStore(_tempPath);

        // Act
        var settings = store.Load();

        // Assert
        Assert.NotNull(settings);
        Assert.Equal("Ctrl+Shift+T", settings.Hotkey);
        // Ensure file was recovered with valid JSON
        var recoveredJson = File.ReadAllText(_tempPath);
        Assert.Contains("Ctrl+Shift+T", recoveredJson);
    }

    [Fact]
    public async Task SaveAsyncAndLoadAsync_WorksCorrectly()
    {
        // Arrange
        var store = new JsonSettingsStore(_tempPath);
        var initial = new AppSettings
        {
            Hotkey = "Ctrl+Alt+S",
            ActiveOnlineProvider = ProviderIdentifiers.Google
        };

        // Act
        await store.SaveSettingsAsync(initial);
        var loaded = await store.LoadSettingsAsync();

        // Assert
        Assert.Equal("Ctrl+Alt+S", loaded.Hotkey);
        Assert.Equal(ProviderIdentifiers.Google, loaded.ActiveOnlineProvider);
    }
}
