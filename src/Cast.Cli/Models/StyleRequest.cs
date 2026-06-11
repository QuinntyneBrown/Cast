namespace Cast.Cli.Models;

/// <summary>
/// The raw inputs gathered from the <c>style</c> command line. The command maps parsed options
/// into this DTO and hands it to <see cref="Services.IStyleService"/>; no
/// <c>System.CommandLine</c> type leaks past the command boundary.
/// </summary>
/// <param name="Path">A <c>.puml</c> file to restyle, or a folder to scan recursively for <c>.puml</c> files.</param>
/// <param name="OuterBoxColor">Optional outer participant-box color overriding <see cref="DiagramStyle.DefaultOuterBoxColor"/>.</param>
/// <param name="InnerBoxColor">Optional inner participant-box color overriding <see cref="DiagramStyle.DefaultInnerBoxColor"/>.</param>
public sealed record StyleRequest(
    string Path,
    string? OuterBoxColor = null,
    string? InnerBoxColor = null);
