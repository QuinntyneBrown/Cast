using System.Collections.Generic;

namespace Cast.Cli.Models;

/// <summary>
/// The render-ready call graph extracted from a single TypeScript source file: the subject (a class
/// or a module) and each of its methods/functions with the outbound calls they make. This is the
/// hand-off type between <see cref="Services.ITypeScriptCallGraphParser"/> (extraction) and
/// <see cref="Services.ICallGraphRenderer"/> (rendering).
/// </summary>
/// <param name="SubjectName">The class name, or the module (file) name when the file has no class.</param>
/// <param name="Kind">Whether the subject is a class or a module of functions.</param>
/// <param name="Methods">The discovered methods/functions, in declaration order.</param>
public sealed record CallGraph(
    string SubjectName,
    CallGraphSubjectKind Kind,
    IReadOnlyList<CallGraphMethod> Methods);
