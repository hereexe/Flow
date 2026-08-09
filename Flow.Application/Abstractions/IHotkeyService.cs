using Flow.Domain;

namespace Flow.Application.Abstractions;

/// <summary>
/// Service abstraction for registering and unregistering global Windows hotkeys
/// and dispatching key press events to the application.
/// </summary>
public interface IHotkeyService
{
    bool Register(HotkeyCombination combination, Action onPressed);
    bool Register(string keyComboString, Action onPressed);
    void Unregister();
    bool IsRegistered { get; }
}
