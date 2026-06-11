using System.Collections.Generic;
using Cast.Cli.Models;

namespace Cast.Cli.Services;

/// <summary>
/// Validates raw participant and message specs and the free-text diagram metadata, turning the
/// specs into model objects. Centralising these rules keeps the <c>generate</c> command and the
/// template-saving path in exact agreement on what constitutes a well-formed diagram definition —
/// and on the error messages users see when it isn't.
/// </summary>
public interface IDiagramSpecValidator
{
    /// <summary>
    /// Parses every spec in <paramref name="specs"/>, rejecting duplicate aliases. Throws
    /// <see cref="Diagnostics.DiagramFormatException"/> on malformed input.
    /// </summary>
    IReadOnlyList<Participant> ParseParticipants(IReadOnlyList<string> specs);

    /// <summary>
    /// Parses every spec in <paramref name="specs"/>, validating that each endpoint refers to one of
    /// the declared <paramref name="participants"/>. Throws
    /// <see cref="Diagnostics.DiagramFormatException"/> on malformed input or an unknown endpoint.
    /// </summary>
    IReadOnlyList<Message> ParseMessages(IReadOnlyList<string> specs, IReadOnlyList<Participant> participants);

    /// <summary>
    /// Validates the free-text title and theme so they cannot break the line-oriented PlantUML
    /// output. Throws <see cref="Diagnostics.DiagramFormatException"/> on invalid metadata.
    /// </summary>
    void ValidateMetadata(string? title, string? theme);
}
