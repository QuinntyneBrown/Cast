using System.Collections.Generic;
using Cast.Core.Models;

namespace Cast.Cli.Models;

/// <summary>
/// The raw, unparsed inputs gathered from the command line. The CLI layer maps parsed
/// options into this DTO and hands it to <see cref="Services.IScaffoldService"/>; no
/// <c>System.CommandLine</c> type leaks past the command boundary, keeping the core
/// services independent of the CLI framework.
/// </summary>
/// <param name="Participants">Raw participant specs (e.g. <c>actor:User:End User</c>). At least one is required.</param>
/// <param name="Messages">Raw message specs (e.g. <c>User -&gt; OS : place order</c>). May be empty.</param>
/// <param name="Title">Optional diagram title.</param>
/// <param name="AutoNumber">Whether to emit <c>autonumber</c>.</param>
/// <param name="Theme">Optional PlantUML theme name.</param>
/// <param name="OutputPath">Destination file path, or <see langword="null"/> to write to standard output.</param>
/// <param name="Force">Overwrite <paramref name="OutputPath"/> if it already exists.</param>
/// <param name="IncludeSampleFlow">
/// When no messages are supplied, generate a placeholder flow connecting the participants
/// so the output is a usable starting point rather than bare declarations.
/// </param>
/// <param name="OuterBoxColor">Optional outer participant-box color overriding <see cref="DiagramStyle.DefaultOuterBoxColor"/>.</param>
/// <param name="InnerBoxColor">Optional inner participant-box color overriding <see cref="DiagramStyle.DefaultInnerBoxColor"/>.</param>
/// <param name="OpenInEditor">
/// Open the written file in an editor after a successful write. Has no effect when
/// <paramref name="OutputPath"/> is <see langword="null"/> (standard output).
/// </param>
public sealed record ScaffoldRequest(
    IReadOnlyList<string> Participants,
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
