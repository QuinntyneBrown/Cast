using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cast.Cli.Diagnostics;

namespace Cast.Cli.Services;

/// <summary>
/// Default <see cref="IPumlFileLocator"/>. An explicit file path is accepted as-is (whatever its
/// extension — the sequence-diagram detector guards what actually gets restyled); a directory is
/// searched recursively for <c>*.puml</c> files, skipping subdirectories the process may not
/// enter so one protected folder never aborts a whole scan. Results are sorted ordinally so runs
/// are deterministic across filesystems.
/// </summary>
public sealed class FileSystemPumlFileLocator : IPumlFileLocator
{
    /// <inheritdoc />
    public IReadOnlyList<string> Locate(string path)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            throw new DiagramFormatException($"'{path}' is not a valid path.", ex);
        }

        if (File.Exists(fullPath))
        {
            return [fullPath];
        }

        if (Directory.Exists(fullPath))
        {
            try
            {
                return Directory
                    .EnumerateFiles(fullPath, "*.puml", new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true,
                        MatchType = MatchType.Win32,
                        AttributesToSkip = 0, // include hidden/system files, like the classic overload
                    })
                    .Order(StringComparer.Ordinal)
                    .ToArray();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                throw new DiagramFormatException($"Failed to scan '{fullPath}': {ex.Message}", ex);
            }
        }

        throw new DiagramFormatException(
            $"Path '{fullPath}' was not found. Pass a .puml file or a folder containing .puml files.");
    }
}
