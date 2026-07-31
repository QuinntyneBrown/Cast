using Cast.Core.Diagnostics;

namespace Cast.Core.Models;

/// <summary>
/// Global styling shared by every rendered diagram: the colors of the double nested box that
/// wraps the non-actor participants (actors always stay outside the boxes). Defaults are
/// <c>#PHYSICAL</c> for the outer box and <c>#AZURE</c> for the inner box; both can be
/// overridden from the command line.
/// </summary>
/// <param name="OuterBoxColor">PlantUML color of the outer participant box.</param>
/// <param name="InnerBoxColor">PlantUML color of the inner participant box.</param>
public sealed record DiagramStyle(
    string OuterBoxColor = DiagramStyle.DefaultOuterBoxColor,
    string InnerBoxColor = DiagramStyle.DefaultInnerBoxColor)
{
    /// <summary>The default color of the outer participant box.</summary>
    public const string DefaultOuterBoxColor = "#PHYSICAL";

    /// <summary>The default color of the inner participant box.</summary>
    public const string DefaultInnerBoxColor = "#AZURE";

    /// <summary>
    /// The fixed background color of every lifeline declaration (participants and actors alike).
    /// Part of the house style, so it is not overridable from the command line.
    /// </summary>
    public const string ParticipantColor = "#63BEF2";

    /// <summary>The style used when the command line overrides nothing.</summary>
    public static DiagramStyle Default { get; } = new();

    /// <summary>
    /// Builds a style from raw command-line values: a missing value falls back to the default,
    /// a leading <c>#</c> is added when absent, and a value containing whitespace is rejected
    /// because PlantUML colors are single tokens.
    /// </summary>
    /// <exception cref="DiagramFormatException">A supplied color is not a single token.</exception>
    public static DiagramStyle FromOptions(string? outerBoxColor, string? innerBoxColor) =>
        new(
            NormalizeColor(outerBoxColor, DefaultOuterBoxColor, "--outer-box-color"),
            NormalizeColor(innerBoxColor, DefaultInnerBoxColor, "--inner-box-color"));

    private static string NormalizeColor(string? value, string fallback, string optionName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        string color = value.Trim();
        foreach (char c in color)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                throw new DiagramFormatException(
                    $"Box color '{color}' for {optionName} must be a single token without whitespace (e.g. #AZURE or LightBlue).");
            }
        }

        return color.StartsWith('#') ? color : "#" + color;
    }
}
