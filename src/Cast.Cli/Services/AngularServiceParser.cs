using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Cast.Cli.Diagnostics;
using Cast.Cli.Models;

namespace Cast.Cli.Services;

/// <summary>
/// Default <see cref="IAngularServiceParser"/>. Works in three steps: (1) <em>sanitise</em> the
/// source into a length-preserving copy where comments and the contents of string, template, and
/// regular-expression literals are blanked to spaces — so pattern matching cannot trip on text that
/// only looks like code, while every character keeps its original index; (2) locate the primary
/// consumer — preferring a class that actually carries a DI decorator (<c>@Injectable</c> and
/// friends), else a matching-suffix class, else an exported functional provider; (3) collect its
/// injected dependencies from field-level <c>inject(X)</c> calls and constructor parameters
/// (honouring <c>@Inject(...)</c> and <c>@Optional()</c>), classifying each as a class (service) or
/// an <c>InjectionToken</c>.
/// </summary>
/// <remarks>
/// This is intentionally a focused scanner, not a full TypeScript parser: it understands the
/// regular, well-known shapes Angular DI takes and ignores the rest, which keeps <c>cast</c> a
/// single self-contained .NET tool. Because the sanitised copy keeps character indices aligned with
/// the original, facts that depend on literal text (a token's string value, <c>providedIn: 'root'</c>)
/// are read back from the original source at the matched position. All regular expressions carry a
/// match timeout and the input is size-bounded, so pathological input fails fast rather than hanging.
/// </remarks>
public sealed partial class AngularServiceParser : IAngularServiceParser
{
    /// <summary>Upper bound on input size; larger files are rejected rather than scanned.</summary>
    private const int MaxSourceLength = 2_000_000;

    /// <summary>TypeScript built-in/structural types that are never Angular DI dependencies.</summary>
    private static readonly HashSet<string> NonInjectableTypes = new(StringComparer.Ordinal)
    {
        "Date", "Array", "Promise", "Map", "Set", "WeakMap", "WeakSet", "RegExp", "Error",
        "Object", "Function", "String", "Number", "Boolean", "Symbol", "BigInt",
    };

    private static readonly string[] DiDecorators = { "@Injectable", "@Component", "@Directive", "@Pipe" };

    /// <inheritdoc />
    public AngularService Parse(string source, string? fileName = null)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw Fail(fileName, "the file is empty.");
        }

        if (source.Length > MaxSourceLength)
        {
            throw Fail(fileName, $"the file is too large to inspect ({source.Length:N0} characters).");
        }

        string code = Sanitize(source);
        HashSet<string> localTokens = CollectLocalTokens(code);

        try
        {
            ClassConsumer? selected = SelectClass(code);

            // A class carrying a DI decorator (@Injectable/@Component/@Directive/@Pipe) is the
            // primary consumer and wins outright — in Angular only a decorated class actually
            // participates in DI. Any other class (a DTO, or even one merely *named* like a service
            // but missing its decorator) is a fallback, tried AFTER functional consumers, so a real
            // injecting function in the same file is not shadowed by it.
            if (selected is ClassConsumer cls && cls.Decorator is not null)
            {
                return ParseClass(source, code, cls, localTokens);
            }

            AngularService? functional = TryParseFunctionalConsumer(code, localTokens);
            if (functional is not null)
            {
                return functional;
            }

            if (selected is ClassConsumer fallback)
            {
                return ParseClass(source, code, fallback, localTokens);
            }
        }
        catch (RegexMatchTimeoutException)
        {
            throw Fail(fileName, "parsing timed out; the file is unusually large or has an unusual shape.");
        }

        throw Fail(
            fileName,
            "no Angular construct that uses dependency injection was found. Point --service at a file that exports " +
            "an @Injectable/@Component/@Directive/@Pipe class, a functional provider " +
            "(e.g. 'export const xInterceptor: HttpInterceptorFn = ...'), or a function that calls inject(...).");
    }

    // ----- consumer selection --------------------------------------------------------------

    /// <summary>A chosen class consumer: its declaration match, the decorator on it, and its name.</summary>
    private readonly record struct ClassConsumer(string Name, int BodyScanFrom, string? Decorator, int? InjectableIndex);

    /// <summary>
    /// Picks the primary class consumer: a class carrying a DI decorator if any, else a class whose
    /// name matches a known Angular suffix, else the first exported class. Associates each decorator
    /// with the class that follows it (decorators precede their class) so the correct one is chosen
    /// even when a DTO/helper class appears earlier in the file.
    /// </summary>
    private static ClassConsumer? SelectClass(string code)
    {
        var classes = new List<Match>();
        foreach (Match m in ClassDeclaration().Matches(code))
        {
            classes.Add(m);
        }

        if (classes.Count == 0)
        {
            return null;
        }

        // Map each class to the decorator/@Injectable that immediately precedes it.
        var decoratorByClass = new Dictionary<int, string>();
        var injectableByClass = new Dictionary<int, int>();
        foreach (Match deco in DiDecorator().Matches(code))
        {
            Match? owner = null;
            foreach (Match c in classes)
            {
                if (c.Index > deco.Index)
                {
                    owner = c;
                    break;
                }
            }

            if (owner is null)
            {
                continue;
            }

            string name = deco.Groups["deco"].Value; // capture excludes the '@'
            decoratorByClass.TryAdd(owner.Index, name);
            if (name == "Injectable")
            {
                injectableByClass.TryAdd(owner.Index, deco.Index);
            }
        }

        Match chosen =
            classes.Find(c => decoratorByClass.ContainsKey(c.Index))
            ?? classes.Find(c => HasKnownSuffix(c.Groups["name"].Value))
            ?? classes[0];

        decoratorByClass.TryGetValue(chosen.Index, out string? decorator);
        int? injectable = injectableByClass.TryGetValue(chosen.Index, out int idx) ? idx : null;

        return new ClassConsumer(chosen.Groups["name"].Value, chosen.Index + chosen.Length, decorator, injectable);
    }

    private static AngularService ParseClass(string source, string code, ClassConsumer cls, HashSet<string> localTokens)
    {
        (int bodyStart, int bodyEnd) = ExtractBraceRange(code, cls.BodyScanFrom);
        string body = code[bodyStart..bodyEnd];

        var dependencies = new List<AngularDependency>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // Field-level inject() lives at brace-depth 0 of the class body; deeper inject() calls are
        // inside method/constructor bodies and run later, not at construction, so they are skipped.
        foreach (Match call in InjectCall().Matches(body))
        {
            if (BraceDepth(body, call.Index) != 0)
            {
                continue;
            }

            string id = call.Groups["id"].Value;
            bool optional = OptionalOption().IsMatch(call.Groups["rest"].Value);
            Add(dependencies, seen, new AngularDependency(id, ClassifyDependency(id, forceToken: false, localTokens), optional));
        }

        CollectConstructorDependencies(source, code, bodyStart, bodyEnd, localTokens, dependencies, seen);

        (bool isSingleton, string? providedIn) = ReadProvidedIn(source, code, cls.InjectableIndex);
        ConsumerKind kind = ClassifyClass(cls.Name, cls.Decorator);

        return new AngularService(cls.Name, kind, IsFunctional: false, isSingleton, providedIn, dependencies);
    }

    /// <summary>
    /// Tries the functional-consumer shapes in order: an exported arrow function (a functional
    /// provider such as an interceptor/guard/resolver) and then an exported <c>function</c>
    /// declaration. Returns the first that is a genuine DI consumer.
    /// </summary>
    private static AngularService? TryParseFunctionalConsumer(string code, HashSet<string> localTokens)
    {
        Match arrow = ArrowConst().Match(code);
        if (arrow.Success)
        {
            AngularService? fn = TryParseArrowFunction(code, arrow, localTokens);
            if (fn is not null)
            {
                return fn;
            }
        }

        // Try every exported function declaration in turn (a file may have helper functions before
        // the one that actually injects), returning the first that is a genuine DI consumer.
        foreach (Match declaration in FunctionDeclaration().Matches(code))
        {
            AngularService? fn = TryParseFunctionDeclaration(code, declaration, localTokens);
            if (fn is not null)
            {
                return fn;
            }
        }

        return null;
    }

    private static AngularService? TryParseArrowFunction(string code, Match constMatch, HashSet<string> localTokens)
    {
        string name = constMatch.Groups["name"].Value;
        string type = constMatch.Groups["type"].Value;

        // An arrow function may be expression-bodied (no { } block), so its inject() calls are
        // collected from the whole sanitised file; such files realistically export one function.
        List<AngularDependency> dependencies = CollectInjectCalls(code, localTokens);

        // Treat the arrow function as a DI consumer only when it is a recognised functional-DI type
        // or it actually injects something — otherwise it is just an exported function.
        ConsumerKind kind = ClassifyFunctional(type, name);
        if (kind == ConsumerKind.Class && dependencies.Count == 0)
        {
            return null;
        }

        return new AngularService(name, kind, IsFunctional: true, IsSingleton: false, ProvidedIn: null, dependencies);
    }

    /// <summary>
    /// Parses an exported <c>function</c> declaration that runs in an injection context (a factory,
    /// an <c>APP_INITIALIZER</c>, a functional guard/resolver written as a declaration, …). Its
    /// <c>inject()</c> calls are read from its own body, so other functions in the file do not bleed
    /// in. A plain function that injects nothing and has no functional-DI name is not a consumer.
    /// </summary>
    private static AngularService? TryParseFunctionDeclaration(string code, Match declaration, HashSet<string> localTokens)
    {
        string name = declaration.Groups["name"].Value;

        int paramOpen = code.IndexOf('(', declaration.Index);
        if (paramOpen < 0)
        {
            return null;
        }

        (_, int paramEnd) = ExtractBalancedRange(code, paramOpen, '(', ')');
        (int bodyStart, int bodyEnd) = ExtractFunctionBody(code, paramEnd + 1);
        if (bodyEnd <= bodyStart)
        {
            return null;
        }

        List<AngularDependency> dependencies = CollectInjectCalls(code[bodyStart..bodyEnd], localTokens);

        ConsumerKind kind = ClassifyFunctional(type: string.Empty, name);
        if (kind == ConsumerKind.Class && dependencies.Count == 0)
        {
            return null;
        }

        return new AngularService(name, kind, IsFunctional: true, IsSingleton: false, ProvidedIn: null, dependencies);
    }

    /// <summary>Collects de-duplicated <c>inject(X)</c> dependencies from a region of sanitised code.</summary>
    private static List<AngularDependency> CollectInjectCalls(string region, HashSet<string> localTokens)
    {
        var dependencies = new List<AngularDependency>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match call in InjectCall().Matches(region))
        {
            string id = call.Groups["id"].Value;
            bool optional = OptionalOption().IsMatch(call.Groups["rest"].Value);
            Add(dependencies, seen, new AngularDependency(id, ClassifyDependency(id, forceToken: false, localTokens), optional));
        }

        return dependencies;
    }

    /// <summary>Parses constructor parameters, honouring <c>@Inject</c> (incl. dotted and string tokens) and <c>@Optional</c>.</summary>
    private static void CollectConstructorDependencies(
        string source,
        string code,
        int bodyStart,
        int bodyEnd,
        HashSet<string> localTokens,
        List<AngularDependency> dependencies,
        HashSet<string> seen)
    {
        Match ctor = ConstructorKeyword().Match(code[bodyStart..bodyEnd]);
        if (!ctor.Success)
        {
            return;
        }

        int open = code.IndexOf('(', bodyStart + ctor.Index);
        if (open < 0 || open >= bodyEnd)
        {
            return;
        }

        (int paramStart, int paramEnd) = ExtractBalancedRange(code, open, '(', ')');
        string paramsCode = code[paramStart..paramEnd];
        string paramsSource = source[paramStart..paramEnd];

        foreach ((int start, int len) in SplitTopLevelRanges(paramsCode))
        {
            string param = paramsCode.Substring(start, len);
            string paramSource = paramsSource.Substring(start, len);
            if (param.Trim().Length == 0)
            {
                continue;
            }

            bool optional = OptionalDecorator().IsMatch(param);

            Match inject = InjectDecorator().Match(param);
            if (inject.Success)
            {
                string token = ReadInjectArgument(param, paramSource, inject.Index);
                if (token.Length > 0)
                {
                    Add(dependencies, seen, new AngularDependency(token, ClassifyDependency(token, forceToken: true, localTokens), optional));
                }

                continue;
            }

            Match typed = ParamType().Match(param);
            if (!typed.Success)
            {
                continue;
            }

            string type = LastSegment(typed.Groups["type"].Value);
            if (IsInjectableType(type))
            {
                Add(dependencies, seen, new AngularDependency(type, ClassifyDependency(type, forceToken: false, localTokens), optional));
            }
        }
    }

    /// <summary>Reads the argument of a constructor <c>@Inject(...)</c> from the original source (so string-literal tokens survive).</summary>
    private static string ReadInjectArgument(string paramCode, string paramSource, int injectIndex)
    {
        int open = paramCode.IndexOf('(', injectIndex);
        if (open < 0)
        {
            return string.Empty;
        }

        (int argStart, int argEnd) = ExtractBalancedRange(paramCode, open, '(', ')');
        string arg = paramSource[argStart..argEnd].Trim();
        if (arg.Length == 0)
        {
            return string.Empty;
        }

        // A string-literal token: '@Inject('app.config')' → app.config.
        if (arg[0] is '\'' or '"' or '`')
        {
            return arg.Trim('\'', '"', '`').Trim();
        }

        // An identifier or member expression: '@Inject(CONFIG.TOKEN)' → keep the full path.
        Match id = TokenReference().Match(arg);
        return id.Success ? id.Value : string.Empty;
    }

    /// <summary>Reads <c>@Injectable({ providedIn: '…' })</c> for the chosen class from the original source.</summary>
    private static (bool IsSingleton, string? ProvidedIn) ReadProvidedIn(string source, string code, int? injectableIndex)
    {
        if (injectableIndex is not int index)
        {
            return (false, null);
        }

        int open = code.IndexOf('(', index);
        if (open < 0)
        {
            return (false, null);
        }

        // Read the decorator argument from the original source, brace-balanced, so a nested object
        // before providedIn does not truncate it and the 'root' string literal is preserved.
        (int argStart, int argEnd) = ExtractBalancedRange(code, open, '(', ')');
        Match scope = ProvidedInValue().Match(source[argStart..argEnd]);
        if (!scope.Success)
        {
            return (false, null);
        }

        string value = scope.Groups["scope"].Value;
        return (value is "root" or "platform", value);
    }

    // ----- classification ------------------------------------------------------------------

    private static ConsumerKind ClassifyClass(string name, string? decorator)
    {
        if (decorator == "Component" || EndsWith(name, "Component")) return ConsumerKind.Component;
        if (decorator == "Directive" || EndsWith(name, "Directive")) return ConsumerKind.Directive;
        if (decorator == "Pipe" || EndsWith(name, "Pipe")) return ConsumerKind.Pipe;
        if (EndsWith(name, "Interceptor")) return ConsumerKind.Interceptor;
        if (EndsWith(name, "Guard")) return ConsumerKind.Guard;
        if (EndsWith(name, "Resolver")) return ConsumerKind.Resolver;
        if (EndsWith(name, "Store")) return ConsumerKind.Store;
        return ConsumerKind.Service;
    }

    private static ConsumerKind ClassifyFunctional(string type, string name)
    {
        if (type.StartsWith("HttpInterceptorFn", StringComparison.Ordinal) || EndsWith(name, "Interceptor"))
            return ConsumerKind.Interceptor;
        if (type.StartsWith("Can", StringComparison.Ordinal) || EndsWith(name, "Guard"))
            return ConsumerKind.Guard;
        if (type.StartsWith("ResolveFn", StringComparison.Ordinal) || EndsWith(name, "Resolver"))
            return ConsumerKind.Resolver;
        return ConsumerKind.Class;
    }

    /// <summary>
    /// A dependency is a token when it is injected through <c>@Inject(...)</c>, declared as an
    /// <c>InjectionToken</c> in this file, or written in <c>SCREAMING_CASE</c> (the universal
    /// convention for tokens, e.g. <c>API_BASE_URL</c>, <c>DOCUMENT</c>). Everything else — a
    /// PascalCase identifier such as <c>HttpClient</c> or <c>Router</c> — is a class the injector
    /// constructs.
    /// </summary>
    private static DependencyKind ClassifyDependency(string name, bool forceToken, HashSet<string> localTokens)
    {
        if (forceToken || localTokens.Contains(name) || TokenName().IsMatch(name))
        {
            return DependencyKind.Token;
        }

        return DependencyKind.Service;
    }

    /// <summary>
    /// A bare (non-<c>@Inject</c>) constructor parameter is an injected dependency only when its type
    /// is an injectable class identifier — PascalCase and not a TypeScript primitive or built-in.
    /// Primitives and type aliases (lower-case initial) are configuration, not DI.
    /// </summary>
    private static bool IsInjectableType(string type)
    {
        if (type.Length == 0 || !char.IsUpper(type[0]))
        {
            return false;
        }

        return !NonInjectableTypes.Contains(type);
    }

    private static bool HasKnownSuffix(string name) =>
        EndsWith(name, "Service") || EndsWith(name, "Store") || EndsWith(name, "Component") ||
        EndsWith(name, "Directive") || EndsWith(name, "Pipe") || EndsWith(name, "Interceptor") ||
        EndsWith(name, "Guard") || EndsWith(name, "Resolver");

    private static HashSet<string> CollectLocalTokens(string code)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in LocalInjectionToken().Matches(code))
        {
            tokens.Add(match.Groups["name"].Value);
        }

        return tokens;
    }

    // ----- text helpers --------------------------------------------------------------------

    /// <summary>
    /// Returns a copy of <paramref name="source"/> of the SAME length, with line and block comments
    /// and the contents of <c>'…'</c>, <c>"…"</c>, <c>`…`</c>, and <c>/…/</c> regex literals replaced
    /// by spaces (newlines preserved). Equal length keeps every index aligned with the original, so
    /// the real text of a token or option can be read back at the matched position, and the
    /// structural braces and parentheses of real code stay intact for balance matching.
    /// </summary>
    private static string Sanitize(string source)
    {
        var sb = new StringBuilder(source.Length);
        int i = 0;
        int n = source.Length;
        char prevSignificant = '\0';

        void Blank(int count)
        {
            for (int k = 0; k < count; k++) sb.Append(' ');
        }

        while (i < n)
        {
            char c = source[i];

            if (c == '/' && i + 1 < n && source[i + 1] == '/')
            {
                while (i < n && source[i] != '\n') { sb.Append(' '); i++; }
                continue;
            }

            if (c == '/' && i + 1 < n && source[i + 1] == '*')
            {
                while (i < n && !(i + 1 < n && source[i] == '*' && source[i + 1] == '/'))
                {
                    sb.Append(source[i] == '\n' ? '\n' : ' ');
                    i++;
                }

                if (i < n) { Blank(Math.Min(2, n - i)); i = Math.Min(i + 2, n); }
                continue;
            }

            if (c is '\'' or '"')
            {
                sb.Append(c);
                i++;
                while (i < n && source[i] != c && source[i] != '\n')
                {
                    if (source[i] == '\\' && i + 1 < n) { Blank(2); i += 2; continue; }
                    sb.Append(' ');
                    i++;
                }

                if (i < n && source[i] == c) { sb.Append(c); i++; }
                prevSignificant = c;
                continue;
            }

            if (c == '`')
            {
                sb.Append('`');
                i++;
                while (i < n)
                {
                    char d = source[i];
                    if (d == '\\' && i + 1 < n) { Blank(2); i += 2; continue; }
                    if (d == '`') { sb.Append('`'); i++; break; }
                    if (d == '$' && i + 1 < n && source[i + 1] == '{')
                    {
                        Blank(2);
                        i += 2;
                        int braces = 1;
                        while (i < n && braces > 0)
                        {
                            char e = source[i];
                            if (e == '{') braces++;
                            else if (e == '}') braces--;
                            sb.Append(e == '\n' ? '\n' : ' ');
                            i++;
                        }

                        continue;
                    }

                    sb.Append(d == '\n' ? '\n' : ' ');
                    i++;
                }

                prevSignificant = '`';
                continue;
            }

            // Regex literal: a '/' in expression position (not division). Heuristic: the previous
            // significant character is not one that can end an operand.
            if (c == '/' && IsRegexStart(prevSignificant))
            {
                sb.Append('/');
                i++;
                bool inClass = false;
                while (i < n && source[i] != '\n')
                {
                    char d = source[i];
                    if (d == '\\' && i + 1 < n) { Blank(2); i += 2; continue; }
                    if (d == '[') inClass = true;
                    else if (d == ']') inClass = false;
                    else if (d == '/' && !inClass) { sb.Append('/'); i++; break; }
                    sb.Append(' ');
                    i++;
                }

                prevSignificant = '/';
                continue;
            }

            sb.Append(c);
            if (!char.IsWhiteSpace(c))
            {
                prevSignificant = c;
            }

            i++;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Whether a <c>/</c> following <paramref name="prev"/> begins a regex literal rather than a
    /// division. A regex can only appear where an operand is expected, i.e. after an operator or an
    /// opening punctuation — never after a value (identifier, number, string, <c>)</c>, <c>]</c>).
    /// This is conservative: it never misclassifies division as a regex, at the cost of not
    /// recognising the rare keyword-prefixed form (<c>return /re/</c>), whose contents are harmless
    /// anyway because class dependency scanning only reads field initialisers (brace-depth 0).
    /// </summary>
    private static bool IsRegexStart(char prev) =>
        prev is '\0' or '(' or ',' or '=' or ':' or '[' or '!' or '&' or '|' or '?' or '{' or '}'
            or ';' or '<' or '>' or '+' or '-' or '*' or '%' or '^' or '~';

    private static (int Start, int End) ExtractBraceRange(string code, int fromIndex)
    {
        int open = code.IndexOf('{', fromIndex);
        return open < 0 ? (fromIndex, fromIndex) : ExtractBalancedRange(code, open, '{', '}');
    }

    /// <summary>
    /// Finds a function's body brace range starting just after its parameter list, skipping an
    /// optional return-type annotation first. A named/generic/array return type contains no
    /// top-level <c>{</c>, so the body is found directly; an object-literal return type
    /// (<c>): { … } { body }</c>) is balance-skipped so it is not mistaken for the body.
    /// </summary>
    private static (int Start, int End) ExtractFunctionBody(string code, int afterParams)
    {
        int i = SkipWhitespace(code, afterParams);
        if (i < code.Length && code[i] == ':')
        {
            i = SkipReturnType(code, i + 1);
        }

        return ExtractBraceRange(code, i);
    }

    /// <summary>
    /// Advances past a return-type annotation that begins with one or more object-literal segments
    /// (e.g. <c>{ a: number }</c>, or <c>{ a } &amp; { b }</c>), so the following brace is the body.
    /// Named/generic/array types (no leading <c>{</c>) are left for <see cref="ExtractBraceRange"/>.
    /// </summary>
    private static int SkipReturnType(string code, int from)
    {
        int i = SkipWhitespace(code, from);
        while (i < code.Length && code[i] == '{')
        {
            (_, int close) = ExtractBalancedRange(code, i, '{', '}');
            i = SkipWhitespace(code, close + 1);
            if (i < code.Length && (code[i] is '|' or '&'))
            {
                i = SkipWhitespace(code, i + 1);
                continue;
            }

            break;
        }

        return i;
    }

    private static int SkipWhitespace(string code, int from)
    {
        int i = from;
        while (i < code.Length && char.IsWhiteSpace(code[i]))
        {
            i++;
        }

        return i;
    }

    /// <summary>Returns the [start, end) content range between the delimiter at <paramref name="openIndex"/> and its match.</summary>
    private static (int Start, int End) ExtractBalancedRange(string code, int openIndex, char open, char close)
    {
        int depth = 0;
        for (int i = openIndex; i < code.Length; i++)
        {
            char c = code[i];
            if (c == open)
            {
                depth++;
            }
            else if (c == close)
            {
                depth--;
                if (depth == 0)
                {
                    return (openIndex + 1, i);
                }
            }
        }

        return (openIndex + 1, code.Length);
    }

    /// <summary>Net brace depth of <paramref name="text"/> up to (not including) <paramref name="index"/>.</summary>
    private static int BraceDepth(string text, int index)
    {
        int depth = 0;
        for (int i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}') depth--;
        }

        return depth;
    }

    /// <summary>Splits a parameter list on top-level commas, returning (start, length) ranges.</summary>
    private static IEnumerable<(int Start, int Len)> SplitTopLevelRanges(string list)
    {
        var ranges = new List<(int, int)>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < list.Length; i++)
        {
            char c = list[i];
            switch (c)
            {
                case '(' or '[' or '{' or '<':
                    depth++;
                    break;
                case ')' or ']' or '}' or '>':
                    if (depth > 0) depth--;
                    break;
                case ',' when depth == 0:
                    ranges.Add((start, i - start));
                    start = i + 1;
                    break;
            }
        }

        ranges.Add((start, list.Length - start));
        return ranges;
    }

    private static string LastSegment(string type)
    {
        int dot = type.LastIndexOf('.');
        return dot < 0 ? type : type[(dot + 1)..];
    }

    private static void Add(List<AngularDependency> dependencies, HashSet<string> seen, AngularDependency dependency)
    {
        if (seen.Add(dependency.Name))
        {
            dependencies.Add(dependency);
        }
    }

    private static bool EndsWith(string name, string suffix) => name.EndsWith(suffix, StringComparison.Ordinal);

    private static DiagramFormatException Fail(string? fileName, string reason)
    {
        string where = string.IsNullOrWhiteSpace(fileName) ? "the source" : $"'{fileName}'";
        return new DiagramFormatException($"Could not read an Angular dependency-injection graph from {where}: {reason}");
    }

    // ----- patterns ------------------------------------------------------------------------
    // Every pattern carries a match timeout so pathological input fails fast instead of hanging.

    [GeneratedRegex(@"\bexport\s+(?:default\s+)?(?:abstract\s+)?class\s+(?<name>[A-Za-z_$][\w$]*)", RegexOptions.None, 2000)]
    private static partial Regex ClassDeclaration();

    [GeneratedRegex(@"@(?<deco>Injectable|Component|Directive|Pipe)\b", RegexOptions.None, 2000)]
    private static partial Regex DiDecorator();

    // No overlapping unbounded quantifiers: the optional type (with optional bounded generic/array)
    // is followed directly by '=' then the arrow '(' — so a typed const with no initialiser fails
    // quickly instead of backtracking catastrophically.
    [GeneratedRegex(@"\bexport\s+const\s+(?<name>[A-Za-z_$][\w$]*)\s*(?::\s*(?<type>[A-Za-z_$][\w$]*)(?:<[^>\n]{0,200}>)?(?:\[\])?\s*)?=\s*(?:async\s*)?(?:<[^>\n]{0,200}>\s*)?\(", RegexOptions.None, 2000)]
    private static partial Regex ArrowConst();

    // Exported function declaration: `export function name(`, optionally `default`/`async` and a
    // bounded generic parameter list. The match ends at the parameter '(' so the body is located by
    // brace balancing from there.
    [GeneratedRegex(@"\bexport\s+(?:default\s+)?(?:async\s+)?function\s+(?<name>[A-Za-z_$][\w$]*)\s*(?:<[^>\n]{0,200}>\s*)?\(", RegexOptions.None, 2000)]
    private static partial Regex FunctionDeclaration();

    [GeneratedRegex(@"\binject\s*(?:<[^()]{0,400}>\s*)?\(\s*(?<id>[A-Za-z_$][\w$]*)(?<rest>[^)]*)\)", RegexOptions.None, 2000)]
    private static partial Regex InjectCall();

    [GeneratedRegex(@"optional\s*:\s*true", RegexOptions.None, 2000)]
    private static partial Regex OptionalOption();

    [GeneratedRegex(@"\bconstructor\s*\(", RegexOptions.None, 2000)]
    private static partial Regex ConstructorKeyword();

    [GeneratedRegex(@"@Inject\b", RegexOptions.None, 2000)]
    private static partial Regex InjectDecorator();

    [GeneratedRegex(@"@Optional\s*\(", RegexOptions.None, 2000)]
    private static partial Regex OptionalDecorator();

    [GeneratedRegex(@":\s*(?<type>[A-Za-z_$][\w$.]*)", RegexOptions.None, 2000)]
    private static partial Regex ParamType();

    [GeneratedRegex(@"[A-Za-z_$][\w$.]*", RegexOptions.None, 2000)]
    private static partial Regex TokenReference();

    [GeneratedRegex(@"providedIn\s*:\s*['""](?<scope>[A-Za-z]+)['""]", RegexOptions.None, 2000)]
    private static partial Regex ProvidedInValue();

    [GeneratedRegex(@"\bconst\s+(?<name>[A-Za-z_$][\w$]*)\s*(?::[^=\n]{0,400})?=\s*new\s+InjectionToken", RegexOptions.None, 2000)]
    private static partial Regex LocalInjectionToken();

    [GeneratedRegex(@"^[A-Z][A-Z0-9_]*$", RegexOptions.None, 2000)]
    private static partial Regex TokenName();
}
