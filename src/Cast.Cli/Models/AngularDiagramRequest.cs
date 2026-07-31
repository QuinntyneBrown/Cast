using Cast.Core.Models;

namespace Cast.Cli.Models;

/// <summary>
/// The raw inputs gathered from the <c>ng</c> command line. The command maps parsed options into
/// this DTO and hands it to <see cref="Services.IAngularDiagramService"/>; no
/// <c>System.CommandLine</c> type leaks past the command boundary.
/// </summary>
/// <param name="ServicePath">Path to the Angular <c>.ts</c> source file to inspect.</param>
/// <param name="Title">Optional diagram title overriding the generated default.</param>
/// <param name="OutputPath">Destination file path, or <see langword="null"/> to write to standard output.</param>
/// <param name="Force">Overwrite <paramref name="OutputPath"/> if it already exists.</param>
/// <param name="OuterBoxColor">Optional outer participant-box color overriding <see cref="DiagramStyle.DefaultOuterBoxColor"/>.</param>
/// <param name="InnerBoxColor">Optional inner participant-box color overriding <see cref="DiagramStyle.DefaultInnerBoxColor"/>.</param>
/// <param name="OpenInEditor">
/// Open the written file in an editor after a successful write. Has no effect when
/// <paramref name="OutputPath"/> is <see langword="null"/> (standard output).
/// </param>
public sealed record AngularDiagramRequest(
    string ServicePath,
    string? Title,
    string? OutputPath,
    bool Force,
    string? OuterBoxColor = null,
    string? InnerBoxColor = null,
    bool OpenInEditor = false);
