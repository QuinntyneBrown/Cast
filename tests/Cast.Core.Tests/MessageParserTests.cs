using Cast.Core.Diagnostics;
using Cast.Core.Models;
using Cast.Core.Services;

namespace Cast.Core.Tests;

public sealed class MessageParserTests
{
    private static MessageParser CreateParser() => new();

    [Fact]
    public void Parse_FullMessage_ExtractsAllParts()
    {
        Message message = CreateParser().Parse("User -> OS : place order");

        Assert.Equal("User", message.Source);
        Assert.Equal("OS", message.Target);
        Assert.Equal("->", message.Arrow);
        Assert.Equal("place order", message.Label);
    }

    [Fact]
    public void Parse_NoSpaces_StillParses()
    {
        Message message = CreateParser().Parse("OS-->User:ok");

        Assert.Equal("OS", message.Source);
        Assert.Equal("User", message.Target);
        Assert.Equal("-->", message.Arrow);
        Assert.Equal("ok", message.Label);
    }

    [Fact]
    public void Parse_NoLabel_LabelIsNull()
    {
        Message message = CreateParser().Parse("A -> B");

        Assert.Equal("A", message.Source);
        Assert.Equal("B", message.Target);
        Assert.Null(message.Label);
    }

    [Fact]
    public void Parse_LabelWithColons_KeepsThemInLabel()
    {
        Message message = CreateParser().Parse("A -> B : GET http://x : 200");

        Assert.Equal("GET http://x : 200", message.Label);
    }

    [Fact]
    public void Parse_SelfMessage_IsAllowed()
    {
        Message message = CreateParser().Parse("Cache -> Cache : refresh");

        Assert.Equal("Cache", message.Source);
        Assert.Equal("Cache", message.Target);
    }

    [Theory]
    [InlineData("A -> B", "->")]
    [InlineData("A --> B", "-->")]
    [InlineData("A ->> B", "->>")]
    [InlineData("A ->x B", "->x")]
    public void Parse_VariousArrows_ArePreserved(string spec, string expectedArrow)
    {
        Message message = CreateParser().Parse(spec);

        Assert.Equal(expectedArrow, message.Arrow);
    }

    // Regression: 'o'/'x' are arrow-head decorators, but a spaceless target whose alias starts
    // with 'o'/'x' must NOT have that first character swallowed into the arrow.
    [Theory]
    [InlineData("api->oauth", "oauth")]
    [InlineData("src->object", "object")]
    [InlineData("A->oB", "oB")]
    [InlineData("A->xtra", "xtra")]
    public void Parse_SpacelessTargetStartingWithOorX_KeepsFullTarget(string spec, string expectedTarget)
    {
        Message message = CreateParser().Parse(spec);

        Assert.Equal("->", message.Arrow);
        Assert.Equal(expectedTarget, message.Target);
    }

    // The 'o'/'x' decorator IS absorbed into the arrow when whitespace separates it from the target.
    [Theory]
    [InlineData("A ->o B", "->o")]
    [InlineData("A ->x B", "->x")]
    [InlineData("A -->o B", "-->o")]
    public void Parse_DecoratedArrowWithTrailingSpace_KeepsDecorator(string spec, string expectedArrow)
    {
        Message message = CreateParser().Parse(spec);

        Assert.Equal(expectedArrow, message.Arrow);
        Assert.Equal("B", message.Target);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A B : no arrow")]   // missing arrow
    [InlineData("A -> ")]            // missing target
    [InlineData("-> B")]             // missing source
    [InlineData("A <> B")]           // arrow without a dash
    [InlineData("UserxOrder")]       // no structural arrow glyph at all
    [InlineData("Aox")]              // 'o'/'x' alone are not an arrow
    public void Parse_InvalidSpec_Throws(string spec)
    {
        Assert.Throws<DiagramFormatException>(() => CreateParser().Parse(spec));
    }
}
