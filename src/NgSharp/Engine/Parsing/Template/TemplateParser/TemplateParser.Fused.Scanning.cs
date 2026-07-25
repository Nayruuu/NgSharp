using System;
using System.Text;
using System.Collections.Generic;

using NgSharp.Ast;
using NgSharp.Rendering;

namespace NgSharp.Parsing;

// The fused parser's scanning layer: the span fast path, the static-open-tag serializer, and the
// low-level scan primitives (suffixed F, same rules as the HtmlLexer).
internal static partial class TemplateParser
{
    // Span fast path: when the tag is provably CANONICAL (all-lowercase names, every attribute exactly
    // ` name="value"`, no '[' anywhere, no '&' in values, no self-closing slash) and carries no special
    // semantics (rawtext/ng-*/component), its span is appended VERBATIM. Anything non-trivial bails out unconsumed.
    private static bool TryEmitTrivialTag(FoldEmitter emitter, string source, ref int pos, List<string> openNames, HashSet<string> components, HashSet<string> directives)
    {
        var j = pos + 1;

        var nameStart = j;
        while (j < source.Length)
        {
            var ch = source[j];
            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '-')
            {
                j++;
                continue;
            }

            break;
        }

        if (j == nameStart || j >= source.Length)
        {
            return false;
        }

        var nameLen = j - nameStart;

        while (true)
        {
            if (j >= source.Length)
            {
                return false;
            }

            if (source[j] == '>')
            {
                break;
            }

            if (source[j] != ' ')
            {
                return false;
            }

            j++;

            // STRICT ASCII whitelist — non-ASCII letters would be changed by the full path's ToLowerInvariant.
            var attrStart = j;
            while (j < source.Length)
            {
                var ch = source[j];
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '-' || ch == '_')
                {
                    j++;
                    continue;
                }

                break;
            }

            if (j == attrStart || j + 1 >= source.Length || source[j] != '=' || source[j + 1] != '"')
            {
                return false;
            }

            j += 2;
            while (j < source.Length && source[j] != '"')
            {
                if (source[j] == '&')
                {
                    return false;
                }

                j++;
            }

            if (j >= source.Length)
            {
                return false;
            }

            j++;
        }

        var name = source.Substring(nameStart, nameLen);
        if (IsRawTextF(name) || name == "ng-container" || name == "ng-template" || components.Contains(name))
        {
            return false;
        }

        var end = j;
        emitter.Const.Append(source, pos, end + 1 - pos);
        pos = end + 1;

        if (HtmlVoidElements.Contains(name))
        {
            return true;
        }

        openNames.Add(name);
        EmitChildrenFused(emitter, source, ref pos, openNames, components, directives);
        openNames.RemoveAt(openNames.Count - 1);
        emitter.Const.Append("</").Append(name).Append('>');

        return true;
    }

    private static void AppendStaticOpenTagF(StringBuilder output, string tagName, List<AttributeNode> attributes)
    {
        output.Append('<').Append(tagName);
        if (attributes is not null)
        {
            for (var k = 0; k < attributes.Count; k++)
            {
                output.Append(' ').Append(attributes[k].Name).Append("=\"")
                      .Append(HtmlEscaper.EscapeAttributeText(attributes[k].Value)).Append('"');
            }
        }

        output.Append('>');
    }

    private static void ReadTagHeaderF(string source, ref int pos, out string name, out List<HtmlAttribute> attributes, out bool selfClosing)
    {
        var j = pos + 1;
        var nameStart = j;
        while (j < source.Length && IsWsF(source[j]) == false && source[j] != '>' && source[j] != '/')
        {
            j++;
        }

        name = source.Substring(nameStart, j - nameStart);
        attributes = null;
        selfClosing = false;

        while (j < source.Length)
        {
            while (j < source.Length && IsWsF(source[j]))
            {
                j++;
            }

            if (j >= source.Length)
            {
                break;
            }

            if (source[j] == '>')
            {
                j++;
                break;
            }

            if (source[j] == '/' && j + 1 < source.Length && source[j + 1] == '>')
            {
                selfClosing = true;
                j += 2;
                break;
            }

            if (source[j] == '/')
            {
                j++;
                continue;
            }

            var attrStart = j;
            while (j < source.Length && IsWsF(source[j]) == false && source[j] != '=' && source[j] != '>' && source[j] != '/')
            {
                j++;
            }

            var attrName = source.Substring(attrStart, j - attrStart);

            while (j < source.Length && IsWsF(source[j]))
            {
                j++;
            }

            var value = string.Empty;
            if (j < source.Length && source[j] == '=')
            {
                j++;
                while (j < source.Length && IsWsF(source[j]))
                {
                    j++;
                }

                value = ReadAttrValueF(source, ref j);
            }

            (attributes ??= new List<HtmlAttribute>()).Add(new HtmlAttribute(attrName, value));
        }

        pos = j;
    }

    private static string ReadAttrValueF(string source, ref int pos)
    {
        if (pos >= source.Length)
        {
            return string.Empty;
        }

        var quote = source[pos];
        if (quote == '"' || quote == '\'')
        {
            pos++;

            var start = pos;
            while (pos < source.Length && source[pos] != quote)
            {
                pos++;
            }

            var value = source.Substring(start, pos - start);
            if (pos < source.Length)
            {
                pos++;
            }

            return value;
        }

        var unquotedStart = pos;
        while (pos < source.Length && IsWsF(source[pos]) == false && source[pos] != '>')
        {
            pos++;
        }

        return source.Substring(unquotedStart, pos - unquotedStart);
    }

    // The close tag's name comes out as a source span (start/length), not a substring.
    private static void ScanCloseTagF(string source, int pos, out int nameStart, out int nameLen, out int after)
    {
        var j = pos + 2;
        nameStart = j;
        while (j < source.Length && source[j] != '>' && IsWsF(source[j]) == false)
        {
            j++;
        }

        nameLen = j - nameStart;

        while (j < source.Length && source[j] != '>')
        {
            j++;
        }

        if (j < source.Length)
        {
            j++;
        }

        after = j;
    }

    // OrdinalIgnoreCase comparison of a source span against an open-element name.
    private static bool SpanNameEqualsF(string source, int start, int len, string name)
    {
        if (len != name.Length)
        {
            return false;
        }

        for (var k = 0; k < len; k++)
        {
            if (char.ToLowerInvariant(source[start + k]) != char.ToLowerInvariant(name[k]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool StackContainsSpanF(List<string> openNames, string source, int start, int len, int count)
    {
        for (var k = 0; k < count; k++)
        {
            if (SpanNameEqualsF(source, start, len, openNames[k]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMarkupStartF(string source, int pos)
    {
        if (pos + 1 >= source.Length)
        {
            return false;
        }

        var ch = source[pos + 1];

        return ch == '/' || ch == '!' || (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z');
    }

    private static bool StartsWithF(string source, int pos, string token)
    {
        if (pos + token.Length > source.Length)
        {
            return false;
        }

        for (var k = 0; k < token.Length; k++)
        {
            if (source[pos + k] != token[k])
            {
                return false;
            }
        }

        return true;
    }

    private static bool NameEqualsF(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool IsRawTextF(string name)
        => NameEqualsF(name, "script") || NameEqualsF(name, "style");

    private static int IndexOfIgnoreCaseF(string source, string token, int from)
    {
        for (var k = from; k + token.Length <= source.Length; k++)
        {
            var match = true;
            for (var m = 0; m < token.Length; m++)
            {
                if (char.ToLowerInvariant(source[k + m]) != char.ToLowerInvariant(token[m]))
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return k;
            }
        }

        return -1;
    }

    private static bool IsWsF(char ch) => ch == ' ' || ch == '\t' || ch == '\n' || ch == '\r' || ch == '\f';
}
