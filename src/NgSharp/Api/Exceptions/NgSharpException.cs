using System;

namespace NgSharp;

/// <summary>
/// The exception NgSharp throws for template-engine errors: an empty template handed to a render or
/// compile overload, an unknown pipe reached at render time, a strict-mode path that does not exist in
/// the model (<see cref="HtmlBuilder.Compile(string, TemplateOptions)"/>), or a strict compile whose
/// template has validation errors (<see cref="HtmlBuilder.Validate(string, TemplateMode)"/>).
/// </summary>
public class NgSharpException : Exception
{
    /// <summary>
    /// Creates the exception with a message describing the template error.
    /// </summary>
    /// <param name="message">The error description.</param>
    public NgSharpException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates the exception with a message and the underlying cause.
    /// </summary>
    /// <param name="message">The error description.</param>
    /// <param name="innerException">The underlying cause.</param>
    public NgSharpException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
