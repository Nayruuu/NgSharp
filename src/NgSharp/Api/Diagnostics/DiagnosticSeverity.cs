namespace NgSharp;

/// <summary>
/// Severity of a <see cref="TemplateDiagnostic"/> reported by <see cref="HtmlBuilder.Validate(string, TemplateMode)"/>.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>
    /// The template will not render as written — the flagged construct is swallowed, emitted as
    /// literal text, or evaluates to nothing. A strict compile
    /// (<see cref="HtmlBuilder.Compile(string, TemplateOptions)"/>) refuses the template.
    /// </summary>
    Error,

    /// <summary>
    /// The template renders, but the flagged construct probably doesn't do what the author intended
    /// (e.g. a pipe that is not registered on this builder, an interpolation kept literal because its
    /// body spans a line break).
    /// </summary>
    Warning
}
