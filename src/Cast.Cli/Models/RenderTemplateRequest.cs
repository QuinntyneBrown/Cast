using System.Collections.Generic;

namespace Cast.Cli.Models;

/// <summary>
/// The raw render-time inputs gathered from the <c>template</c> command line. The command maps
/// parsed options into this DTO and hands it to <see cref="Services.ITemplateService"/>; no
/// <c>System.CommandLine</c> type leaks past the command boundary. Render-time values override
/// the stored template: messages replace the stored ones entirely, the nullable fields fall back
/// to the stored values when not supplied, and <paramref name="AutoNumber"/> can only switch
/// numbering on (a command-line flag cannot express "off" when the template says "on").
/// </summary>
/// <param name="Name">Name of the stored template to render.</param>
/// <param name="Messages">Raw message specs replacing the template's stored messages. Empty means "use the stored ones".</param>
/// <param name="Title">Optional title overriding the template's stored title.</param>
/// <param name="AutoNumber">Emit <c>autonumber</c> even when the template doesn't.</param>
/// <param name="Theme">Optional PlantUML theme name overriding the template's stored theme.</param>
/// <param name="OutputPath">Destination file path, or <see langword="null"/> to write to standard output.</param>
/// <param name="Force">Overwrite <paramref name="OutputPath"/> if it already exists.</param>
/// <param name="IncludeSampleFlow">When the merged message list is empty, generate a placeholder flow.</param>
/// <param name="OuterBoxColor">Optional outer participant-box color overriding the template's stored color.</param>
/// <param name="InnerBoxColor">Optional inner participant-box color overriding the template's stored color.</param>
/// <param name="OpenInEditor">
/// Open the written file in an editor after a successful write. Has no effect when
/// <paramref name="OutputPath"/> is <see langword="null"/> (standard output).
/// </param>
public sealed record RenderTemplateRequest(
    string Name,
    IReadOnlyList<string> Messages,
    string? Title,
    bool AutoNumber,
    string? Theme,
    string? OutputPath,
    bool Force,
    bool IncludeSampleFlow,
    string? OuterBoxColor = null,
    string? InnerBoxColor = null,
    bool OpenInEditor = false);
