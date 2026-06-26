using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Cast.Cli.Diagnostics;
using Cast.Cli.Models;

namespace Cast.Cli.Services;

/// <summary>
/// Default <see cref="ITypeScriptCallGraphParser"/>. A focused, comment/string-aware scanner — not a
/// full TypeScript parser — that extracts an approximate call graph from the common, well-known
/// shapes TypeScript code takes:
/// <list type="number">
/// <item><em>Sanitise</em> the source (via <see cref="TypeScriptScanner"/>) so comments and literal
/// contents cannot masquerade as code while every index stays aligned with the original.</item>
/// <item>Choose the subject: the primary class (preferring an exported one) and its methods, or —
/// when the file declares no class — the module's exported functions.</item>
/// <item>Build a symbol table from class fields, constructor parameter-properties, and each member's
/// own parameters and locals, so a call's receiver (<c>this.repo.save()</c>, <c>repo.save()</c>)
/// can be resolved to a type.</item>
/// <item>For every member, collect the outbound calls it makes — self calls, collaborator calls, and
/// <c>new T(...)</c> constructions — in source order, de-duplicated.</item>
/// </list>
/// Intentionally conservative: arrow-function class properties are not treated as methods, calls on
/// values typed as a primitive and calls to <c>console</c> are dropped as noise, and unresolved
/// receivers are shown under the identifier as written. All regular expressions carry a match
/// timeout and the input is size-bounded, so pathological input fails fast rather than hanging.
/// </summary>
public sealed partial class TypeScriptCallGraphParser : ITypeScriptCallGraphParser
{
    /// <summary>Upper bound on input size; larger files are rejected rather than scanned.</summary>
    private const int MaxSourceLength = 2_000_000;

    /// <summary>Identifiers that can be followed by <c>(</c> but are control flow/operators, never calls.</summary>
    private static readonly HashSet<string> CallKeywords = new(StringComparer.Ordinal)
    {
        "if", "for", "while", "switch", "catch", "return", "await", "typeof", "throw", "do", "with",
        "function", "yield", "in", "of", "new", "case", "void", "delete", "instanceof", "as", "async",
        "super", "this", "else", "try", "finally", "import", "export", "satisfies", "keyof",
    };

    /// <summary>Member names that can never be a real method declaration (defensive against odd matches).</summary>
    private static readonly HashSet<string> NonMethodNames = new(StringComparer.Ordinal)
    {
        "if", "for", "while", "switch", "catch", "do", "else", "try", "finally", "return", "function",
    };

    /// <summary>Ubiquitous globals whose calls are noise in a call graph.</summary>
    private static readonly HashSet<string> IgnoredReceivers = new(StringComparer.Ordinal)
    {
        "console",
    };

    /// <summary>TypeScript primitive/structural types; a call on a value of one of these is dropped as noise.</summary>
    private static readonly HashSet<string> PrimitiveTypes = new(StringComparer.Ordinal)
    {
        "string", "number", "boolean", "bigint", "symbol", "any", "unknown", "void", "never",
        "null", "undefined", "object",
    };

    /// <inheritdoc />
    public CallGraph Parse(string source, string? fileName = null)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw Fail(fileName, "the file is empty.");
        }

        if (source.Length > MaxSourceLength)
        {
            throw Fail(fileName, $"the file is too large to inspect ({source.Length:N0} characters).");
        }

        string code = TypeScriptScanner.Sanitize(source);

        try
        {
            Match? cls = SelectClass(code);
            if (cls is not null)
            {
                CallGraph graph = ParseClass(code, cls);
                if (graph.Methods.Count > 0)
                {
                    return graph;
                }

                throw Fail(fileName, $"class '{cls.Groups["name"].Value}' declares no methods to diagram.");
            }

            CallGraph module = ParseModule(code, fileName);
            if (module.Methods.Count > 0)
            {
                return module;
            }
        }
        catch (RegexMatchTimeoutException)
        {
            throw Fail(fileName, "parsing timed out; the file is unusually large or has an unusual shape.");
        }

        throw Fail(
            fileName,
            "no class or exported function was found. Point --source at a .ts file that declares a class " +
            "or exports a function.");
    }

    // ----- class subject -------------------------------------------------------------------

    /// <summary>Picks the primary class: the first exported class if any, else the first class declared.</summary>
    private static Match? SelectClass(string code)
    {
        Match? first = null;
        foreach (Match m in ClassDeclaration().Matches(code))
        {
            first ??= m;
            if (m.Groups["export"].Success)
            {
                return m;
            }
        }

        return first;
    }

    private CallGraph ParseClass(string code, Match cls)
    {
        string name = cls.Groups["name"].Value;
        (int bodyStart, int bodyEnd) = TypeScriptScanner.ExtractBraceRange(code, cls.Index + cls.Length);
        string body = code[bodyStart..bodyEnd];

        var symbols = new Dictionary<string, string>(StringComparer.Ordinal);
        CollectMemberTypes(body, symbols);

        List<MemberHeader> headers = FindMethodHeaders(body);
        var known = new HashSet<string>(headers.Select(h => h.Name), StringComparer.Ordinal);

        var methods = new List<CallGraphMethod>();
        foreach (MemberHeader header in headers)
        {
            if (header.Name == "constructor")
            {
                continue; // construction is rendered per-call (new T); the ctor itself is not a method
            }

            methods.Add(BuildMethod(body, header, name, symbols, known));
        }

        return new CallGraph(name, CallGraphSubjectKind.Class, methods);
    }

    /// <summary>
    /// Finds class methods at brace-depth 0 of the body that have a real body (not an overload
    /// signature), skipping decorator calls, and de-duplicating by name (so a get/set pair counts
    /// once). Arrow-function properties (<c>name = () =&gt; …</c>) are intentionally not treated as
    /// methods.
    /// </summary>
    private static List<MemberHeader> FindMethodHeaders(string body)
    {
        var headers = new List<MemberHeader>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match m in MethodHeader().Matches(body))
        {
            if (TypeScriptScanner.BraceDepth(body, m.Index) != 0 || IsDecoratorCall(body, m.Index))
            {
                continue;
            }

            string name = m.Groups["name"].Value;
            if (NonMethodNames.Contains(name))
            {
                continue;
            }

            int parenOpen = m.Index + m.Length - 1; // the matched '('
            (int paramStart, int paramEnd) = TypeScriptScanner.ExtractBalancedRange(body, parenOpen, '(', ')');
            (bool ok, int bStart, int bEnd) = FindBody(body, paramEnd + 1);
            if (!ok || !seen.Add(name))
            {
                continue;
            }

            HashSet<string> mods = SplitModifiers(m.Groups["mods"].Value);
            headers.Add(new MemberHeader(
                name,
                Visibility(name, mods),
                mods.Contains("static"),
                mods.Contains("async"),
                paramStart,
                paramEnd,
                bStart,
                bEnd));
        }

        return headers;
    }

    /// <summary>Collects class-level field and constructor parameter-property types into <paramref name="symbols"/>.</summary>
    private static void CollectMemberTypes(string body, Dictionary<string, string> symbols)
    {
        // Fields and constructor parameter-properties both live at brace-depth 0 (the latter inside
        // the constructor's parentheses, which do not change brace depth).
        foreach (Match m in TypedMember().Matches(body))
        {
            if (TypeScriptScanner.BraceDepth(body, m.Index) == 0)
            {
                symbols.TryAdd(m.Groups["name"].Value, TypeScriptScanner.LastSegment(m.Groups["type"].Value));
            }
        }

        foreach (Match m in InjectField().Matches(body))
        {
            if (TypeScriptScanner.BraceDepth(body, m.Index) == 0)
            {
                symbols.TryAdd(m.Groups["name"].Value, m.Groups["type"].Value);
            }
        }

        foreach (Match m in NewField().Matches(body))
        {
            if (TypeScriptScanner.BraceDepth(body, m.Index) == 0)
            {
                symbols.TryAdd(m.Groups["name"].Value, m.Groups["type"].Value);
            }
        }
    }

    // ----- module subject ------------------------------------------------------------------

    private CallGraph ParseModule(string code, string? fileName)
    {
        var symbols = new Dictionary<string, string>(StringComparer.Ordinal);
        CollectModuleConstTypes(code, symbols);

        List<MemberHeader> functions = FindExportedFunctions(code);

        // Self-call detection considers every module-local function callable — including non-exported
        // helpers — even though only the exported ones become diagram entries.
        var known = new HashSet<string>(functions.Select(f => f.Name), StringComparer.Ordinal);
        foreach (Match m in FunctionName().Matches(code))
        {
            known.Add(m.Groups["name"].Value);
        }

        var methods = new List<CallGraphMethod>();
        foreach (MemberHeader fn in functions)
        {
            methods.Add(BuildMethod(code, fn, DeriveModuleName(fileName), symbols, known));
        }

        return new CallGraph(DeriveModuleName(fileName), CallGraphSubjectKind.Module, methods);
    }

    /// <summary>Finds exported <c>function</c> declarations and block/expression-bodied exported arrow consts.</summary>
    private static List<MemberHeader> FindExportedFunctions(string code)
    {
        var headers = new List<MemberHeader>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match m in ExportFunction().Matches(code))
        {
            int parenOpen = m.Index + m.Length - 1;
            (int paramStart, int paramEnd) = TypeScriptScanner.ExtractBalancedRange(code, parenOpen, '(', ')');
            (bool ok, int bStart, int bEnd) = FindBody(code, paramEnd + 1);
            if (ok && seen.Add(m.Groups["name"].Value))
            {
                headers.Add(new MemberHeader(
                    m.Groups["name"].Value, MethodVisibility.Public, IsStatic: false,
                    m.Groups["async"].Success, paramStart, paramEnd, bStart, bEnd));
            }
        }

        foreach (Match m in ExportArrowConst().Matches(code))
        {
            int parenOpen = m.Index + m.Length - 1;
            (int paramStart, int paramEnd) = TypeScriptScanner.ExtractBalancedRange(code, parenOpen, '(', ')');
            int arrow = FindArrow(code, paramEnd + 1);
            if (arrow < 0)
            {
                continue; // a `const x = foo(...)` call, not an arrow function
            }

            (bool ok, int bStart, int bEnd) = FindArrowBody(code, arrow);
            if (ok && seen.Add(m.Groups["name"].Value))
            {
                headers.Add(new MemberHeader(
                    m.Groups["name"].Value, MethodVisibility.Public, IsStatic: false,
                    m.Groups["async"].Success, paramStart, paramEnd, bStart, bEnd));
            }
        }

        headers.Sort((a, b) => a.BodyStart.CompareTo(b.BodyStart));
        return headers;
    }

    private static void CollectModuleConstTypes(string code, Dictionary<string, string> symbols)
    {
        foreach (Match m in InjectField().Matches(code))
        {
            if (TypeScriptScanner.BraceDepth(code, m.Index) == 0)
            {
                symbols.TryAdd(m.Groups["name"].Value, m.Groups["type"].Value);
            }
        }

        foreach (Match m in NewField().Matches(code))
        {
            if (TypeScriptScanner.BraceDepth(code, m.Index) == 0)
            {
                symbols.TryAdd(m.Groups["name"].Value, m.Groups["type"].Value);
            }
        }
    }

    private static string DeriveModuleName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "module";
        }

        string name = Path.GetFileName(fileName);
        int dot = name.IndexOf('.');
        if (dot > 0)
        {
            name = name[..dot];
        }

        return name.Length == 0 ? "module" : name;
    }

    // ----- per-member extraction -----------------------------------------------------------

    private CallGraphMethod BuildMethod(
        string container,
        MemberHeader header,
        string subjectName,
        IReadOnlyDictionary<string, string> outerSymbols,
        HashSet<string> knownMethods)
    {
        string paramsText = container[header.ParamStart..header.ParamEnd];
        string bodyText = container[header.BodyStart..header.BodyEnd];
        string signature = $"{header.Name}({BuildParamList(paramsText)})";

        var locals = new Dictionary<string, string>(StringComparer.Ordinal);
        CollectParameterTypes(paramsText, locals);
        CollectLocalTypes(bodyText, locals);

        List<MethodCall> calls = CollectCalls(bodyText, subjectName, outerSymbols, locals, knownMethods);

        return new CallGraphMethod(
            header.Name, signature, header.Visibility, header.IsStatic, header.IsAsync, calls);
    }

    /// <summary>Collects de-duplicated outbound calls from a member body, in first-appearance order.</summary>
    private static List<MethodCall> CollectCalls(
        string body,
        string subjectName,
        IReadOnlyDictionary<string, string> outerSymbols,
        IReadOnlyDictionary<string, string> locals,
        HashSet<string> knownMethods)
    {
        var calls = new List<MethodCall>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match m in CallSite().Matches(body))
        {
            MethodCall? call = Interpret(m, subjectName, outerSymbols, locals, knownMethods);
            if (call is null)
            {
                continue;
            }

            if (seen.Add($"{call.Kind}|{call.ReceiverKey}|{call.Method}"))
            {
                calls.Add(call);
            }
        }

        return calls;
    }

    private static MethodCall? Interpret(
        Match m,
        string subjectName,
        IReadOnlyDictionary<string, string> outerSymbols,
        IReadOnlyDictionary<string, string> locals,
        HashSet<string> knownMethods)
    {
        if (m.Groups["ctor"].Success)
        {
            string type = m.Groups["ctor"].Value;
            return CallKeywords.Contains(type) ? null : new MethodCall(type, type, CallKind.Construction, string.Empty);
        }

        if (m.Groups["m2"].Success) // this.field.method(
        {
            return ResolveCall(m.Groups["f"].Value, m.Groups["m2"].Value, outerSymbols, locals);
        }

        if (m.Groups["m1"].Success) // this.method(
        {
            return new MethodCall(subjectName, subjectName, CallKind.Self, m.Groups["m1"].Value);
        }

        if (m.Groups["ms"].Success) // super.method(
        {
            return new MethodCall(subjectName, subjectName, CallKind.Self, m.Groups["ms"].Value);
        }

        if (m.Groups["m3"].Success) // obj.method(
        {
            string obj = m.Groups["obj"].Value;
            return obj is "this" or "super" ? null : ResolveCall(obj, m.Groups["m3"].Value, outerSymbols, locals);
        }

        if (m.Groups["fn"].Success) // bare call: only a sibling method counts (else it is a library/global)
        {
            string fn = m.Groups["fn"].Value;
            return knownMethods.Contains(fn) ? new MethodCall(subjectName, subjectName, CallKind.Self, fn) : null;
        }

        return null;
    }

    /// <summary>Resolves <c>receiver.method()</c> to a collaborator call, or drops it as noise.</summary>
    private static MethodCall? ResolveCall(
        string receiver,
        string method,
        IReadOnlyDictionary<string, string> outerSymbols,
        IReadOnlyDictionary<string, string> locals)
    {
        if (IgnoredReceivers.Contains(receiver) || CallKeywords.Contains(receiver))
        {
            return null;
        }

        if (locals.TryGetValue(receiver, out string? type) || outerSymbols.TryGetValue(receiver, out type))
        {
            if (PrimitiveTypes.Contains(type))
            {
                return null; // a call on a primitive value (e.g. id.toString()) is noise
            }

            return new MethodCall(type, type, CallKind.Collaborator, method);
        }

        // Unresolved: a static/imported class (PascalCase) or an untyped local/field — show it as written.
        return new MethodCall(receiver, receiver, CallKind.Collaborator, method);
    }

    private static void CollectParameterTypes(string paramsText, Dictionary<string, string> locals)
    {
        foreach ((int start, int len) in TypeScriptScanner.SplitTopLevelRanges(paramsText))
        {
            string param = paramsText.Substring(start, len);
            Match typed = ParamType().Match(param);
            if (typed.Success)
            {
                locals.TryAdd(typed.Groups["name"].Value, TypeScriptScanner.LastSegment(typed.Groups["type"].Value));
            }
        }
    }

    private static void CollectLocalTypes(string body, Dictionary<string, string> locals)
    {
        foreach (Match m in TypedLocal().Matches(body))
        {
            locals.TryAdd(m.Groups["name"].Value, TypeScriptScanner.LastSegment(m.Groups["type"].Value));
        }

        foreach (Match m in InjectLocal().Matches(body))
        {
            locals[m.Groups["name"].Value] = m.Groups["type"].Value;
        }

        foreach (Match m in NewLocal().Matches(body))
        {
            locals[m.Groups["name"].Value] = m.Groups["type"].Value;
        }
    }

    /// <summary>Builds a display parameter list ("orderId, payload") from a parameter source range.</summary>
    private static string BuildParamList(string paramsText)
    {
        var names = new List<string>();
        foreach ((int start, int len) in TypeScriptScanner.SplitTopLevelRanges(paramsText))
        {
            string param = paramsText.Substring(start, len).Trim();
            if (param.Length == 0)
            {
                continue;
            }

            param = ParamModifiers().Replace(param, string.Empty).TrimStart();
            bool rest = param.StartsWith("...", StringComparison.Ordinal);
            if (rest)
            {
                param = param[3..].TrimStart();
            }

            if (param.Length == 0 || param[0] is '{' or '[')
            {
                names.Add("…");
                continue;
            }

            Match id = Identifier().Match(param);
            if (!id.Success || id.Value == "this")
            {
                continue;
            }

            names.Add(rest ? "..." + id.Value : id.Value);
        }

        return string.Join(", ", names);
    }

    // ----- body location -------------------------------------------------------------------

    /// <summary>
    /// Locates a method body that begins after the parameter list at <paramref name="afterParams"/>,
    /// skipping an optional return-type annotation. Returns <c>ok = false</c> for an overload/abstract
    /// signature that has no body.
    /// </summary>
    private static (bool Ok, int Start, int End) FindBody(string code, int afterParams)
    {
        int i = TypeScriptScanner.SkipWhitespace(code, afterParams);
        int brace;
        if (i < code.Length && code[i] == ':')
        {
            brace = LocateBodyBrace(code, i + 1);
        }
        else
        {
            brace = i < code.Length && code[i] == '{' ? i : -1;
        }

        if (brace < 0)
        {
            return (false, 0, 0);
        }

        (int s, int e) = TypeScriptScanner.ExtractBalancedRange(code, brace, '{', '}');
        return (true, s, e);
    }

    /// <summary>
    /// From just after the return-type colon, returns the index of the body's opening <c>{</c>, or
    /// <c>-1</c> when a top-level <c>;</c>/<c>}</c> (a signature with no body) comes first. Leading
    /// object-literal return types (<c>{ … } | …</c>) are skipped so they are not mistaken for the body.
    /// </summary>
    private static int LocateBodyBrace(string code, int from)
    {
        int i = TypeScriptScanner.SkipWhitespace(code, from);
        while (i < code.Length && code[i] == '{')
        {
            (_, int close) = TypeScriptScanner.ExtractBalancedRange(code, i, '{', '}');
            i = TypeScriptScanner.SkipWhitespace(code, close + 1);
            if (i < code.Length && code[i] is '|' or '&')
            {
                i = TypeScriptScanner.SkipWhitespace(code, i + 1);
                continue;
            }

            break;
        }

        int depth = 0;
        for (; i < code.Length; i++)
        {
            char c = code[i];
            if (c is '(' or '[' or '<')
            {
                depth++;
            }
            else if (c is ')' or ']' or '>')
            {
                if (depth > 0) depth--;
            }
            else if (depth == 0)
            {
                if (c == '{') return i;
                if (c is ';' or '}' or ',') return -1;
            }
        }

        return -1;
    }

    /// <summary>Returns the index just past the <c>=&gt;</c> following an arrow function's parameters, or -1.</summary>
    private static int FindArrow(string code, int afterParams)
    {
        int depth = 0;
        for (int i = afterParams; i < code.Length - 1; i++)
        {
            char c = code[i];
            if (c is '(' or '[' or '{' or '<')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}' or '>')
            {
                if (depth > 0) depth--;
            }
            else if (depth == 0)
            {
                if (c == '=' && code[i + 1] == '>')
                {
                    return i + 2;
                }

                if (c is ';' or '{')
                {
                    return -1; // reached a statement/body boundary without an arrow
                }
            }
        }

        return -1;
    }

    /// <summary>Locates an arrow function's body: a balanced block, or the expression up to the next top-level <c>;</c>.</summary>
    private static (bool Ok, int Start, int End) FindArrowBody(string code, int afterArrow)
    {
        int i = TypeScriptScanner.SkipWhitespace(code, afterArrow);
        if (i >= code.Length)
        {
            return (false, 0, 0);
        }

        if (code[i] == '{')
        {
            (int s, int e) = TypeScriptScanner.ExtractBalancedRange(code, i, '{', '}');
            return (true, s, e);
        }

        // An expression body ends at the first top-level ';', at an unbalanced closing bracket (we
        // have left the surrounding scope), or at a line break that begins a new top-level statement
        // — the last keeps a multi-line expression / fluent chain intact while preventing a body with
        // no terminator from swallowing the following declaration. Only ()[]{} count as nesting: '<'
        // and '>' are comparison operators here and, crucially, '>' is part of any nested '=>' arrow.
        int depth = 0;
        for (int j = i; j < code.Length; j++)
        {
            char c = code[j];
            if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}')
            {
                if (depth == 0) return (true, i, j);
                depth--;
            }
            else if (depth == 0)
            {
                if (c == ';') return (true, i, j);
                if (c == '\n' && StartsNewStatement(code, j + 1)) return (true, i, j);
            }
        }

        return (true, i, code.Length);
    }

    /// <summary>Top-level keywords (and a leading decorator) that begin a new statement, ending a preceding expression body.</summary>
    private static readonly HashSet<string> StatementStarters = new(StringComparer.Ordinal)
    {
        "export", "import", "const", "let", "var", "function", "class", "async",
        "declare", "interface", "type", "enum",
    };

    /// <summary>Whether the text at <paramref name="from"/> (after any blank lines) begins a new top-level statement.</summary>
    private static bool StartsNewStatement(string code, int from)
    {
        int i = TypeScriptScanner.SkipWhitespace(code, from);
        if (i >= code.Length || code[i] == '@')
        {
            return true;
        }

        int start = i;
        while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] is '_' or '$'))
        {
            i++;
        }

        return start < i && StatementStarters.Contains(code[start..i]);
    }

    // ----- small helpers -------------------------------------------------------------------

    private static bool IsDecoratorCall(string body, int index)
    {
        int i = index - 1;
        while (i >= 0 && char.IsWhiteSpace(body[i]))
        {
            i--;
        }

        return i >= 0 && body[i] == '@';
    }

    private static HashSet<string> SplitModifiers(string mods) =>
        new(mods.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);

    private static MethodVisibility Visibility(string name, HashSet<string> mods)
    {
        if (name.StartsWith('#') || mods.Contains("private"))
        {
            return MethodVisibility.Private;
        }

        return mods.Contains("protected") ? MethodVisibility.Protected : MethodVisibility.Public;
    }

    private static DiagramFormatException Fail(string? fileName, string reason)
    {
        string where = string.IsNullOrWhiteSpace(fileName) ? "the source" : $"'{fileName}'";
        return new DiagramFormatException($"Could not read a call graph from {where}: {reason}");
    }

    /// <summary>A located member: its name, modifiers, and the parameter/body content ranges into the container.</summary>
    private readonly record struct MemberHeader(
        string Name,
        MethodVisibility Visibility,
        bool IsStatic,
        bool IsAsync,
        int ParamStart,
        int ParamEnd,
        int BodyStart,
        int BodyEnd);

    // ----- patterns ------------------------------------------------------------------------
    // Every pattern carries a match timeout so pathological input fails fast instead of hanging.

    [GeneratedRegex(@"\b(?<export>export\s+(?:default\s+)?)?(?:abstract\s+)?class\s+(?<name>[A-Za-z_$][\w$]*)", RegexOptions.None, 2000)]
    private static partial Regex ClassDeclaration();

    [GeneratedRegex(@"(?<mods>(?:\b(?:public|private|protected|readonly|static|async|abstract|override|declare)\b\s+){0,6})(?<name>#?[A-Za-z_$][\w$]*)\s*(?:<[^>{}();]{0,200}>\s*)?\(", RegexOptions.None, 2000)]
    private static partial Regex MethodHeader();

    [GeneratedRegex(@"(?<name>#?[A-Za-z_$][\w$]*)\s*[?!]?\s*:\s*(?<type>[A-Za-z_$][\w$.]*)", RegexOptions.None, 2000)]
    private static partial Regex TypedMember();

    [GeneratedRegex(@"(?<name>#?[A-Za-z_$][\w$]*)\s*=\s*inject\s*(?:<[^>()]{0,200}>\s*)?\(\s*(?<type>[A-Za-z_$][\w$]*)", RegexOptions.None, 2000)]
    private static partial Regex InjectField();

    [GeneratedRegex(@"(?<name>#?[A-Za-z_$][\w$]*)\s*=\s*(?:await\s+)?new\s+(?<type>[A-Za-z_$][\w$]*)", RegexOptions.None, 2000)]
    private static partial Regex NewField();

    [GeneratedRegex(@"\bexport\s+(?:default\s+)?(?<async>async\s+)?function\s+(?<name>[A-Za-z_$][\w$]*)\s*(?:<[^>\n]{0,200}>\s*)?\(", RegexOptions.None, 2000)]
    private static partial Regex ExportFunction();

    [GeneratedRegex(@"\bfunction\s+(?<name>[A-Za-z_$][\w$]*)", RegexOptions.None, 2000)]
    private static partial Regex FunctionName();

    [GeneratedRegex(@"\bexport\s+const\s+(?<name>[A-Za-z_$][\w$]*)\s*(?::[^=\n]{0,200})?=\s*(?<async>async\s+)?(?:<[^>\n]{0,200}>\s*)?\(", RegexOptions.None, 2000)]
    private static partial Regex ExportArrowConst();

    [GeneratedRegex(@"\b(?:const|let|var)\s+(?<name>[A-Za-z_$][\w$]*)\s*:\s*(?<type>[A-Za-z_$][\w$.]*)", RegexOptions.None, 2000)]
    private static partial Regex TypedLocal();

    [GeneratedRegex(@"\b(?:const|let|var)\s+(?<name>[A-Za-z_$][\w$]*)\s*=\s*inject\s*(?:<[^>()]{0,200}>\s*)?\(\s*(?<type>[A-Za-z_$][\w$]*)", RegexOptions.None, 2000)]
    private static partial Regex InjectLocal();

    [GeneratedRegex(@"\b(?:const|let|var)\s+(?<name>[A-Za-z_$][\w$]*)\s*=\s*(?:await\s+)?new\s+(?<type>[A-Za-z_$][\w$]*)", RegexOptions.None, 2000)]
    private static partial Regex NewLocal();

    [GeneratedRegex(@"^(?<name>[A-Za-z_$][\w$]*)\s*[?!]?\s*:\s*(?<type>[A-Za-z_$][\w$.]*)", RegexOptions.None, 2000)]
    private static partial Regex ParamType();

    [GeneratedRegex(@"^(?:(?:public|private|protected|readonly)\s+)+", RegexOptions.None, 2000)]
    private static partial Regex ParamModifiers();

    [GeneratedRegex(@"[A-Za-z_$][\w$]*", RegexOptions.None, 2000)]
    private static partial Regex Identifier();

    // The `[!?]?\.` between receiver and member matches a plain dot, optional chaining (`?.`), and
    // a non-null-assertion access (`!.`), all of which are ordinary TypeScript member access.
    [GeneratedRegex(
        @"\bnew\s+(?<ctor>[A-Za-z_$][\w$]*)" +
        @"|\bthis[!?]?\.(?<f>[A-Za-z_$][\w$]*)[!?]?\.(?<m2>[A-Za-z_$][\w$]*)\s*\(" +
        @"|\bthis[!?]?\.(?<m1>[A-Za-z_$][\w$]*)\s*\(" +
        @"|\bsuper[!?]?\.(?<ms>[A-Za-z_$][\w$]*)\s*\(" +
        @"|\b(?<obj>[A-Za-z_$][\w$]*)[!?]?\.(?<m3>[A-Za-z_$][\w$]*)\s*\(" +
        @"|\b(?<fn>[A-Za-z_$][\w$]*)\s*\(",
        RegexOptions.None, 2000)]
    private static partial Regex CallSite();
}
