using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace DropSorter;

public partial class SettingsWindow : Window
{
    readonly ObservableCollection<Rule> _rules;

    public SettingsWindow()
    {
        InitializeComponent();
        _rules = new ObservableCollection<Rule>(SorterService.Config.Rules.Select(r => r.Clone()));
        RulesGrid.ItemsSource = _rules;
        FallbackBox.Text = SorterService.Config.Fallback;
        StartupBox.IsChecked = SorterService.IsRunAtStartup();
    }

    void Commit() => RulesGrid.CommitEdit(DataGridEditingUnit.Row, true);

    void Add_Click(object sender, RoutedEventArgs e)
    {
        Commit();
        var r = new Rule { Name = "New rule" };
        _rules.Add(r);
        RulesGrid.SelectedItem = r;
        RulesGrid.ScrollIntoView(r);
    }

    void Remove_Click(object sender, RoutedEventArgs e)
    {
        Commit();
        if (RulesGrid.SelectedItem is Rule r) _rules.Remove(r);
    }

    void Up_Click(object sender, RoutedEventArgs e) => Move(-1);
    void Down_Click(object sender, RoutedEventArgs e) => Move(1);

    void Move(int delta)
    {
        Commit();
        if (RulesGrid.SelectedItem is not Rule r) return;
        int i = _rules.IndexOf(r), j = i + delta;
        if (j < 0 || j >= _rules.Count) return;
        _rules.Move(i, j);
        RulesGrid.SelectedItem = r;
    }

    string? PickFolder()
    {
        var dlg = new OpenFolderDialog { Title = "Choose a folder" };
        return dlg.ShowDialog(this) == true ? dlg.FolderName : null;
    }

    void BrowseRule_Click(object sender, RoutedEventArgs e)
    {
        Commit();
        if (RulesGrid.SelectedItem is not Rule r)
        {
            MessageBox.Show(this, "Select a rule first.", "DropSorter");
            return;
        }
        var folder = PickFolder();
        if (folder == null) return;
        r.Destination = folder;
        RulesGrid.Items.Refresh();
    }

    void BrowseFallback_Click(object sender, RoutedEventArgs e)
    {
        var folder = PickFolder();
        if (folder != null) FallbackBox.Text = folder;
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        Commit();
        SorterService.Config.Rules = _rules.ToList();
        SorterService.Config.Fallback = FallbackBox.Text.Trim();
        SorterService.SaveConfig();
        try { SorterService.SetRunAtStartup(StartupBox.IsChecked == true); } catch { }
        DialogResult = true;
    }
}
