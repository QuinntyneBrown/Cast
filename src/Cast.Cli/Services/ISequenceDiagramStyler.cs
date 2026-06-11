using Cast.Cli.Models;

namespace Cast.Cli.Services;

/// <summary>
/// Applies the cast house style to an existing PlantUML sequence diagram: <c>!pragma teoz true</c>,
/// <c>skinparam defaultFontSize 10</c>, and the double nested box around the non-actor lifelines —
/// each only when not already present. The transformation is idempotent: applying it to its own
/// output changes nothing.
/// </summary>
public interface ISequenceDiagramStyler
{
    /// <summary>
    /// Returns <paramref name="content"/> with any missing house-style rules applied, using
    /// <paramref name="style"/> for the box colors.
    /// </summary>
    StylerResult Apply(string content, DiagramStyle style);
}
