using Microsoft.Win32;

namespace TsDssConverter.Tray;

/// <summary>
/// "Start with Windows": a value under HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
/// That is per user and needs no admin rights. The settings window reads the REAL registry state,
/// so it stays correct even if someone changed the registry by hand.
/// </summary>
internal class StartWithWindows
{
    public const string DefaultKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TsDssConverter";

    private readonly string _keyPath;
    private readonly string _exePath;

    /// <param name="keyPath">Only the tests use another key, so they never touch the real Run key.</param>
    /// <param name="exePath">The program to start. Default: this exe.</param>
    public StartWithWindows(string keyPath = DefaultKeyPath, string? exePath = null)
    {
        _keyPath = keyPath;
        _exePath = exePath ?? Environment.ProcessPath ?? Application.ExecutablePath;
    }

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
        return key?.GetValue(ValueName) is string command && command.Trim() != "";
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            using var key = Registry.CurrentUser.CreateSubKey(_keyPath);
            // Quotes: the path may contain spaces.
            key.SetValue(ValueName, "\"" + _exePath + "\"", RegistryValueKind.String);
        }
        else
        {
            using var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
