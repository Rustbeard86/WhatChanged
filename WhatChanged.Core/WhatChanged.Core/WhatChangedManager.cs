using WhatChanged.Core.Models;
using WhatChanged.Core.Services;

// ReSharper disable UnusedMember.Global

namespace WhatChanged.Core;

/// <summary>
///     Provides a unified and simplified interface for the WhatChanged.Core library functionality.
///     This is the primary entry point for consumers of the library.
/// </summary>
public class WhatChangedManager
{
    private readonly ArchiveService _archiveService = new();
    private readonly ComparisonService _comparisonService = new();
    private readonly ManifestService _manifestService = new();
    private readonly SnapshotService _snapshotService = new();

    /// <summary>
    ///     Generates a snapshot of the specified directory.
    /// </summary>
    /// <param name="path">The root directory to snapshot.</param>
    /// <param name="baseline">An optional baseline manifest to optimize hashing.</param>
    /// <returns>A new manifest representing the state of the directory.</returns>
    public async Task<Manifest> CreateSnapshotAsync(string path, Manifest? baseline = null)
    {
        var snapshotEntries = await _snapshotService.CreateSnapshotAsync(path, baseline?.Entries);
        return new Manifest
        {
            TimestampUtc = DateTime.UtcNow,
            Entries = new Dictionary<string, FileSystemEntry>(snapshotEntries, StringComparer.OrdinalIgnoreCase)
        };
    }

    /// <summary>
    ///     Compares two manifests and generates a report of the differences.
    /// </summary>
    /// <param name="baseline">The baseline manifest.</param>
    /// <param name="current">The current manifest.</param>
    /// <returns>A report detailing added, modified, and removed files.</returns>
    public ChangeReport CompareSnapshots(Manifest baseline, Manifest current)
    {
        return _comparisonService.GenerateChangeReport(baseline.Entries, current.Entries);
    }

    /// <summary>
    ///     Reads a manifest file from the specified path.
    /// </summary>
    public async Task<Manifest> ReadManifestAsync(string manifestPath)
    {
        return await _manifestService.ReadManifestAsync(manifestPath);
    }

    /// <summary>
    ///     Writes a manifest to the specified path.
    /// </summary>
    public async Task WriteManifestAsync(string manifestPath, Manifest manifest)
    {
        await _manifestService.WriteManifestAsync(manifestPath, manifest);
    }

    /// <summary>
    ///     Creates a compressed .7z update archive based on a change report.
    /// </summary>
    /// <param name="rootDirectory">The root directory of the application being updated.</param>
    /// <param name="report">The change report containing the files to include.</param>
    /// <returns>The path to the created archive.</returns>
    public string CreateUpdateArchive(DirectoryInfo rootDirectory, ChangeReport report)
    {
        return _archiveService.CreateUpdateArchive(rootDirectory, report);
    }
}