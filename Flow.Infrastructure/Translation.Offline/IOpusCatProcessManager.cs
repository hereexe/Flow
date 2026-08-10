namespace Flow.Infrastructure.Translation.Offline;

public interface IOpusCatProcessManager : IDisposable
{
    bool IsRunning { get; }
    Task EnsureStartedAsync(CancellationToken ct = default);
    Task<bool> CheckHealthAsync(CancellationToken ct = default);
    void Stop();
}
