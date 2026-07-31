using Cast.Core.Diagnostics;
using Cast.Core.Models;

namespace Cast.Core.Tests;

public sealed class DiagramStyleTests
{
    [Fact]
    public void Default_UsesPhysicalOuterAndAzureInner()
    {
        Assert.Equal("#PHYSICAL", DiagramStyle.Default.OuterBoxColor);
        Assert.Equal("#AZURE", DiagramStyle.Default.InnerBoxColor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromOptions_MissingValue_FallsBackToDefault(string? value)
    {
        DiagramStyle style = DiagramStyle.FromOptions(value, value);

        Assert.Equal(DiagramStyle.Default, style);
    }

    [Fact]
    public void FromOptions_ExplicitColors_AreUsedAsGiven()
    {
        DiagramStyle style = DiagramStyle.FromOptions("#DeepSkyBlue", "#FFFFFF");

        Assert.Equal("#DeepSkyBlue", style.OuterBoxColor);
        Assert.Equal("#FFFFFF", style.InnerBoxColor);
    }

    [Fact]
    public void FromOptions_MissingHashPrefix_IsAdded()
    {
        DiagramStyle style = DiagramStyle.FromOptions("LightGray", "white");

        Assert.Equal("#LightGray", style.OuterBoxColor);
        Assert.Equal("#white", style.InnerBoxColor);
    }

    [Fact]
    public void FromOptions_SurroundingWhitespace_IsTrimmed()
    {
        DiagramStyle style = DiagramStyle.FromOptions("  #AAA  ", "  #BBB  ");

        Assert.Equal("#AAA", style.OuterBoxColor);
        Assert.Equal("#BBB", style.InnerBoxColor);
    }

    [Theory]
    [InlineData("light gray")]
    [InlineData("#A\tB")]
    public void FromOptions_ColorWithEmbeddedWhitespace_Throws(string color)
    {
        Assert.Throws<DiagramFormatException>(() => DiagramStyle.FromOptions(color, null));
        Assert.Throws<DiagramFormatException>(() => DiagramStyle.FromOptions(null, color));
    }
}
