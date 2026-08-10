using System;
using System.IO;
using Flow.Domain;
using Flow.Infrastructure.Windows;
using Xunit;

namespace Flow.Application.Tests;

public class CredentialManagerSecretStoreTests : IDisposable
{
    private readonly string _tempFallbackPath;
    private readonly CredentialManagerSecretStore _store;

    public CredentialManagerSecretStoreTests()
    {
        _tempFallbackPath = Path.Combine(Path.GetTempPath(), $"flow_test_secrets_{Guid.NewGuid():N}.dat");
        _store = new CredentialManagerSecretStore(_tempFallbackPath);
    }

    public void Dispose()
    {
        try { _store.DeleteSecret(ProviderIdentifiers.Azure); } catch { }
        try { _store.DeleteSecret(ProviderIdentifiers.DeepL); } catch { }
        try { _store.DeleteSecret(ProviderIdentifiers.Google); } catch { }

        if (File.Exists(_tempFallbackPath))
        {
            try { File.Delete(_tempFallbackPath); } catch { }
        }
    }

    [Fact]
    public void SaveSecretAndGetSecret_RetrievesCorrectSecretValue()
    {
        // Arrange
        var provider = ProviderIdentifiers.Azure;
        var testKey = "test-azure-api-key-999";

        // Act
        _store.SaveSecret(provider, testKey);
        var retrieved = _store.GetSecret(provider);

        // Assert
        Assert.True(_store.HasSecret(provider));
        Assert.Equal(testKey, retrieved);
    }

    [Fact]
    public void DeleteSecret_RemovesSecretFromStore()
    {
        // Arrange
        var provider = ProviderIdentifiers.DeepL;
        var testKey = "test-deepl-key-888";
        _store.SaveSecret(provider, testKey);

        // Act
        _store.DeleteSecret(provider);

        // Assert
        Assert.False(_store.HasSecret(provider));
        Assert.Null(_store.GetSecret(provider));
    }

    [Fact]
    public void GetSecret_NonExistentProvider_ReturnsNull()
    {
        // Act
        var result = _store.GetSecret("NonExistentProvider_12345");

        // Assert
        Assert.Null(result);
        Assert.False(_store.HasSecret("NonExistentProvider_12345"));
    }
}
