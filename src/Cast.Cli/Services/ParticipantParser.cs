using System.Text.RegularExpressions;
using Cast.Cli.Diagnostics;
using Cast.Cli.Models;

namespace Cast.Cli.Services;

/// <summary>
/// Default <see cref="IParticipantParser"/>. Splits the optional kind prefix from the
/// alias and optional display name, validating that the alias is a usable PlantUML
/// identifier. Kind resolution is delegated to <see cref="IParticipantKindCatalog"/>.
/// </summary>
public sealed partial class ParticipantParser : IParticipantParser
{
    private readonly IParticipantKindCatalog _kinds;

    public ParticipantParser(IParticipantKindCatalog kinds) => _kinds = kinds;

    /// <inheritdoc />
    public Participant Parse(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
        {
            throw new DiagramFormatException("A participant spec cannot be empty. Expected '[kind:]alias[:Display Name]'.");
        }

        var (kind, remainder) = SplitKindPrefix(spec.Trim());
        var (alias, displayName) = SplitAliasAndDisplay(remainder);

        if (!IdentifierPattern().IsMatch(alias))
        {
            throw new DiagramFormatException(
                $"Invalid participant alias '{alias}' in spec '{spec}'. " +
                "An alias must start with a letter or underscore and contain only letters, digits, or underscores. " +
                "Use the optional display name for friendly text, e.g. 'OS:Order Service'.");
        }

        return new Participant(alias, kind, displayName);
    }

    /// <summary>
    /// Peels an optional leading <c>kind:</c> prefix. A prefix is only recognised when text
    /// precedes a colon AND that text is a known kind keyword, so a bare alias that happens
    /// to be named like a keyword (e.g. <c>actor</c> with no colon) is treated as an alias.
    /// </summary>
    private (ParticipantKind Kind, string Remainder) SplitKindPrefix(string spec)
    {
        int colon = spec.IndexOf(':');
        if (colon > 0)
        {
            string candidate = spec[..colon].Trim();
            if (_kinds.TryResolve(candidate, out var kind))
            {
                return (kind, spec[(colon + 1)..]);
            }
        }

        return (ParticipantKind.Participant, spec);
    }

    /// <summary>
    /// Splits the remainder into alias and an optional display name on the first colon.
    /// The display name keeps everything after that colon (so it may itself contain colons).
    /// </summary>
    private static (string Alias, string? DisplayName) SplitAliasAndDisplay(string remainder)
    {
        int colon = remainder.IndexOf(':');
        if (colon < 0)
        {
            return (remainder.Trim(), null);
        }

        string alias = remainder[..colon].Trim();
        string display = remainder[(colon + 1)..].Trim();
        return (alias, string.IsNullOrEmpty(display) ? null : display);
    }

    [GeneratedRegex(@"^[A-Za-z_]\w*$")]
    private static partial Regex IdentifierPattern();
}
