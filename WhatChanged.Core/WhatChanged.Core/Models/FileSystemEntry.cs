namespace WhatChanged.Core.Models;

public enum EntryType
{
    File,
    Directory
}

public record FileSystemEntry(
    EntryType Type,
    string RelativePath,
    string Hash,
    long Size = 0,
    DateTime LastWriteTimeUtc = default
);