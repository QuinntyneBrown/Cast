using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Cast.Core.Models;

namespace Cast.Core.Services;

/// <summary>
/// Default <see cref="ISequenceDiagramStyler"/>. Rewrites an existing sequence diagram in four
/// independent, idempotent steps:
/// <list type="number">
/// <item><c>!pragma teoz true</c> is inserted directly after <c>@startuml</c> unless a teoz
/// pragma already exists anywhere in the document.</item>
/// <item><c>skinparam defaultFontSize 10</c> is inserted after the pragma — or after the last
/// <c>!theme</c> line so the font size still wins over the theme — unless any
/// <c>defaultFontSize</c> setting already exists.</item>
/// <item>Every lifeline declaration that carries no color of its own gains the house
/// participant color (<see cref="DiagramStyle.ParticipantColor"/>). A declaration with any
/// <c>#</c> token outside quotes — an explicit color or a colored stereotype — keeps what it
/// has.</item>
/// <item>The explicitly declared non-actor lifelines are wrapped in the double nested box
/// (actors stay outside, declared first, matching the generator's convention). This step is
/// skipped when any <c>box</c> already exists, when there are no non-actor declarations, or when
/// the declarations are interleaved with other statements — reordering those could change the
/// diagram, and skipping is always safe.</item>
/// </list>
/// The original line-ending style (CRLF or LF) and presence of a trailing newline are preserved.
/// Documents containing more (or fewer) than one <c>@startuml</c> are returned unchanged.
/// </summary>
public sealed class PlantUmlSequenceStyler : ISequenceDiagramStyler
{
    private static readonly Regex StartUml = new(@"^@startuml\b|^@startuml$", RegexOptions.Compiled);
    private static readonly Regex TeozPragma = new(@"^!pragma\s+teoz\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Theme = new(@"^!theme\b", RegexOptions.Compiled);
    // Anchored to setting position ("skinparam defaultFontSize 12" or a bare "defaultFontSize 12"
    // inside a skinparam block) so the word appearing in a message label cannot suppress the rule.
    private static readonly Regex DefaultFontSize = new(@"^(skinparam\s+)?defaultFontSize\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Box = new(@"^box\b|^box$", RegexOptions.Compiled);

    private static readonly Regex ActorDeclaration = new(@"^actor\s+\S", RegexOptions.Compiled);

    private static readonly Regex LifelineDeclaration = new(
        @"^(participant|actor|boundary|control|database|collections|queue)\s+\S|^entity\s+[^{]+$",
        RegexOptions.Compiled);

    /// <inheritdoc />
    public StylerResult Apply(string content, DiagramStyle style)
    {
        string newline = content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        bool endsWithNewline = content.EndsWith('\n');

        var lines = new List<string>(PlantUmlScanner.SplitLines(content));
        if (endsWithNewline && lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        // Only single-diagram documents are restyled; in a multi-diagram file the per-block
        // bookkeeping isn't worth the risk of a half-applied style.
        if (Effective(lines).Count(line => StartUml.IsMatch(line.Trimmed)) != 1)
        {
            return new StylerResult(content, false, false, false, false,
                SkipReason: "it contains multiple @startuml blocks");
        }

        bool appliedPragma = InsertTeozPragma(lines);
        bool appliedFontSize = InsertDefaultFontSize(lines);
        bool appliedColors = ColorLifelines(lines);
        bool appliedBoxes = WrapLifelinesInBoxes(lines, style);

        if (!appliedPragma && !appliedFontSize && !appliedColors && !appliedBoxes)
        {
            return new StylerResult(content, false, false, false, false);
        }

        string result = string.Join(newline, lines) + (endsWithNewline ? newline : string.Empty);
        return new StylerResult(result, appliedPragma, appliedFontSize, appliedColors, appliedBoxes);
    }

    private static bool InsertTeozPragma(List<string> lines)
    {
        if (Effective(lines).Any(line => TeozPragma.IsMatch(line.Trimmed)))
        {
            return false;
        }

        lines.Insert(StartUmlIndex(lines) + 1, "!pragma teoz true");
        return true;
    }

    private static bool InsertDefaultFontSize(List<string> lines)
    {
        if (Effective(lines).Any(line => DefaultFontSize.IsMatch(line.Trimmed)))
        {
            return false;
        }

        // After the last !theme line when present, so the mandated size wins over the theme;
        // otherwise directly under @startuml (below the teoz pragma just inserted there).
        int lastTheme = Effective(lines)
            .Where(line => Theme.IsMatch(line.Trimmed))
            .Select(line => line.Index)
            .DefaultIfEmpty(-1)
            .Last();

        int insertAt = lastTheme >= 0 ? lastTheme + 1 : StartUmlIndex(lines) + 1;
        int pragmaIndex = Effective(lines)
            .Where(line => TeozPragma.IsMatch(line.Trimmed))
            .Select(line => line.Index)
            .DefaultIfEmpty(-1)
            .First();

        if (pragmaIndex >= 0 && insertAt <= pragmaIndex)
        {
            insertAt = pragmaIndex + 1;
        }

        lines.Insert(insertAt, "skinparam defaultFontSize 10");
        return true;
    }

    private static bool ColorLifelines(List<string> lines)
    {
        bool applied = false;
        foreach ((int index, string trimmed) in Effective(lines).ToList())
        {
            if (!LifelineDeclaration.IsMatch(trimmed) ||
                trimmed.EndsWith('[') || // multi-line declaration — too exotic to rewrite
                ContainsColorToken(trimmed))
            {
                continue;
            }

            lines[index] = $"{lines[index].TrimEnd()} {DiagramStyle.ParticipantColor}";
            applied = true;
        }

        return applied;
    }

    /// <summary>
    /// Whether the declaration already carries a <c>#</c> token outside quotes — an explicit
    /// color, or a colored stereotype like <c>&lt;&lt; (S,#ADD1B2) &gt;&gt;</c>. Either way the
    /// declaration is left alone; a <c>#</c> inside a quoted display name does not count.
    /// </summary>
    private static bool ContainsColorToken(string declaration)
    {
        bool inQuotes = false;
        foreach (char c in declaration)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == '#' && !inQuotes)
            {
                return true;
            }
        }

        return false;
    }

    private static bool WrapLifelinesInBoxes(List<string> lines, DiagramStyle style)
    {
        var effective = Effective(lines).ToList();
        if (effective.Any(line => Box.IsMatch(line.Trimmed)))
        {
            return false;
        }

        var declarations = effective.Where(line => LifelineDeclaration.IsMatch(line.Trimmed)).ToList();
        if (declarations.Count == 0 ||
            declarations.Any(line => line.Trimmed.EndsWith('['))) // multi-line declaration — too exotic to rewrite
        {
            return false;
        }

        // Every line between the first and last declaration must be a declaration or blank;
        // anything else (a message, a note, a comment) means wrapping could reorder meaning,
        // so the box step backs off and leaves the file alone.
        int first = declarations[0].Index;
        int last = declarations[^1].Index;
        var declarationIndexes = declarations.Select(line => line.Index).ToHashSet();
        for (int i = first; i <= last; i++)
        {
            if (!declarationIndexes.Contains(i) && lines[i].Trim().Length != 0)
            {
                return false;
            }
        }

        var actors = declarations.Where(line => ActorDeclaration.IsMatch(line.Trimmed)).ToList();
        var boxed = declarations.Where(line => !ActorDeclaration.IsMatch(line.Trimmed)).ToList();
        if (boxed.Count == 0)
        {
            return false;
        }

        var replacement = new List<string>(actors.Count + boxed.Count + 4);
        replacement.AddRange(actors.Select(line => line.Trimmed));
        replacement.Add($"box {style.OuterBoxColor}");
        replacement.Add($"  box {style.InnerBoxColor}");
        replacement.AddRange(boxed.Select(line => $"    {line.Trimmed}"));
        replacement.Add("  end box");
        replacement.Add("end box");

        lines.RemoveRange(first, last - first + 1);
        lines.InsertRange(first, replacement);
        return true;
    }

    private static int StartUmlIndex(List<string> lines) =>
        Effective(lines).First(line => StartUml.IsMatch(line.Trimmed)).Index;

    private static IEnumerable<(int Index, string Trimmed)> Effective(List<string> lines) =>
        PlantUmlScanner.EffectiveLines(lines);
}
