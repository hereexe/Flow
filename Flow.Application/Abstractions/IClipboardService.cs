using Flow.Application.Models;

namespace Flow.Application.Abstractions;

/// <summary>
/// Service abstraction for interacting with the OS clipboard, performing transient
/// copy/paste operations and capturing/restoring ClipboardSnapshot instances.
/// </summary>
public interface IClipboardService
{
    ClipboardSnapshot CaptureSnapshot();
    Task<string> CaptureSelectedTextAsync(CancellationToken ct = default);
    Task ReplaceSelectedTextAsync(string translatedText, CancellationToken ct = default);
    void RestoreSnapshot(ClipboardSnapshot snapshot);
}
