using AngleSharp.Dom;

using System.Linq;
using System.Text.Json;
using System.Collections.Generic;

namespace NgSharp.Directives
{
    public class NotEmptyDirective : IDirective
    {
        public string DirectiveName => "not-empty";

        public bool ApplyWhileParsing => false;

        public void Apply(HtmlBuilder builder, string directiveName, IElement childElement, NgElement content, Dictionary<string, string> optionalArguments = null)
        {
            var anyElement = content
                .Children
                .Any();

            if (anyElement == false)
            {
                childElement.ParentElement.RemoveChild(childElement);
            }
        }
    }
}

