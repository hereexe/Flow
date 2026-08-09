namespace Flow.Domain;

public record ClipboardSnapshot
{
    public IReadOnlyDictionary<string, object> FormatEntries { get; init; }
    public DateTime CapturedAtUtc { get; init; }

    public ClipboardSnapshot(
        IReadOnlyDictionary<string, object>? formatEntries = null,
        DateTime? capturedAtUtc = null)
    {
        FormatEntries = formatEntries ?? new Dictionary<string, object>();
        CapturedAtUtc = capturedAtUtc ?? DateTime.UtcNow;
    }

    public static ClipboardSnapshot Empty => new();

    public bool IsEmpty => FormatEntries.Count == 0;

    public bool ContainsFormat(string formatName) => FormatEntries.ContainsKey(formatName);

    public object? GetData(string formatName) =>
        FormatEntries.TryGetValue(formatName, out var data) ? data : null;
}
