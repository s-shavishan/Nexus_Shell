using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Services;
using Windows.Foundation;

namespace Nexus.Shell.UI;

// Pointer-driven rubber-band selection on empty canvas space. No global hooks,
// native injection, or simulated keyboard modifiers. Existing Ctrl/Shift item
// selection is delegated to WinUI's Extended selection mode.
internal sealed class SelectionMarquee
{
    private readonly UIElement _pointerSource;
    private readonly ListViewBase _items;
    private readonly Canvas _layer;
    private readonly Func<bool> _enabled;
    private readonly Func<object?, bool> _canStart;
    private readonly Border _outline = new()
    {
        IsHitTestVisible = false,
        Visibility = Visibility.Collapsed,
        BorderThickness = new Thickness(1),
        BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(220, 95, 174, 255)),
        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(38, 95, 174, 255))
    };
    private Point? _start;
    private uint _pointerId;
    private bool _dragging;

    internal SelectionMarquee(UIElement pointerSource, ListViewBase items, Canvas layer,
        Func<bool> enabled, Func<object?, bool> canStart)
    {
        _pointerSource = pointerSource; _items = items; _layer = layer;
        _enabled = enabled; _canStart = canStart;
        _layer.IsHitTestVisible = false;
        _layer.Children.Add(_outline);
        _pointerSource.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(Pressed), true);
        _pointerSource.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(Moved), true);
        _pointerSource.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(Released), true);
        _pointerSource.PointerCanceled += (_, _) => Reset();
        _pointerSource.PointerCaptureLost += (_, _) => Reset();
    }

    private void Pressed(object sender, PointerRoutedEventArgs args)
    {
        if (_start is not null || !_enabled() || !_canStart(args.OriginalSource)
            || !args.GetCurrentPoint(_layer).Properties.IsLeftButtonPressed)
            return;
        var point = args.GetCurrentPoint(_layer).Position;
        if (point.X < 0 || point.Y < 0 || point.X >= _layer.ActualWidth || point.Y >= _layer.ActualHeight)
            return;
        if (!_pointerSource.CapturePointer(args.Pointer)) return;
        _start = point; _pointerId = args.Pointer.PointerId; _dragging = false;
    }

    private void Moved(object sender, PointerRoutedEventArgs args)
    {
        if (_start is not { } origin || _pointerId != args.Pointer.PointerId) return;
        if (!_enabled() || !args.GetCurrentPoint(_layer).Properties.IsLeftButtonPressed)
        { Reset(); return; }
        var location = args.GetCurrentPoint(_layer).Position;
        var point = new Point(Math.Clamp(location.X, 0, _layer.ActualWidth),
            Math.Clamp(location.Y, 0, _layer.ActualHeight));
        if (!_dragging && !SelectionMarqueeGeometry.HasStarted(origin.X, origin.Y, point.X, point.Y))
            return;
        if (!_dragging)
        {
            _dragging = true;
            _items.SelectedItems.Clear();
        }
        var rect = SelectionMarqueeGeometry.Between(origin.X, origin.Y, point.X, point.Y);
        Canvas.SetLeft(_outline, rect.X); Canvas.SetTop(_outline, rect.Y);
        _outline.Width = rect.Width; _outline.Height = rect.Height;
        _outline.Visibility = Visibility.Visible;

        // Only materialized containers can have native pointer geometry.
        // Virtualized offscreen items cannot overlap a visible selection box.
        foreach (var item in _items.Items)
        {
            if (_items.ContainerFromItem(item) is not FrameworkElement container
                || container.Visibility != Visibility.Visible || container.ActualWidth <= 0 || container.ActualHeight <= 0)
                continue;
            try
            {
                var corner = container.TransformToVisual(_layer).TransformPoint(new Point());
                bool selected = SelectionMarqueeGeometry.Intersects(rect,
                    new(corner.X, corner.Y, container.ActualWidth, container.ActualHeight));
                if (selected && !_items.SelectedItems.Contains(item)) _items.SelectedItems.Add(item);
                else if (!selected && _items.SelectedItems.Contains(item)) _items.SelectedItems.Remove(item);
            }
            catch (ArgumentException)
            {
                // A container may be recycled during an asynchronous Files refresh.
            }
        }
        args.Handled = true;
    }

    private void Released(object sender, PointerRoutedEventArgs args)
    {
        if (_start is null || _pointerId != args.Pointer.PointerId) return;
        bool hadSelection = _dragging;
        Reset();
        if (hadSelection) args.Handled = true;
    }

    private void Reset()
    {
        _start = null; _dragging = false;
        _outline.Visibility = Visibility.Collapsed;
        _pointerSource.ReleasePointerCaptures();
    }
}
