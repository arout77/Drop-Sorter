using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace DropSorter;

public partial class MainWindow : Window
{
    const string Prompt = "Drop files here";
    const double M = 28;    // transparent margin around the visible card (room for shadow + wobble)
    const double Gap = 12;  // distance kept from the screen edge when snapped

    static readonly Brush Idle = new SolidColorBrush(Color.FromArgb(0x55, 255, 255, 255));
    static readonly Brush IdleFill = new SolidColorBrush(Color.FromArgb(0x22, 255, 255, 255));
    static readonly Brush Hot = new SolidColorBrush(Color.FromRgb(0x6C, 0x9E, 0xFF));
    static readonly Brush HotFill = new SolidColorBrush(Color.FromArgb(0x30, 0x6C, 0x9E, 0xFF));

    readonly ObservableCollection<HistoryEntry> _items;
    readonly DispatcherTimer _reset = new() { Interval = TimeSpan.FromSeconds(4) };
    bool _historyOpen;

    // drag + wobble state
    bool _dragging;
    Point _grab;
    double _lastLeft, _lastTop;
    double _ax, _vx, _ay, _vy, _ar, _vr;   // skew X / skew Y / rotation: angle and velocity
    bool _animating;

    public MainWindow()
    {
        InitializeComponent();
        _items = new ObservableCollection<HistoryEntry>(SorterService.History);
        HistoryList.ItemsSource = _items;
        _items.CollectionChanged += (_, _) => UpdateHistoryView();
        _reset.Tick += (_, _) => { _reset.Stop(); Status.Text = Prompt; };
        Loaded += (_, _) => PlaceWindow();
    }

    // ---- file drag & drop ----
    void Root_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop)) Highlight(true);
        SetEffects(e);
    }

    void Root_DragOver(object sender, DragEventArgs e) => SetEffects(e);

    void Root_DragLeave(object sender, DragEventArgs e) => Highlight(false);

    static void SetEffects(DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    void Root_Drop(object sender, DragEventArgs e)
    {
        Highlight(false);
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var paths = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        int ok = 0;
        var errors = new List<string>();
        foreach (var p in paths)
        {
            try
            {
                _items.Insert(0, SorterService.Sort(p));
                ok++;
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(p)}: {ex.Message}");
            }
        }
        while (_items.Count > SorterService.MaxHistory) _items.RemoveAt(_items.Count - 1);

        Status.Text = errors.Count == 0
            ? $"Sorted {ok} file{(ok == 1 ? "" : "s")} ✓"
            : $"Sorted {ok}, {errors.Count} failed\n{errors[0]}";
        _reset.Stop();
        _reset.Start();
    }

    void Highlight(bool on)
    {
        Zone.BorderBrush = on ? Hot : Idle;
        Zone.Background = on ? HotFill : IdleFill;
    }

    // ---- history ----
    void History_Click(object sender, RoutedEventArgs e)
    {
        _historyOpen = !_historyOpen;
        HistoryBtn.Content = _historyOpen ? "History ▴" : "History ▾";
        UpdateHistoryView();
    }

    void UpdateHistoryView()
    {
        HistoryList.Visibility = _historyOpen && _items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Visibility = _historyOpen && _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not HistoryEntry h) return;
        try
        {
            SorterService.Undo(h);
            Status.Text = $"Restored {h.FileName}";
        }
        catch (Exception ex)
        {
            Status.Text = $"Undo failed: {ex.Message}";
        }
        _items.Remove(h);
        _reset.Stop();
        _reset.Start();
    }

    void Settings_Click(object sender, RoutedEventArgs e) => App.OpenSettings();

    void Hide_Click(object sender, RoutedEventArgs e) => Hide();

    void Close_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    // ---- dragging the widget (custom, so we can lift the shadow and wobble) ----
    Point CursorDip(MouseEventArgs e)
    {
        var screen = PointToScreen(e.GetPosition(this));
        var src = PresentationSource.FromVisual(this);
        return src?.CompositionTarget != null ? src.CompositionTarget.TransformFromDevice.Transform(screen) : screen;
    }

    void Root_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed || HistoryList.IsMouseOver) return;
        var cur = CursorDip(e);
        _grab = new Point(cur.X - Left, cur.Y - Top);
        _dragging = true;
        Root.CaptureMouse();
        LiftShadow(true);
        StartWobble();
        e.Handled = true;
    }

    void Root_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        var cur = CursorDip(e);
        Left = cur.X - _grab.X;
        Top = cur.Y - _grab.Y;
    }

    void Root_MouseUp(object sender, MouseButtonEventArgs e) { if (_dragging) EndDrag(); }

    void Root_LostCapture(object sender, MouseEventArgs e) { if (_dragging) EndDrag(); }

    void EndDrag()
    {
        _dragging = false;
        if (Root.IsMouseCaptured) Root.ReleaseMouseCapture();
        LiftShadow(false);
        SnapToEdge();
        SorterService.Config.Left = Left;
        SorterService.Config.Top = Top;
        SorterService.SaveConfig();
    }

    void LiftShadow(bool up)
    {
        var d = TimeSpan.FromMilliseconds(up ? 120 : 240);
        Shadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, new DoubleAnimation(up ? 30 : 10, d));
        Shadow.BeginAnimation(DropShadowEffect.ShadowDepthProperty, new DoubleAnimation(up ? 10 : 2, d));
        Shadow.BeginAnimation(DropShadowEffect.OpacityProperty, new DoubleAnimation(up ? 0.55 : 0.3, d));
    }

    // ---- wobbly-window effect: spring-driven skew/tilt that lags behind the motion ----
    void StartWobble()
    {
        _lastLeft = Left;
        _lastTop = Top;
        if (_animating) return;
        _animating = true;
        CompositionTarget.Rendering += OnFrame;
    }

    static void Spring(ref double angle, ref double vel, double target)
    {
        vel = (vel + (target - angle) * 0.16) * 0.88;
        angle += vel;
    }

    void OnFrame(object? sender, EventArgs e)
    {
        double dx = 0, dy = 0;
        if (_dragging) { dx = Left - _lastLeft; dy = Top - _lastTop; }
        _lastLeft = Left;
        _lastTop = Top;

        // keep the skew small enough that the card stays inside the transparent margin
        double maxX = Math.Min(8, Math.Atan(20 / Math.Max(60, Root.ActualHeight / 2)) * 180 / Math.PI);
        const double maxY = 5, maxR = 3;

        Spring(ref _ax, ref _vx, Math.Clamp(-dx * 0.4, -maxX, maxX));
        Spring(ref _ay, ref _vy, Math.Clamp(-dy * 0.3, -maxY, maxY));
        Spring(ref _ar, ref _vr, Math.Clamp(dx * 0.1, -maxR, maxR));

        Skew.AngleX = _ax;
        Skew.AngleY = _ay;
        Tilt.Angle = _ar;

        if (!_dragging &&
            Math.Abs(_ax) < 0.02 && Math.Abs(_ay) < 0.02 && Math.Abs(_ar) < 0.02 &&
            Math.Abs(_vx) < 0.02 && Math.Abs(_vy) < 0.02 && Math.Abs(_vr) < 0.02)
        {
            _ax = _ay = _ar = _vx = _vy = _vr = 0;
            Skew.AngleX = Skew.AngleY = Tilt.Angle = 0;
            CompositionTarget.Rendering -= OnFrame;
            _animating = false;
        }
    }

    // ---- snapping & placement ----
    void SnapToEdge()
    {
        var wa = SystemParameters.WorkArea;
        const double snap = 28;
        double vl = Left + M, vr = Left + ActualWidth - M;
        double vt = Top + M, vb = Top + ActualHeight - M;

        if (Math.Abs(vl - (wa.Left + Gap)) < snap) Left = wa.Left + Gap - M;
        else if (Math.Abs((wa.Right - Gap) - vr) < snap) Left = wa.Right - Gap - ActualWidth + M;

        if (Math.Abs(vt - (wa.Top + Gap)) < snap) Top = wa.Top + Gap - M;
        else if (Math.Abs((wa.Bottom - Gap) - vb) < snap) Top = wa.Bottom - Gap - ActualHeight + M;
    }

    void PlaceWindow()
    {
        var c = SorterService.Config;
        bool onScreen = c.Left.HasValue && c.Top.HasValue
            && c.Left > SystemParameters.VirtualScreenLeft - 40
            && c.Left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 80
            && c.Top > SystemParameters.VirtualScreenTop - 40
            && c.Top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 80;
        if (onScreen) { Left = c.Left!.Value; Top = c.Top!.Value; return; }

        var wa = SystemParameters.WorkArea;
        Left = wa.Right - Gap - ActualWidth + M;
        Top = wa.Top + 120;
    }
}
