using Cast.Cli.Diagnostics;
using Cast.Cli.Models;
using Cast.Cli.Services;

namespace Cast.Cli.Tests;

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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A B : no arrow")]   // missing arrow
    [InlineData("A -> ")]            // missing target
    [InlineData("-> B")]             // missing source
    [InlineData("A <> B")]           // arrow without a dash
    public void Parse_InvalidSpec_Throws(string spec)
    {
        Assert.Throws<DiagramFormatException>(() => CreateParser().Parse(spec));
    }
}
