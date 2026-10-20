using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace DropSorter;

public class Rule
{
    public string Name { get; set; } = "";
    /// <summary>Comma-separated, e.g. ".png, .jpg"</summary>
    public string Extensions { get; set; } = "";
    /// <summary>Comma-separated words; the file name must contain any one of them.</summary>
    public string Keywords { get; set; } = "";
    public string Destination { get; set; } = "";

    public Rule Clone() => (Rule)MemberwiseClone();

    public bool Matches(string path)
    {
        var exts = Split(Extensions);
        var keys = Split(Keywords);
        if (exts.Count == 0 && keys.Count == 0) return false;

        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        bool extOk = exts.Count == 0 ||
            exts.Exists(x => string.Equals(NormExt(x), ext, StringComparison.OrdinalIgnoreCase));
        bool keyOk = keys.Count == 0 ||
            keys.Exists(k => name.Contains(k, StringComparison.OrdinalIgnoreCase));
        return extOk && keyOk;
    }

    static List<string> Split(string s) => new(s.Split(new[] { ',', ';' },
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    static string NormExt(string e) => e.StartsWith('.') ? e : "." + e;
}

public class AppConfig
{
    public List<Rule> Rules { get; set; } = new();
    public string Fallback { get; set; } = "";
    public double? Left { get; set; }
    public double? Top { get; set; }
}

public class HistoryEntry
{
    public DateTime Time { get; set; }
    public string FileName { get; set; } = "";
    public string Source { get; set; } = "";
    public string Destination { get; set; } = "";
    public string RuleName { get; set; } = "";

    [JsonIgnore]
    public string Detail => $"{RuleName} → {Path.GetDirectoryName(Destination)} · {Time:t}";
}
