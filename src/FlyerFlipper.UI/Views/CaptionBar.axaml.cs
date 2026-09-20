using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FlyerFlipper.UI.Chrome;

namespace FlyerFlipper.UI.Views;

/// <summary>
/// The app-drawn title bar (decision 20): window title, app mark, and the minimize / maximize-restore / close
/// buttons, plus drag-to-move and double-click-to-maximize.
/// </summary>
/// <remarks>
/// Everything here replaces something the OS title bar used to provide, because the window sets
/// <see cref="Window.SystemDecorations"/> to <see cref="SystemDecorations.None"/>. The window is found at attach
/// time rather than injected, so the control stays usable from XAML with no wiring.
/// </remarks>
public partial class CaptionBar : UserControl
{
    private Window? _window;

    public CaptionBar()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _window = TopLevel.GetTopLevel(this) as Window;
        if (_window is null)
        {
            return;
        }

        _window.PropertyChanged += OnWindowPropertyChanged;
        SyncTitle();
        SyncMaximizeGlyph();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_window is not null)
        {
            _window.PropertyChanged -= OnWindowPropertyChanged;
            _window = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.TitleProperty)
        {
            SyncTitle();
        }
        else if (e.Property == Window.WindowStateProperty)
        {
            SyncMaximizeGlyph();
        }
    }

    private void SyncTitle() => TitleText.Text = _window?.Title ?? string.Empty;

    private void SyncMaximizeGlyph()
    {
        var restored = _window?.WindowState is WindowState.Maximized or WindowState.FullScreen;

        MaximizeGlyph.IsVisible = !restored;
        RestoreGlyph.IsVisible = restored;
        ToolTip.SetTip(MaximizeButton, restored ? "Restore Down" : "Maximize");
    }

    /// <summary>Drags the window by its title bar. The caption buttons mark the event handled, so they win.</summary>
    private void OnCaptionPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled || _window is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        // Dragging a maximized window is the OS's "tear off and restore" gesture; leave that to a later slice
        // rather than half-implementing it.
        if (_window.WindowState == WindowState.Normal)
        {
            _window.BeginMoveDrag(e);
        }
    }

    private void OnCaptionDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_window is null || !_window.CanResize)
        {
            return;
        }

        _window.WindowState = WindowChromeRules.ToggleMaximized(_window.WindowState);
        e.Handled = true;
    }

    private void OnMinimize(object? sender, RoutedEventArgs e)
    {
        if (_window is not null)
        {
            _window.WindowState = WindowState.Minimized;
        }
    }

    private void OnToggleMaximize(object? sender, RoutedEventArgs e)
    {
        if (_window is not null && _window.CanResize)
        {
            _window.WindowState = WindowChromeRules.ToggleMaximized(_window.WindowState);
        }
    }

    private void OnClose(object? sender, RoutedEventArgs e) => _window?.Close();
}
