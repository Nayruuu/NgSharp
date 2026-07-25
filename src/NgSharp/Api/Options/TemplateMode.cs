using System;
using System.Collections.Generic;

using NgSharp.Ast;
using NgSharp.Parsing;
using NgSharp.Rendering;

namespace NgSharp;

/// <summary>
/// The dialect a template is written in — pass it via <see cref="TemplateOptions.Mode"/> to
/// <see cref="HtmlBuilder.BuildFromTemplate(string, object, TemplateOptions)"/> or
/// <see cref="HtmlBuilder.Compile(string, TemplateOptions)"/>. <see cref="Html"/>, the default
/// everywhere, is the Angular-style HTML dialect: elements, bindings, components, directives, and
/// HTML-escaped output. <see cref="Text"/> targets non-HTML output (plain-text emails, JSON, CSV…):
/// the template is raw text plus <c>{{ }}</c> interpolations and <c>@if</c>/<c>@else</c>/<c>@for</c>
/// blocks, and everything — static text and interpolated values alike — is emitted verbatim, with no
/// escaping of any kind. Bare interpolated booleans and numbers write machine literals
/// (<c>true</c>/<c>false</c>, culture-invariant digits) so JSON/CSV output stays parseable; pipes
/// keep formatting for humans with the current culture, and the <c>json</c> pipe emits complete,
/// escaped JSON literals for string data.
/// </summary>
public sealed class TemplateMode
{
    #region Fields

    // Each instance CARRIES its strategy: the parse routine bakes the escaping contract into the
    // program it emits (escaped vs raw nodes), so the shared renderer needs no mode at render time.
    private readonly string _name;

    private readonly Func<string, IEnumerable<string>, IEnumerable<string>, IReadOnlyList<TemplateNode>> _parse;

    private readonly bool _hasNgTemplates;

    /// <summary>
    /// The Angular-style HTML dialect — the default of every render and compile overload.
    /// </summary>
    public static readonly TemplateMode Html = new TemplateMode("Html", TemplateParser.ParseDocument, hasNgTemplates: true);

    /// <summary>
    /// The plain-text dialect: <c>{{ }}</c> interpolations and <c>@if</c>/<c>@else</c>/<c>@for</c>
    /// blocks only, rendered without any escaping.
    /// </summary>
    public static readonly TemplateMode Text = new TemplateMode("Text", (template, _, _) => TemplateParser.ParseTextDocument(template), hasNgTemplates: false);

    #endregion

    #region Constructors

    private TemplateMode(string name, Func<string, IEnumerable<string>, IEnumerable<string>, IReadOnlyList<TemplateNode>> parse, bool hasNgTemplates)
    {
        _name = name;
        _parse = parse;
        _hasNgTemplates = hasNgTemplates;
    }

    #endregion

    #region Public methods

    /// <summary>
    /// The mode's name (<c>Html</c> / <c>Text</c>).
    /// </summary>
    public override string ToString() => _name;

    #endregion

    #region Internal methods

    internal IReadOnlyList<TemplateNode> Parse(string template, IEnumerable<string> componentNames, IEnumerable<string> directiveNames)
        => _parse(template, componentNames, directiveNames);

    // Null when the dialect has no <ng-template> or the source has none — it must appear literally
    // (the preprocessor never generates it), so the IndexOf fast-out is safe.
    internal IReadOnlyDictionary<string, IReadOnlyList<TemplateNode>> CollectTemplates(string template, IReadOnlyList<TemplateNode> nodes)
        => _hasNgTemplates && template.IndexOf("<ng-template", StringComparison.OrdinalIgnoreCase) >= 0
            ? TemplateRenderer.CollectTemplates(nodes)
            : null;

    #endregion
}
