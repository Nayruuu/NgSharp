using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace NgSharp.Components
{
    public class CustomComponent : IComponent
    {
        public string ComponentName => "custom-component";
        
        public string ComponentText { get; set; }

        public void Render(IElement element)
        {
            var htmlParser = new HtmlParser();

            var image = $"<div>" +
                        $"{ComponentText}" +
                        $"</div>";

            var node = htmlParser.ParseFragment(image, element);

            element.Parent.InsertBefore(node.First(), element);
            element.Parent.RemoveElement(element);
        }
    }
}

