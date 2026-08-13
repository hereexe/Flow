using Microsoft.Win32;
using Flow.Application.Abstractions;

namespace Flow.Infrastructure.Windows;

public class RegistryStartupService : IStartupService
{
    private const string AppName = "Flow";
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    public void Enable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
            var path = Environment.ProcessPath;
            if (path != null)
            {
                key?.SetValue(AppName, path);
            }
        }
        catch
        {
            // Ignore errors for robustness
        }
    }

    public void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
            key?.DeleteValue(AppName, false);
        }
        catch
        {
            // Ignore errors for robustness
        }
    }
}
