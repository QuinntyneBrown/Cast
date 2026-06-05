namespace Cast.Cli.Models;

/// <summary>
/// A single sequence-diagram lifeline.
/// </summary>
/// <param name="Alias">
/// The stable identifier referenced by messages (e.g. <c>OS</c>). Always a valid
/// PlantUML identifier: starts with a letter or underscore, contains only word
/// characters, and never includes whitespace.
/// </param>
/// <param name="Kind">The lifeline kind, controlling the PlantUML keyword used.</param>
/// <param name="DisplayName">
/// Optional human-friendly label rendered as <c>"Display Name" as Alias</c>. When
/// <see langword="null"/> the alias is rendered on its own.
/// </param>
public sealed record Participant(string Alias, ParticipantKind Kind, string? DisplayName = null);
