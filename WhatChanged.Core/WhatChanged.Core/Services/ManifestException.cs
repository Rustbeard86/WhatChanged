namespace WhatChanged.Core.Services;

public class ManifestException : Exception
{
    public ManifestException(string message, string manifestPath, bool isReadOperation,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ManifestPath = manifestPath;
        IsReadOperation = isReadOperation;
    }

    public ManifestException(string message, string manifestPath, int lineNumber, string? lineContent,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ManifestPath = manifestPath;
        LineNumber = lineNumber;
        LineContent = lineContent;
        IsReadOperation = true;
    }

    public string ManifestPath { get; }
    public int? LineNumber { get; }
    public string? LineContent { get; }
    public bool IsReadOperation { get; }
}