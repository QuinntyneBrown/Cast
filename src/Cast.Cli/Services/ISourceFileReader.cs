using System.Threading;
using System.Threading.Tasks;

namespace Cast.Cli.Services;

/// <summary>
/// Reads the text of a source file to inspect. This is the single read-side filesystem boundary,
/// isolated (like <see cref="IDiagramWriter"/> on the write side) so the rest of the core stays
/// free of I/O concerns and remains unit-testable without touching disk.
/// </summary>
public interface ISourceFileReader
{
    /// <summary>Reads the full contents of <paramref name="path"/> as text.</summary>
    /// <exception cref="System.IO.FileNotFoundException">The file does not exist or the path is invalid.</exception>
    /// <exception cref="System.IO.IOException">The file exists but could not be read.</exception>
    Task<string> ReadAsync(string path, CancellationToken cancellationToken);
}
