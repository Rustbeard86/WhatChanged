using System.Diagnostics;
using System.Text;
using WhatChanged.Core.Models;

namespace WhatChanged.Core.Services;

public class ArchiveService
{
    private const string ParamName = "rootDirectory";

    public static string CreateUpdateArchive(DirectoryInfo rootDirectory, ChangeReport report)
    {
        var (sevenZipPath, triedPaths) = TryFind7Z();

        if (string.IsNullOrEmpty(sevenZipPath))
            throw new ArchiveException(
                "7-Zip executable (7za.exe or 7z.exe) not found. Please ensure it is in the application's directory or in your system's PATH.",
                triedPaths);

        return Create7ZArchive(sevenZipPath, rootDirectory, report);
    }

    private static (string? path, IReadOnlyList<string> triedPaths) TryFind7Z()
    {
        var candidates = new List<string>();

        try
        {
            var baseDir = AppContext.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDir))
            {
                candidates.Add(Path.Combine(baseDir, "7za.exe"));
                candidates.Add(Path.Combine(baseDir, "7z.exe"));
            }
        }
        catch
        {
            /* ignored */
        }

        try
        {
            var cwd = Directory.GetCurrentDirectory();
            candidates.Add(Path.Combine(cwd, "7za.exe"));
            candidates.Add(Path.Combine(cwd, "7z.exe"));
        }
        catch
        {
            /* ignored */
        }

        try
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var part in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                candidates.Add(Path.Combine(part, "7za.exe"));
                candidates.Add(Path.Combine(part, "7z.exe"));
            }
        }
        catch
        {
            /* ignored */
        }

        var tried = new List<string>();

        foreach (var c in candidates.Where(p => !string.IsNullOrEmpty(p)))
        {
            tried.Add(c);
            try
            {
                if (File.Exists(c))
                    return (c, tried);
            }
            catch
            {
                /* ignored */
            }
        }

        return (null, tried);
    }

    private static string Create7ZArchive(string sevenZipPath, DirectoryInfo rootDirectory, ChangeReport report)
    {
        var archiveName = $"{rootDirectory.Name}.7z";
        var archivePath = Path.Combine(rootDirectory.Parent!.FullName, archiveName);

        try
        {
            if (File.Exists(archivePath)) File.Delete(archivePath);
        }
        catch (Exception ex)
        {
            throw new ArchiveException($"Failed to delete existing archive at '{archivePath}'.", archivePath,
                string.Empty, innerException: ex);
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "WhatChanged_" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(tempDir);
        }
        catch (Exception ex)
        {
            throw new ArchiveException($"Failed to create temporary directory '{tempDir}'.", archivePath, tempDir,
                innerException: ex);
        }

        try
        {
            var itemsToArchive = report.Added.Concat(report.Modified).ToList();

            foreach (var item in itemsToArchive)
            {
                var relativeForFileSystem = PathHelpers.ToPlatformPath(item.RelativePath);
                var sourcePath = Path.Combine(rootDirectory.FullName, relativeForFileSystem);
                var destPath = Path.Combine(tempDir, relativeForFileSystem);

                try
                {
                    switch (item.Type)
                    {
                        case EntryType.File:
                            if (!File.Exists(sourcePath)) continue;
                            Directory.CreateDirectory(Path.GetDirectoryName(destPath) ?? tempDir);
                            File.Copy(sourcePath, destPath, true);
                            break;

                        case EntryType.Directory:
                            if (!Directory.Exists(sourcePath)) continue;
                            foreach (var file in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
                            {
                                var rel = Path.GetRelativePath(rootDirectory.FullName, file)
                                    .Replace(Path.DirectorySeparatorChar, '/');
                                var relFs = PathHelpers.ToPlatformPath(rel);
                                var target = Path.Combine(tempDir, relFs);
                                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? tempDir);
                                File.Copy(file, target, true);
                            }

                            if (!Directory.EnumerateFileSystemEntries(sourcePath).Any())
                            {
                                var placeholderDir = Path.Combine(tempDir, relativeForFileSystem);
                                Directory.CreateDirectory(placeholderDir);
                                File.WriteAllText(Path.Combine(placeholderDir, ".empty_dir"), string.Empty);
                            }

                            break;
                        default:
                            throw new ArgumentOutOfRangeException(ParamName);
                    }
                }
                catch (Exception ex)
                {
                    throw new ArchiveException($"Failed to copy item '{item.RelativePath}' to temporary folder.",
                        archivePath, tempDir, sevenZipPath, ex);
                }
            }

            if (report.Removed.Any())
                try
                {
                    File.WriteAllText(Path.Combine(tempDir, "README_INSTRUCTIONS.txt"), GetInstructionsContent());
                    File.WriteAllText(Path.Combine(tempDir, "remove_deleted_files.ps1"),
                        GetPowerShellScriptContent(report.Removed));
                    File.WriteAllText(Path.Combine(tempDir, "remove_deleted_files.bat"),
                        GetBatchScriptContent(report.Removed));
                }
                catch (Exception ex)
                {
                    throw new ArchiveException("Failed to write cleanup scripts to temporary folder.", archivePath,
                        tempDir, sevenZipPath, ex);
                }

            var psi = new ProcessStartInfo
            {
                FileName = sevenZipPath,
                Arguments = $"a -t7z -mx=9 -m0=LZMA2 -ms=on \"{archivePath}\" *",
                WorkingDirectory = tempDir,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc is null)
                throw new ArchiveException("Failed to start 7-Zip process.", archivePath, tempDir, sevenZipPath);

            proc.WaitForExit();

            var outStr = string.Empty;
            var errStr = string.Empty;
            try
            {
                outStr = proc.StandardOutput.ReadToEnd();
                errStr = proc.StandardError.ReadToEnd();
            }
            catch
            {
                // ignore read failures but keep whatever we have
            }

            if (proc.ExitCode != 0)
            {
                Console.Error.WriteLine($"7za failed with code {proc.ExitCode}\n{outStr}\n{errStr}");
                throw new ArchiveException("7za failed to create archive. See error output.", archivePath, tempDir,
                    proc.ExitCode, outStr, errStr, sevenZipPath);
            }

            return archivePath;
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, true);
            }
            catch
            {
                /* Ignore cleanup failures */
            }
        }
    }

    private static string GetInstructionsContent()
    {
        return """
               ================================
               Update Instructions
               ================================

               To apply this update, please follow these steps:

               1.  EXTRACT:
                   Extract all contents of this archive into your main application folder. 
                   You should overwrite files if prompted.

               2.  CLEAN UP (IMPORTANT):
                   This archive contains scripts named 'remove_deleted_files' to clean up obsolete files.
                   After extracting, please run ONE of them to complete the update.

                   -   For modern Windows (10/11): Right-click on 'remove_deleted_files.ps1' and select "Run with PowerShell".
                   -   For any Windows version: Double-click 'remove_deleted_files.bat'.
               """;
    }

    private static string GetPowerShellScriptContent(IEnumerable<FileSystemEntry> removed)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Auto-generated by WhatChanged to remove obsolete files.");
        sb.AppendLine("Write-Host 'Cleaning up obsolete files...' -ForegroundColor Yellow");
        foreach (var item in removed)
        {
            var pathForScript = item.RelativePath.Replace('/', '\\');
            var command = item.Type == EntryType.Directory
                ? "Remove-Item -Path .\\{0} -Recurse -Force"
                : "Remove-Item -Path .\\{0} -Force";
            sb.AppendLine(string.Format(command, pathForScript));
        }

        sb.AppendLine("Write-Host 'Cleanup complete.' -ForegroundColor Green");
        return sb.ToString();
    }

    private static string GetBatchScriptContent(IEnumerable<FileSystemEntry> removed)
    {
        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("echo Cleaning up obsolete files...");
        foreach (var item in removed)
        {
            var pathForScript = item.RelativePath.Replace('/', '\\');
            var command = item.Type == EntryType.Directory ? "rd /s /q \".\\{0}\"" : "del /f /q \".\\{0}\"";
            sb.AppendLine(string.Format(command, pathForScript));
        }

        sb.AppendLine("echo Cleanup complete.");
        return sb.ToString();
    }
}