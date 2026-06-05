using System.Collections.Generic;
using Cast.Cli.Models;

namespace Cast.Cli.Services;

/// <summary>
/// Produces placeholder messages for a set of participants. Used when the caller supplies
/// participants but no messages, so the scaffold is an editable starting point rather than a
/// bare list of lifelines.
/// </summary>
public interface ISampleFlowGenerator
{
    /// <summary>
    /// Generates a placeholder flow across <paramref name="participants"/> (in order). Returns
    /// an empty list when there is nothing to connect.
    /// </summary>
    IReadOnlyList<Message> Generate(IReadOnlyList<Participant> participants);
}
