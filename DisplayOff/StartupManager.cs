using System.Security;
using Microsoft.Win32;

namespace DisplayOff;

internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DisplayOff";

    public static bool TryApply(bool enabled, out string? error)
    {
        error = null;

        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
                ?? throw new InvalidOperationException("Windows did not open the current-user startup key.");

            if (!enabled)
            {
                if (key.GetValue(ValueName) is not null)
                    key.DeleteValue(ValueName, throwOnMissingValue: false);

                return true;
            }

            string? path = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("The application path is not available.");

            if (path.Contains('"', StringComparison.Ordinal))
                throw new InvalidOperationException("The application path cannot be registered for startup.");

            string command = $"\"{path}\"";
            if (!string.Equals(key.GetValue(ValueName) as string, command, StringComparison.OrdinalIgnoreCase))
                key.SetValue(ValueName, command);

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or InvalidOperationException)
        {
            error = "Couldn't update Start with Windows. " + ex.Message;
            return false;
        }
    }
}
