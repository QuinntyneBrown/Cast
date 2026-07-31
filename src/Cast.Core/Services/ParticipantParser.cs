using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Cast.Core.Diagnostics;
using Cast.Core.Models;

namespace Cast.Core.Services;

/// <summary>
/// Default <see cref="IParticipantParser"/>. Splits the optional kind prefix from the alias and
/// optional display name, then validates that the alias is a usable PlantUML identifier (ASCII,
/// not a reserved keyword) and that the display name contains no characters that would break the
/// rendered output. Kind resolution is delegated to <see cref="IParticipantKindCatalog"/>.
/// </summary>
public sealed partial class ParticipantParser : IParticipantParser
{
    // PlantUML structural keywords that, used bare in a declaration or as a message endpoint,
    // produce invalid output. The check is case-sensitive (PlantUML keywords are lower-case), so
    // capitalised names such as "Note" or "Database" remain valid aliases.
    private static readonly string[] StructuralKeywords =
    {
        "as", "note", "ref", "box", "title", "header", "footer", "legend", "caption",
        "activate", "deactivate", "destroy", "create", "autonumber", "newpage",
        "alt", "else", "opt", "loop", "par", "break", "critical", "group", "also", "end",
        "over", "left", "right", "of", "link", "return", "hide", "show", "skinparam",
    };

    private readonly IParticipantKindCatalog _kinds;
    private readonly HashSet<string> _reservedAliases;

    /// <summary>Initializes the parser with the supported participant-kind catalog.</summary>
    public ParticipantParser(IParticipantKindCatalog kinds)
    {
        _kinds = kinds;

        // Reserved = structural keywords plus the kind keywords (reused from the catalog so the
        // vocabulary is not duplicated here).
        _reservedAliases = new HashSet<string>(StructuralKeywords, StringComparer.Ordinal);
        foreach (var (_, keyword) in kinds.Kinds)
        {
            _reservedAliases.Add(keyword);
        }
    }

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
                "An alias must start with an ASCII letter or underscore and contain only ASCII letters, digits, or underscores. " +
                "Use the optional display name for friendly text, e.g. 'OS:Order Service'.");
        }

        if (_reservedAliases.Contains(alias))
        {
            throw new DiagramFormatException(
                $"'{alias}' is a reserved PlantUML keyword and cannot be used as a participant alias in spec '{spec}'. " +
                "Choose a different alias (capitalising it, e.g. 'End', is enough).");
        }

        if (displayName is not null && HasUnsupportedDisplayChar(displayName))
        {
            throw new DiagramFormatException(
                $"Display name '{displayName}' in spec '{spec}' contains an unsupported character. " +
                "Double quotes and control characters (including line breaks) are not allowed in a display name.");
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

    /// <summary>A display name is embedded in a PlantUML quoted string, so quotes and control
    /// characters (which would close the string early or split the line) are rejected.</summary>
    private static bool HasUnsupportedDisplayChar(string value)
    {
        foreach (char c in value)
        {
            if (c == '"' || char.IsControl(c))
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex IdentifierPattern();
}
