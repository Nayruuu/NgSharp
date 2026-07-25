using System;

using NgSharp.Pipes;

namespace NgSharp.Benchmark;

// A custom pipe ({{ Name | initials }}): the uppercased first letter of each whitespace-separated word.
public sealed class InitialsPipe : IPipe
{
    public string PipeName => "initials";

    public string Transform(string tagName, NgElement value, string argument) => Compute(value.GetString());

    // Shared with the Handlebars helper and called directly from the Razor port so all three engines
    // emit identical text.
    public static string Compute(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var initials = new char[words.Length];

        for (var i = 0; i < words.Length; i++)
        {
            initials[i] = char.ToUpperInvariant(words[i][0]);
        }

        return new string(initials);
    }
}
