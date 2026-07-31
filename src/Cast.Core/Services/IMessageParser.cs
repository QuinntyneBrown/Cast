using Cast.Core.Models;

namespace Cast.Core.Services;

/// <summary>
/// Parses a single raw message spec into a <see cref="Message"/>.
/// </summary>
/// <remarks>
/// Grammar: <c>SOURCE ARROW TARGET [ : LABEL ]</c>, e.g.
/// <c>User -&gt; OS : place order</c> or <c>OS--&gt;User</c>. Whitespace around the arrow is
/// optional and the label (everything after the first colon) is optional. Parsing is purely
/// syntactic; checking that the endpoints refer to declared participants is the caller's job.
/// </remarks>
public interface IMessageParser
{
    /// <summary>Parses <paramref name="spec"/> or throws <see cref="Diagnostics.DiagramFormatException"/> on malformed input.</summary>
    Message Parse(string spec);
}
