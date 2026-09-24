using DesktopBoard.Core.Interfaces;
using Microsoft.Win32;

namespace DesktopBoard.Windows.Startup;

/// <summary>
/// "Start with Windows" through HKCU\...\Run. Per-user, no administrator rights, and
/// visible/removable in Task Manager > Startup like any other app.
/// </summary>
public sealed class StartupService : IStartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DesktopBoard";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        var value = key?.GetValue(ValueName) as string;
        return !string.IsNullOrEmpty(value) && value.Contains(ExecutablePath, StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (key is null) return;
        if (enabled)
            key.SetValue(ValueName, $"\"{ExecutablePath}\" --autostart", RegistryValueKind.String);
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string ExecutablePath => Environment.ProcessPath ?? throw new InvalidOperationException("Cannot resolve the executable path.");
}
