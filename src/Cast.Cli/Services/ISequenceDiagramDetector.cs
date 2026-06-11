namespace Cast.Cli.Services;

/// <summary>
/// Classifies PlantUML source text as a sequence diagram or not, so the <c>style</c> command can
/// restyle sequence diagrams while leaving every other diagram type (class, component, state,
/// activity, use-case, ER, …) untouched. The classification is deliberately conservative: when
/// the evidence is mixed or absent the document is <em>not</em> treated as a sequence diagram,
/// because the cost of skipping a file is zero while the cost of rewriting a non-sequence
/// diagram is a broken file.
/// </summary>
public interface ISequenceDiagramDetector
{
    /// <summary>Whether <paramref name="content"/> is a PlantUML sequence diagram.</summary>
    bool IsSequenceDiagram(string content);
}
