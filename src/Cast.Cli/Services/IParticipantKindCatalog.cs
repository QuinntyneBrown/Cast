using System.Collections.Generic;
using Cast.Cli.Models;

namespace Cast.Cli.Services;

/// <summary>
/// The single source of truth for the mapping between <see cref="ParticipantKind"/>
/// values and their PlantUML declaration keywords. Both the participant parser (input)
/// and the renderer (output) depend on this abstraction so the keyword vocabulary lives
/// in exactly one place.
/// </summary>
public interface IParticipantKindCatalog
{
    /// <summary>All supported kinds together with their PlantUML keyword, in display order.</summary>
    IReadOnlyList<(ParticipantKind Kind, string Keyword)> Kinds { get; }

    /// <summary>Returns the PlantUML keyword for a kind (e.g. <see cref="ParticipantKind.Database"/> -&gt; <c>database</c>).</summary>
    string KeywordFor(ParticipantKind kind);

    /// <summary>
    /// Attempts to resolve a keyword (case-insensitively) to its kind.
    /// </summary>
    /// <returns><see langword="true"/> if <paramref name="keyword"/> names a known kind.</returns>
    bool TryResolve(string keyword, out ParticipantKind kind);
}
