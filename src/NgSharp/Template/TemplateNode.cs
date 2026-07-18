using System.Collections.Generic;

using NgSharp.Expressions;

namespace NgSharp.Template
{
    public abstract class TemplateNode
    {
    }

    public sealed class TextNode : TemplateNode
    {
        public string Text { get; }

        public TextNode(string text)
        {
            Text = text;
        }
    }

    public sealed class CommentNode : TemplateNode
    {
        public string Text { get; }

        public CommentNode(string text)
        {
            Text = text;
        }
    }

    public sealed class InterpolationNode : TemplateNode
    {
        public Expression Expression { get; }

        public InterpolationNode(Expression expression)
        {
            Expression = expression;
        }
    }

    public sealed class AttributeNode
    {
        public string Name { get; }

        public string Value { get; }

        public AttributeNode(string name, string value)
        {
            Name = name;
            Value = value;
        }
    }

    public enum BindingKind
    {
        Attribute,
        Style,
        Class,
        Html
    }

    public sealed class BindingNode
    {
        public BindingKind Kind { get; }

        public string Target { get; }

        public Expression Expression { get; }

        public BindingNode(BindingKind kind, string target, Expression expression)
        {
            Kind = kind;
            Target = target;
            Expression = expression;
        }
    }

    public sealed class DirectiveBinding
    {
        public string Name { get; }

        public Expression Expression { get; }

        public DirectiveBinding(string name, Expression expression)
        {
            Name = name;
            Expression = expression;
        }
    }

    public sealed class ElementNode : TemplateNode
    {
        public string TagName { get; }

        public IReadOnlyList<AttributeNode> Attributes { get; }

        public IReadOnlyList<BindingNode> Bindings { get; }

        public IReadOnlyList<DirectiveBinding> Directives { get; }

        public IReadOnlyList<TemplateNode> Children { get; }

        public ElementNode(string tagName, IReadOnlyList<AttributeNode> attributes, IReadOnlyList<BindingNode> bindings, IReadOnlyList<DirectiveBinding> directives, IReadOnlyList<TemplateNode> children)
        {
            TagName = tagName;
            Attributes = attributes;
            Bindings = bindings;
            Directives = directives;
            Children = children;
        }
    }

    public sealed class IfNode : TemplateNode
    {
        public Expression Condition { get; }

        public TemplateNode Body { get; }

        // Rendered when Condition is false. null for a plain @if; an @else body, or a nested IfNode
        // for an @else if chain.
        public TemplateNode Else { get; }

        public IfNode(Expression condition, TemplateNode body, TemplateNode elseNode = null)
        {
            Condition = condition;
            Body = body;
            Else = elseNode;
        }
    }

    public sealed class ForNode : TemplateNode
    {
        public Expression Collection { get; }

        public TemplateNode Body { get; }

        public ForNode(Expression collection, TemplateNode body)
        {
            Collection = collection;
            Body = body;
        }
    }

    public sealed class NotEmptyNode : TemplateNode
    {
        public Expression Collection { get; }

        public TemplateNode Body { get; }

        public NotEmptyNode(Expression collection, TemplateNode body)
        {
            Collection = collection;
            Body = body;
        }
    }

    public sealed class ComponentNode : TemplateNode
    {
        public string Name { get; }

        public IReadOnlyDictionary<string, Expression> Properties { get; }

        public ComponentNode(string name, IReadOnlyDictionary<string, Expression> properties)
        {
            Name = name;
            Properties = properties;
        }
    }
}
