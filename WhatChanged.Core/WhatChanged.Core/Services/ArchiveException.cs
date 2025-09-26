namespace WhatChanged.Core.Services;

public class ArchiveException : Exception
{
    public ArchiveException(string message, Exception? innerException = null) : base(message, innerException)
    {
    }

    public ArchiveException(string message, IReadOnlyList<string>? triedPaths, Exception? innerException = null)
        : base(message, innerException)
    {
        TriedPaths = triedPaths;
    }

    public ArchiveException(string message, string archivePath, string tempDirectory, string? sevenZipPath = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ArchivePath = archivePath;
        TempDirectory = tempDirectory;
        SevenZipPath = sevenZipPath;
    }

    public ArchiveException(string message, string archivePath, string tempDirectory, int exitCode, string? stdOut,
        string? stdErr, string? sevenZipPath = null)
        : base(message)
    {
        ArchivePath = archivePath;
        TempDirectory = tempDirectory;
        ExitCode = exitCode;
        StdOut = stdOut;
        StdErr = stdErr;
        SevenZipPath = sevenZipPath;
    }

    public string? ArchivePath { get; }
    public string? TempDirectory { get; }
    public string? SevenZipPath { get; }
    public int? ExitCode { get; }
    public string? StdOut { get; }
    public string? StdErr { get; }
    public IReadOnlyList<string>? TriedPaths { get; }
}