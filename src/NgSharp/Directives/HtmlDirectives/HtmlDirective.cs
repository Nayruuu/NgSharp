using AngleSharp.Dom;

using System.Text.Json;
using System.Collections.Generic;

namespace NgSharp.Directives
{
    public class HtmlDirective : IDirective
    {
        public string DirectiveName => "html";

        public bool ApplyWhileParsing => false;

        public void Apply(HtmlBuilder builder, string directiveName, IElement childElement, NgElement content, Dictionary<string, string> optionalArguments = null)
        {
            if (content is not { ValueKind: JsonValueKind.String })
                return;

            childElement.InnerHtml = content.GetString();
        }
    }
}

