using System.Text;
using System.Collections.Generic;

namespace NgSharp.Parsing
{
    // Turns the text-level control-flow syntax into transparent wrapper elements the DOM
    // parser already understands:
    //   @if (EXPR) { BODY }        ->  <ng-container [if]="EXPR">BODY</ng-container>
    //   @for (EXPR) { BODY }       ->  <ng-container [for]="EXPR">BODY</ng-container>
    //   @else if (EXPR) { BODY }   ->  <ng-container [else-if]="EXPR">BODY</ng-container>
    //   @else { BODY }             ->  <ng-container [else]="">BODY</ng-container>
    // An @else / @else if is only recognised right after the closing brace of an @if (or a
    // preceding @else if) — the two become DOM siblings the parser groups back into one chain.
    // Brace matching skips {{ }} interpolation and balances literal braces (CSS, etc.) so
    // only a block's own closing brace closes it. (<style>/<script> masking is a follow-up.)
    internal static class ControlFlowPreprocessor
    {
        public static string Expand(string template)
        {
            if (string.IsNullOrEmpty(template) || template.IndexOf('@') < 0)
            {
                return template;
            }

            var builder = new StringBuilder(template.Length + 32);
            // Each open block records its brace depth and its kind: 'i' = @if / @else if (a
            // chain that an @else may follow), 'f' = @for, 'e' = @else (terminal).
            var openBlocks = new Stack<(int depth, char kind)>();
            var depth = 0;
            var i = 0;

            while (i < template.Length)
            {
                if (i + 1 < template.Length && template[i] == '{' && template[i + 1] == '{')
                {
                    var end = template.IndexOf("}}", i + 2, System.StringComparison.Ordinal);
                    if (end < 0)
                    {
                        end = template.Length - 2;
                    }
                    builder.Append(template, i, (end + 2) - i);
                    i = end + 2;
                    continue;
                }

                if (template[i] == '@' && TryReadOpener(template, i, out var directive, out var expression, out var next))
                {
                    builder.Append("<ng-container [").Append(directive).Append("]=\"").Append(expression).Append("\">");
                    depth++;
                    openBlocks.Push((depth, directive == "for" ? 'f' : 'i'));
                    i = next;
                    continue;
                }

                if (template[i] == '{')
                {
                    depth++;
                    builder.Append('{');
                    i++;
                    continue;
                }

                if (template[i] == '}')
                {
                    if (openBlocks.Count > 0 && openBlocks.Peek().depth == depth)
                    {
                        var kind = openBlocks.Pop().kind;
                        builder.Append("</ng-container>");
                        if (depth > 0)
                        {
                            depth--;
                        }

                        // A closing @if / @else if may be chained to an @else / @else if sibling.
                        if (kind == 'i' && TryReadElse(template, i + 1, out var elseDirective, out var elseExpression, out var elseNext))
                        {
                            builder.Append("<ng-container [").Append(elseDirective).Append("]=\"").Append(elseExpression ?? string.Empty).Append("\">");
                            depth++;
                            openBlocks.Push((depth, elseDirective == "else-if" ? 'i' : 'e'));
                            i = elseNext;
                            continue;
                        }

                        i++;
                        continue;
                    }

                    builder.Append('}');
                    if (depth > 0)
                    {
                        depth--;
                    }
                    i++;
                    continue;
                }

                builder.Append(template[i]);
                i++;
            }

            return builder.ToString();
        }

        private static bool TryReadOpener(string s, int i, out string directive, out string expression, out int next)
        {
            directive = null;
            expression = null;
            next = i;

            var j = i + 1;
            if (MatchesKeyword(s, j, "if"))
            {
                directive = "if";
                j += 2;
            }
            else if (MatchesKeyword(s, j, "for"))
            {
                directive = "for";
                j += 3;
            }
            else
            {
                return false;
            }

            j = SkipWhitespace(s, j);

            if (!TryReadParens(s, ref j, out expression))
            {
                return false;
            }

            j = SkipWhitespace(s, j);

            if (j >= s.Length || s[j] != '{')
            {
                return false;
            }

            next = j + 1;
            return true;
        }

        // Recognises an @else / @else if chained right after a closing brace. pos is the index
        // just past the '}'. On success directive is "else-if" (with expression) or "else"
        // (expression null), and next is the index past the block's opening '{'.
        private static bool TryReadElse(string s, int pos, out string directive, out string expression, out int next)
        {
            directive = null;
            expression = null;
            next = pos;

            // Whitespace and HTML comments between the closing brace and @else are skipped, matching
            // TemplateParser.NextElementIndex which skips the same nodes when folding the else chain.
            var j = SkipWhitespaceAndComments(s, pos);

            if (j >= s.Length || s[j] != '@')
            {
                return false;
            }

            j++;
            if (!MatchesKeyword(s, j, "else"))
            {
                return false;
            }

            j = SkipWhitespace(s, j + 4);

            if (MatchesKeyword(s, j, "if"))
            {
                j = SkipWhitespace(s, j + 2);

                if (!TryReadParens(s, ref j, out expression))
                {
                    return false;
                }

                j = SkipWhitespace(s, j);

                if (j >= s.Length || s[j] != '{')
                {
                    return false;
                }

                directive = "else-if";
                next = j + 1;
                return true;
            }

            if (j < s.Length && s[j] == '{')
            {
                directive = "else";
                next = j + 1;
                return true;
            }

            return false;
        }

        // Reads a parenthesised expression: s[j] must be '('. Sets expression to the trimmed
        // inner text and advances j past the matching ')'. Returns false on an unbalanced group.
        private static bool TryReadParens(string s, ref int j, out string expression)
        {
            expression = null;

            if (j >= s.Length || s[j] != '(')
            {
                return false;
            }

            j++;
            var start = j;
            var parens = 1;
            while (j < s.Length && parens > 0)
            {
                if (s[j] == '(')
                {
                    parens++;
                }
                else if (s[j] == ')')
                {
                    parens--;
                    if (parens == 0)
                    {
                        break;
                    }
                }
                j++;
            }

            if (parens != 0)
            {
                return false;
            }

            expression = s.Substring(start, j - start).Trim();
            j++;
            return true;
        }

        private static int SkipWhitespace(string s, int j)
        {
            while (j < s.Length && char.IsWhiteSpace(s[j]))
            {
                j++;
            }

            return j;
        }

        // Skips runs of whitespace and HTML comments. Stops at an unterminated comment so the
        // caller can decide (the raw text is then emitted as-is by the main scan).
        private static int SkipWhitespaceAndComments(string s, int j)
        {
            while (true)
            {
                j = SkipWhitespace(s, j);

                if (j + 3 < s.Length && s[j] == '<' && s[j + 1] == '!' && s[j + 2] == '-' && s[j + 3] == '-')
                {
                    var end = s.IndexOf("-->", j + 4, System.StringComparison.Ordinal);
                    if (end < 0)
                    {
                        return j;
                    }

                    j = end + 3;
                    continue;
                }

                return j;
            }
        }

        private static bool MatchesKeyword(string s, int i, string keyword)
        {
            if (i + keyword.Length > s.Length)
            {
                return false;
            }

            for (var k = 0; k < keyword.Length; k++)
            {
                if (s[i + k] != keyword[k])
                {
                    return false;
                }
            }

            var after = i + keyword.Length;
            return after >= s.Length || !char.IsLetterOrDigit(s[after]);
        }
    }
}
