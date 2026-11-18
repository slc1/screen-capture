using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace ScreenCapture.App.Editor;

public partial class JpegQualityDialog : Window
{
    public JpegQualityDialog()
    {
        InitializeComponent();
        QualitySlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty)
                ValueText.Text = ((int)Math.Round(QualitySlider.Value)).ToString();
        };
    }

    /// <summary>Returns the chosen quality, or null when cancelled.</summary>
    public static Task<int?> ShowAsync(Window owner, int initial)
    {
        var dialog = new JpegQualityDialog();
        var value = Math.Clamp(initial, 1, 100);
        dialog.QualitySlider.Value = value;
        dialog.ValueText.Text = value.ToString();
        return dialog.ShowDialog<int?>(owner);
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close((int)Math.Round(QualitySlider.Value));

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
