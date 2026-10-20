using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DropSorter;

public partial class MainWindow : Window
{
    const string Prompt = "Drop files here";
    static readonly Brush Idle = new SolidColorBrush(Color.FromArgb(0x55, 255, 255, 255));
    static readonly Brush Hot = new SolidColorBrush(Color.FromRgb(0x6C, 0x9E, 0xFF));
    static readonly Brush HotFill = new SolidColorBrush(Color.FromArgb(0x30, 0x6C, 0x9E, 0xFF));

    readonly ObservableCollection<HistoryEntry> _items;
    readonly DispatcherTimer _reset = new() { Interval = TimeSpan.FromSeconds(4) };
    bool _historyOpen;

    public MainWindow()
    {
        InitializeComponent();
        _items = new ObservableCollection<HistoryEntry>(SorterService.History);
        HistoryList.ItemsSource = _items;
        _items.CollectionChanged += (_, _) => UpdateHistoryView();
        _reset.Tick += (_, _) => { _reset.Stop(); Status.Text = Prompt; };
        Loaded += (_, _) => PlaceWindow();
    }

    // ---- drag & drop ----
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
        Zone.Background = on ? HotFill : Brushes.Transparent;
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

    // ---- moving & snapping ----
    void Root_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed || HistoryList.IsMouseOver) return;
        try { DragMove(); } catch { }
        SnapToEdge();
        SorterService.Config.Left = Left;
        SorterService.Config.Top = Top;
        SorterService.SaveConfig();
    }

    void SnapToEdge()
    {
        var wa = SystemParameters.WorkArea;
        const double snap = 24;
        if (Math.Abs(Left - wa.Left) < snap) Left = wa.Left;
        else if (Math.Abs(Left + ActualWidth - wa.Right) < snap) Left = wa.Right - ActualWidth;
        if (Math.Abs(Top - wa.Top) < snap) Top = wa.Top;
        else if (Math.Abs(Top + ActualHeight - wa.Bottom) < snap) Top = wa.Bottom - ActualHeight;
    }

    void PlaceWindow()
    {
        var c = SorterService.Config;
        bool onScreen = c.Left.HasValue && c.Top.HasValue
            && c.Left > SystemParameters.VirtualScreenLeft - 20
            && c.Left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 60
            && c.Top > SystemParameters.VirtualScreenTop - 20
            && c.Top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 60;
        if (onScreen) { Left = c.Left!.Value; Top = c.Top!.Value; return; }

        var wa = SystemParameters.WorkArea;
        Left = wa.Right - ActualWidth;
        Top = wa.Top + 120;
    }
}
