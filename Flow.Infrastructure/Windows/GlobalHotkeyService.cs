using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Flow.Application.Abstractions;
using Flow.Domain;

namespace Flow.Infrastructure.Windows;

/// <summary>
/// Production implementation of <see cref="IHotkeyService"/> using Win32 RegisterHotKey/UnregisterHotKey.
/// Hooks into the WPF message loop via <see cref="HwndSource"/> to intercept WM_HOTKEY messages.
/// </summary>
public class GlobalHotkeyService : IHotkeyService, IDisposable
{
    private const int HotkeyId = 0x0001;

    private IntPtr _windowHandle;
    private HwndSource? _hwndSource;
    private Action? _onPressed;
    private bool _disposed;

    public bool IsRegistered { get; private set; }

    /// <inheritdoc />
    public bool Register(HotkeyCombination combination, Action onPressed)
    {
        ArgumentNullException.ThrowIfNull(onPressed);

        // Unregister any previous hotkey before registering a new one
        if (IsRegistered)
        {
            Unregister();
        }

        EnsureWindowHandle();

        uint modifiers = NativeMethods.ToModifiers(combination.Modifiers);
        uint vk = NativeMethods.ToVirtualKey(combination.Key);

        bool success = NativeMethods.RegisterHotKey(_windowHandle, HotkeyId, modifiers, vk);
        if (!success)
        {
            int error = Marshal.GetLastWin32Error();
            if (error == NativeMethods.ERROR_HOTKEY_ALREADY_REGISTERED)
            {
                // Hotkey is occupied by another application — report conflict gracefully
                return false;
            }

            throw new Win32Exception(error, $"Failed to register hotkey '{combination}'.");
        }

        _onPressed = onPressed;
        IsRegistered = true;
        return true;
    }

    /// <inheritdoc />
    public bool Register(string keyComboString, Action onPressed)
    {
        if (!HotkeyCombination.TryParse(keyComboString, out var combination))
        {
            return false;
        }

        return Register(combination, onPressed);
    }

    /// <inheritdoc />
    public void Unregister()
    {
        if (!IsRegistered || _windowHandle == IntPtr.Zero)
            return;

        NativeMethods.UnregisterHotKey(_windowHandle, HotkeyId);

        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProc);
        }

        _onPressed = null;
        IsRegistered = false;
    }

    /// <summary>
    /// Ensures we have a window handle to receive WM_HOTKEY messages.
    /// Creates a hidden message-only window if necessary.
    /// </summary>
    private void EnsureWindowHandle()
    {
        if (_windowHandle != IntPtr.Zero)
            return;

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(EnsureWindowHandle);
            return;
        }

        // Try to use the main application window first
        var mainWindow = System.Windows.Application.Current?.MainWindow;
        if (mainWindow != null)
        {
            var interopHelper = new WindowInteropHelper(mainWindow);
            _windowHandle = interopHelper.EnsureHandle();
            _hwndSource = HwndSource.FromHwnd(_windowHandle);
        }
        else
        {
            // Create a message-only window for receiving hotkey messages
            _hwndSource = new HwndSource(new HwndSourceParameters("FlowHotkeyMessageWindow")
            {
                Width = 0,
                Height = 0,
                PositionX = -100,
                PositionY = -100,
                WindowStyle = 0
            });
            _windowHandle = _hwndSource.Handle;
        }

        _hwndSource?.AddHook(WndProc);
    }

    /// <summary>
    /// WndProc hook that intercepts WM_HOTKEY messages and invokes the registered callback.
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            _onPressed?.Invoke();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Unregister();
        GC.SuppressFinalize(this);
    }
}
