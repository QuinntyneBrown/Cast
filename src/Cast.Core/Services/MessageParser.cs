using System.Text.RegularExpressions;
using Cast.Core.Diagnostics;
using Cast.Core.Models;

namespace Cast.Core.Services;

/// <summary>
/// Default <see cref="IMessageParser"/>. A single regex captures the source identifier, the
/// arrow token, the target identifier, and the optional label. The arrow must begin with a
/// structural glyph (<c>-</c>, <c>&lt;</c>, <c>&gt;</c>, <c>\</c>, <c>/</c>); an <c>o</c>/<c>x</c> head
/// decorator (as in <c>-&gt;o</c> / <c>-&gt;x</c>) is only absorbed into the arrow when whitespace
/// separates it from the target, so a spaceless target keeps its leading <c>o</c>/<c>x</c>
/// (e.g. <c>api-&gt;oauth</c> targets <c>oauth</c>, not <c>auth</c>).
/// </summary>
public sealed partial class MessageParser : IMessageParser
{
    /// <inheritdoc />
    public Message Parse(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
        {
            throw new DiagramFormatException("A message spec cannot be empty. Expected 'Source -> Target : label'.");
        }

        Match match = MessagePattern().Match(spec.Trim());
        if (!match.Success)
        {
            throw new DiagramFormatException(
                $"Could not parse message '{spec}'. Expected 'Source -> Target : label', " +
                "where Source and Target are participant aliases and the arrow is a PlantUML arrow such as '->', '-->' or '->x'.");
        }

        string arrow = match.Groups["arrow"].Value;
        if (!arrow.Contains('-'))
        {
            throw new DiagramFormatException(
                $"Invalid arrow '{arrow}' in message '{spec}'. A sequence arrow must contain at least one '-', e.g. '->' or '-->'.");
        }

        string label = match.Groups["label"].Value.Trim();

        return new Message(
            Source: match.Groups["src"].Value,
            Target: match.Groups["tgt"].Value,
            Arrow: arrow,
            Label: string.IsNullOrEmpty(label) ? null : label);
    }

    // src/tgt are ASCII PlantUML identifiers. The arrow is either a structural run followed by
    // an o/x decorator that MUST be followed by whitespace ((?=\s) guards against swallowing a
    // target's leading o/x), or a plain structural run. label is everything after the first
    // colon, so labels may themselves contain colons.
    [GeneratedRegex(@"^(?<src>[A-Za-z_][A-Za-z0-9_]*)\s*(?<arrow>[-<>\\/]+[ox]+(?=\s)|[-<>\\/]+)\s*(?<tgt>[A-Za-z_][A-Za-z0-9_]*)\s*(?::\s*(?<label>.*))?$")]
    private static partial Regex MessagePattern();
}
