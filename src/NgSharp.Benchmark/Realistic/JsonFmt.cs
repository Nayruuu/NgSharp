using System;
using System.Text;
using System.Text.Json;
using System.Globalization;

namespace NgSharp.Benchmark.Realistic;

// Shared JSON formatting used by the Fluid / Handlebars / Scriban custom filters of the TEXT arena so
// all four engines produce byte-identical JSON — a faithful replica of what NgSharp's TemplateMode.Text
// emits natively (the Fmt pattern, applied to the machine-literal contract):
//   Str  ≡ the built-in json pipe on a string (quoted, JSON-escaped — JsonPipe.WriteString);
//   Num  ≡ a bare {{ decimal }} interpolation (FromObject ingests decimal AS double, then the raw
//          writer formats it culture-invariant shortest-round-trip — hence 19.90m -> "19.9");
//   Bool ≡ a bare {{ bool }} interpolation (lowercase machine literal);
//   Iso  ≡ a bare {{ DateTime }} interpolation (FromObject stores the STJ scalar string:
//          serialize + strip the quotes, e.g. "2024-03-14T00:00:00").
// The engines then differ only in template parse + render — never in JSON formatting.
public static class JsonFmt
{
    public static string Num(decimal value)
        => ((double)value).ToString(CultureInfo.InvariantCulture);

    public static string Bool(bool value) => value ? "true" : "false";

    // Dates in the model are DateTime(Unspecified) — STJ emits no escapes, so the quote strip is safe.
    public static string Iso(DateTime value)
    {
        var json = JsonSerializer.Serialize(value);

        return json.Substring(1, json.Length - 2);
    }

    public static string Str(string value)
    {
        if (value is null)
        {
            return "null";
        }

        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');

        foreach (var ch in value)
        {
            switch (ch)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (ch < ' ')
                    {
                        builder.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(ch);
                    }

                    break;
            }
        }

        builder.Append('"');

        return builder.ToString();
    }
}
