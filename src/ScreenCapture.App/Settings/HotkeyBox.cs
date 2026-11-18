using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ScreenCapture.Core.Hotkeys;
using ScreenCapture.Core.Localization;

namespace ScreenCapture.App.Settings;

/// <summary>Read-only box that records the next key combination pressed while it has focus.</summary>
public sealed class HotkeyBox : TextBox
{
    public static readonly StyledProperty<Hotkey?> HotkeyProperty =
        AvaloniaProperty.Register<HotkeyBox, Hotkey?>(nameof(Hotkey));

    public static readonly StyledProperty<HotkeyId> IdProperty =
        AvaloniaProperty.Register<HotkeyBox, HotkeyId>(nameof(Id));

    public HotkeyBox()
    {
        IsReadOnly = true;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public Hotkey? Hotkey
    {
        get => GetValue(HotkeyProperty);
        set => SetValue(HotkeyProperty, value);
    }

    public HotkeyId Id
    {
        get => GetValue(IdProperty);
        set => SetValue(IdProperty, value);
    }

    public bool IsRecording { get; private set; }

    public event EventHandler? RecordingChanged;

    public event EventHandler<Hotkey>? HotkeyCaptured;

    protected override Type StyleKeyOverride => typeof(TextBox);

    public static Hotkey? FromKey(Key key, KeyModifiers modifiers)
    {
        if (key is Key.None or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return null;

        var mods = HotkeyModifiers.None;
        if (modifiers.HasFlag(KeyModifiers.Control)) mods |= HotkeyModifiers.Ctrl;
        if (modifiers.HasFlag(KeyModifiers.Alt)) mods |= HotkeyModifiers.Alt;
        if (modifiers.HasFlag(KeyModifiers.Shift)) mods |= HotkeyModifiers.Shift;
        if (modifiers.HasFlag(KeyModifiers.Meta)) mods |= HotkeyModifiers.Win;
        return new Hotkey(mods, key.ToString());
    }

    /// <summary>Feeds a combination that never reached this box as a key event (e.g. one held by RegisterHotKey).</summary>
    public void Capture(Hotkey hotkey)
    {
        if (IsRecording) HotkeyCaptured?.Invoke(this, hotkey);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HotkeyProperty) UpdateText();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Loc.Instance.PropertyChanged += OnLocChanged;
        UpdateText();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Loc.Instance.PropertyChanged -= OnLocChanged;
        SetRecording(false);
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        SetRecording(true);
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        SetRecording(false);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Tab)
        {
            base.OnKeyDown(e);
            return;
        }

        e.Handled = true;
        if (e.Key == Key.Escape)
        {
            TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
            return;
        }

        // Windows usually delivers PrintScreen only as a key-up.
        if (e.Key == Key.PrintScreen) return;
        Record(e.Key, e.KeyModifiers);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key == Key.PrintScreen)
        {
            e.Handled = true;
            Record(e.Key, e.KeyModifiers);
            return;
        }

        base.OnKeyUp(e);
    }

    private void Record(Key key, KeyModifiers modifiers)
    {
        if (IsRecording && FromKey(key, modifiers) is { } hotkey) HotkeyCaptured?.Invoke(this, hotkey);
    }

    private void SetRecording(bool value)
    {
        if (IsRecording == value) return;
        IsRecording = value;
        UpdateText();
        RecordingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnLocChanged(object? sender, PropertyChangedEventArgs e) => UpdateText();

    private void UpdateText()
    {
        var loc = Loc.Instance;
        Text = IsRecording ? loc["Settings_HotkeyPrompt"] : Hotkey?.ToString() ?? loc["Settings_HotkeyNone"];
    }
}
