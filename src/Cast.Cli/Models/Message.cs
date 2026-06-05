namespace Cast.Cli.Models;

/// <summary>
/// A directed interaction between two lifelines.
/// </summary>
/// <param name="Source">Alias of the sending participant.</param>
/// <param name="Target">Alias of the receiving participant. May equal <paramref name="Source"/> for a self-message.</param>
/// <param name="Arrow">The PlantUML arrow token connecting the two (e.g. <c>-&gt;</c>, <c>--&gt;</c>, <c>-&gt;&gt;</c>).</param>
/// <param name="Label">Optional text describing the interaction. <see langword="null"/> renders an unlabelled arrow.</param>
public sealed record Message(string Source, string Target, string Arrow, string? Label = null);
