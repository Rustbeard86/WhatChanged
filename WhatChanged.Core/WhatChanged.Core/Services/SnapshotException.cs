namespace WhatChanged.Core.Services;

public class SnapshotException(
    string message,
    string rootPath,
    int totalFiles = 0,
    int processedFiles = 0,
    IReadOnlyList<string>? failedFiles = null,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public string RootPath { get; } = rootPath;
    public int TotalFiles { get; } = totalFiles;
    public int ProcessedFiles { get; } = processedFiles;
    public IReadOnlyList<string>? FailedFiles { get; } = failedFiles;
}