using System.Collections.Generic;

namespace Cast.Cli.Models;

/// <summary>
/// The raw inputs gathered from the <c>calls</c> command line. The command maps parsed options into
/// this DTO and hands it to <see cref="Services.ICallGraphService"/>; no <c>System.CommandLine</c>
/// type leaks past the command boundary.
/// </summary>
/// <param name="SourcePath">Path to the TypeScript <c>.ts</c> source file to inspect.</param>
/// <param name="Title">Optional diagram title overriding the generated default.</param>
/// <param name="OutputPath">Destination file path, or <see langword="null"/> to write to standard output.</param>
/// <param name="Force">Overwrite <paramref name="OutputPath"/> if it already exists.</param>
/// <param name="Methods">
/// When non-empty, only these members (matched case-insensitively by name) are diagrammed, and they
/// are shown regardless of visibility; a name that matches nothing is an error.
/// </param>
/// <param name="IncludePrivate">Include non-public members too (default: public members only). Ignored when <paramref name="Methods"/> is non-empty.</param>
/// <param name="OuterBoxColor">Optional outer participant-box color overriding <see cref="DiagramStyle.DefaultOuterBoxColor"/>.</param>
/// <param name="InnerBoxColor">Optional inner participant-box color overriding <see cref="DiagramStyle.DefaultInnerBoxColor"/>.</param>
/// <param name="OpenInEditor">Open the written file in an editor after a successful write (no effect for standard output).</param>
public sealed record CallGraphRequest(
    string SourcePath,
    string? Title,
    string? OutputPath,
    bool Force,
    IReadOnlyList<string> Methods,
    bool IncludePrivate,
    string? OuterBoxColor = null,
    string? InnerBoxColor = null,
    bool OpenInEditor = false);
