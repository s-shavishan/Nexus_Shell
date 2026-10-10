using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Desktop;
using Nexus.Shell.Services;
using Windows.System;

namespace Nexus.Shell.UI.Controls;

internal sealed class CalculatorView : Grid
{
    private readonly DesktopEnvironment _environment;
    private readonly CalculatorEngine _engine = new();
    private readonly TextBlock _display = new() { Text = "0", FontSize = 36, TextAlignment = TextAlignment.Right, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _expression = new() { FontSize = 12, TextAlignment = TextAlignment.Right };
    private readonly List<(Button Button, bool Operator)> _buttons = [];
    private readonly Border _screen;
    internal CalculatorView(DesktopEnvironment environment)
    {
        _environment = environment; Padding = new Thickness(14, 6, 14, 16); RowSpacing = 14;
        RowDefinitions.Add(new() { Height = new GridLength(96) }); RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        var words = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center }; words.Children.Add(_expression); words.Children.Add(_display);
        _screen = new() { Child = words, Padding = new Thickness(14), CornerRadius = new CornerRadius(13) }; Children.Add(_screen);
        var keypad = new Grid { ColumnSpacing = 7, RowSpacing = 7 };
        for (int i = 0; i < 4; i++) keypad.ColumnDefinitions.Add(new());
        for (int i = 0; i < 5; i++) keypad.RowDefinitions.Add(new());
        string[] keys = ["C", "+/−", "%", "÷", "7", "8", "9", "×", "4", "5", "6", "−", "1", "2", "3", "+", "0", ".", "Back", "="];
        for (int i = 0; i < keys.Length; i++)
        {
            string key = keys[i]; bool op = key is "÷" or "×" or "−" or "+" or "=";
            var button = new Button { Content = key == "Back" ? "⌫" : key, FontSize = 21, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, CornerRadius = new CornerRadius(16), Style = (Style)Application.Current.Resources["AuraSurfaceButton"] };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, key == "Back" ? "Backspace" : key);
            button.Click += (_, _) => Input(key); Grid.SetRow(button, i / 4); Grid.SetColumn(button, i % 4); keypad.Children.Add(button); _buttons.Add((button, op));
        }
        Grid.SetRow(keypad, 1); Children.Add(keypad);
        KeyDown += (_, args) =>
        {
            // Accelerators belong to this surface only; native shortcuts stay with Windows.
            string? key = args.Key switch
            {
                >= VirtualKey.Number0 and <= VirtualKey.Number9 => ((int)args.Key - (int)VirtualKey.Number0).ToString(),
                >= VirtualKey.NumberPad0 and <= VirtualKey.NumberPad9 => ((int)args.Key - (int)VirtualKey.NumberPad0).ToString(),
                VirtualKey.Add => "+", VirtualKey.Subtract => "−", VirtualKey.Multiply => "×", VirtualKey.Divide => "÷",
                VirtualKey.Decimal => ".", VirtualKey.Enter => "=", VirtualKey.Back => "Back", VirtualKey.Escape => "C", _ => null
            };
            if (key is null) return; Input(key); args.Handled = true;
        };
        ApplyAppearance();
    }
    private void Input(string key) { _engine.Input(key); _display.Text = _engine.Display; _expression.Text = _engine.Expression; _display.FontSize = _engine.HasError ? 20 : 36; }
    internal void ApplyAppearance()
    {
        var theme = _environment.Theme; RequestedTheme = theme.ElementTheme; _screen.Background = theme.Brush("NexusInput"); _display.Foreground = theme.Brush("NexusText"); _expression.Foreground = theme.Brush("NexusMuted");
        foreach (var (button, op) in _buttons)
        { button.Background = op && !theme.HighContrast ? new SolidColorBrush(ShellTheme.Color("FFFF9F0A")) : theme.Brush("NexusCard"); button.Foreground = op && !theme.HighContrast ? new SolidColorBrush(ShellTheme.Color("FF201507")) : theme.Brush("NexusText"); }
    }
}
