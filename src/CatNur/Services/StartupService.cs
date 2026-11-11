namespace CatNur.Services;

using System;
using System.IO;
using Microsoft.Win32;

/// <summary>
/// Manages Windows auto-start registration via HKCU Registry.
/// Automatically updates the registry to point to the current active executable path.
/// </summary>
public static class StartupService
{
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "CatNur";

    /// <summary>
    /// Ensures that CatNur is automatically registered in Windows Startup on first run,
    /// and ensures the registered path always matches the currently executing CatNur.exe.
    /// </summary>
    public static void EnsureAutoStartRegistered()
    {
        string currentExe = GetCurrentExecutablePath();
        if (string.IsNullOrEmpty(currentExe) || !File.Exists(currentExe))
            return;

        string? registered = GetRegisteredPath();
        if (string.IsNullOrEmpty(registered) || !string.Equals(registered.Trim('\"'), currentExe, StringComparison.OrdinalIgnoreCase))
        {
            SetAutoStart(true);
        }
    }

    /// <summary>
    /// Gets the normalized path of the currently executing CatNur.exe.
    /// </summary>
    private static string GetCurrentExecutablePath()
    {
        string exePath = Environment.ProcessPath ?? string.Empty;
        if (string.IsNullOrEmpty(exePath) || exePath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            string targetExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CatNur.exe");
            if (File.Exists(targetExe))
            {
                exePath = targetExe;
            }
        }
        return exePath;
    }

    /// <summary>
    /// Gets the current path registered in the Windows Run key (if any).
    /// </summary>
    public static string? GetRegisteredPath()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(AppName) as string;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Enables or disables auto-start on Windows login.
    /// </summary>
    public static bool SetAutoStart(bool enable)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null) return false;

            if (enable)
            {
                string exePath = GetCurrentExecutablePath();
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                    return false;

                // Wrap in quotes to safely handle paths with spaces
                key.SetValue(AppName, $"\"{exePath}\"");
                Console.WriteLine($"[StartupService] Auto-start path updated in Registry: \"{exePath}\"");
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
                Console.WriteLine("[StartupService] Auto-start removed from Windows Registry.");
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[StartupService] Registry error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks whether auto-start is currently enabled.
    /// </summary>
    public static bool IsAutoStartEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }
}
