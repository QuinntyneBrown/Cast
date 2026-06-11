using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Cast.Cli.Services;

/// <summary>
/// Shared line-scanning helpers for reading existing PlantUML text. Both the sequence-diagram
/// detector and the styler must ignore the same non-semantic regions — line comments, block
/// comments, and the free-text bodies of multi-line notes, legends, titles, headers, and
/// footers — so that prose inside a note can never be mistaken for a diagram element.
/// </summary>
internal static class PlantUmlScanner
{
    private static readonly Regex MultiLineNoteStart = new(
        @"^[rh]?note\b(?!.*:)", RegexOptions.Compiled);

    private static readonly Regex MultiLineNoteEnd = new(
        @"^end\s*[rh]?note\b", RegexOptions.Compiled);

    // Multi-line free-text regions other than notes: "legend" … "end legend",
    // bare "title" … "end title", "header"/"footer" (with an optional alignment prefix) …
    // "endheader"/"endfooter", and "ref over A, B" … "end ref". The single-line forms
    // ("title Text", "center header Text", "ref over A : label") do not open a region.
    private static readonly Regex LegendStart = new(@"^legend\b", RegexOptions.Compiled);
    private static readonly Regex LegendEnd = new(@"^end\s*legend\b", RegexOptions.Compiled);
    private static readonly Regex TitleBlockStart = new(@"^title\s*$", RegexOptions.Compiled);
    private static readonly Regex TitleBlockEnd = new(@"^end\s*title\b", RegexOptions.Compiled);
    private static readonly Regex HeaderFooterStart = new(@"^((left|right|center)\s+)?(header|footer)\s*$", RegexOptions.Compiled);
    private static readonly Regex HeaderFooterEnd = new(@"^end\s*(header|footer)\b", RegexOptions.Compiled);
    private static readonly Regex RefBlockStart = new(@"^ref\s+over\b(?!.*:)", RegexOptions.Compiled);
    private static readonly Regex RefBlockEnd = new(@"^end\s*ref\b", RegexOptions.Compiled);

    /// <summary>
    /// Yields the index and trimmed text of every line that carries diagram meaning: blank
    /// lines, <c>'</c> line comments, <c>/' … '/</c> block comments, and the bodies of
    /// multi-line note/legend/title/header/footer regions are skipped.
    /// </summary>
    public static IEnumerable<(int Index, string Trimmed)> EffectiveLines(IReadOnlyList<string> lines)
    {
        bool inBlockComment = false;
        Regex? activeRegionEnd = null;

        for (int i = 0; i < lines.Count; i++)
        {
            string trimmed = lines[i].Trim();

            if (inBlockComment)
            {
                if (trimmed.Contains("'/"))
                {
                    inBlockComment = false;
                }

                continue;
            }

            if (activeRegionEnd is not null)
            {
                if (activeRegionEnd.IsMatch(trimmed))
                {
                    activeRegionEnd = null;
                }

                continue;
            }

            if (trimmed.Length == 0 || trimmed.StartsWith('\''))
            {
                continue;
            }

            if (trimmed.StartsWith("/'"))
            {
                // A block comment that closes on the same line is just a comment line.
                inBlockComment = !trimmed[2..].Contains("'/");
                continue;
            }

            // Region-opening lines are themselves meaningful (e.g. "note over A" is a strong
            // sequence signal), so they are yielded; only the free-text bodies are skipped.
            if (MultiLineNoteStart.IsMatch(trimmed))
            {
                activeRegionEnd = MultiLineNoteEnd;
            }
            else if (LegendStart.IsMatch(trimmed))
            {
                activeRegionEnd = LegendEnd;
            }
            else if (TitleBlockStart.IsMatch(trimmed))
            {
                activeRegionEnd = TitleBlockEnd;
            }
            else if (HeaderFooterStart.IsMatch(trimmed))
            {
                activeRegionEnd = HeaderFooterEnd;
            }
            else if (RefBlockStart.IsMatch(trimmed))
            {
                activeRegionEnd = RefBlockEnd;
            }

            yield return (i, trimmed);
        }
    }

    /// <summary>Splits content into lines, normalizing line endings away from the caller.</summary>
    public static string[] SplitLines(string content) =>
        content.Replace("\r\n", "\n").Split('\n');
}
