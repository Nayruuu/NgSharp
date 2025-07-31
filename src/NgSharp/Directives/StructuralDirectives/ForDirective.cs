using AngleSharp.Dom;

using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NgSharp.Directives
{
    public class ForDirective : IDirective
    {
        public string DirectiveName => "for";

        public bool ApplyWhileParsing => true;

        public void Apply(HtmlBuilder builder, string directiveName, IElement childElement, NgElement content, Dictionary<string, string> optionalArguments = null)
        {
            var i = 0;
            var list = content.Children;
            
            var parent = childElement.Parent;
            var referenceNode = childElement;
            
            var clones = new List<IElement>();
            
            childElement.Attributes.RemoveNamedItem("[for]");

            foreach (var listElement in list)
            {
                if (optionalArguments.ContainsKey("SplitIndexes"))
                {
                    var splitIndexes = Token(content, optionalArguments["SplitIndexes"])
                        .Children
                        .Select(child => child.Value)
                        .ToArray();
                
                    if (splitIndexes.Contains(i))
                    {
                        if (optionalArguments.ContainsKey("Repeat"))
                        {
                            if (optionalArguments["Repeat"] == "Previous")
                            {
                                var previousElementClone = childElement.PreviousElementSibling.Clone() as IElement;
                
                                clones.Add(previousElementClone);
                            }
                        }
                    }
                }
                else if (optionalArguments.ContainsKey("Split"))
                {
                    var splitValue = int.Parse(optionalArguments["Split"]);
                
                    if ((i + 1) % splitValue == 0)
                    {
                        if (optionalArguments.ContainsKey("Repeat"))
                        {
                            if (optionalArguments["Repeat"] == "Previous")
                            {
                                var previousElementClone = childElement.PreviousElementSibling.Clone() as IElement;
                
                                clones.Add(previousElementClone);
                            }
                        }
                        else
                        {
                            clones.Add(CreatePageBreaker(childElement.Owner));
                        }
                
                        i = 0;
                    }
                }

                var childElementClone = childElement.Clone(deep: true) as IElement;

                ParseChildren(childElementClone, listElement);

                clones.Add(childElementClone);

                i++;
            }
            
            foreach (var clone in clones)
            {
                SafeInsertAfter(referenceNode, clone);
                referenceNode = clone;
            }

            childElement.Remove();
        }

        private void ParseChildren(IElement element, NgElement content)
        {
            foreach (var child in element.Children)
            {
                ParseChildren(child, content);

                ReplaceDirective(child, content);
                ReplaceInterpolation(child, content);
            }
        }

        #region Page breaker
        public IElement CreatePageBreaker(IDocument document)
        {
            var pageBreaker = document.CreateElement("div");

            pageBreaker.SetAttribute("style", "page-break-before: always;break-before: always;");

            return pageBreaker;
        }
        #endregion

        #region Directives
        private void ReplaceDirective(IElement element, NgElement content)
        {
            foreach (var attribute in element.Attributes)
            {
                if (attribute.Name.StartsWith("["))
                {
                    if (!TryParseDirectiveCondition(attribute, content) &&
                        !TryParseDirectivePipe(attribute, content))
                    {
                        var arguments = attribute.Value
                            .Split(";");

                        var optionalArguments = arguments
                            .Where(_ => arguments.Length > 1)
                            .Skip(1)
                            .Select(x => x.Split(":"))
                            .ToDictionary(x => x.ElementAt(0), x =>
                            {
                                var optionalArgumentValue = Token(content, x.ElementAt(1)); 

                                if (optionalArgumentValue != null)
                                {
                                    return optionalArgumentValue.ToString();
                                }
                                else
                                {
                                    return x.ElementAt(1);
                                }
                            });

                        var attributeValue = content.SelectToken(arguments.First());

                        if (attributeValue != null)
                        {
                            attribute.Value = attributeValue.Path;
                        }
                        else
                        {
                            attribute.Value = arguments.First();
                        }

                        if (optionalArguments.Count > 0)
                        {
                            attribute.Value += $";{string.Join(";", optionalArguments.Select(x => $"{x.Key}:{x.Value}"))}";
                        }
                    }
                }
            }
        }

        private bool TryParseDirectivePipe(IAttr attribute, NgElement content)
        {
            var match = Regex.Match(attribute.Value, @"(.*)\s+\|\s+([a-zA-Z]+)(:{1}\s?['|""](.*)['|""])?\s*");

            if (match.Success)
            {
                attribute.Value = Regex.Replace(attribute.Value, @"(.*)\s+\|\s+([a-zA-Z]+)(:{1}\s?['|""](.*)['|""])?\s*", match =>
                {
                    var instanceValue = Token(content, match.Groups[1].Value.Trim());

                    return match.Value.Replace(match.Groups[1].Value, instanceValue.Path);
                });
            }

            return match.Success;
        }

        private bool TryParseDirectiveCondition(IAttr attribute, NgElement content)
        {
            var match = Regex.Match(attribute.Value, @"(.*)\s+(!=|==)\s(null|.*)\s?(\?\s+('?.*'?)\s+:\s+('?.*'?))?");

            if (match.Success)
            {
                attribute.Value = Regex.Replace(attribute.Value, @"(.*)\s+(!=|==)\s(null|.*)\s?(\?\s+('?.*'?)\s+:\s+('?.*'?))?", match =>
                {
                    var instanceValue = Token(content, match.Groups[1].Value.Trim());

                    return match.Value.Replace(match.Groups[1].Value, instanceValue.Path);
                });
            }

            return match.Success;
        }
        #endregion

        #region Interpolation
        private void ReplaceInterpolation(IElement element, NgElement content)
        {
            var childNodes = element.ChildNodes.Where(node => node.GetType().Name == "TextNode");

            if (childNodes.Any())
            {
                foreach (var node in childNodes)
                {
                    node.TextContent = Regex.Replace(node.TextContent, @"{{\s*([\w.]+)(\s*\|\s*(\w+)(:\s*'([^']*)')?)?\s*}}", match =>
                    {
                        var interpolationValue = Token(content, match.Groups[1].Value.Trim());

                        if (interpolationValue != null)
                        {
                            return match.Value.Replace(match.Groups[1].Value, interpolationValue.Path);
                        }
                        else
                        {
                            return match.Value;
                        }
                    });
                }
            }
        }
        #endregion

        #region Token
        public NgElement Token(NgElement content, string instanceToken)
        {
            if (!string.IsNullOrWhiteSpace(instanceToken))
            {
                var element = content.SelectToken(instanceToken);

                if (element != null)
                {
                    return element;
                }

                if (content.Parent != null)
                {
                    return Token(content.Parent, instanceToken);
                }
            }

            return null;
        }
        #endregion
        
        #region SafeInsert
        public static void SafeInsertAfter(INode referenceNode, INode newNode)
        {
            var parent = referenceNode.Parent;
            var next = referenceNode.NextSibling;

            if (next != null)
                parent.InsertBefore(newNode, next);
            else
                parent.AppendChild(newNode);
        }
        #endregion
    }
}
