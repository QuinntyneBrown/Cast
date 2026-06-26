using System;
using System.Collections.Generic;
using System.Text;

namespace Cast.Cli.Services;

/// <summary>
/// Shared, comment/string-aware text utilities for the TypeScript scanners (the <c>ng</c> and
/// <c>calls</c> commands). The centrepiece is <see cref="Sanitize"/>, which blanks comments and the
/// contents of string/template/regex literals to spaces while preserving every character index, so
/// downstream pattern matching cannot trip on text that only <em>looks</em> like code and the
/// structural braces/parentheses of real code stay intact for balance matching. The remaining
/// helpers operate on that sanitised copy (or any code) to walk balanced delimiters.
/// </summary>
internal static class TypeScriptScanner
{
    /// <summary>
    /// Returns a copy of <paramref name="source"/> of the SAME length, with line and block comments
    /// and the contents of <c>'…'</c>, <c>"…"</c>, <c>`…`</c>, and <c>/…/</c> regex literals replaced
    /// by spaces (newlines preserved). Equal length keeps every index aligned with the original, so
    /// the real text of a symbol can be read back at the matched position, and the structural braces
    /// and parentheses of real code stay intact for balance matching.
    /// </summary>
    public static string Sanitize(string source)
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
    /// Conservative: never misclassifies division as a regex.
    /// </summary>
    private static bool IsRegexStart(char prev) =>
        prev is '\0' or '(' or ',' or '=' or ':' or '[' or '!' or '&' or '|' or '?' or '{' or '}'
            or ';' or '<' or '>' or '+' or '-' or '*' or '%' or '^' or '~';

    /// <summary>Returns the brace range of the first <c>{ … }</c> block at or after <paramref name="fromIndex"/>.</summary>
    public static (int Start, int End) ExtractBraceRange(string code, int fromIndex)
    {
        int open = code.IndexOf('{', fromIndex);
        return open < 0 ? (fromIndex, fromIndex) : ExtractBalancedRange(code, open, '{', '}');
    }

    /// <summary>Returns the [start, end) content range between the delimiter at <paramref name="openIndex"/> and its match.</summary>
    public static (int Start, int End) ExtractBalancedRange(string code, int openIndex, char open, char close)
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

    /// <summary>
    /// Finds a function/method body brace range starting just after its parameter list, skipping an
    /// optional return-type annotation first. A named/generic/array return type contains no
    /// top-level <c>{</c>, so the body is found directly; an object-literal return type
    /// (<c>): { … } { body }</c>) is balance-skipped so it is not mistaken for the body.
    /// </summary>
    public static (int Start, int End) ExtractFunctionBody(string code, int afterParams)
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

    public static int SkipWhitespace(string code, int from)
    {
        int i = from;
        while (i < code.Length && char.IsWhiteSpace(code[i]))
        {
            i++;
        }

        return i;
    }

    /// <summary>Net brace depth of <paramref name="text"/> up to (not including) <paramref name="index"/>.</summary>
    public static int BraceDepth(string text, int index)
    {
        int depth = 0;
        for (int i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}') depth--;
        }

        return depth;
    }

    /// <summary>Splits a delimited list on top-level commas, returning (start, length) ranges into <paramref name="list"/>.</summary>
    public static IEnumerable<(int Start, int Len)> SplitTopLevelRanges(string list)
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

    /// <summary>Returns the last dotted segment of <paramref name="type"/> (e.g. <c>ns.Foo</c> -&gt; <c>Foo</c>).</summary>
    public static string LastSegment(string type)
    {
        int dot = type.LastIndexOf('.');
        return dot < 0 ? type : type[(dot + 1)..];
    }
}
