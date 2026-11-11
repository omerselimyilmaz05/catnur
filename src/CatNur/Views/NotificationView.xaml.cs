namespace CatNur.Views;

using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using CatNur.OS;

/// <summary>
/// Small notification popup that appears near the pet for water reminders.
/// Auto-dismisses after a few seconds, or on click.
/// Never appears in Alt-Tab or taskbar.
/// </summary>
public partial class NotificationView : Window
{
    private readonly DispatcherTimer _autoDismissTimer;

    public NotificationView()
    {
        InitializeComponent();

        _autoDismissTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(8)
        };
        _autoDismissTimer.Tick += (_, _) => Dismiss();

        Loaded += NotificationView_Loaded;
    }

    private void NotificationView_Loaded(object sender, RoutedEventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        IntPtr hwnd = helper.Handle;

        // Hide completely from Alt-Tab and prevent taking focus
        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_TOPMOST);
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    /// <summary>
    /// Shows the notification with the given message near the specified position.
    /// </summary>
    public void ShowMessage(string message, Point petPosition)
    {
        MessageText.Text = message;

        // Position above the pet
        Left = petPosition.X - 50;
        Top = petPosition.Y - 60;

        Show();
        _autoDismissTimer.Start();
    }

    private void Dismiss()
    {
        _autoDismissTimer.Stop();
        Hide();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Dismiss();
    }
}
