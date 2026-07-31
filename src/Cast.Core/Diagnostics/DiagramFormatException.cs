using System;

namespace Cast.Core.Diagnostics;

/// <summary>
/// Thrown when user-supplied input (a participant or message spec, a theme name, etc.)
/// cannot be parsed into a valid diagram element. The message is written for an end user
/// and is safe to print directly; callers translate it into a usage exit code rather than
/// a stack trace.
/// </summary>
public sealed class DiagramFormatException : Exception
{
    /// <summary>Initializes an input-format exception with a user-facing message.</summary>
    public DiagramFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes an input-format exception caused by another exception.</summary>
    public DiagramFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
