using Flow.Application.Abstractions;
using Flow.Application.Models;
using Flow.Domain;
using Xunit;

namespace Flow.Application.Tests;

public class SettingsAndSecretsTests
{
    private class MemorySecretStore : ISecretStore
    {
        private readonly Dictionary<string, string> _secrets = new(StringComparer.OrdinalIgnoreCase);

        public void SaveSecret(string providerId, string secret) => _secrets[providerId] = secret;
        public string? GetSecret(string providerId) => _secrets.TryGetValue(providerId, out var val) ? val : null;
        public bool HasSecret(string providerId) => _secrets.ContainsKey(providerId);
        public void DeleteSecret(string providerId) => _secrets.Remove(providerId);
    }

    [Fact]
    public void AppSettings_Validate_WithSameLanguages_ReturnsValidationError()
    {
        // Arrange
        var settings = new AppSettings
        {
            PrimaryLanguage = Language.Ru,
            SecondaryLanguage = Language.Ru
        };

        // Act
        bool isValid = settings.Validate(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("Primary and secondary languages must be different"));
    }

    [Fact]
    public void AppSettings_Validate_WithInvalidOnlineProvider_ReturnsValidationError()
    {
        // Arrange
        var settings = new AppSettings
        {
            Mode = TranslationMode.Online,
            ActiveOnlineProvider = "UnsupportedProvider"
        };

        // Act
        bool isValid = settings.Validate(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("Invalid online provider"));
    }

    [Fact]
    public void SecretStore_SaveAndRetrieve_ResolvesCorrectSecretByIdentifier()
    {
        // Arrange
        ISecretStore store = new MemorySecretStore();

        // Act
        store.SaveSecret(ProviderIdentifiers.Azure, "test-azure-api-key-123");

        // Assert
        Assert.True(store.HasSecret(ProviderIdentifiers.Azure));
        Assert.Equal("test-azure-api-key-123", store.GetSecret(ProviderIdentifiers.Azure));
    }
}
