namespace Flow.Application.Abstractions;

public interface IHotkeyService
{
    void Register(string hotkey, Action onPressed);
    void Unregister();
}
