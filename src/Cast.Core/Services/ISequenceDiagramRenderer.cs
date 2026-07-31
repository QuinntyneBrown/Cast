using Cast.Core.Models;

namespace Cast.Core.Services;

/// <summary>
/// Renders a validated <see cref="SequenceDiagram"/> into diagram source text. Kept abstract
/// so an alternate target (e.g. Mermaid) could be added without changing any caller.
/// </summary>
public interface ISequenceDiagramRenderer
{
    /// <summary>Renders <paramref name="diagram"/> to its textual representation.</summary>
    string Render(SequenceDiagram diagram);
}
