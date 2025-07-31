using AngleSharp.Dom;

using System.Text.Json;
using System.Collections.Generic;

namespace NgSharp.Directives
{
    public class StyleDirective : IDirective
    {
        public string DirectiveName => "style";

        public bool ApplyWhileParsing => false;

        public void Apply(HtmlBuilder builder, string directiveName, IElement childElement, NgElement content, Dictionary<string, string> optionalArguments = null)
        {
            var subAttribute = directiveName.Split(".")[1];

            var strValue = content.GetString();
            if (!string.IsNullOrWhiteSpace(strValue))
            {
                if (childElement.HasAttribute("style"))
                {
                    var styleAttribute = childElement.Attributes.GetNamedItem("style");

                    styleAttribute.Value = $"{styleAttribute.Value}; {subAttribute}: {strValue}";
                }
                else
                {
                    childElement.SetAttribute("style", $"{subAttribute}: {strValue}");
                }
            }
        }
    }
}
