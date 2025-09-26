using WhatChanged.Core.Models;

namespace WhatChanged.Core.Services;

public class ComparisonService
{
    public static ChangeReport GenerateChangeReport(
        IReadOnlyDictionary<string, FileSystemEntry> baseline,
        IReadOnlyDictionary<string, FileSystemEntry> current)
    {
        try
        {
            var added = current.Values.Where(c => !baseline.ContainsKey(c.RelativePath)).ToList();
            var removed = baseline.Values.Where(b => !current.ContainsKey(b.RelativePath)).ToList();
            var modified = new List<FileSystemEntry>();

            foreach (var (path, baselineEntry) in baseline)
                try
                {
                    if (current.TryGetValue(path, out var currentEntry) && baselineEntry.Hash != currentEntry.Hash)
                        modified.Add(currentEntry);
                }
                catch (Exception ex)
                {
                    // Wrap and throw with context about where the failure occurred
                    throw new ComparisonException("Error while comparing entries.", baseline.Count, current.Count, path,
                        ex);
                }

            return new ChangeReport(added, modified, removed);
        }
        catch (ComparisonException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ComparisonException("Unexpected error while generating change report.", baseline.Count,
                current.Count, null, ex);
        }
    }
}