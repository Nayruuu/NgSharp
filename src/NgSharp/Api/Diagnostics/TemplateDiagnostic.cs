namespace NgSharp;

/// <summary>
/// One problem found in a template by <see cref="HtmlBuilder.Validate(string, TemplateMode)"/> —
/// typically a construct today's lenient renderer would swallow silently (an unclosed <c>{{</c>, an
/// <c>@for (x in …)</c>, an unclosed <c>@if</c> block, an orphan <c>[else]</c>…).
/// </summary>
public sealed class TemplateDiagnostic
{
    /// <summary>
    /// Whether the finding blocks a strict compile (<see cref="DiagnosticSeverity.Error"/>) or is a
    /// probable-mistake advisory (<see cref="DiagnosticSeverity.Warning"/>).
    /// </summary>
    public DiagnosticSeverity Severity { get; }

    /// <summary>
    /// What is wrong, and when possible, how to fix it.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Character offset of the finding in the template source. Best effort: exact for structural
    /// findings (unclosed blocks, interpolations, orphan branches); for a finding inside an
    /// expression it points at the expression's first occurrence in the source.
    /// </summary>
    public int Position { get; }

    internal TemplateDiagnostic(DiagnosticSeverity severity, string message, int position)
    {
        Severity = severity;
        Message = message;
        Position = position;
    }

    /// <summary>
    /// <c>Severity [position N]: message</c> — a log-friendly single line.
    /// </summary>
    public override string ToString() => $"{Severity} [position {Position}]: {Message}";
}
