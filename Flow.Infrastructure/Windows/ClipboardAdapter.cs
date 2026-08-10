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
        // Clear clipboard first to ensure we only capture newly copied text
        await DispatchOnStaAsync(ClearClipboardWithRetry);

        // Simulate Ctrl+C to copy selected text to clipboard
        await DispatchOnStaAsync(SendCopyCommand);

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
        await DispatchOnStaAsync(SendPasteCommand);

        // Wait for the target application to process the Ctrl+V and read the clipboard
        // before we allow the orchestrator to restore the old clipboard snapshot!
        await Task.Delay(150, ct); 
    }

    private static void ReleaseModifiers()
    {
        // Release common modifiers that might be held down by the user
        if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0)
            NativeMethods.keybd_event(NativeMethods.VK_SHIFT, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0)
            NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_MENU) & 0x8000) != 0)
            NativeMethods.keybd_event(NativeMethods.VK_MENU, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_LWIN) & 0x8000) != 0)
            NativeMethods.keybd_event(NativeMethods.VK_LWIN, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_RWIN) & 0x8000) != 0)
            NativeMethods.keybd_event(NativeMethods.VK_RWIN, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    private static void SendCopyCommand()
    {
        ReleaseModifiers();
        Thread.Sleep(10);
        const byte VK_C = 0x43;
        NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(VK_C, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(VK_C, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    private static void SendPasteCommand()
    {
        ReleaseModifiers();
        Thread.Sleep(10);
        const byte VK_V = 0x56;
        NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(VK_V, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(VK_V, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
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

    private static void ClearClipboardWithRetry()
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                System.Windows.Clipboard.Clear();
                return;
            }
            catch (ExternalException) when (attempt < MaxRetries - 1)
            {
                Thread.Sleep(RetryDelaysMs[attempt]);
            }
            catch
            {
                // Fallback ignore
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
