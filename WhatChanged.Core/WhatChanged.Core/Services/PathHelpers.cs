namespace WhatChanged.Core.Services;

internal static class PathHelpers
{
    // Convert manifest-style relative path (forward slashes) into file system relative path
    public static string ToPlatformPath(string manifestRelative)
    {
        return string.IsNullOrEmpty(manifestRelative)
            ? manifestRelative
            : manifestRelative.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
    }

    // Convert to a normalized archive entry path (always forward slashes)
    public static string ToArchiveEntryPath(string manifestRelative)
    {
        return string.IsNullOrEmpty(manifestRelative)
            ? manifestRelative
            : manifestRelative.Replace('\\', '/').TrimStart('/', '\\');
    }
}