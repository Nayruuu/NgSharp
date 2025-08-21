using AngleSharp;
using AngleSharp.Dom;

using System;
using System.Linq;
using System.Text.Json;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using NgSharp.Pipes;
using NgSharp.Directives;
using NgSharp.Components;

namespace NgSharp
{
    public class HtmlBuilder
    {
        private readonly Dictionary<string, IPipe> pipes;

        private readonly Dictionary<string, IDirective> directives;

        private readonly Dictionary<string, IComponent> components;

        private readonly Dictionary<Guid, List<string>> instance;

        private readonly Dictionary<Guid, (INode node, List<string> tokens)> interpolations;

        private readonly Dictionary<Guid, (IElement element, IPipe pipe, string argument)> pipeAttributions;

        private readonly Dictionary<Guid, (IElement element, IComponent component)> componentAttributions;

        private readonly Dictionary<Guid, (IElement element, IDirective directive, string directiveValue)> directiveAttributions;

        public IReadOnlyDictionary<string, IPipe> Pipes { get => pipes; }
        
        public IReadOnlyDictionary<string, IDirective> Directives { get => directives; }
        
        public IReadOnlyDictionary<string, IComponent> Components { get => components; }

        private HtmlBuilder()
        {
            instance = new Dictionary<Guid, List<string>>();

            pipes = new Dictionary<string, IPipe>();
            components = new Dictionary<string, IComponent>();
            directives = new Dictionary<string, IDirective>();
            interpolations = new Dictionary<Guid, (INode node, List<string> tokens)>();
            pipeAttributions = new Dictionary<Guid, (IElement element, IPipe pipe, string argument)>();
            componentAttributions = new Dictionary<Guid, (IElement element, IComponent component)>();
            directiveAttributions = new Dictionary<Guid, (IElement element, IDirective directive, string directiveValue)>();

            RegisterPipe<DatePipe>();
            RegisterPipe<ImagePipe>();
            RegisterPipe<UpperPipe>();
            RegisterPipe<NumberPipe>();
            RegisterPipe<LargeNumberPipe>();

            RegisterComponent<MapComponent>();

            RegisterDirective<IfDirective>();
            RegisterDirective<ForDirective>();
            RegisterDirective<HtmlDirective>();
            RegisterDirective<StyleDirective>();
            RegisterDirective<NotEmptyDirective>();
            RegisterDirective<AttributeDirective>();
        }

        public static HtmlBuilder Default => new();

        public void RegisterPipe<T>() where T : class, IPipe
        {
            var pipe = Activator.CreateInstance(typeof(T)) as T;

            this.pipes[pipe.PipeName] = pipe;
        }

        public void RegisterDirective<T>() where T : class, IDirective
        {
            var directive = Activator.CreateInstance(typeof(T)) as T;

            this.directives[directive.DirectiveName] = directive;
        }

        public void RegisterComponent<T>() where T : class, IComponent
        {
            var component = Activator.CreateInstance(typeof(T)) as T;

            this.components[component.ComponentName] = component;
        }

        public async Task<string> BuildFromTemplateAsync(string template, object model)
        {
            var ngElement = NgElement.FromJson(ToJsonElement(model));
            
            if (string.IsNullOrEmpty(template))
            {
                throw new Exception("Can't replace an empty html template");
            }

            return await RemplaceTemplateValues(template, ngElement);
        }

        private async Task<string> RemplaceTemplateValues(string template, NgElement ngElement)
        {
            try
            {
                var config = Configuration.Default;
                var context = BrowsingContext.New(config);

                var document = await context.OpenAsync(req => req.Content(template));

                ParseDocumentStyle(document, ngElement);
                
                var count = document.Body.Children.Count();
                for (int i = 0; i < count; i++)
                {
                    var element = document.Body.Children.ElementAt(i);

                    await ParseDocumentElement(document.Body, element, ngElement);

                    if (count != document.Body.Children.Count() || element != document.Body.Children.ElementAt(i))
                    {
                        i--;
                    }
                    count = document.Body.Children.Count();
                }

                ApplyDirectives(ngElement);
                ApplyComponents(ngElement);
                ApplyInterpolation(ngElement);

                return MinifyHtml(document.DocumentElement.OuterHtml);
            }
            catch
            {
                throw;
            }
        }

        private void ParseDocumentStyle(IDocument document, NgElement? content)
        {
            var styleSheet = document
                .GetElementsByTagName("style")
                .FirstOrDefault();

            if (styleSheet != null)
            {
                styleSheet.TextContent = Regex.Replace(styleSheet.TextContent, @"{{(\s[^{}]+\s)}}", match =>
                {
                    var interpolationValuePath = Token(content, match.Groups[1].Value.Trim());

                    return match.Value.Replace(match.Groups[0].Value, interpolationValuePath.GetString());
                });
            }
        }

        private async Task ParseDocumentElement(IElement parent, IElement childElement, NgElement? content)
        {
            ParseComponent(childElement);
            ParseDirectives(childElement, content);

            if (parent.Contains(childElement))
            {
                ParseInterpolation(childElement);

                var count = childElement.Children.Count();
                for (int i = 0; i < count; i++)
                {
                    var element = childElement.Children.ElementAt(i);

                    await ParseDocumentElement(childElement, element, content);

                    if (count != childElement.Children.Count() || element != childElement.Children.ElementAt(i))
                    {
                        i--;
                    }
                    count = childElement.Children.Count();
                }
            }
        }

        #region Directives
        private void ParseDirectives(IElement childElement, NgElement content)
        {
            var attributes = childElement.Attributes;

            if (attributes.Any())
            {
                foreach (var attribute in attributes)
                {
                    var match = Regex.Match(attribute.Name, @"\[(([a-zA-Z|\-]+\.?)+)\]");

                    if (match.Success)
                    {
                        var directiveGuid = Guid.NewGuid();
                        var directiveNameTrigger = match.Groups[1].Value.Split(".")[0];

                        var attributeValue = CleanFromPipe(childElement, directiveGuid, attribute.Value);

                        if (this.directives.ContainsKey(directiveNameTrigger))
                        {
                            if (this.directives[directiveNameTrigger].ApplyWhileParsing)
                            {
                                var arguments = attributeValue
                                    .Split(";");

                                var optionalArguments = arguments
                                    .Where(_ => arguments.Length > 1)
                                    .Skip(1)
                                    .Select(x => x.Split(":"))
                                    .ToDictionary(x => x.ElementAt(0), x => x.ElementAt(1));

                                var instanceValue = Token(content, arguments[0]);

                                this.directives[directiveNameTrigger].Apply(this, null, childElement, instanceValue, optionalArguments);

                                if (childElement.Parent != null)
                                {
                                    childElement?.RemoveAttribute($"[{directiveNameTrigger}]");
                                }

                                break;
                            }
                            else
                            {
                                this.instance[directiveGuid] = new List<string>() { attributeValue };
                                this.directiveAttributions[directiveGuid] = (childElement, this.directives[directiveNameTrigger], match.Groups[1].Value);
                            }
                        }
                    }
                }
            }
        }

        private void ApplyDirectives(NgElement content)
        {
            foreach (var directiveAttribution in directiveAttributions)
            {
                var directiveContent = this.instance[directiveAttribution.Key][0];

                if (TryParseDirectiveCondition(content, directiveContent, out var result))
                {
                    directiveAttribution.Value.directive.Apply(this, directiveAttribution.Value.directiveValue, directiveAttribution.Value.element, result);
                    
                    if (directiveAttribution.Value.element.Parent != null)
                    {
                        directiveAttribution.Value.element?.RemoveAttribute($"[{directiveAttribution.Value.directiveValue}]");
                    }
                }
                else
                {
                    var instanceValue = Token(content, directiveContent);

                    if (this.pipeAttributions.ContainsKey(directiveAttribution.Key))
                    {
                        var pipeAttribution = this.pipeAttributions[directiveAttribution.Key];

                        instanceValue = NgElement.Parse(pipeAttribution.pipe.Transform(pipeAttribution.element, instanceValue, pipeAttribution.argument));
                    }
                    directiveAttribution.Value.directive.Apply(this, directiveAttribution.Value.directiveValue, directiveAttribution.Value.element, instanceValue);
                    
                    if (directiveAttribution.Value.element.Parent != null)
                    {
                        directiveAttribution.Value.element?.RemoveAttribute($"[{directiveAttribution.Value.directiveValue}]");
                    }
                }
            }
        }

        private bool TryParseDirectiveCondition(NgElement content, string value, out NgElement conditionResult)
        {
            var match = Regex.Match(value, @"(.*)\s+(!=|==)\s(null|.*)\s?(\?\s+('?.*'?)\s+:\s+('?.*'?))?");

            conditionResult = NgElement.Parse("false");
            if (match.Success)
            {
                var instanceValue = Token(content, match.Groups[1].Value);
                var rightValue = NgElement.Parse(match.Groups[3].Value.Trim());

                switch (match.Groups[2].Value)
                {
                    case "!=":
                        if (IsNumber(instanceValue) && IsNumber(rightValue))
                        {
                            var result = !double.Parse(instanceValue.Value.ToString())
                                .Equals(double.Parse(rightValue.Value.ToString()));
                            
                            conditionResult = NgElement.Parse(result.ToString());
                        }
                        else
                        {
                            var result = !instanceValue.Equals(rightValue);

                            conditionResult = NgElement.Parse(result.ToString());
                        }
                        break;
                    case "==":
                        if (IsNumber(instanceValue) && IsNumber(rightValue))
                        {
                            var result = double.Parse(instanceValue.ToString()).Equals(double.Parse(rightValue.ToString()));
                            
                            conditionResult = NgElement.Parse(result.ToString());
                        }
                        else
                        {
                            var result = instanceValue.Equals(rightValue);
                            
                            conditionResult = NgElement.Parse(result.ToString());
                        }
                        break;
                    default:
                        break;
                }

                if (!string.IsNullOrWhiteSpace(match.Groups[4].Value))
                {
                    var result =
                        (conditionResult.GetBoolean().Value ? match.Groups[5].Value : match.Groups[6].Value).Replace("'", "");
                    
                    conditionResult =  NgElement.Parse(result.ToString());
                }
            }

            return match.Success;
        }

        private bool IsNumber(NgElement? token)
        {
            return token.ValueKind == JsonValueKind.Number;
        }
        #endregion

        #region Components
        private void ParseComponent(IElement childElement)
        {
            var name = childElement.LocalName;

            if (this.components.ContainsKey(name))
            {
                var componentGuid = Guid.NewGuid();

                this.componentAttributions[componentGuid] = (childElement, this.components[name]);
            }
        }

        private void ApplyComponents(NgElement content)
        {
            foreach (var componentAttribution in componentAttributions)
            {
                var componentElement = componentAttribution.Value.component;
                var componentElementAttributes = componentAttribution.Value.element.Attributes;

                var componentType = componentElement
                    .GetType();

                var componentPropertiesMap = componentType
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .ToDictionary(property => property.Name.ToLowerInvariant());

                foreach (var attribute in componentElementAttributes)
                {
                    var attributeNameRegex = Regex.Match(attribute.Name, @"\[(([a-zA-Z|\-]+\.?)+)\]");

                    if (attributeNameRegex.Success)
                    {
                        var attributeName = attributeNameRegex.Groups[1].Value.Split(".")[0];

                        if (componentPropertiesMap.TryGetValue(attributeName, out var componentProperty))
                        {
                            var token = Token(content, attribute.Value);

                            if (token != null)
                            {
                                object convertedValue = ConvertJsonElement(token, componentProperty.PropertyType);
                                componentProperty.SetValue(componentElement, convertedValue);
                            }
                        }
                    }
                }

                componentElement.Render(componentAttribution.Value.element);
            }
        }
        
        private object ConvertJsonElement(NgElement element, Type targetType)
        {
            try
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.String:
                        var strVal = element.GetString();
                        
                        if (targetType == typeof(DateTime)) 
                            return DateTime.Parse(strVal);
                        if (targetType.IsEnum) 
                            return Enum.Parse(targetType, strVal, ignoreCase: true);
                        if (targetType == typeof(Guid) || targetType == typeof(Guid?)) 
                            return Guid.Parse(strVal);
                        if (targetType == typeof(byte[])) 
                            return Convert.FromBase64String(strVal);

                        return Convert.ChangeType(strVal, Nullable.GetUnderlyingType(targetType) ?? targetType);

                    case JsonValueKind.Number:
                        if (targetType == typeof(int) || targetType == typeof(int?)) 
                            return element.GetInt();
                        if (targetType == typeof(long) || targetType == typeof(long?)) 
                            return element.GetLong();
                        if (targetType == typeof(float) || targetType == typeof(float?)) 
                            return element.GetFloat();
                        if (targetType == typeof(double) || targetType == typeof(double?)) 
                            return element.GetDouble();
                        if (targetType == typeof(decimal) || targetType == typeof(decimal?)) 
                            return element.GetDecimal();
                        
                        return Convert.ChangeType(element.GetDouble(), Nullable.GetUnderlyingType(targetType) ?? targetType);

                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        return Convert.ChangeType(element.GetBoolean(), Nullable.GetUnderlyingType(targetType) ?? targetType);

                    case JsonValueKind.Null:
                        return null;

                    default:
                        return JsonSerializer.Deserialize(element.Value?.ToString(), targetType);
                }
            }
            catch
            {
                return null;
            }
        }
        #endregion

        #region Interpolation
        private void ParseInterpolation(IElement childElement)
        {
            var textNodes = childElement.ChildNodes.Where(node => node.GetType().Name == "TextNode");

            if (textNodes.Any())
            {
                foreach (var textNode in textNodes)
                {
                    var matches = Regex.Matches(textNode.Text(), @"{{(\s[^{}]+\s)}}");

                    if (matches.Count > 0)
                    {
                        var interpolationGuid = Guid.NewGuid();

                        this.instance[interpolationGuid] = new List<string>();
                        this.interpolations[interpolationGuid] = (textNode, new List<string>());

                        foreach (Match match in matches)
                        {
                            var interpolationValue = CleanFromPipe(childElement, interpolationGuid, match.Groups[1].Value);

                            this.instance[interpolationGuid].Add(interpolationValue);
                            this.interpolations[interpolationGuid].tokens.Add(match.Value);
                        }
                    }
                }
            }
        }

        private void ApplyInterpolation(NgElement content)
        {
            foreach (var interpolation in interpolations)
            {
                var nodeText = interpolation.Value.node.Text();

                for (int i = 0; i < interpolation.Value.tokens.Count; i++)
                {
                    var instanceValue = Token(content, instance[interpolation.Key][i]);

                    if (this.pipeAttributions.ContainsKey(interpolation.Key))
                    {
                        var pipeAttribution = this.pipeAttributions[interpolation.Key];

                        nodeText = pipeAttribution.pipe.Transform(pipeAttribution.element, instanceValue, pipeAttribution.argument);
                    }
                    else
                    {
                        nodeText = nodeText.Replace(interpolation.Value.tokens[i], instanceValue?.Value?.ToString());
                    }
                }

                interpolation.Value.node.NodeValue = nodeText;
            }
        }
        #endregion

        #region Pipes
        private string CleanFromPipe(IElement childElement, Guid directiveGuid, string value)
        {
            var match = Regex.Match(value, @"(.*)\s+\|\s+([a-zA-Z]+)(:{1}\s?['|""](.*)['|""])?\s*");

            if (match.Success)
            {
                var argument = match.Groups[4].Value.Trim();
                var pipeNameTrigger = match.Groups[2].Value.Trim();

                this.pipeAttributions[directiveGuid] = (childElement, this.pipes[pipeNameTrigger], argument);

                value = match.Groups[1].Value;
            }

            return value.Trim();
        }
        #endregion

        #region Value Getter
        public NgElement Token(NgElement content, string instanceToken)
        {
            if (!string.IsNullOrWhiteSpace(instanceToken))
            {
                var element = content.SelectToken(instanceToken);

                if (element != null)
                {
                    return element;
                }
                else
                {
                    if (int.TryParse(instanceToken, out var value))
                    {
                        return NgElement.Parse(instanceToken);
                    }

                    return NgElement.Parse("null");
                }
            }

            return null;
        }
        #endregion
        
        #region Minify
        public static string MinifyHtml(string html)
        {
            var result = Regex.Replace(html, @"\r|\n|\t", "");          // supprime retours et tabulations
            result = Regex.Replace(result, @">\s+<", "><");             // supprime espaces entre balises
            result = Regex.Replace(result, @"(?<=>)\s+(?=<)", "");      // supprime indentation texte vide
            result = Regex.Replace(result, @"\s{2,}", " ");             // compresse multiples espaces en 1
            return result.Trim();
        }
        #endregion
        
        #region Parse Json
        private static JsonElement ToJsonElement(object obj)
        {
            var json = JsonSerializer.Serialize(obj);

            using var doc = JsonDocument.Parse(json);

            return doc.RootElement.Clone();
        }
        #endregion
    }
}
