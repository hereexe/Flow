using System.Runtime.InteropServices;
using System.Windows;
using Flow.Application.Abstractions;
using Flow.Application.Models;

namespace Flow.Infrastructure.Windows;

/// <summary>
/// Production implementation of <see cref="IClipboardService"/> using WPF Clipboard API.
/// Captures/restores full clipboard snapshots (all formats including images, RTF, HTML),
/// and simulates Ctrl+C/V for text capture and replacement via SendKeys.
/// All clipboard operations are dispatched to the STA (UI) thread.
/// </summary>
public class ClipboardAdapter : IClipboardService
{
    private const int ClipboardDelayMs = 80;
    private const int PasteDelayMs = 50;
    private const int MaxRetries = 3;
    private static readonly int[] RetryDelaysMs = [50, 100, 200];

    /// <inheritdoc />
    public ClipboardSnapshot CaptureSnapshot()
    {
        return DispatchOnSta(() =>
        {
            var snapshot = new ClipboardSnapshot();
            try
            {
                var dataObject = System.Windows.Clipboard.GetDataObject();
                if (dataObject != null)
                {
                    // Store the raw IDataObject reference for full fidelity restoration
                    var formats = dataObject.GetFormats();
                    var entries = new Dictionary<string, object>();
                    foreach (var format in formats)
                    {
                        try
                        {
                            var data = dataObject.GetData(format);
                            if (data != null)
                            {
                                entries[format] = data;
                            }
                        }
                        catch
                        {
                            // Some formats may not be serializable — skip gracefully
                        }
                    }
                    snapshot.DataObject = entries;
                }
            }
            catch
            {
                // Clipboard may be locked — return empty snapshot
            }
            return snapshot;
        });
    }

    /// <inheritdoc />
    public async Task<string> CaptureSelectedTextAsync(CancellationToken ct = default)
    {
        // Simulate Ctrl+C to copy selected text to clipboard
        await DispatchOnStaAsync(() =>
        {
            System.Windows.Forms.SendKeys.SendWait("^c");
        });

        // Wait for the clipboard to update
        await Task.Delay(ClipboardDelayMs, ct);

        // Read the copied text from clipboard
        return DispatchOnSta(() =>
        {
            try
            {
                return System.Windows.Clipboard.GetText() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        });
    }

    /// <inheritdoc />
    public async Task ReplaceSelectedTextAsync(string translatedText, CancellationToken ct = default)
    {
        // Place translated text on clipboard
        await DispatchOnStaAsync(() =>
        {
            System.Windows.Clipboard.SetText(translatedText);
        });

        await Task.Delay(PasteDelayMs, ct);

        // Simulate Ctrl+V to paste
        await DispatchOnStaAsync(() =>
        {
            System.Windows.Forms.SendKeys.SendWait("^v");
        });
    }

    /// <inheritdoc />
    public void RestoreSnapshot(ClipboardSnapshot snapshot)
    {
        if (snapshot.DataObject is not Dictionary<string, object> entries || entries.Count == 0)
            return;

        DispatchOnSta(() =>
        {
            var dataObject = new System.Windows.DataObject();
            foreach (var kvp in entries)
            {
                try
                {
                    dataObject.SetData(kvp.Key, kvp.Value);
                }
                catch
                {
                    // Skip formats that cannot be restored
                }
            }

            SetClipboardWithRetry(dataObject);
        });
    }

    /// <summary>
    /// Attempts to set clipboard data with retry and exponential backoff
    /// to handle cases where another application holds the clipboard lock.
    /// </summary>
    private static void SetClipboardWithRetry(System.Windows.DataObject dataObject)
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                System.Windows.Clipboard.SetDataObject(dataObject, true);
                return;
            }
            catch (ExternalException) when (attempt < MaxRetries - 1)
            {
                Thread.Sleep(RetryDelaysMs[attempt]);
            }
        }
    }

    /// <summary>
    /// Dispatches an action to the STA (UI) thread synchronously.
    /// </summary>
    private static T DispatchOnSta<T>(Func<T> action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            return action();
        }

        return dispatcher.Invoke(action);
    }

    /// <summary>
    /// Dispatches an action to the STA (UI) thread synchronously.
    /// </summary>
    private static void DispatchOnSta(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }

    /// <summary>
    /// Dispatches an action to the STA (UI) thread asynchronously.
    /// </summary>
    private static async Task DispatchOnStaAsync(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        await dispatcher.InvokeAsync(action);
    }
}
