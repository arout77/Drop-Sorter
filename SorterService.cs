using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;

namespace DropSorter;

public static class SorterService
{
    static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DropSorter");
    static readonly string ConfigPath = Path.Combine(Dir, "config.json");
    static readonly string HistoryPath = Path.Combine(Dir, "history.json");
    static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
    public const int MaxHistory = 200;

    public static AppConfig Config { get; private set; } = new();
    public static List<HistoryEntry> History { get; private set; } = new();

    public static void Load()
    {
        Config = Read<AppConfig>(ConfigPath) ?? Defaults();
        if (Config.Rules.Count == 0 && !File.Exists(ConfigPath)) Config = Defaults();
        History = Read<List<HistoryEntry>>(HistoryPath) ?? new();
        if (!File.Exists(ConfigPath)) SaveConfig();
    }

    static T? Read<T>(string path) where T : class
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) : null; }
        catch { return null; }
    }

    public static void SaveConfig()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Config, JsonOpts));
    }

    public static void SaveHistory()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(HistoryPath, JsonSerializer.Serialize(History, JsonOpts));
        }
        catch { /* history is best-effort */ }
    }

    static AppConfig Defaults()
    {
        string F(Environment.SpecialFolder f) => Environment.GetFolderPath(f);
        var docs = F(Environment.SpecialFolder.MyDocuments);
        var pics = F(Environment.SpecialFolder.MyPictures);
        var vids = F(Environment.SpecialFolder.MyVideos);
        var music = F(Environment.SpecialFolder.MyMusic);
        var dl = Path.Combine(F(Environment.SpecialFolder.UserProfile), "Downloads");

        return new AppConfig
        {
            Fallback = Path.Combine(docs, "Unsorted"),
            Rules = new()
            {
                new Rule { Name = "Finance", Keywords = "invoice, receipt, statement, payslip", Destination = Path.Combine(docs, "Finance") },
                new Rule { Name = "Screenshots", Extensions = ".png, .jpg, .jpeg", Keywords = "screenshot, screen shot, snip, capture", Destination = Path.Combine(pics, "Screenshots") },
                new Rule { Name = "Images", Extensions = ".png, .jpg, .jpeg, .gif, .webp, .bmp, .svg, .heic", Destination = pics },
                new Rule { Name = "Documents", Extensions = ".pdf, .doc, .docx, .txt, .rtf, .odt, .md", Destination = docs },
                new Rule { Name = "Office files", Extensions = ".xls, .xlsx, .csv, .ppt, .pptx", Destination = Path.Combine(docs, "Office") },
                new Rule { Name = "Video", Extensions = ".mp4, .mov, .mkv, .avi, .webm", Destination = vids },
                new Rule { Name = "Audio", Extensions = ".mp3, .wav, .flac, .m4a", Destination = music },
                new Rule { Name = "Archives", Extensions = ".zip, .rar, .7z, .tar, .gz", Destination = Path.Combine(docs, "Archives") },
                new Rule { Name = "Installers", Extensions = ".exe, .msi", Destination = Path.Combine(dl, "Installers") },
            }
        };
    }

    /// <summary>Sorts one file. Throws with a readable message on failure.</summary>
    public static HistoryEntry Sort(string path)
    {
        if (Directory.Exists(path)) throw new InvalidOperationException("folders aren't supported");
        if (!File.Exists(path)) throw new FileNotFoundException("file not found");

        var rule = Config.Rules.FirstOrDefault(r => r.Matches(path) && !string.IsNullOrWhiteSpace(r.Destination));
        string destDir, ruleName;
        if (rule != null) { destDir = rule.Destination; ruleName = rule.Name; }
        else if (!string.IsNullOrWhiteSpace(Config.Fallback)) { destDir = Config.Fallback; ruleName = "Unmatched"; }
        else throw new InvalidOperationException("no rule matched");

        destDir = Environment.ExpandEnvironmentVariables(destDir);
        var srcDir = Path.GetDirectoryName(Path.GetFullPath(path))!;
        if (string.Equals(srcDir.TrimEnd('\\'), Path.GetFullPath(destDir).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("already in the right folder");

        Directory.CreateDirectory(destDir);
        var dest = UniquePath(Path.Combine(destDir, Path.GetFileName(path)));
        File.Move(path, dest);

        var entry = new HistoryEntry
        {
            Time = DateTime.Now,
            FileName = Path.GetFileName(dest),
            Source = path,
            Destination = dest,
            RuleName = ruleName
        };
        History.Insert(0, entry);
        if (History.Count > MaxHistory) History.RemoveRange(MaxHistory, History.Count - MaxHistory);
        SaveHistory();
        return entry;
    }

    /// <summary>Moves a sorted file back where it came from (renaming if that name is taken).</summary>
    public static string Undo(HistoryEntry e)
    {
        History.Remove(e);
        SaveHistory();
        if (!File.Exists(e.Destination)) throw new FileNotFoundException("file is no longer in the sorted folder");

        Directory.CreateDirectory(Path.GetDirectoryName(e.Source)!);
        var back = UniquePath(e.Source);
        File.Move(e.Destination, back);
        return back;
    }

    static string UniquePath(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (int i = 2; ; i++)
        {
            var p = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(p) && !Directory.Exists(p)) return p;
        }
    }

    // ---- run at startup (per-user, no admin needed) ----
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsRunAtStartup()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey);
        return k?.GetValue("DropSorter") != null;
    }

    public static void SetRunAtStartup(bool on)
    {
        using var k = Registry.CurrentUser.CreateSubKey(RunKey);
        if (on) k.SetValue("DropSorter", $"\"{Environment.ProcessPath}\"");
        else k.DeleteValue("DropSorter", false);
    }
}
