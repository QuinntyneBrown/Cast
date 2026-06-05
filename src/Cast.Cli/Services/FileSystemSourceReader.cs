using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Cast.Cli.Services;

/// <summary>
/// Default <see cref="ISourceFileReader"/>: reads a UTF-8 source file from disk, translating a
/// missing file or invalid path into <see cref="FileNotFoundException"/> and an access failure
/// into <see cref="IOException"/> so callers can react without catching framework-specific types.
/// </summary>
public sealed class FileSystemSourceReader : ISourceFileReader
{
    /// <inheritdoc />
    public async Task<string> ReadAsync(string path, CancellationToken cancellationToken)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            throw new FileNotFoundException($"'{path}' is not a valid file path.", path, ex);
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Service file '{fullPath}' was not found.", fullPath);
        }

        try
        {
            return await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException($"Access to '{fullPath}' was denied.", ex);
        }
    }
}
