using System.Collections.Concurrent;
using System.IO.Hashing;
using WhatChanged.Core.Models;

namespace WhatChanged.Core.Services;

public class SnapshotService
{
    public async Task<Dictionary<string, FileSystemEntry>> CreateAsync(
        string rootPath,
        IReadOnlyDictionary<string, FileSystemEntry>? baseline = null,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        var entries = new Dictionary<string, FileSystemEntry>(StringComparer.OrdinalIgnoreCase);
        var filesToHash = new List<(string relativePath, string fullPath, long size, DateTime lastWriteUtc)>();
            
        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
        };

        const double timestampToleranceSeconds = 1.0;

        foreach (var entryPath in Directory.EnumerateFileSystemEntries(rootPath, "*", enumerationOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(rootPath, entryPath).Replace(Path.DirectorySeparatorChar, '/');

            if (File.GetAttributes(entryPath).HasFlag(FileAttributes.Directory))
            {
                entries[relativePath] = new FileSystemEntry(EntryType.Directory, relativePath, "DIRECTORY");
                continue;
            }

            var fi = new FileInfo(entryPath);
            var size = fi.Length;
            var lastWriteUtc = fi.LastWriteTimeUtc;

            if (baseline is not null && baseline.TryGetValue(relativePath, out var baselineEntry) &&
                baselineEntry.Size == size &&
                Math.Abs((baselineEntry.LastWriteTimeUtc - lastWriteUtc).TotalSeconds) <= timestampToleranceSeconds)
                entries[relativePath] =
                    new FileSystemEntry(EntryType.File, relativePath, baselineEntry.Hash, size, lastWriteUtc);
            else
                filesToHash.Add((relativePath, entryPath, size, lastWriteUtc));
        }

        if (filesToHash.Any())
        {
            var hashedEntries = await HashFilesAsync(filesToHash, log, cancellationToken);
            foreach (var kvp in hashedEntries) entries[kvp.Key] = kvp.Value;
        }

        return entries;
    }

    private async Task<ConcurrentDictionary<string, FileSystemEntry>> HashFilesAsync(
        List<(string relativePath, string fullPath, long size, DateTime lastWriteUtc)> files,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        var hashedEntries = new ConcurrentDictionary<string, FileSystemEntry>(StringComparer.OrdinalIgnoreCase);
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(files, parallelOptions, async (file, token) =>
        {
            try
            {
                var hash = await CalculateXxHash64Async(file.fullPath, token);
                var entry = new FileSystemEntry(EntryType.File, file.relativePath, hash, file.size, file.lastWriteUtc);
                hashedEntries[file.relativePath] = entry;
            }
            catch (Exception ex)
            {
                log?.Invoke($"Failed to hash '{file.relativePath}': {ex.Message}");
            }
        });

        return hashedEntries;
    }

    private static async Task<string> CalculateXxHash64Async(string filePath, CancellationToken cancellationToken)
    {
        const int bufferSize = 1024 * 1024; // 1 MB
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize,
            FileOptions.Asynchronous);

        var hasher = new XxHash64();
        var buffer = new byte[bufferSize];
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            hasher.Append(new ReadOnlySpan<byte>(buffer, 0, bytesRead));

        var hashBytes = new byte[8];
        return hasher.TryGetCurrentHash(hashBytes, out var written)
            ? Convert.ToHexString(hashBytes, 0, written)
            : throw new InvalidOperationException("Failed to get XXHash64 result.");
    }
}