namespace Flow.Application.Abstractions;

public interface ISecretStore
{
    void Save(string providerId, string secret);
    string? Load(string providerId);
    void Delete(string providerId);
}
