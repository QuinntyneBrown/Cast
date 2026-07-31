using System.Collections.Generic;

namespace Cast.Core.Models;

/// <summary>
/// The fully-resolved, render-ready model of a sequence diagram. This is the single
/// hand-off type between the parsing stage and the rendering stage: by the time a
/// <see cref="SequenceDiagram"/> exists, every value has been validated.
/// </summary>
/// <param name="Participants">Declared lifelines, in declaration order.</param>
/// <param name="Messages">Interactions, in chronological order.</param>
/// <param name="Title">Optional diagram title.</param>
/// <param name="AutoNumber">When <see langword="true"/>, emits <c>autonumber</c>.</param>
/// <param name="Theme">Optional PlantUML theme name emitted as <c>!theme &lt;name&gt;</c>.</param>
/// <param name="Style">Box colors and other global styling; <see langword="null"/> means <see cref="DiagramStyle.Default"/>.</param>
public sealed record SequenceDiagram(
    IReadOnlyList<Participant> Participants,
    IReadOnlyList<Message> Messages,
    string? Title = null,
    bool AutoNumber = false,
    string? Theme = null,
    DiagramStyle? Style = null);
