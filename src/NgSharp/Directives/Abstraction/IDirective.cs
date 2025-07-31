using AngleSharp.Dom;

using System.Collections.Generic;

namespace NgSharp.Directives
{
    public interface IDirective
    {
        string DirectiveName { get; }

        bool ApplyWhileParsing { get; }

        void Apply(HtmlBuilder builder, string directiveName, IElement childElement, NgElement content, Dictionary<string, string> optionalArguments = null);
    }
}
