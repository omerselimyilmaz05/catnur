namespace CatNur.Views;

using System.Windows;
using System.Windows.Controls;
using CatNur.Behaviors;

/// <summary>
/// Displays collected sunflowers above the taskbar.
/// Each sunflower is shown as a 🌻 emoji TextBlock.
/// </summary>
public partial class SunflowerView : Window
{
    public SunflowerView()
    {
        InitializeComponent();

        // Position above taskbar
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + 20;
        Top = workArea.Bottom - Height;
    }

    /// <summary>
    /// Adds a sunflower emoji to the display.
    /// </summary>
    public void AddSunflower(SunflowerItem item)
    {
        var text = new TextBlock
        {
            Text = "🌻",
            FontSize = 24,
            Margin = new Thickness(2, 0, 2, 0),
            VerticalAlignment = VerticalAlignment.Bottom
        };
        SunflowerPanel.Children.Add(text);
    }
}
