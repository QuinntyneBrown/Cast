using System.Collections.Generic;

namespace Cast.Cli.Services;

/// <summary>
/// Resolves the <c>style</c> command's path argument into the list of PlantUML files to process:
/// a file path yields just that file, a directory path yields every <c>.puml</c> file beneath it
/// (recursively). This is the discovery-side filesystem boundary, isolated (like
/// <see cref="ISourceFileReader"/> and <see cref="IDiagramWriter"/>) so the orchestrator stays
/// free of I/O concerns.
/// </summary>
public interface IPumlFileLocator
{
    /// <summary>Returns the PlantUML files designated by <paramref name="path"/>, in a deterministic order.</summary>
    /// <exception cref="Cast.Core.Diagnostics.DiagramFormatException"><paramref name="path"/> does not exist or is invalid.</exception>
    IReadOnlyList<string> Locate(string path);
}
