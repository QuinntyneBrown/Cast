using Cast.Cli.Models;
using Cast.Cli.Services;

namespace Cast.Cli.Tests;

public sealed class PlantUmlSequenceStylerTests
{
    private static readonly PlantUmlSequenceStyler Styler = new();

    private static StylerResult Apply(string content, DiagramStyle? style = null) =>
        Styler.Apply(content, style ?? DiagramStyle.Default);

    // ----- full transformation ----------------------------------------------------------------

    [Fact]
    public void Apply_PlainDiagram_AddsPragmaFontSizeAndBoxes()
    {
        string input = string.Join("\n",
            "@startuml",
            "title Checkout",
            "",
            "actor User",
            "participant \"Order Service\" as OS",
            "database DB",
            "",
            "User -> OS : place order",
            "OS -> DB : persist",
            "@enduml") + "\n";

        string expected = string.Join("\n",
            "@startuml",
            "!pragma teoz true",
            "skinparam defaultFontSize 10",
            "title Checkout",
            "",
            "actor User #63BEF2",
            "box #PHYSICAL",
            "  box #AZURE",
            "    participant \"Order Service\" as OS #63BEF2",
            "    database DB #63BEF2",
            "  end box",
            "end box",
            "",
            "User -> OS : place order",
            "OS -> DB : persist",
            "@enduml") + "\n";

        StylerResult result = Apply(input);

        Assert.Equal(expected, result.Content);
        Assert.True(result.AppliedTeozPragma);
        Assert.True(result.AppliedFontSize);
        Assert.True(result.AppliedParticipantColors);
        Assert.True(result.AppliedBoxes);
    }

    [Fact]
    public void Apply_IsIdempotent()
    {
        string input = "@startuml\nactor U\nparticipant A\nU -> A : go\n@enduml\n";

        StylerResult first = Apply(input);
        StylerResult second = Apply(first.Content);

        Assert.True(first.Changed);
        Assert.False(second.Changed);
        Assert.Equal(first.Content, second.Content);
    }

    [Fact]
    public void Apply_FullyStyledCastOutput_IsUnchanged()
    {
        string input = string.Join("\n",
            "@startuml",
            "' Scaffolded by cast",
            "!pragma teoz true",
            "skinparam defaultFontSize 10",
            "",
            "actor User #63BEF2",
            "box #PHYSICAL",
            "  box #AZURE",
            "    participant \"Order Service\" as OS #63BEF2",
            "  end box",
            "end box",
            "",
            "User -> OS : place order",
            "@enduml") + "\n";

        StylerResult result = Apply(input);

        Assert.False(result.Changed);
        Assert.Equal(input, result.Content);
        Assert.Null(result.SkipReason); // genuinely already styled, not a refusal
    }

    // ----- pragma ------------------------------------------------------------------------------

    [Fact]
    public void Apply_ExistingPragma_IsNotDuplicated()
    {
        string input = "@startuml\n!pragma teoz true\nparticipant A\nA -> A : x\n@enduml\n";

        StylerResult result = Apply(input);

        Assert.False(result.AppliedTeozPragma);
        Assert.Equal(2, result.Content.Split("!pragma teoz").Length); // exactly one occurrence
    }

    [Fact]
    public void Apply_PragmaGoesDirectlyAfterStartUml()
    {
        StylerResult result = Apply("@startuml\ntitle T\nparticipant A\n@enduml\n");

        Assert.StartsWith("@startuml\n!pragma teoz true\n", result.Content);
    }

    // ----- font size ----------------------------------------------------------------------------

    [Fact]
    public void Apply_ExistingFontSize_IsRespected()
    {
        string input = "@startuml\nskinparam defaultFontSize 14\nparticipant A\nA -> A : x\n@enduml\n";

        StylerResult result = Apply(input);

        Assert.False(result.AppliedFontSize);
        Assert.Contains("defaultFontSize 14", result.Content);
        Assert.DoesNotContain("defaultFontSize 10", result.Content);
    }

    [Fact]
    public void Apply_WithTheme_FontSizeGoesAfterTheme()
    {
        string input = "@startuml\n!theme plain\nparticipant A\nA -> A : x\n@enduml\n";

        StylerResult result = Apply(input);

        Assert.Contains("!theme plain\nskinparam defaultFontSize 10\n", result.Content);
    }

    [Fact]
    public void Apply_MultipleThemes_FontSizeGoesAfterLastTheme()
    {
        string input = "@startuml\n!theme plain\n!theme spacelab\nparticipant A\nA -> A : x\n@enduml\n";

        StylerResult result = Apply(input);

        Assert.Contains("!theme spacelab\nskinparam defaultFontSize 10\n", result.Content);
        Assert.DoesNotContain("!theme plain\nskinparam defaultFontSize", result.Content);
    }

    [Fact]
    public void Apply_FontSizeInsideSkinparamBlock_IsRespected()
    {
        string input = "@startuml\nskinparam sequence {\n  defaultFontSize 12\n}\nparticipant A\nA -> A : x\n@enduml\n";

        StylerResult result = Apply(input);

        Assert.False(result.AppliedFontSize);
        Assert.DoesNotContain("defaultFontSize 10", result.Content);
    }

    [Fact]
    public void Apply_FontSizeAnyCase_IsRespected()
    {
        StylerResult result = Apply("@startuml\nskinparam defaultfontsize 12\nparticipant A\nA -> A : x\n@enduml\n");

        Assert.False(result.AppliedFontSize);
        Assert.DoesNotContain("defaultFontSize 10", result.Content);
    }

    [Fact]
    public void Apply_FontSizeMentionedInMessageLabel_StillInsertsTheSetting()
    {
        string input = "@startuml\nparticipant A\nA -> A : please set defaultFontSize everywhere\n@enduml\n";

        StylerResult result = Apply(input);

        Assert.True(result.AppliedFontSize);
        Assert.Contains("skinparam defaultFontSize 10", result.Content);
    }

    [Fact]
    public void Apply_FontSizeMentionedInNoteBody_StillInsertsTheSetting()
    {
        string input = "@startuml\nparticipant A\nA -> A : x\nnote over A\n  we chose defaultFontSize 99 manually\nend note\n@enduml\n";

        StylerResult result = Apply(input);

        Assert.True(result.AppliedFontSize);
        Assert.Contains("skinparam defaultFontSize 10", result.Content);
    }

    // ----- boxes --------------------------------------------------------------------------------

    [Fact]
    public void Apply_ExistingBox_IsLeftAlone()
    {
        string input = string.Join("\n",
            "@startuml",
            "box \"Backend\" #LightBlue",
            "participant A",
            "end box",
            "A -> A : x",
            "@enduml") + "\n";

        StylerResult result = Apply(input);

        Assert.False(result.AppliedBoxes);
        Assert.Contains("box \"Backend\" #LightBlue", result.Content);
        Assert.DoesNotContain("#PHYSICAL", result.Content);
    }

    [Fact]
    public void Apply_CustomStyle_UsesConfiguredColors()
    {
        StylerResult result = Apply(
            "@startuml\nparticipant A\nA -> A : x\n@enduml\n",
            new DiagramStyle("#Gray", "#White"));

        Assert.Contains("box #Gray\n  box #White\n    participant A #63BEF2\n  end box\nend box\n", result.Content);
    }

    [Fact]
    public void Apply_ActorsAreHoistedAboveTheBox()
    {
        string input = string.Join("\n",
            "@startuml",
            "participant A",
            "actor U",
            "participant B",
            "U -> A : go",
            "@enduml") + "\n";

        StylerResult result = Apply(input);

        Assert.Contains(string.Join("\n",
            "actor U #63BEF2",
            "box #PHYSICAL",
            "  box #AZURE",
            "    participant A #63BEF2",
            "    participant B #63BEF2",
            "  end box",
            "end box"), result.Content);
    }

    [Fact]
    public void Apply_OnlyActors_GetsNoBox()
    {
        StylerResult result = Apply("@startuml\nactor U\nactor V\nU -> V : hi\n@enduml\n");

        Assert.False(result.AppliedBoxes);
        Assert.DoesNotContain("box", result.Content);
        Assert.True(result.AppliedTeozPragma); // other rules still apply
    }

    [Fact]
    public void Apply_NoExplicitDeclarations_GetsNoBox()
    {
        StylerResult result = Apply("@startuml\nA -> B : implicit lifelines\n@enduml\n");

        Assert.False(result.AppliedBoxes);
        Assert.True(result.AppliedTeozPragma);
        Assert.True(result.AppliedFontSize);
    }

    [Fact]
    public void Apply_DeclarationsInterleavedWithMessages_GetsNoBoxButOtherRules()
    {
        string input = string.Join("\n",
            "@startuml",
            "participant A",
            "A -> A : early message",
            "participant B",
            "@enduml") + "\n";

        StylerResult result = Apply(input);

        Assert.False(result.AppliedBoxes);
        Assert.True(result.AppliedTeozPragma);
        Assert.Contains("A -> A : early message\nparticipant B", result.Content);
    }

    [Fact]
    public void Apply_CommentInsideDeclarationBlock_GetsNoBox()
    {
        string input = string.Join("\n",
            "@startuml",
            "participant A",
            "' the backend",
            "participant B",
            "A -> B : x",
            "@enduml") + "\n";

        Assert.False(Apply(input).AppliedBoxes);
    }

    [Fact]
    public void Apply_BlankLinesInsideDeclarationBlock_AreAbsorbed()
    {
        string input = string.Join("\n",
            "@startuml",
            "participant A",
            "",
            "participant B",
            "A -> B : x",
            "@enduml") + "\n";

        StylerResult result = Apply(input);

        Assert.True(result.AppliedBoxes);
        Assert.Contains("    participant A #63BEF2\n    participant B #63BEF2\n", result.Content);
    }

    [Fact]
    public void Apply_DeclarationSuffixes_ArePreserved()
    {
        string input = "@startuml\nparticipant \"P\" as P #red\nP -> P : x\n@enduml\n";

        StylerResult result = Apply(input);

        Assert.Contains("    participant \"P\" as P #red\n", result.Content);
    }

    // ----- lifeline colors ------------------------------------------------------------------------

    [Fact]
    public void Apply_BoxBackOff_StillColorsTheLifelines()
    {
        // Declarations interleaved with a message: the box step backs off, the color step doesn't.
        string input = "@startuml\nparticipant A\nA -> A : early\nparticipant B\n@enduml\n";

        StylerResult result = Apply(input);

        Assert.False(result.AppliedBoxes);
        Assert.True(result.AppliedParticipantColors);
        Assert.Contains("participant A #63BEF2", result.Content);
        Assert.Contains("participant B #63BEF2", result.Content);
    }

    [Fact]
    public void Apply_AlreadyColoredLifelines_AreNotRecolored()
    {
        string input = string.Join("\n",
            "@startuml",
            "!pragma teoz true",
            "skinparam defaultFontSize 10",
            "actor U #Gold",
            "box #PHYSICAL",
            "  box #AZURE",
            "    participant A #red",
            "  end box",
            "end box",
            "U -> A : x",
            "@enduml") + "\n";

        StylerResult result = Apply(input);

        Assert.False(result.AppliedParticipantColors);
        Assert.False(result.Changed);
        Assert.DoesNotContain("#63BEF2", result.Content);
    }

    [Fact]
    public void Apply_HashInsideQuotedDisplayName_DoesNotCountAsColor()
    {
        string input = "@startuml\nparticipant \"Issue #42\" as I\nI -> I : x\n@enduml\n";

        StylerResult result = Apply(input);

        Assert.True(result.AppliedParticipantColors);
        Assert.Contains("participant \"Issue #42\" as I #63BEF2", result.Content);
    }

    [Fact]
    public void Apply_ColoredStereotype_IsLeftAlone()
    {
        string input = "@startuml\nparticipant A << (S,#ADD1B2) >>\nA -> A : x\n@enduml\n";

        StylerResult result = Apply(input);

        Assert.False(result.AppliedParticipantColors);
        Assert.DoesNotContain("#63BEF2", result.Content);
    }

    // ----- formatting fidelity ------------------------------------------------------------------

    [Fact]
    public void Apply_CrlfInput_KeepsCrlf()
    {
        string input = "@startuml\r\nparticipant A\r\nA -> A : x\r\n@enduml\r\n";

        StylerResult result = Apply(input);

        Assert.Contains("\r\n!pragma teoz true\r\n", result.Content);
        Assert.DoesNotContain('\n', result.Content.Replace("\r\n", string.Empty)); // no bare LFs
        Assert.EndsWith("@enduml\r\n", result.Content);
    }

    [Fact]
    public void Apply_NoTrailingNewline_StaysWithout()
    {
        StylerResult result = Apply("@startuml\nparticipant A\nA -> A : x\n@enduml");

        Assert.EndsWith("@enduml", result.Content);
        Assert.False(result.Content.EndsWith('\n'));
    }

    [Fact]
    public void Apply_CrlfWithoutTrailingNewline_PreservesBoth()
    {
        StylerResult result = Apply("@startuml\r\nparticipant A\r\nA -> A : x\r\n@enduml");

        Assert.Contains("\r\n!pragma teoz true\r\n", result.Content);
        Assert.DoesNotContain('\n', result.Content.Replace("\r\n", string.Empty)); // no bare LFs
        Assert.EndsWith("@enduml", result.Content);
        Assert.False(result.Content.EndsWith('\n'));
    }

    [Fact]
    public void Apply_BoxWordInsideRefBody_DoesNotSuppressBoxing()
    {
        string input = string.Join("\n",
            "@startuml",
            "participant A",
            "participant B",
            "A -> B : go",
            "ref over A, B",
            "  box handling is described elsewhere",
            "end ref",
            "@enduml") + "\n";

        StylerResult result = Apply(input);

        Assert.True(result.AppliedBoxes);
        Assert.Contains("box #PHYSICAL", result.Content);
        Assert.Contains("box handling is described elsewhere", result.Content); // body untouched
    }

    [Fact]
    public void Apply_SingleLineRef_DoesNotSwallowTheRestOfTheDiagram()
    {
        string input = string.Join("\n",
            "@startuml",
            "participant A",
            "participant B",
            "ref over A, B : authentication",
            "A -> B : go",
            "@enduml") + "\n";

        StylerResult result = Apply(input);

        Assert.True(result.AppliedTeozPragma);
        Assert.True(result.AppliedBoxes);
    }

    [Fact]
    public void Apply_BoxWordInsideAlignedFooterBody_DoesNotSuppressBoxing()
    {
        string input = string.Join("\n",
            "@startuml",
            "participant A",
            "A -> A : x",
            "center footer",
            "  box art by the docs team",
            "endfooter",
            "@enduml") + "\n";

        Assert.True(Apply(input).AppliedBoxes);
    }

    // ----- idempotency depth ----------------------------------------------------------------------

    public static TheoryData<string, DiagramStyle> SecondPassFixtures => new()
    {
        { "@startuml\n!theme plain\nparticipant A\nA -> A : x\n@enduml\n", DiagramStyle.Default },      // themed
        { "@startuml\r\nparticipant A\r\nA -> A : x\r\n@enduml\r\n", DiagramStyle.Default },            // CRLF
        { "@startuml\nparticipant A\nA -> A : x\n@enduml\n", new DiagramStyle("#Gray", "#White") },     // custom colors, restyled with defaults
        { "@startuml\nparticipant A\nA -> A : early\nparticipant B\n@enduml\n", DiagramStyle.Default }, // box back-off
        { "@startuml\nparticipant A\nA -> A : x\n@enduml", DiagramStyle.Default },                      // no trailing newline
    };

    [Theory]
    [MemberData(nameof(SecondPassFixtures))]
    public void Apply_SecondPass_IsAlwaysNoOp(string input, DiagramStyle firstStyle)
    {
        StylerResult first = Styler.Apply(input, firstStyle);
        StylerResult second = Styler.Apply(first.Content, DiagramStyle.Default);

        Assert.False(second.Changed);
        Assert.Equal(first.Content, second.Content);
    }

    // ----- documents that must not be touched ----------------------------------------------------

    [Fact]
    public void Apply_MultipleDiagramsInOneFile_IsUnchangedWithSkipReason()
    {
        string input = "@startuml\nA -> B : x\n@enduml\n@startuml\nC -> D : y\n@enduml\n";

        StylerResult result = Apply(input);

        Assert.False(result.Changed);
        Assert.Equal(input, result.Content);
        Assert.NotNull(result.SkipReason);
    }

    [Fact]
    public void Apply_MultiLineParticipantDeclaration_GetsNoBox()
    {
        string input = string.Join("\n",
            "@startuml",
            "participant Bob [",
            "  =Title",
            "]",
            "Bob -> Bob : x",
            "@enduml") + "\n";

        Assert.False(Apply(input).AppliedBoxes);
    }
}
