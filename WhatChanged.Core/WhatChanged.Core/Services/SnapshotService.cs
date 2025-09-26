using System.Collections.Concurrent;
using System.IO.Hashing;
using WhatChanged.Core.Models;

namespace WhatChanged.Core.Services;

public class SnapshotService
{
    public static async Task<Dictionary<string, FileSystemEntry>> CreateSnapshotAsync(
        string rootPath,
        IReadOnlyDictionary<string, FileSystemEntry>? baseline = null,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        var entries = new Dictionary<string, FileSystemEntry>(StringComparer.OrdinalIgnoreCase);
        var filesToHash =
            new BlockingCollection<(string relativePath, string fullPath, long size, DateTime lastWriteUtc)>();
        var failedFiles = new ConcurrentBag<string>();
        var hashedEntries = new ConcurrentDictionary<string, FileSystemEntry>(StringComparer.OrdinalIgnoreCase);

        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
        };

        const double timestampToleranceSeconds = 1.0;

        var totalEnumerated = 0;

        var hashingTask = HashFilesAsync(filesToHash, hashedEntries, log, cancellationToken, failedFiles);

        try
        {
            foreach (var entryPath in Directory.EnumerateFileSystemEntries(rootPath, "*", enumerationOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();
                totalEnumerated++;
                string relativePath;
                try
                {
                    relativePath = Path.GetRelativePath(rootPath, entryPath).Replace(Path.DirectorySeparatorChar, '/');
                }
                catch (Exception ex)
                {
                    // If we can't compute relative path, record and continue
                    failedFiles.Add(entryPath);
                    log?.Invoke($"Failed to compute relative path for '{entryPath}': {ex.Message}");
                    continue;
                }

                try
                {
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
                        Math.Abs((baselineEntry.LastWriteTimeUtc - lastWriteUtc).TotalSeconds) <=
                        timestampToleranceSeconds)
                        entries[relativePath] =
                            new FileSystemEntry(EntryType.File, relativePath, baselineEntry.Hash, size, lastWriteUtc);
                    else
                        filesToHash.Add((relativePath, entryPath, size, lastWriteUtc), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failedFiles.Add(entryPath);
                    log?.Invoke($"Failed to inspect '{entryPath}': {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SnapshotException("Failed while enumerating files.", rootPath, totalEnumerated, filesToHash.Count,
                failedFiles.ToArray(), ex);
        }
        finally
        {
            filesToHash.CompleteAdding();
        }

        await hashingTask;

        foreach (var kvp in hashedEntries) entries[kvp.Key] = kvp.Value;

        if (failedFiles.Count > 0)
            log?.Invoke(
                $"Snapshot completed with {failedFiles.Count} failed entries. See FailedFiles in SnapshotException if thrown.");

        return entries;

        static async Task HashFilesAsync(
            BlockingCollection<(string relativePath, string fullPath, long size, DateTime lastWriteUtc)> files,
            ConcurrentDictionary<string, FileSystemEntry> hashedEntries,
            Action<string>? log = null,
            CancellationToken cancellationToken = default,
            ConcurrentBag<string>? failedFiles = null)
        {
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(files.GetConsumingEnumerable(cancellationToken), parallelOptions,
                async (file, token) =>
                {
                    try
                    {
                        var hash = await CalculateXxHash64Async(file.fullPath, token);
                        var entry = new FileSystemEntry(EntryType.File, file.relativePath, hash, file.size,
                            file.lastWriteUtc);
                        hashedEntries[file.relativePath] = entry;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        failedFiles?.Add(file.fullPath);
                        log?.Invoke($"Failed to hash '{file.relativePath}': {ex.Message}");
                    }
                });
        }
    }

    private static async Task<string> CalculateXxHash64Async(string filePath, CancellationToken cancellationToken)
    {
        const int bufferSize = 1024 * 1024; // 1 MB
        try
        {
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize,
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SnapshotException($"Failed to calculate hash for '{filePath}'.", filePath,
                failedFiles: [filePath], innerException: ex);
        }
    }
}