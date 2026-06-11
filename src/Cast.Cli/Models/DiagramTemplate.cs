using System.Collections.Generic;

namespace Cast.Cli.Models;

/// <summary>
/// A named, persisted diagram definition: the cast of participants a user always diagrams with,
/// plus optional default messages and metadata. Saved as JSON by
/// <see cref="Services.ITemplateStore"/> and rendered through the regular scaffolding pipeline.
/// Unlike the other request records this one uses init-properties rather than a positional
/// parameter list so the JSON round-trip stays stable: a hand-edited file may omit any property
/// except <see cref="Name"/>, and properties added in later versions deserialize from old files
/// without error.
/// </summary>
public sealed record DiagramTemplate
{
    /// <summary>The template's unique name; also the file name it is stored under.</summary>
    public required string Name { get; init; }

    /// <summary>Raw participant specs (e.g. <c>actor:User:End User</c>). At least one is required to save.</summary>
    public IReadOnlyList<string> Participants { get; init; } = [];

    /// <summary>Raw default message specs (e.g. <c>User -&gt; OS : place order</c>). Replaced entirely by render-time messages.</summary>
    public IReadOnlyList<string> Messages { get; init; } = [];

    /// <summary>Optional default diagram title.</summary>
    public string? Title { get; init; }

    /// <summary>Whether to emit <c>autonumber</c> by default.</summary>
    public bool AutoNumber { get; init; }

    /// <summary>Optional default PlantUML theme name.</summary>
    public string? Theme { get; init; }

    /// <summary>Optional default outer participant-box color overriding <see cref="DiagramStyle.DefaultOuterBoxColor"/>.</summary>
    public string? OuterBoxColor { get; init; }

    /// <summary>Optional default inner participant-box color overriding <see cref="DiagramStyle.DefaultInnerBoxColor"/>.</summary>
    public string? InnerBoxColor { get; init; }
}
