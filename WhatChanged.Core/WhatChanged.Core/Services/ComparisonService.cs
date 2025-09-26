using WhatChanged.Core.Models;

namespace WhatChanged.Core.Services;

public class ComparisonService
{
    public ChangeReport GenerateChangeReport(
        IReadOnlyDictionary<string, FileSystemEntry> baseline,
        IReadOnlyDictionary<string, FileSystemEntry> current)
    {
        var added = current.Values.Where(c => !baseline.ContainsKey(c.RelativePath)).ToList();
        var removed = baseline.Values.Where(b => !current.ContainsKey(b.RelativePath)).ToList();
        var modified = new List<FileSystemEntry>();

        foreach (var (path, baselineEntry) in baseline)
            if (current.TryGetValue(path, out var currentEntry) && baselineEntry.Hash != currentEntry.Hash)
                modified.Add(currentEntry);

        return new ChangeReport(added, modified, removed);
    }
}