using Cast.Cli.Models;

namespace Cast.Cli.Services;

/// <summary>
/// Renders an <see cref="AngularService"/> as a narrated PlantUML sequence diagram that explains
/// how Angular's injector supplies the consumer's dependencies. Kept abstract so an alternate
/// target (e.g. Mermaid) could be added without changing any caller.
/// </summary>
public interface IAngularDiagramRenderer
{
    /// <summary>
    /// Renders <paramref name="service"/> to PlantUML source.
    /// </summary>
    /// <param name="service">The parsed consumer and its dependencies.</param>
    /// <param name="title">An explicit title, or <see langword="null"/> to use a generated one.</param>
    string Render(AngularService service, string? title = null);
}
