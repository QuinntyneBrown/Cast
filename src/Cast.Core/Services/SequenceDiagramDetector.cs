using System.Linq;
using System.Text.RegularExpressions;

namespace Cast.Core.Services;

/// <summary>
/// Default <see cref="ISequenceDiagramDetector"/>. A document is a sequence diagram when it
/// contains <c>@startuml</c>, at least one positive sequence signal (a lifeline declaration, a
/// message arrow, <c>activate</c>, <c>autonumber</c>, an <c>alt</c>/<c>loop</c> frame, …) and no
/// signal belonging to another diagram type (<c>class</c>, <c>component</c>, <c>state</c>,
/// activity actions, class-diagram relation arrows, …). Comments and the free-text bodies of
/// notes/legends are ignored so prose can never sway the classification, and message labels
/// (text after <c>:</c>) are ignored when looking at arrows.
/// </summary>
public sealed class SequenceDiagramDetector : ISequenceDiagramDetector
{
    /// <summary>Keywords that open elements of other diagram types (class, component, state, …).</summary>
    private static readonly Regex NonSequenceKeyword = new(
        @"^(abstract\s+class|class|interface|enum|annotation|struct|exception|metaclass|protocol|object|map|json|usecase|component|node|artifact|card|agent|rectangle|package|cloud|file|folder|frame|hexagon|person|storage|stack|state|circle|diamond|label|action|archimate|salt|nwdiag|robust|concise|binary|clock)\b",
        RegexOptions.Compiled);

    /// <summary>An <c>entity</c>/<c>database</c>/<c>queue</c> opening a body is ER/deployment, not a lifeline.</summary>
    private static readonly Regex ElementWithBody = new(
        @"^(entity|database|queue|usecase|actor)\b.*\{\s*$", RegexOptions.Compiled);

    /// <summary>State-diagram start/end marker (<c>[*]</c>).</summary>
    private static readonly Regex StateMarker = new(@"^\[\*\]", RegexOptions.Compiled);

    /// <summary>A bracketed component reference such as <c>[First Component]</c>.</summary>
    private static readonly Regex ComponentReference = new(@"^\[[^\]]+\]", RegexOptions.Compiled);

    /// <summary>A parenthesised use-case reference such as <c>(Checkout)</c>.</summary>
    private static readonly Regex UseCaseReference = new(@"^\([^)]*\)", RegexOptions.Compiled);

    /// <summary>An activity action line such as <c>:do something;</c>.</summary>
    private static readonly Regex ActivityAction = new(@"^:.*[;|<>\]}]$", RegexOptions.Compiled);

    /// <summary>Activity-diagram control keywords with no sequence-diagram meaning.</summary>
    private static readonly Regex ActivityControl = new(
        @"^(start|stop|kill|detach|fork|split|backward|endwhile|endif|endfork|endswitch)\s*$|^(repeat|fork|split)\s+(again|while)\b|^(while|if|elseif|switch)\s*\(",
        RegexOptions.Compiled);

    /// <summary>
    /// Class-diagram relation arrows: inheritance (<c>--|&gt;</c>, <c>&lt;|--</c>), composition
    /// and aggregation (<c>*--</c>, <c>o--</c>), and dotted relations (<c>A .. B</c>,
    /// <c>A ..&gt; B</c>). Applied to the text before any <c>:</c> label.
    /// </summary>
    private static readonly Regex ClassRelationArrow = new(
        @"(<\|[.-])|([.-]\|>)|((?<![\w>])(\*--|--\*|o--|--o)(?![\w<]))|(\w\s*\.{2,}\s*[\w|>])",
        RegexOptions.Compiled);

    /// <summary>ER crow's-foot relation arrows (<c>}|--||</c>, <c>||--o{</c>, …).</summary>
    private static readonly Regex CrowsFootArrow = new(
        @"(\}[|o]|\|o|o\||\|\|)\s*(--|\.\.)|(--|\.\.)\s*([|o]\{|o\||\|o|\|\|)",
        RegexOptions.Compiled);

    /// <summary>A complete use-case (<c>(Case)</c>) or component (<c>[Comp]</c>) reference token.</summary>
    private static readonly Regex ElementReferenceToken = new(
        @"^(\([^)]*\)|\[[^\]]+\])$", RegexOptions.Compiled);

    /// <summary>Explicit lifeline declarations (an <c>entity</c> lifeline has no <c>{</c> body).</summary>
    private static readonly Regex LifelineDeclaration = new(
        @"^(participant|actor|boundary|control|database|collections|queue)\s+\S|^entity\s+[^{]+$",
        RegexOptions.Compiled);

    /// <summary>Sequence-only statements.</summary>
    private static readonly Regex SequenceStatement = new(
        @"^(autonumber\b|autoactivate\b|(activate|deactivate|destroy)\s+\S|(alt|opt|loop|par|critical|group|break)\b|box\b|end\s+box$|[rh]?note\s+(over|across)\b|ref\s+over\b|==.*==$|\.\.\.|return\b|create\s+\S)",
        RegexOptions.Compiled);

    /// <summary>
    /// A message arrow before any <c>:</c> label — plain (<c>-&gt;</c>, <c>--&gt;&gt;</c>,
    /// <c>&lt;-</c>), with an inline style (<c>-[#red]&gt;</c>), or the async forms
    /// (<c>-\</c>, <c>-/</c>, which require trailing whitespace so prose and paths don't match).
    /// </summary>
    private static readonly Regex MessageArrow = new(
        @"(<{1,2}(\[[^\]]*\])?-{1,2})|(-{1,2}(\[[^\]]*\])?>{1,2})|(-{1,2}(\[[^\]]*\])?[\\/]{1,2}(?=\s))",
        RegexOptions.Compiled);

    /// <summary>Sequence dividers (<c>== text ==</c>) and delays (<c>...</c>), which may contain free prose.</summary>
    private static readonly Regex DividerOrDelay = new(@"^==.*==$|^\.\.\.", RegexOptions.Compiled);

    /// <summary>Double-quoted spans (display names), masked out before structural checks.</summary>
    private static readonly Regex QuotedSpan = new("\"[^\"]*\"", RegexOptions.Compiled);

    /// <inheritdoc />
    public bool IsSequenceDiagram(string content)
    {
        string[] lines = PlantUmlScanner.SplitLines(content);

        bool hasStartUml = false;
        bool hasPositiveSignal = false;

        foreach ((_, string line) in PlantUmlScanner.EffectiveLines(lines))
        {
            if (line.StartsWith("@startuml", StringComparison.Ordinal))
            {
                hasStartUml = true;
                continue;
            }

            if (line.StartsWith('@') || line.StartsWith('!') ||
                line.StartsWith("skinparam", StringComparison.Ordinal) ||
                line.StartsWith("scale", StringComparison.Ordinal) ||
                line.StartsWith("title", StringComparison.Ordinal) ||
                line.StartsWith("caption", StringComparison.Ordinal) ||
                line.StartsWith("hide", StringComparison.Ordinal) ||
                line.StartsWith("show", StringComparison.Ordinal))
            {
                // Directives shared by every diagram type carry no signal either way.
                continue;
            }

            // Dividers and delays are sequence-only syntax whose free prose must never reach
            // the structural checks below ("== Phase 2 ... cleanup ==" is not a dotted relation).
            if (DividerOrDelay.IsMatch(line))
            {
                hasPositiveSignal = true;
                continue;
            }

            // Arrow checks must ignore message labels ("A -> B : see foo..bar" is a sequence
            // message whose label must not look like a class relation) and quoted display
            // names ("\"Foo (bar)\" -> X" must not look like a use-case reference).
            string head = QuotedSpan.Replace(HeadOf(line), "\"\"");

            if (NonSequenceKeyword.IsMatch(line) ||
                ElementWithBody.IsMatch(line) ||
                StateMarker.IsMatch(line) ||
                ComponentReference.IsMatch(head) ||
                UseCaseReference.IsMatch(head) ||
                ActivityAction.IsMatch(line) ||
                ActivityControl.IsMatch(line) ||
                ClassRelationArrow.IsMatch(head) ||
                CrowsFootArrow.IsMatch(head))
            {
                return false;
            }

            Match arrow = MessageArrow.Match(head);
            if (arrow.Success && HasElementReferenceEndpoint(head, arrow))
            {
                // "User --> (Login)" / "Web --> [Api]" are use-case/component relations even
                // though the reference is the arrow's target rather than the line's start.
                return false;
            }

            if (!hasPositiveSignal &&
                (LifelineDeclaration.IsMatch(line) ||
                 SequenceStatement.IsMatch(line) ||
                 arrow.Success))
            {
                hasPositiveSignal = true;
            }
        }

        return hasStartUml && hasPositiveSignal;
    }

    /// <summary>
    /// Whether either side of the matched arrow is a complete <c>(Case)</c> or <c>[Comp]</c>
    /// token. Sequence gate syntax (<c>[-&gt; A</c>, <c>A -&gt;]</c>) has a bare unpaired
    /// bracket, so it never forms such a token.
    /// </summary>
    private static bool HasElementReferenceEndpoint(string head, Match arrow)
    {
        string left = head[..arrow.Index].Trim();
        string right = head[(arrow.Index + arrow.Length)..].Trim();
        return ElementReferenceToken.IsMatch(left) || ElementReferenceToken.IsMatch(right);
    }

    /// <summary>The part of a line before its <c>:</c> label, or the whole line without one.</summary>
    private static string HeadOf(string line)
    {
        int colon = line.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? line : line[..colon];
    }
}
