using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using FlyerFlipper.Core.Settings;

namespace FlyerFlipper.UI.Settings;

/// <summary>
/// Restores the main window's saved size, position, and maximized state, then tracks changes so they can be saved.
/// Remembers the <em>normal</em> (restore-down) bounds even while the window is maximized or minimized.
/// </summary>
public sealed class WindowPlacementTracker : IWindowPlacementSource
{
    /// <summary>
    /// How long to let the window manager settle before checking whether it honoured the position we set.
    /// Reading straight away reports a stale position on X11, which settles about 140 ms after the window opens.
    /// The window is hidden for this long per attempt, so it is kept as short as the measurements allow.
    /// </summary>
    private static readonly TimeSpan RestoreSettleDelay = TimeSpan.FromMilliseconds(150);

    /// <summary>
    /// How many times to re-ask before giving up. Restoring competes with the folder reload for the UI thread, so
    /// a tick can land before the window manager has settled and read a stale position; retrying absorbs that.
    /// </summary>
    private const int MaxRestoreAttempts = 5;

    /// <summary>
    /// Runs an action after a delay on the UI thread. Injectable so tests can drive the restore without a real
    /// clock — <see cref="DispatcherTimer"/> does not tick under the headless platform.
    /// </summary>
    private readonly Action<TimeSpan, Action> _schedule;

    private Window? _window;
    private PixelPoint _normalPosition;
    private Size _normalSize;
    private bool _maximized;

    /// <summary>The position handed to the window while restoring, until the origin check below has finished.</summary>
    private PixelPoint? _restoreTarget;

    /// <summary>The window's own opacity, held while the window is hidden for the restore.</summary>
    private double _opacityBeforeRestore = 1;

    public WindowPlacementTracker()
        : this(ScheduleOnDispatcher)
    {
    }

    internal WindowPlacementTracker(Action<TimeSpan, Action> schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        _schedule = schedule;
    }

    public WindowPlacement? Current { get; private set; }

    public event EventHandler? Changed;

    private static void ScheduleOnDispatcher(TimeSpan delay, Action action)
    {
        var timer = new DispatcherTimer { Interval = delay };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            action();
        };

        timer.Start();
    }

    /// <summary>
    /// Applies <paramref name="saved"/> (if any) to <paramref name="window"/> — call before the window is shown —
    /// and starts tracking it.
    /// </summary>
    public void Attach(Window window, WindowPlacement? saved)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (_window is not null)
        {
            throw new InvalidOperationException("A window is already attached.");
        }

        if (saved is not null)
        {
            _restoreTarget = Apply(window, saved);
        }

        _window = window;
        _normalPosition = _restoreTarget ?? window.Position;
        _normalSize = new Size(window.Width, window.Height);
        _maximized = window.WindowState == WindowState.Maximized;

        if (_restoreTarget is not null)
        {
            // A window manager that ignores the pre-show position puts the window up in the wrong place first and
            // only then honours the restore, which reads as a visible jump. Keep it transparent until it is where
            // it belongs. Where the pre-show position is honoured (Windows) the check passes on the first tick, so
            // this costs one settle delay.
            _opacityBeforeRestore = window.Opacity;
            window.Opacity = 0;

            void OnOpened(object? sender, EventArgs e)
            {
                window.Opened -= OnOpened;
                CompleteRestore(window);
            }

            window.Opened += OnOpened;
        }

        window.PositionChanged += (_, _) => Update();
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property == TopLevel.ClientSizeProperty || e.Property == Window.WindowStateProperty)
            {
                Update();
            }
        };

        Update();
    }

    /// <summary>Applies <paramref name="saved"/>, returning the position assigned, or null if the window was centred.</summary>
    private static PixelPoint? Apply(Window window, WindowPlacement saved)
    {
        var width = Math.Max(saved.Width, Math.Max(window.MinWidth, WindowPlacementRules.MinWidth));
        var height = Math.Max(saved.Height, Math.Max(window.MinHeight, WindowPlacementRules.MinHeight));
        var placement = saved with { Width = width, Height = height };

        var screens = window.Screens?.All
            .Select(static s => new ScreenArea(s.WorkingArea.X, s.WorkingArea.Y, s.WorkingArea.Width, s.WorkingArea.Height, s.Scaling))
            .ToList() ?? [];

        PixelPoint? target = null;
        if (screens.Count > 0 && WindowPlacementRules.IsReachable(placement, screens))
        {
            target = new PixelPoint(placement.X, placement.Y);
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Position = target.Value;
        }
        else
        {
            // The saved spot is off every current screen (e.g. a monitor was unplugged): keep the size, center it.
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        window.Width = placement.Width;
        window.Height = placement.Height;
        if (placement.IsMaximized)
        {
            window.WindowState = WindowState.Maximized;
        }

        return target;
    }

    /// <summary>
    /// Finishes restoring the position once the window is actually on screen.
    /// </summary>
    /// <remarks>
    /// Two window-manager behaviours have to be worked around, neither of which shows up on Windows:
    /// <list type="number">
    /// <item>X11 under WSLg <em>discards</em> a position set before the window is mapped — it accepts the value,
    /// then places the window wherever it likes once shown. So the position is applied again here.</item>
    /// <item>It then reports <c>Position</c> back in a different origin than the setter uses (32px short on both
    /// axes), so a final nudge lands the window where the saved coordinates mean. See
    /// <see cref="WindowPlacementRules.OriginCorrection"/>.</item>
    /// </list>
    /// Each step needs the window manager to settle first, hence the staged timer. On Windows the re-apply is a
    /// no-op and the read-back is exact, so no nudge happens.
    /// </remarks>
    private void CompleteRestore(Window window)
    {
        var attempts = 0;
        PixelPoint? assigned = null;

        void Step()
        {
            // Abandon quietly if the window went away, or was maximized/minimized, while we were waiting.
            if (_window is null || _restoreTarget is not { } target || window.WindowState != WindowState.Normal)
            {
                FinishRestore();
                return;
            }

            var reported = window.Position;
            if (reported == target || ++attempts > MaxRestoreAttempts)
            {
                FinishRestore();
                return;
            }

            // First attempt: just ask for the target — a window manager that dropped the pre-show position will
            // honour it now. Later attempts: measure how far the last assignment landed from where it was
            // reported, and aim past the target by that much.
            var next = target;
            if (assigned is { } last
                && WindowPlacementRules.OriginCorrection(target.X, target.Y, last.X, last.Y, reported.X, reported.Y)
                    is { } corrected)
            {
                next = new PixelPoint(corrected.X, corrected.Y);
            }

            assigned = next;
            window.Position = next;
            _schedule(RestoreSettleDelay, Step);
        }

        _schedule(RestoreSettleDelay, Step);
    }

    /// <summary>
    /// Reveals the window and stops pinning the tracked position to the restore target; the window's own position
    /// rules from here. Safe to call more than once, and reached from every exit of the restore.
    /// </summary>
    private void FinishRestore()
    {
        if (_window is not null)
        {
            _window.Opacity = _opacityBeforeRestore;
        }

        _restoreTarget = null;
        Update();
    }

    private void Update()
    {
        if (_window is null)
        {
            return;
        }

        switch (_window.WindowState)
        {
            case WindowState.Normal:
                // While restoring, keep the position we asked for: the window manager may not have applied it yet,
                // and saving the half-settled value back is what used to walk the window off-screen.
                _normalPosition = _restoreTarget ?? _window.Position;
                _normalSize = _window.ClientSize;
                _maximized = false;
                break;
            case WindowState.Maximized:
            case WindowState.FullScreen:
                _maximized = true;
                break;
            // Minimized: keep the last normal bounds and maximized flag — never restore minimized.
        }

        var placement = new WindowPlacement(_normalPosition.X, _normalPosition.Y, _normalSize.Width, _normalSize.Height, _maximized);
        if (placement != Current)
        {
            Current = placement;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
