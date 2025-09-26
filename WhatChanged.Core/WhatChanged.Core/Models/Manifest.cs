namespace WhatChanged.Core.Models;

public class Manifest
{
    public Version Version { get; set; } = new(1, 0, 0);
    public DateTime TimestampUtc { get; set; }
    public string HashAlgorithm { get; set; } = "XxHash64";
    public Dictionary<string, FileSystemEntry> Entries { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}