using AngleSharp.Dom;

using System.Text.Json;
using System.Collections.Generic;

namespace NgSharp.Directives
{
    public class AttributeDirective : IDirective
    {
        public string DirectiveName => "attr";

        public bool ApplyWhileParsing => false;

        public void Apply(HtmlBuilder builder, string directiveName, IElement childElement, NgElement content, Dictionary<string, string> optionalArguments = null)
        {
            var attributeName = directiveName.Split(".")[1];

            var strValue = content.GetString();
            
            if (attributeName == "class" && strValue.Length > 0)
            {
                childElement.ClassList.Add(strValue.Replace("\"", "'"));
            }
            else if (attributeName != "class")
            {
                childElement.SetAttribute(attributeName, strValue);
            }
        }
    }
}
