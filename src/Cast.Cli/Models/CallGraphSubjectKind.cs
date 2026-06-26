namespace Cast.Cli.Models;

/// <summary>
/// Whether a <see cref="CallGraph"/> describes the methods of a single class/type or the exported
/// functions of a module (a <c>.ts</c> file with no class). Used only to choose friendly wording
/// in the rendered diagram — the call story is the same either way.
/// </summary>
public enum CallGraphSubjectKind
{
    /// <summary>A class whose methods are diagrammed.</summary>
    Class,

    /// <summary>A module — a file with no class, whose exported functions are diagrammed.</summary>
    Module,
}
