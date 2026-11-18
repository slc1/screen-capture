using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ScreenCapture.App.Common;

public partial class MessageWindow : Window
{
    public MessageWindow() => InitializeComponent();

    public MessageWindow(string message) : this() => MessageText.Text = message;

    private void OnOk(object? sender, RoutedEventArgs e) => Close();
}
