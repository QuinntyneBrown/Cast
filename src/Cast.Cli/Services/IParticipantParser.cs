using Cast.Cli.Models;

namespace Cast.Cli.Services;

/// <summary>
/// Parses a single raw participant spec into a <see cref="Participant"/>.
/// </summary>
/// <remarks>
/// Grammar: <c>[kind:]alias[:Display Name]</c>
/// <list type="bullet">
///   <item><c>User</c> -&gt; participant aliased <c>User</c></item>
///   <item><c>actor:User</c> -&gt; actor aliased <c>User</c></item>
///   <item><c>database:DB:Main Database</c> -&gt; database aliased <c>DB</c> labelled "Main Database"</item>
/// </list>
/// </remarks>
public interface IParticipantParser
{
    /// <summary>Parses <paramref name="spec"/> or throws <see cref="Diagnostics.DiagramFormatException"/> on malformed input.</summary>
    Participant Parse(string spec);
}
