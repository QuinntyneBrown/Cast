using System.Threading;
using System.Threading.Tasks;
using Cast.Cli.Models;

namespace Cast.Cli.Services;

/// <summary>
/// The in-place editing filesystem boundary used by the <c>style</c> command: reads a text file
/// together with its detected encoding, and writes content back in that same encoding (and
/// byte-order mark), so a rewrite never silently transcodes a file. Kept separate from
/// <see cref="ISourceFileReader"/>/<see cref="IDiagramWriter"/>, which serve commands that
/// create <em>new</em> UTF-8 output and have no fidelity obligation to an existing file.
/// </summary>
public interface ITextFileEditor
{
    /// <summary>Reads <paramref name="path"/>, detecting its encoding from the byte-order mark.</summary>
    /// <exception cref="System.IO.FileNotFoundException">The file does not exist or the path is invalid.</exception>
    /// <exception cref="System.IO.IOException">The file exists but could not be read.</exception>
    Task<TextFile> ReadAsync(string path, CancellationToken cancellationToken);

    /// <summary>Writes <paramref name="file"/>'s content to its path in its recorded encoding.</summary>
    /// <exception cref="System.IO.IOException">The write fails.</exception>
    Task WriteAsync(TextFile file, CancellationToken cancellationToken);
}
