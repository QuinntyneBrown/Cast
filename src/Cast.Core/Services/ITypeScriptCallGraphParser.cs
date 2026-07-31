using Cast.Core.Models;

namespace Cast.Core.Services;

/// <summary>
/// Extracts a <see cref="CallGraph"/> from TypeScript source: the primary class (or, when the file
/// has none, its exported functions) and the outbound calls each member makes. Kept abstract so the
/// scanner can be swapped without touching the renderer or orchestrator.
/// </summary>
public interface ITypeScriptCallGraphParser
{
    /// <summary>Parses <paramref name="source"/> into a call graph.</summary>
    /// <param name="source">The full TypeScript source text.</param>
    /// <param name="fileName">Optional file name, used for diagnostics and to name a module subject.</param>
    /// <exception cref="Diagnostics.DiagramFormatException">No class or callable function could be found.</exception>
    CallGraph Parse(string source, string? fileName = null);
}
