using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Flow.Presentation.Settings;

/// <summary>
/// Code-behind for SettingsWindow. Handles hotkey capture via PreviewKeyDown
/// and window lifecycle for the singleton pattern.
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        // Add the converter to window resources before InitializeComponent uses them
        Resources.Add("StringToVisibilityConverter", new StringToVisibilityConverter());

        InitializeComponent();
        DataContext = viewModel;

        // Subscribe to ViewModel's close request
        viewModel.CloseRequested += OnCloseRequested;
        Closed += OnWindowClosed;
    }

    private void OnCloseRequested()
    {
        Close();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.CloseRequested -= OnCloseRequested;
        }
    }

    private void HotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;

        // Ignore lone modifier keys — wait for a non-modifier key
        var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
        if (key is System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl
            or System.Windows.Input.Key.LeftAlt or System.Windows.Input.Key.RightAlt
            or System.Windows.Input.Key.LeftShift or System.Windows.Input.Key.RightShift
            or System.Windows.Input.Key.LWin or System.Windows.Input.Key.RWin)
        {
            return;
        }

        var modifiers = System.Windows.Input.Keyboard.Modifiers;

        // Build the hotkey string in the same format as HotkeyCombination.ToString()
        var parts = new List<string>();
        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Windows)) parts.Add("Win");

        // Convert Key enum to a readable string
        parts.Add(key.ToString().ToUpperInvariant());

        var hotkeyString = string.Join("+", parts);

        if (DataContext is SettingsViewModel vm)
        {
            vm.Hotkey = hotkeyString;
        }
    }

    private void HotkeyTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        HotkeyTextBox.ToolTip = "Press a key combination...";
    }

    private void HotkeyTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        HotkeyTextBox.ToolTip = "Click and press a key combination to set the hotkey";
    }
}

/// <summary>
/// Converts a non-empty string to Visible, empty/null to Collapsed.
/// Used for the error message TextBlock.
/// </summary>
internal class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
