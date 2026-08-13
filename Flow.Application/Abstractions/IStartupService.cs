namespace Flow.Application.Abstractions;

public interface IStartupService
{
    bool IsEnabled();
    void Enable();
    void Disable();
}
