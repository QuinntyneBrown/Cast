using Cast.Core.Services;

namespace Cast.Core.Tests;

public sealed class SequenceDiagramDetectorTests
{
    private static readonly SequenceDiagramDetector Detector = new();

    private static string Puml(params string[] body) =>
        "@startuml\n" + string.Join("\n", body) + "\n@enduml\n";

    // ----- sequence diagrams are recognised -------------------------------------------------

    [Fact]
    public void MessageArrows_AreSequence()
    {
        Assert.True(Detector.IsSequenceDiagram(Puml("Alice -> Bob : hello", "Bob --> Alice : hi")));
    }

    [Fact]
    public void ParticipantDeclarations_AreSequence()
    {
        Assert.True(Detector.IsSequenceDiagram(Puml("participant A", "database \"Main\" as DB")));
    }

    [Fact]
    public void ActorOnlyWithMessages_IsSequence()
    {
        Assert.True(Detector.IsSequenceDiagram(Puml("actor User", "User -> User : think")));
    }

    [Theory]
    [InlineData("autonumber")]
    [InlineData("activate A")]
    [InlineData("deactivate A")]
    [InlineData("destroy A")]
    [InlineData("box #AZURE")]
    [InlineData("== Initialization ==")]
    [InlineData("...five minutes later...")]
    [InlineData("note over A : remember")]
    [InlineData("entity Order")]
    public void SequenceOnlyStatements_AreSequence(string statement)
    {
        Assert.True(Detector.IsSequenceDiagram(Puml(statement)));
    }

    [Fact]
    public void GeneratedCastOutput_IsSequence()
    {
        string content = string.Join("\n",
            "@startuml",
            "' Scaffolded by cast",
            "!pragma teoz true",
            "skinparam defaultFontSize 10",
            "title Checkout",
            "",
            "actor User",
            "box #PHYSICAL",
            "  box #AZURE",
            "    participant \"Order Service\" as OS",
            "  end box",
            "end box",
            "",
            "User -> OS : place order",
            "@enduml") + "\n";

        Assert.True(Detector.IsSequenceDiagram(content));
    }

    [Fact]
    public void AltElseBlocks_AreSequence()
    {
        Assert.True(Detector.IsSequenceDiagram(
            Puml("A -> B : try", "alt success", "B --> A : ok", "else failure", "B --> A : ko", "end")));
    }

    // ----- other diagram types are rejected -------------------------------------------------

    [Fact]
    public void ClassDiagram_IsNotSequence()
    {
        Assert.False(Detector.IsSequenceDiagram(
            Puml("class Order {", "  +total : decimal", "}", "Order --> Customer : belongs to")));
    }

    [Fact]
    public void ClassRelationArrows_AreNotSequence()
    {
        Assert.False(Detector.IsSequenceDiagram(Puml("Dog --|> Animal")));
        Assert.False(Detector.IsSequenceDiagram(Puml("Engine *-- Car")));
        Assert.False(Detector.IsSequenceDiagram(Puml("Wheel o-- Car")));
        Assert.False(Detector.IsSequenceDiagram(Puml("ServiceImpl ..|> IService")));
        Assert.False(Detector.IsSequenceDiagram(Puml("A ..> B : uses")));
    }

    [Fact]
    public void ComponentDiagram_IsNotSequence()
    {
        Assert.False(Detector.IsSequenceDiagram(
            Puml("component \"Web\" as W", "[Database] --> W")));
    }

    [Fact]
    public void StateDiagram_IsNotSequence()
    {
        Assert.False(Detector.IsSequenceDiagram(
            Puml("[*] --> Idle", "Idle --> Running : start", "state Running")));
    }

    [Fact]
    public void ActivityDiagram_IsNotSequence()
    {
        Assert.False(Detector.IsSequenceDiagram(
            Puml("start", ":validate input;", "if (ok?) then (yes)", "  :process;", "endif", "stop")));
    }

    [Fact]
    public void UseCaseDiagram_IsNotSequence()
    {
        Assert.False(Detector.IsSequenceDiagram(
            Puml("actor Customer", "(Buy goods) as Buy", "Customer --> Buy")));
    }

    [Theory]
    [InlineData("User --> (Login)")]      // use-case reference as arrow target
    [InlineData("(Login) <-- User")]      // ... and as arrow source
    [InlineData("Web --> [Api]")]         // component reference as arrow target
    [InlineData("[Db] <-- Web")]          // ... and as arrow source
    public void ElementReferenceArrowEndpoints_AreNotSequence(string line)
    {
        Assert.False(Detector.IsSequenceDiagram(Puml("actor User", line)));
    }

    [Theory]
    [InlineData("Order }|--|| Customer")]
    [InlineData("Order }|..|| Customer")]
    [InlineData("Order ||--o{ Customer")]
    [InlineData("Order }o--o{ Customer")]
    public void CrowsFootRelations_AreNotSequence(string relation)
    {
        // The arrow alone would look like a message; the crow's-foot cardinality must veto it.
        Assert.False(Detector.IsSequenceDiagram(Puml("entity Order", "entity Customer", relation)));
    }

    [Theory]
    [InlineData("robust \"Web\" as W")]
    [InlineData("concise \"CPU\" as C")]
    [InlineData("binary \"Enable\" as EN")]
    [InlineData("clock clk with period 1")]
    [InlineData("object user")]
    [InlineData("map Capitals {")]
    [InlineData("actor Admin {")]
    public void TimingObjectMapAndBodiedActorDiagrams_AreNotSequence(string declaration)
    {
        // "A -> B : x" supplies a positive signal, so the test passes only because the
        // foreign keyword vetoes the classification.
        Assert.False(Detector.IsSequenceDiagram(Puml(declaration, "A -> B : x")));
    }

    [Fact]
    public void EntityRelationshipDiagram_IsNotSequence()
    {
        Assert.False(Detector.IsSequenceDiagram(
            Puml("entity Order {", "  * id : int", "}", "Order }|--|| Customer")));
    }

    [Fact]
    public void DeploymentDatabaseWithBody_IsNotSequence()
    {
        Assert.False(Detector.IsSequenceDiagram(
            Puml("database \"Orders\" {", "  folder \"tables\"", "}")));
    }

    [Fact]
    public void JsonDiagram_IsNotSequence()
    {
        Assert.False(Detector.IsSequenceDiagram(Puml("json Data {", "  \"a\": 1", "}")));
    }

    // ----- low-evidence and malformed inputs are rejected ------------------------------------

    [Fact]
    public void MissingStartUml_IsNotSequence()
    {
        Assert.False(Detector.IsSequenceDiagram("participant A\nA -> B : x\n"));
    }

    [Fact]
    public void EmptyOrDirectivesOnly_IsNotSequence()
    {
        Assert.False(Detector.IsSequenceDiagram("@startuml\n@enduml\n"));
        Assert.False(Detector.IsSequenceDiagram(Puml("title Nothing here", "skinparam shadowing false")));
        Assert.False(Detector.IsSequenceDiagram(string.Empty));
    }

    // ----- comments and note bodies carry no signal -------------------------------------------

    [Fact]
    public void SequenceKeywordsInsideComments_AreIgnored()
    {
        Assert.False(Detector.IsSequenceDiagram(
            Puml("' participant A", "/' A -> B : commented out '/", "class Real")));
    }

    [Fact]
    public void ClassKeywordInsideNoteBody_DoesNotDisqualify()
    {
        Assert.True(Detector.IsSequenceDiagram(
            Puml("A -> B : go", "note over A", "  this class diagram talk is prose", "end note")));
    }

    [Fact]
    public void ClassKeywordInsideBlockComment_DoesNotDisqualify()
    {
        Assert.True(Detector.IsSequenceDiagram(
            Puml("/'", "class NotReal", "'/", "A -> B : go")));
    }

    [Fact]
    public void DottedTextInMessageLabel_DoesNotDisqualify()
    {
        Assert.True(Detector.IsSequenceDiagram(Puml("A -> B : read config..values")));
    }

    // ----- lookalikes that must still be recognised as sequence diagrams -----------------------

    [Theory]
    [InlineData("[-> A : incoming")]      // gate: message from outside
    [InlineData("A ->] : outgoing")]      // gate: message to outside
    [InlineData("[o-> A : found")]
    public void GateArrows_AreSequence(string line)
    {
        Assert.True(Detector.IsSequenceDiagram(Puml(line)));
    }

    [Theory]
    [InlineData("A -[#red]> B : alert")]
    [InlineData("B <[#0000FF]- A")]
    [InlineData("A -[#blue]-> B : styled")]
    public void ColoredArrows_AreSequence(string line)
    {
        Assert.True(Detector.IsSequenceDiagram(Puml(line)));
    }

    [Fact]
    public void ParenthesesInMessageLabel_DoNotDisqualify()
    {
        Assert.True(Detector.IsSequenceDiagram(Puml("A --> B : reply (cached)")));
    }

    [Fact]
    public void QuotedDisplayNamesWithBracketsAndDots_DoNotDisqualify()
    {
        Assert.True(Detector.IsSequenceDiagram(
            Puml("participant \"Orders (v2)...\" as O", "\"Foo (bar)\" -> O : go")));
    }

    [Fact]
    public void DividerContainingEllipsis_DoesNotDisqualify()
    {
        Assert.True(Detector.IsSequenceDiagram(
            Puml("A -> B : start", "== Phase 2 ... cleanup ==", "B --> A : done")));
    }

    [Fact]
    public void RefBlockBody_CarriesNoSignal()
    {
        Assert.True(Detector.IsSequenceDiagram(
            Puml("A -> B : go", "ref over A, B", "  see steps 2..3 of the state machine", "end ref")));
    }

    [Fact]
    public void AlignedHeaderBody_CarriesNoSignal()
    {
        Assert.True(Detector.IsSequenceDiagram(
            Puml("center header", "  state of the build pipeline", "endheader", "A -> B : go")));
    }

    [Fact]
    public void CrlfContent_IsSequence()
    {
        Assert.True(Detector.IsSequenceDiagram("@startuml\r\nA -> B : x\r\n@enduml\r\n"));
    }

    [Fact]
    public void ClassDiagramDottedDependency_IsStillRejected()
    {
        // Guard against over-correcting for ellipsis prose: a real dotted relation stays negative.
        Assert.False(Detector.IsSequenceDiagram(Puml("OrderService ..> IOrderRepository")));
    }
}
