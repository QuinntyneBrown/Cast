using System;
using System.Collections.Generic;
using System.Linq;
using Cast.Core.Models;

namespace Cast.Core.Services;

/// <summary>
/// Default <see cref="IParticipantKindCatalog"/>. The keyword for each kind is simply its
/// lower-cased enum name, which matches PlantUML's vocabulary
/// (<c>participant</c>, <c>actor</c>, <c>boundary</c>, <c>control</c>, <c>entity</c>,
/// <c>database</c>, <c>collections</c>, <c>queue</c>).
/// </summary>
public sealed class ParticipantKindCatalog : IParticipantKindCatalog
{
    private readonly IReadOnlyList<(ParticipantKind Kind, string Keyword)> _kinds;
    private readonly IReadOnlyDictionary<ParticipantKind, string> _keywordByKind;
    private readonly IReadOnlyDictionary<string, ParticipantKind> _kindByKeyword;

    /// <summary>Initializes the built-in PlantUML participant-kind catalog.</summary>
    public ParticipantKindCatalog()
    {
        // The lower-casing rule lives in exactly one place; every lookup reads from these tables.
        _kinds = Enum.GetValues<ParticipantKind>()
            .Select(kind => (Kind: kind, Keyword: kind.ToString().ToLowerInvariant()))
            .ToArray();

        _keywordByKind = _kinds.ToDictionary(entry => entry.Kind, entry => entry.Keyword);

        _kindByKeyword = _kinds.ToDictionary(
            entry => entry.Keyword,
            entry => entry.Kind,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public IReadOnlyList<(ParticipantKind Kind, string Keyword)> Kinds => _kinds;

    /// <inheritdoc />
    public string KeywordFor(ParticipantKind kind) =>
        _keywordByKind.TryGetValue(kind, out string? keyword)
            ? keyword
            : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown participant kind.");

    /// <inheritdoc />
    public bool TryResolve(string keyword, out ParticipantKind kind) =>
        _kindByKeyword.TryGetValue(keyword, out kind);
}
