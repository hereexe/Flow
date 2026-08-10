using System.Runtime.InteropServices;
using System.Windows.Input;
using Flow.Domain;

namespace Flow.Infrastructure.Windows;

/// <summary>
/// P/Invoke declarations for Win32 hotkey registration (user32.dll)
/// and helper methods for mapping domain types to Win32 constants.
/// </summary>
internal static class NativeMethods
{
    // Win32 message constants
    internal const int WM_HOTKEY = 0x0312;

    // Win32 modifier constants (match HotkeyModifiers enum values by design)
    internal const uint MOD_ALT = 0x0001;
    internal const uint MOD_CONTROL = 0x0002;
    internal const uint MOD_SHIFT = 0x0004;
    internal const uint MOD_WIN = 0x0008;

    // Win32 error codes
    internal const int ERROR_HOTKEY_ALREADY_REGISTERED = 1409;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>
    /// Maps domain-level <see cref="HotkeyModifiers"/> flags to Win32 MOD_* constants.
    /// The enum values are intentionally aligned with Win32 constants, so a direct cast works.
    /// </summary>
    internal static uint ToModifiers(HotkeyModifiers modifiers)
    {
        uint result = 0;
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) result |= MOD_ALT;
        if (modifiers.HasFlag(HotkeyModifiers.Control)) result |= MOD_CONTROL;
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) result |= MOD_SHIFT;
        if (modifiers.HasFlag(HotkeyModifiers.Win)) result |= MOD_WIN;
        return result;
    }

    /// <summary>
    /// Converts a key name string (e.g. "T", "F1", "SPACE") to a Win32 virtual key code
    /// using WPF's <see cref="KeyInterop"/> and <see cref="Key"/> enum.
    /// </summary>
    internal static uint ToVirtualKey(string key)
    {
        if (Enum.TryParse<Key>(key, ignoreCase: true, out var wpfKey))
        {
            return (uint)KeyInterop.VirtualKeyFromKey(wpfKey);
        }

        // Fallback: for single character keys like "T", "A", etc.
        // Key enum names match uppercase letters directly
        if (key.Length == 1 && char.IsAsciiLetterUpper(key[0]))
        {
            if (Enum.TryParse<Key>(key, ignoreCase: true, out var letterKey))
            {
                return (uint)KeyInterop.VirtualKeyFromKey(letterKey);
            }
        }

        throw new ArgumentException($"Unable to resolve virtual key for '{key}'.", nameof(key));
    }
}
