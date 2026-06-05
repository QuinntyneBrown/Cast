using System;
using System.Collections.Generic;
using System.Linq;
using Cast.Cli.Models;

namespace Cast.Cli.Services;

/// <summary>
/// Default <see cref="IParticipantKindCatalog"/>. The keyword for each kind is simply its
/// lower-cased enum name, which matches PlantUML's vocabulary
/// (<c>participant</c>, <c>actor</c>, <c>boundary</c>, <c>control</c>, <c>entity</c>,
/// <c>database</c>, <c>collections</c>, <c>queue</c>).
/// </summary>
public sealed class ParticipantKindCatalog : IParticipantKindCatalog
{
    private readonly IReadOnlyList<(ParticipantKind Kind, string Keyword)> _kinds;
    private readonly IReadOnlyDictionary<string, ParticipantKind> _byKeyword;

    public ParticipantKindCatalog()
    {
        _kinds = Enum.GetValues<ParticipantKind>()
            .Select(kind => (Kind: kind, Keyword: kind.ToString().ToLowerInvariant()))
            .ToArray();

        _byKeyword = _kinds.ToDictionary(
            entry => entry.Keyword,
            entry => entry.Kind,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public IReadOnlyList<(ParticipantKind Kind, string Keyword)> Kinds => _kinds;

    /// <inheritdoc />
    public string KeywordFor(ParticipantKind kind) => kind.ToString().ToLowerInvariant();

    /// <inheritdoc />
    public bool TryResolve(string keyword, out ParticipantKind kind) =>
        _byKeyword.TryGetValue(keyword, out kind);
}
