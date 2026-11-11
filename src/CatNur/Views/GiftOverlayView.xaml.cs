namespace CatNur.Views;

using System;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CatNur.OS;

public partial class GiftOverlayView : Window
{
    private bool _isPlaying;

    public GiftOverlayView()
    {
        InitializeComponent();
        Loaded += GiftOverlayView_Loaded;
    }

    private void GiftOverlayView_Loaded(object sender, RoutedEventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        IntPtr hwnd = helper.Handle;

        int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_TOPMOST);
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

        try
        {
            string imgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", "nur_hediye.png");
            if (File.Exists(imgPath))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(imgPath, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                GiftImage.Source = bmp;
            }
        }
        catch { }
    }

    public void Play(Point petCenterPosition)
    {
        if (_isPlaying) return;
        _isPlaying = true;

        // Position above the pet centered with clear vertical clearance
        Left = petCenterPosition.X - (Width / 2.0);
        Top = petCenterPosition.Y - Height - 20.0;

        var workArea = SystemParameters.WorkArea;
        if (Left < workArea.Left + 10) Left = workArea.Left + 10;
        if (Left + Width > workArea.Right - 10) Left = workArea.Right - Width - 10;
        if (Top < workArea.Top + 10) Top = petCenterPosition.Y + 60.0;

        Show();

        // Fade in & scale in smoothly
        var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromSeconds(1.0))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var scaleIn = new DoubleAnimation(0.3, 1.0, TimeSpan.FromSeconds(1.0))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
        };

        CardContainer.BeginAnimation(OpacityProperty, fadeIn);
        CardScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleIn);
        CardScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleIn);

        var holdTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
        holdTimer.Tick += (s, e) =>
        {
            holdTimer.Stop();

            var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromSeconds(0.8))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            var scaleOut = new DoubleAnimation(1.0, 0.7, TimeSpan.FromSeconds(0.8))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (_, _) =>
            {
                Hide();
                _isPlaying = false;
            };

            CardContainer.BeginAnimation(OpacityProperty, fadeOut);
            CardScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleOut);
            CardScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleOut);
        };
        holdTimer.Start();
    }
}
