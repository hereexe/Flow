namespace Flow.Application.Abstractions;

/// <summary>
/// Abstraction for storing sensitive credentials (e.g. API keys) securely in Windows Credential Manager.
/// </summary>
public interface ISecretStore
{
    void SaveSecret(string providerId, string secret);
    string? GetSecret(string providerId);
    bool HasSecret(string providerId);
    void DeleteSecret(string providerId);
}
