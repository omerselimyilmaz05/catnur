namespace CatNur;

using System;
using System.IO;
using System.Threading;
using System.Windows;
using CatNur.Views;

/// <summary>
/// Application entry point.
/// Enforces single-instance execution via system-wide Mutex and creates the main pet window.
/// </summary>
public partial class App : Application
{
    private Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        const string mutexName = "Local\\CatNur_SingleInstance_App_Mutex";
        _singleInstanceMutex = new Mutex(true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            // Another instance is already running -> exit gracefully
            Shutdown();
            return;
        }

        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
        {
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), ev.ExceptionObject?.ToString() ?? "Unknown error");
        };

        DispatcherUnhandledException += (s, ev) =>
        {
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), ev.Exception?.ToString() ?? "Unknown error");
        };

        try
        {
            EnsureAutoStartRegistered();

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), ex.ToString());
        }
    }

    /// <summary>
    /// Ensures that CatNur is always registered in Windows Startup (HKCU Run registry)
    /// with the exact path of the currently running executable.
    /// </summary>
    private static void EnsureAutoStartRegistered()
    {
        try
        {
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return;

            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key != null)
            {
                string formattedPath = $"\"{exePath}\"";
                object? existing = key.GetValue("CatNur");
                if (existing == null || !string.Equals(existing.ToString(), formattedPath, StringComparison.OrdinalIgnoreCase))
                {
                    key.SetValue("CatNur", formattedPath);
                    Console.WriteLine($"[App] Registered CatNur Auto-Start in registry: {formattedPath}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[App] Failed to update Auto-Start registry: {ex.Message}");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
        }
        catch { }

        base.OnExit(e);
    }
}
