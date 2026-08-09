using Flow.Application.Models;

namespace Flow.Application.Abstractions;

public interface IClipboardBackupService
{
    ClipboardSnapshot Capture();
    Task<string> ReadAfterCopyAsync(CancellationToken ct = default);
    void SetText(string text);
    Task PasteAsync(CancellationToken ct = default);
    void Restore(ClipboardSnapshot snapshot);
}
