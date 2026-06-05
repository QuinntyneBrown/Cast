using System.Text.RegularExpressions;
using Cast.Cli.Diagnostics;
using Cast.Cli.Models;

namespace Cast.Cli.Services;

/// <summary>
/// Default <see cref="IMessageParser"/>. A single regex captures the source identifier, the
/// arrow token, the target identifier, and the optional label. The arrow is accepted
/// leniently (any run of PlantUML arrow characters containing a dash) so the full family of
/// sequence arrows — <c>-&gt;</c>, <c>--&gt;</c>, <c>-&gt;&gt;</c>, <c>-&gt;x</c>, <c>\-</c>, … — works without enumeration.
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

    // src/tgt are PlantUML identifiers; arrow is any run of arrow glyphs; label is the
    // remainder after the first colon (so labels may themselves contain colons).
    [GeneratedRegex(@"^(?<src>[A-Za-z_]\w*)\s*(?<arrow>[-<>\\/ox]+)\s*(?<tgt>[A-Za-z_]\w*)\s*(?::\s*(?<label>.*))?$")]
    private static partial Regex MessagePattern();
}
