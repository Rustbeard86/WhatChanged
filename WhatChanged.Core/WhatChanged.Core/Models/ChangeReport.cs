namespace WhatChanged.Core.Models;

public record ChangeReport(
    IReadOnlyList<FileSystemEntry> Added,
    IReadOnlyList<FileSystemEntry> Modified,
    IReadOnlyList<FileSystemEntry> Removed
);