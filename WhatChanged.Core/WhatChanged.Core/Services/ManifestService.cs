using System.Globalization;
using System.Text;
using WhatChanged.Core.Models;

namespace WhatChanged.Core.Services;

public class ManifestService
{
    public async Task<Manifest> ReadManifestAsync(string manifestPath)
    {
        var manifest = new Manifest();
        if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
            return manifest;

        await foreach (var line in File.ReadLinesAsync(manifestPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.StartsWith('#'))
            {
                var parts = line.TrimStart('#').Trim().Split(':', 2);
                if (parts.Length != 2) continue;

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                if (key.Equals("ManifestVersion", StringComparison.OrdinalIgnoreCase) &&
                    Version.TryParse(value, out var version))
                    manifest.Version = version;
                else if (key.Equals("Timestamp (UTC)", StringComparison.OrdinalIgnoreCase) && DateTime.TryParse(value,
                             null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var ts))
                    manifest.TimestampUtc = ts;
                else if (key.Equals("Hash Algorithm", StringComparison.OrdinalIgnoreCase))
                    manifest.HashAlgorithm = value;
            }
            else
            {
                // Parse FileSystemEntry
                var partsLine = line.Split(':', 2);
                if (partsLine.Length != 2) continue;

                var entryDetails = partsLine[0].Trim().Split(' ', 2);
                if (entryDetails.Length != 2) continue;

                var typeStr = entryDetails[0];
                var path = entryDetails[1].Trim().Replace('\\', '/');
                if (path.StartsWith("./")) path = path[2..];
                path = path.TrimStart('/', '\\');

                var right = partsLine[1].Trim();
                var metaParts = right.Split('|', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();
                var hash = metaParts.Length > 0 ? metaParts[0] : string.Empty;
                long.TryParse(metaParts.Length > 1 ? metaParts[1] : "0", out var size);
                DateTime.TryParse(metaParts.Length > 2 ? metaParts[2] : "", null,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var lastWriteUtc);

                if (Enum.TryParse<EntryType>(typeStr, true, out var type))
                    manifest.Entries[path] = new FileSystemEntry(type, path, hash, size, lastWriteUtc);
            }
        }

        return manifest;
    }

    public async Task WriteManifestAsync(string manifestPath, Manifest manifest)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# WhatChanged Baseline Manifest");
        sb.AppendLine($"# ManifestVersion: {manifest.Version}");
        sb.AppendLine($"# Timestamp (UTC): {manifest.TimestampUtc:O}");
        sb.AppendLine($"# Hash Algorithm: {manifest.HashAlgorithm}");
        sb.AppendLine("# -----------------------------------");

        foreach (var entry in manifest.Entries.Values.OrderBy(e => e.RelativePath))
        {
            var normalized = entry.RelativePath.Replace(Path.DirectorySeparatorChar, '/');
            if (entry.Type == EntryType.File)
                sb.AppendLine(
                    $"{entry.Type.ToString().ToUpperInvariant()} {normalized} : {entry.Hash} | {entry.Size} | {entry.LastWriteTimeUtc:O}");
            else
                sb.AppendLine($"{entry.Type.ToString().ToUpperInvariant()} {normalized} : {entry.Hash}");
        }

        await File.WriteAllTextAsync(manifestPath, sb.ToString());
    }
}