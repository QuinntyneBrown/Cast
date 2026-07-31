namespace Cast.Core.Models;

/// <summary>
/// The set of lifeline kinds PlantUML can declare in a sequence diagram.
/// Each value maps to a PlantUML declaration keyword (see
/// <see cref="Services.IParticipantKindCatalog"/>).
/// </summary>
public enum ParticipantKind
{
    /// <summary>A plain <c>participant</c> box (the default).</summary>
    Participant,

    /// <summary>An <c>actor</c> stick figure.</summary>
    Actor,

    /// <summary>A <c>boundary</c> element.</summary>
    Boundary,

    /// <summary>A <c>control</c> element.</summary>
    Control,

    /// <summary>An <c>entity</c> element.</summary>
    Entity,

    /// <summary>A <c>database</c> cylinder.</summary>
    Database,

    /// <summary>A <c>collections</c> stack.</summary>
    Collections,

    /// <summary>A <c>queue</c> element.</summary>
    Queue,
}
