using NgSharp.Pipes;
using NgSharp.Directives;
using NgSharp.Components;

namespace NgSharp.Tests.Extensibility;

// RegisterPipe(IPipe) / RegisterDirective(IDirective) / RegisterComponent<T>(T instance): the
// DI-friendly instance registrations. Pipes and directives use the SHARED instance (ctor-injected
// configuration must survive); a component instance is a PROTOTYPE — each render still activates a
// fresh T via its public parameterless constructor, so ctor state does NOT flow into renders.
public class InstanceRegistrationTests
{
    #region Pipes

    [Fact]
    public void RegisterPipe_Instance_Keeps_Its_Ctor_Configuration_Across_Renders()
    {
        var builder = HtmlBuilder.Create();
        builder.RegisterPipe(new SuffixPipe(" — SAS Dupont"));

        var compiled = builder.Compile("<p>{{ Name | brand }}</p>");

        Assert.Contains("<p>Alice — SAS Dupont</p>", compiled.Render(new { Name = "Alice" }));
        Assert.Contains("<p>Bob — SAS Dupont</p>", compiled.Render(new { Name = "Bob" }));
    }

    [Fact]
    public void RegisterPipe_Instance_Replaces_A_Pipe_Under_The_Same_Name()
    {
        var builder = HtmlBuilder.Create();
        builder.RegisterPipe(new SuffixPipe("!"));
        builder.RegisterPipe(new SuffixPipe("?"));

        Assert.Contains("<p>x?</p>", builder.BuildFromTemplate("<p>{{ Name | brand }}</p>", new { Name = "x" }));
    }

    #endregion

    #region Directives

    [Fact]
    public void RegisterDirective_Instance_Keeps_Its_Ctor_Configuration()
    {
        var builder = HtmlBuilder.Create();
        builder.RegisterDirective(new MarkDirective("data-state"));

        var html = builder.BuildFromTemplate("<p [mark]=\"Ok\">x</p>", new { Ok = true });

        Assert.Contains("<p data-state=\"on\">x</p>", html);
    }

    #endregion

    #region Components

    [Fact]
    public void RegisterComponent_Instance_Registers_Under_The_Instance_Name_And_Binds_Properties()
    {
        var builder = HtmlBuilder.Create();
        builder.RegisterComponent(new BadgeComponent("ignored-ctor-label"));

        var html = builder.BuildFromTemplate("<count-badge [count]=\"Total\"></count-badge>", new { Total = 7 });

        Assert.Contains("7", html);
        Assert.Contains("<span", html);
    }

    [Fact]
    public void RegisterComponent_Instance_Is_A_Prototype_Renders_Activate_A_Fresh_Instance()
    {
        // The documented contract: ctor state stays on the prototype — the render's fresh instance
        // comes from the public parameterless constructor (label back to its default).
        var builder = HtmlBuilder.Create();
        builder.RegisterComponent(new BadgeComponent("fancy"));

        var html = builder.BuildFromTemplate("<count-badge [count]=\"Total\"></count-badge>", new { Total = 7 });

        Assert.Contains("class=\"badge\"", html);
        Assert.DoesNotContain("fancy", html);
    }

    [Fact]
    public void RegisterComponent_Instance_Under_A_Base_Or_Interface_Type_Throws_ArgumentException()
    {
        var builder = HtmlBuilder.Create();

        Assert.Throws<ArgumentException>(() => builder.RegisterComponent<CardBase>(new FancyCard()));
        Assert.Throws<ArgumentException>(() => builder.RegisterComponent<IComponent>(new FancyCard()));
    }

    [Fact]
    public void RegisterComponent_Instance_With_The_Concrete_Type_Registers_And_Renders()
    {
        var builder = HtmlBuilder.Create();
        builder.RegisterComponent(new FancyCard());

        var html = builder.BuildFromTemplate("<base-card></base-card>", new { });

        Assert.Contains("<div>base</div>", html);
    }

    #endregion

    #region Null guards

    [Fact]
    public void Null_Instance_Registrations_Throw_ArgumentNullException()
    {
        var builder = HtmlBuilder.Create();

        Assert.Throws<ArgumentNullException>(() => builder.RegisterPipe((IPipe)null!));
        Assert.Throws<ArgumentNullException>(() => builder.RegisterDirective((IDirective)null!));
        Assert.Throws<ArgumentNullException>(() => builder.RegisterComponent((BadgeComponent)null!));
    }

    #endregion

    private sealed class SuffixPipe : IPipe
    {
        private readonly string _suffix;

        public SuffixPipe(string suffix)
        {
            _suffix = suffix;
        }

        public string PipeName => "brand";

        public string Transform(string tagName, NgElement value, string argument)
            => (value.GetString() ?? string.Empty) + _suffix;
    }

    private sealed class MarkDirective : IDirective
    {
        private readonly string _attribute;

        public MarkDirective(string attribute)
        {
            _attribute = attribute;
        }

        public string DirectiveName => "mark";

        public void Apply(DirectiveElement element, NgElement content)
        {
            if (content.GetBoolean() == true)
            {
                element.SetAttribute(_attribute, "on");
            }
        }
    }

    private class CardBase : IComponent
    {
        public string ComponentName => "base-card";

        public string Render() => "<div>base</div>";
    }

    private sealed class FancyCard : CardBase
    {
    }

    private sealed class BadgeComponent : IComponent
    {
        private readonly string _label;

        public BadgeComponent()
            : this("badge")
        {
        }

        public BadgeComponent(string label)
        {
            _label = label;
        }

        public int Count { get; set; }

        public string ComponentName => "count-badge";

        public string Render() => $"<span class=\"{_label}\">{Count}</span>";
    }
}
