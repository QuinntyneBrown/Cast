using System.Threading;
using System.Threading.Tasks;

namespace Cast.Cli.Services;

/// <summary>
/// Writes rendered diagram text to its destination: a file, or standard output when no path
/// is given. This is the single filesystem/console boundary, isolated so the rest of the core
/// stays free of I/O concerns.
/// </summary>
public interface IDiagramWriter
{
    /// <summary>
    /// Writes <paramref name="content"/> to <paramref name="outputPath"/>, or to standard output
    /// when it is <see langword="null"/>.
    /// </summary>
    /// <param name="overwrite">Permit replacing an existing file; ignored for standard output.</param>
    /// <exception cref="System.IO.IOException">The target file exists and <paramref name="overwrite"/> is false, or the write fails.</exception>
    Task WriteAsync(string content, string? outputPath, bool overwrite, CancellationToken cancellationToken);
}
