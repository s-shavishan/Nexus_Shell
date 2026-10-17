// නමෝ බුද්ධාය 🤍
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Nexus.Runtime.Files;
using Nexus.Shell.Interop;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using Windows.System;

namespace Nexus.Shell.UI.Files;

internal sealed class QuickLookView : Grid
{
    private readonly QuickLookSession _session;
    private readonly Action<string> _open;
    private readonly int _totalFiles;
    private readonly TextBlock _name = Text("", 18), _path = Text(""), _status = Text(""), _metadata = Text("");
    private readonly Border _body = new() { MinHeight = 150 };
    private readonly Image _image = new() { Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly ScrollViewer _pictureScroll;
    private readonly TextBox _text = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap,
        FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 13, MinHeight = 180, MaxHeight = 430, IsSpellCheckEnabled = false };
    private readonly ComboBox _encoding = new() { Header = "Text encoding", ItemsSource = new[] { "Auto (BOM / UTF-8)", "UTF-8", "UTF-16 LE", "UTF-16 BE", "Windows-1252" }, SelectedIndex = 0 };
    private readonly ComboBox _zoom = new() { Header = "Preview zoom", ItemsSource = new[] { "Fit", "100%", "150%", "200%" }, SelectedIndex = 0 };
    private readonly TextBox _page = new() { Header = "PDF page", Text = "1", MaxLength = 6, Width = 90 };
    private readonly PasswordBox _password = new() { Header = "PDF password (if required)", MaxLength = 200, MinWidth = 140 };
    private readonly StackPanel _pdfControls = new() { Orientation = Orientation.Horizontal, Spacing = 7 };
    private readonly Button _previous, _next, _openButton, _copy, _retry, _cancel, _pagePrevious, _pageNext, _pageGo;
    private FileStream? _file;
    private IRandomAccessStream? _random;
    private PdfDocument? _pdf;
    private MediaPlayer? _player;
    private MediaSource? _mediaSource;
    private MediaPlayerElement? _media;
    private CancellationTokenSource? _cancelRead;
    private QuickLookFormat _format = new(QuickLookKind.Unsupported, "");
    private QuickLookSize? _renderSize;
    private uint _pageIndex;
    private int _version;
    private bool _busy, _released;
    internal bool Busy => _busy;
    internal string CurrentPath => _session.Path;

    internal QuickLookView(IReadOnlyList<string> paths, string selected, Action<string> open, int totalFiles)
    {
        _session = new(paths, selected); _open = open; _totalFiles = Math.Max(paths.Count, totalFiles);
        MinWidth = 240; MaxWidth = 780; IsTabStop = true;
        _path.MaxLines = 2; _path.TextTrimming = TextTrimming.CharacterEllipsis;
        _name.MaxLines = 3; _name.TextTrimming = TextTrimming.CharacterEllipsis;
        _pictureScroll = new() { Content = _image, MaxHeight = 440, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(_name); panel.Children.Add(_path); panel.Children.Add(_metadata);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        _previous = Button("Previous file", () => MoveAsync(-1)); _next = Button("Next file", () => MoveAsync(1));
        _openButton = Button("Open externally", () => { if (!_busy) _open(_session.Path); return Task.CompletedTask; });
        _copy = Button("Copy path", () => { var data = new DataPackage(); data.SetText(_session.Path); Clipboard.SetContent(data); _status.Text = "Path copied."; return Task.CompletedTask; });
        _retry = Button("Reload preview", LoadAsync); _cancel = Button("Cancel read", () => { RequestCancel(); return Task.CompletedTask; });
        foreach (var button in new[] { _previous, _next, _openButton, _copy, _retry, _cancel }) actions.Children.Add(button);
        panel.Children.Add(Horizontal(actions));
        var options = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 }; options.Children.Add(_encoding); options.Children.Add(_zoom); panel.Children.Add(Horizontal(options));
        _pagePrevious = Button("Previous page", () => ChangePageAsync(-1)); _pageNext = Button("Next page", () => ChangePageAsync(1));
        _pageGo = Button("Go", () =>
        {
            if (!uint.TryParse(_page.Text, out uint page) || _pdf is null || page < 1 || page > _pdf.PageCount) throw new ArgumentException("Enter an available PDF page number.");
            _pageIndex = page - 1; return RenderPdfAsync();
        });
        foreach (var element in new FrameworkElement[] { _pagePrevious, _page, _pageGo, _pageNext, _password }) _pdfControls.Children.Add(element);
        panel.Children.Add(Horizontal(_pdfControls)); panel.Children.Add(_body); panel.Children.Add(_status);
        panel.Children.Add(Text("Esc closes · Alt+←/→ browses files · Ctrl+Enter opens externally. Preview is read-only; media starts only when you press Play."));
        var scroll = new ScrollViewer { Content = panel, MaxHeight = 660, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Children.Add(scroll);
        void Fit() { if (!_released && XamlRoot is { } root) scroll.MaxHeight = Math.Clamp(root.Size.Height - 160, 160, 660); FitImage(); }
        SizeChanged += (_, _) => Fit(); Loaded += (_, _) => { Fit(); Focus(FocusState.Programmatic); };
        _encoding.SelectionChanged += async (_, _) => { if (!_busy && _format.Kind == QuickLookKind.Text) await RunAsync(LoadAsync); };
        _zoom.SelectionChanged += async (_, _) => { if (!_busy) { if (_pdf is not null) await RunAsync(RenderPdfAsync); else FitImage(); } };
        Shortcut(VirtualKey.Left, VirtualKeyModifiers.Menu, () => MoveAsync(-1)); Shortcut(VirtualKey.Right, VirtualKeyModifiers.Menu, () => MoveAsync(1));
        Shortcut(VirtualKey.Enter, VirtualKeyModifiers.Control, () => { if (!_busy) _open(_session.Path); return Task.CompletedTask; });
        KeyDown += async (_, args) =>
        {
            if (_busy || args.Key is not (VirtualKey.Left or VirtualKey.Right)) return;
            DependencyObject? focus = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
            while (focus is not null && !ReferenceEquals(focus, this))
            {
                if (focus is TextBox or PasswordBox or ComboBox or MediaPlayerElement) return;
                focus = VisualTreeHelper.GetParent(focus);
            }
            args.Handled = true; await RunAsync(() => MoveAsync(args.Key == VirtualKey.Left ? -1 : 1));
        };
        AutomationProperties.SetName(_text, "Read-only text preview"); AutomationProperties.SetName(_image, "Current image or PDF page preview");
        AutomationProperties.SetName(_encoding, "Text preview encoding"); AutomationProperties.SetName(_zoom, "Preview zoom");
        UpdateActions();
    }
    private static TextBlock Text(string value, double size = 12) => new() { Text = value, FontSize = size, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
    private static ScrollViewer Horizontal(UIElement content) => new() { Content = content, HorizontalScrollMode = ScrollMode.Enabled,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollMode = ScrollMode.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private Button Button(string label, Func<Task> action)
    {
        var button = new Button { Content = label, Style = (Style)Application.Current.Resources["QuietButton"] };
        AutomationProperties.SetName(button, label); button.Click += async (_, _) => await RunAsync(action); return button;
    }
    private void Shortcut(VirtualKey key, VirtualKeyModifiers modifiers, Func<Task> action)
    {
        var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
        accelerator.Invoked += async (_, args) => { args.Handled = true; await RunAsync(action); }; KeyboardAccelerators.Add(accelerator);
    }
    private async Task RunAsync(Func<Task> action)
    {
        if (_released) return;
        try { await action(); }
        catch (Exception error) { if (!_released) _status.Text = error.Message; }
    }
    private async Task MoveAsync(int direction)
    {
        if (_busy || _released) return;
        int before = _session.Index; _session.Move(direction);
        if (before == _session.Index) return;
        _password.Password = ""; _pageIndex = 0; await LoadAsync();
    }
    internal Task StartAsync() => RunAsync(LoadAsync);
    private Task LoadAsync()
    {
        if (_busy || _released) return Task.CompletedTask;
        Reset();
        _format = QuickLookPolicy.Format(_session.Path);
        _name.Text = Path.GetFileName(_session.Path); _path.Text = _session.Path;
        ToolTipService.SetToolTip(_path, _session.Path);
        _metadata.Text = $"{_session.Index + 1}/{_session.Paths.Count} captured files · {_format.Kind}" +
            (_totalFiles > _session.Paths.Count ? $" · {_totalFiles - _session.Paths.Count} outside this preview window" : "");
        _pageIndex = 0; UpdateActions();
        return Work(async token =>
        {
            var read = await Task.Run(() => QuickLookFile.Open(_session.Path)); _file = read.Stream; token.ThrowIfCancellationRequested();
            long length = read.Bytes; QuickLookPolicy.ValidateLength(_format.Kind, length);
            _metadata.Text += $" · {length:N0} bytes · modified {read.ModifiedUtc.ToLocalTime():g}";
            if (_format.Kind == QuickLookKind.Text)
            {
                var encoding = (QuickLookEncoding)Math.Max(0, _encoding.SelectedIndex);
                var text = await Task.Run(() => QuickLookPolicy.ReadTextAsync(_file, encoding, token));
                token.ThrowIfCancellationRequested(); _text.Text = text.Text; _body.Child = _text;
                _status.Text = text.Text.Length == 0 ? "Empty text file." : text.Encoding + (text.Truncated ? " · Partial preview: byte/character/line limit reached. Open externally for the full file." : " · Read-only text; select and copy as usual.");
                return;
            }
            if (_format.Kind == QuickLookKind.Unsupported)
            { _body.Child = Text("No built-in preview for this format. Use Open externally to open it in its usual application."); _status.Text = "No file content was read or executed."; return; }
            _random = _file.AsRandomAccessStream();
            if (_format.Kind == QuickLookKind.Image)
            {
                var decoder = await Await(BitmapDecoder.CreateAsync(_random), token);
                QuickLookPolicy.ValidateImage(decoder.PixelWidth, decoder.PixelHeight);
                _renderSize = QuickLookPolicy.RenderSize(decoder.OrientedPixelWidth, decoder.OrientedPixelHeight);
                _random.Seek(0); var bitmap = new BitmapImage { DecodePixelWidth = (int)_renderSize.Width, DecodePixelHeight = (int)_renderSize.Height, AutoPlay = false };
                await Await(bitmap.SetSourceAsync(_random), token); _image.Source = bitmap; _body.Child = _pictureScroll; FitImage();
                _metadata.Text += $" · {decoder.OrientedPixelWidth} × {decoder.OrientedPixelHeight}";
                _status.Text = "Read-only image preview. Animated formats show a still frame; codec availability depends on Windows.";
            }
            else if (_format.Kind == QuickLookKind.Pdf)
            {
                string password = _password.Password;
                _pdf = await Await(password.Length == 0 ? PdfDocument.LoadFromStreamAsync(_random) : PdfDocument.LoadFromStreamAsync(_random, password), token);
                if (_pdf.PageCount is 0 or > 10000) throw new IOException("The PDF page count exceeds the preview limit.");
                await RenderPageAsync(token);
            }
            else
            {
                int version = _version;
                _mediaSource = MediaSource.CreateFromStream(_random, _format.ContentType);
                _player = new MediaPlayer { AutoPlay = false };
                _player.CommandManager.IsEnabled = false; _player.SystemMediaTransportControls.IsEnabled = false;
                _player.MediaFailed += (_, args) => DispatcherQueue.TryEnqueue(() =>
                { if (!_released && version == _version) _status.Text = "Windows could not play this file: " + args.ErrorMessage + ". A codec may be unavailable; try Open externally."; });
                _media = new MediaPlayerElement { AutoPlay = false, AreTransportControlsEnabled = true, MinHeight = 150, MaxHeight = 430, Stretch = Stretch.Uniform };
                _media.SetMediaPlayer(_player); _body.Child = _media; _player.Source = _mediaSource;
                _status.Text = "Press Play to start. Playback uses the current Windows audio device; nothing starts automatically.";
            }
        });
    }
    private Task ChangePageAsync(int direction)
    {
        if (_busy || _pdf is null || direction < 0 && _pageIndex == 0 || direction > 0 && _pageIndex + 1 >= _pdf.PageCount) return Task.CompletedTask;
        _pageIndex = (uint)(_pageIndex + direction); return RenderPdfAsync();
    }
    private Task RenderPdfAsync() => Work(RenderPageAsync);
    private async Task RenderPageAsync(CancellationToken token)
    {
        if (_pdf is null) return;
        _page.Text = (_pageIndex + 1).ToString();
        using var page = _pdf.GetPage(_pageIndex);
        double zoom = _zoom.SelectedIndex switch { 2 => 1.5, 3 => 2, _ => 1 };
        _renderSize = QuickLookPolicy.RenderSize(page.Size.Width, page.Size.Height, zoom);
        using var output = new InMemoryRandomAccessStream();
        await Await(page.RenderToStreamAsync(output, new PdfPageRenderOptions { DestinationWidth = _renderSize.Width, DestinationHeight = _renderSize.Height, BackgroundColor = Microsoft.UI.Colors.White }), token);
        output.Seek(0); var bitmap = new BitmapImage(); await Await(bitmap.SetSourceAsync(output), token);
        _image.Source = bitmap; _body.Child = _pictureScroll; _page.Text = (_pageIndex + 1).ToString(); FitImage();
        _status.Text = $"PDF page {_pageIndex + 1}/{_pdf.PageCount}. Links, scripts and attachments are not activated by this raster preview.";
    }
    private async Task Work(Func<CancellationToken, Task> action)
    {
        if (_busy || _released) return;
        using var lease = QuickLookWork.Enter(); _busy = true;
        _cancelRead = new CancellationTokenSource(TimeSpan.FromSeconds(30)); var token = _cancelRead.Token;
        _status.Text = "Reading preview… Cancel takes effect when the current read/decoder returns."; UpdateActions();
        try { await action(token); token.ThrowIfCancellationRequested(); }
        catch (OperationCanceledException) { if (!_released) { Reset(); _status.Text = "Preview read cancelled or timed out. Reload to try again."; } }
        catch (Exception error)
        {
            if (!_released) { StopPlayback(); _image.Source = null; _body.Child = Text("Preview unavailable. " + error.Message);
                _status.Text = _format.Kind == QuickLookKind.Pdf ? "For an encrypted PDF, enter its password and Reload preview, or Open externally." : "Open externally to use the file's usual application."; }
        }
        finally
        {
            _cancelRead.Dispose(); _cancelRead = null; _busy = false;
            if (_released) Reset(); else UpdateActions();
        }
    }
    private static async Task<T> Await<T>(IAsyncOperation<T> operation, CancellationToken token)
    { using var registration = token.Register(() => Cancel(operation)); var result = await operation.AsTask(); token.ThrowIfCancellationRequested(); return result; }
    private static async Task Await(IAsyncAction operation, CancellationToken token)
    { using var registration = token.Register(() => Cancel(operation)); await operation.AsTask(); token.ThrowIfCancellationRequested(); }
    private static void Cancel(IAsyncInfo operation) { try { operation.Cancel(); } catch (Exception) { } }
    private void StopPlayback()
    {
        _version++; _media?.SetMediaPlayer(null!); _player?.Dispose(); _player = null; _media = null;
        _mediaSource?.Dispose(); _mediaSource = null;
    }
    private void Reset()
    {
        StopPlayback(); _body.Child = null; _image.Source = null; _text.Text = ""; _pdf = null; _renderSize = null;
        _random?.Dispose(); _random = null; _file?.Dispose(); _file = null;
    }
    private void FitImage()
    {
        if (_released || _renderSize is not { } size) return;
        if (_zoom.SelectedIndex == 0)
        { _image.Width = Math.Max(200, Math.Min(720, ActualWidth - 20)); _image.Height = Math.Clamp((XamlRoot?.Size.Height ?? 720) - 350, 160, 430); }
        else
        { double zoom = _pdf is not null ? 1 : _zoom.SelectedIndex switch { 2 => 1.5, 3 => 2, _ => 1 }; _image.Width = size.Width * zoom; _image.Height = size.Height * zoom; }
    }
    private void UpdateActions()
    {
        if (_released) return;
        _previous.IsEnabled = !_busy && _session.CanPrevious; _next.IsEnabled = !_busy && _session.CanNext;
        _openButton.IsEnabled = _copy.IsEnabled = _retry.IsEnabled = !_busy;
        _encoding.Visibility = _format.Kind == QuickLookKind.Text ? Visibility.Visible : Visibility.Collapsed;
        _encoding.IsEnabled = !_busy;
        _zoom.Visibility = _format.Kind is QuickLookKind.Pdf or QuickLookKind.Image ? Visibility.Visible : Visibility.Collapsed; _zoom.IsEnabled = !_busy;
        _pdfControls.Visibility = _format.Kind == QuickLookKind.Pdf ? Visibility.Visible : Visibility.Collapsed;
        _pagePrevious.IsEnabled = !_busy && _pdf is not null && _pageIndex > 0; _pageNext.IsEnabled = !_busy && _pdf is not null && _pageIndex + 1 < _pdf.PageCount;
        _pageGo.IsEnabled = _page.IsEnabled = !_busy && _pdf is not null; _password.IsEnabled = !_busy;
        _cancel.IsEnabled = _busy && _cancelRead?.IsCancellationRequested == false;
    }
    internal void RequestCancel() { _cancelRead?.Cancel(); if (!_released && _busy) _status.Text = "Stopping after the current read/decoder returns…"; UpdateActions(); }
    internal void Release() { _released = true; _cancelRead?.Cancel(); _password.Password = ""; if (!_busy) Reset(); }
}
