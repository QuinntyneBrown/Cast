using Cast.Cli.Models;

namespace Cast.Cli.Services;

/// <summary>
/// Renders a <see cref="CallGraph"/> as a PlantUML sequence diagram — one interaction per method,
/// showing the calls it makes to itself and its collaborators. Kept abstract so an alternate target
/// (e.g. Mermaid) could be added without changing any caller.
/// </summary>
public interface ICallGraphRenderer
{
    /// <summary>Renders <paramref name="graph"/> to PlantUML source.</summary>
    /// <param name="graph">The parsed subject and the methods to diagram (already filtered by the caller).</param>
    /// <param name="title">An explicit title, or <see langword="null"/> to use a generated one.</param>
    /// <param name="style">Box colors and other global styling, or <see langword="null"/> for <see cref="DiagramStyle.Default"/>.</param>
    string Render(CallGraph graph, string? title = null, DiagramStyle? style = null);
}
