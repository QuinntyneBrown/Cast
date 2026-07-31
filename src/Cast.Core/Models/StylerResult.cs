namespace Cast.Core.Models;

/// <summary>
/// The outcome of restyling one PlantUML document: the (possibly rewritten) content plus a flag
/// per styling rule saying whether that rule had to be applied. When every flag is
/// <see langword="false"/> the document already followed the house style (or could not be safely
/// restyled) and <see cref="Content"/> is the unchanged input.
/// </summary>
/// <param name="Content">The document text after styling.</param>
/// <param name="AppliedTeozPragma"><c>!pragma teoz true</c> was inserted.</param>
/// <param name="AppliedFontSize"><c>skinparam defaultFontSize 10</c> was inserted.</param>
/// <param name="AppliedParticipantColors">
/// At least one uncolored lifeline declaration was given the house participant color.
/// </param>
/// <param name="AppliedBoxes">The non-actor lifelines were wrapped in the double nested box.</param>
/// <param name="SkipReason">
/// When the styler refused to touch the document at all (rather than finding it already
/// styled), a user-facing phrase saying why; otherwise <see langword="null"/>.
/// </param>
public sealed record StylerResult(
    string Content,
    bool AppliedTeozPragma,
    bool AppliedFontSize,
    bool AppliedParticipantColors,
    bool AppliedBoxes,
    string? SkipReason = null)
{
    /// <summary>Whether any styling rule changed the document.</summary>
    public bool Changed => AppliedTeozPragma || AppliedFontSize || AppliedParticipantColors || AppliedBoxes;
}
