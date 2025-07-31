using System.Collections.Generic;
using AngleSharp.Dom;

using System.Text.Json;

namespace NgSharp.Directives
{
    public class IfDirective : IDirective
    {
        public string DirectiveName => "if";

        public bool ApplyWhileParsing => false;

        public void Apply(HtmlBuilder builder, string directiveName, IElement childElement, NgElement content, Dictionary<string, string> optionalArguments = null)
        {
            var booleanResult = content?.GetBoolean();

            if (booleanResult.HasValue == false || booleanResult.Value == false)
            {
                childElement.ParentElement.RemoveChild(childElement);
            }
        }
    }
}
