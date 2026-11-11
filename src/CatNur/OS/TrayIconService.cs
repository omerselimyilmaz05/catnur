namespace CatNur.OS;

using System;
using System.IO;
using System.Windows;
using CatNur.Core;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

/// <summary>
/// Manages the system tray icon (NotifyIcon) in the Windows taskbar "gizli simgeler" area.
/// Allows user to exit the app, reset/center the pet if it ever gets lost or bugged, or trigger meow.
/// </summary>
public class TrayIconService : IDisposable
{
    private Forms.NotifyIcon? _notifyIcon;
    private readonly PetContext _context;

    public TrayIconService(PetContext context)
    {
        _context = context;
        Initialize();
    }

    private void Initialize()
    {
        try
        {
            _notifyIcon = new Forms.NotifyIcon
            {
                Text = "CatNur - Masaustu Kedisi",
                Visible = true
            };

            // Load icon
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(iconPath))
            {
                _notifyIcon.Icon = new Drawing.Icon(iconPath);
            }
            else
            {
                _notifyIcon.Icon = Drawing.SystemIcons.Application;
            }

            // Context Menu
            var menu = new Forms.ContextMenuStrip();

            var titleItem = new Forms.ToolStripMenuItem("🐱 CatNur")
            {
                Enabled = false,
                Font = new Drawing.Font(Drawing.FontFamily.GenericSansSerif, 9, Drawing.FontStyle.Bold)
            };
            menu.Items.Add(titleItem);
            menu.Items.Add(new Forms.ToolStripSeparator());

            var resetItem = new Forms.ToolStripMenuItem("🔄 Kediyi Merkeze Al / Kurtar", null, (s, e) =>
            {
                ResetPetToCenter();
            });
            menu.Items.Add(resetItem);

            var switchMonitorItem = new Forms.ToolStripMenuItem("🖥️ Monitörler Arası Geçiş Yap", null, (s, e) =>
            {
                SwitchToOtherMonitor();
            });
            menu.Items.Add(switchMonitorItem);

            menu.Items.Add(new Forms.ToolStripSeparator());

            var exitItem = new Forms.ToolStripMenuItem("❌ Cikis", null, (s, e) =>
            {
                ExitApplication();
            });
            menu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = menu;

            // Double click on tray icon centers pet
            _notifyIcon.DoubleClick += (s, e) =>
            {
                ResetPetToCenter();
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TrayIconService] Failed to initialize tray icon: {ex.Message}");
        }
    }

    /// <summary>
    /// Centers the pet at the top of the primary monitor and drops it down gently.
    /// Useful if the pet ever gets lost off-screen or stuck.
    /// </summary>
    public void ResetPetToCenter()
    {
        Application.Current?.Dispatcher.Invoke(() => _context.ResetToPrimaryMonitor());
    }

    /// <summary>
    /// Deliberately sends the pet across to the OTHER monitor (does nothing if only one is
    /// connected), landing in that monitor's bottom-right corner. This is the only way the
    /// pet crosses monitors on its own — normal jumping/sleeping/wandering stay scoped to
    /// whichever monitor it's currently on, so it never drifts over involuntarily.
    /// </summary>
    public void SwitchToOtherMonitor()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            if (!_context.SwitchToOtherMonitor())
            {
                Console.WriteLine("[TrayIconService] Only one monitor connected — nothing to switch to.");
            }
        });
    }

    private void ExitApplication()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            Dispose();
            Application.Current.Shutdown();
        });
    }

    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
