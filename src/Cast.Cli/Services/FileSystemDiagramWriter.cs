using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Cast.Cli.Services;

/// <summary>
/// Default <see cref="IDiagramWriter"/>. Writes to a UTF-8 file (creating any missing parent
/// directories) or, when no path is supplied, to the provided standard-output writer. Logs and
/// diagnostics are expected to go to standard error, so standard output carries only the diagram.
/// </summary>
public sealed class FileSystemDiagramWriter : IDiagramWriter
{
    private readonly TextWriter _standardOutput;

    /// <summary>Creates a writer that emits path-less output to <see cref="System.Console.Out"/>.</summary>
    public FileSystemDiagramWriter()
        : this(System.Console.Out)
    {
    }

    /// <summary>Creates a writer that emits path-less output to <paramref name="standardOutput"/> (useful for tests).</summary>
    public FileSystemDiagramWriter(TextWriter standardOutput) => _standardOutput = standardOutput;

    /// <inheritdoc />
    public async Task WriteAsync(string content, string? outputPath, bool overwrite, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            await _standardOutput.WriteAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
            return;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(outputPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            throw new IOException($"'{outputPath}' is not a valid file path.", ex);
        }

        if (File.Exists(fullPath) && !overwrite)
        {
            throw new IOException(
                $"Output file '{fullPath}' already exists. Re-run with --force to overwrite it.");
        }

        // Translate access failures into IOException so the contract holds; cancellation
        // (OperationCanceledException) is intentionally left to propagate.
        try
        {
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(fullPath, content, cancellationToken).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException($"Access to '{fullPath}' was denied.", ex);
        }
    }
}
