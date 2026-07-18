using System.Threading.Tasks;

using NgSharp;

namespace NgSharp.Tests.Model;

// Built-in computed members on NgElement: Count (child/array count) and Length (string length),
// usable in template paths and — the real point — typed in conditions like [if]="Items.Count > 0".
public class ComputedMembersTests
{
    private static Task<string> Render(string tpl, object model)
        => HtmlBuilder.Default.BuildFromTemplateAsync(tpl, model);

    [Fact]
    public async Task Count_of_an_array_is_its_element_count()
    {
        var content = await Render("<p>{{ Items.Count }}</p>", new { Items = new[] { 10, 20, 30 } });

        Assert.Contains("<p>3</p>", content);
    }

    [Fact]
    public async Task Length_of_a_string_is_its_character_count()
    {
        var content = await Render("<p>{{ Name.Length }}</p>", new { Name = "hello" });

        Assert.Contains("<p>5</p>", content);
    }

    [Fact]
    public async Task Count_at_the_root_context()
    {
        var content = await Render("<ul>@for (Items) { <li>{{ Tags.Count }}</li> }</ul>",
            new { Items = new[] { new { Tags = new[] { "a", "b" } }, new { Tags = new[] { "x" } } } });

        Assert.Contains("<li>2</li>", content);
        Assert.Contains("<li>1</li>", content);
    }

    [Fact]
    public async Task Count_is_typed_and_usable_in_a_condition()
    {
        var some = await Render("<div>@if (Items.Count > 0) { <p>has</p> } @else { <p>none</p> }</div>",
            new { Items = new[] { 1 } });
        Assert.Contains("<p>has</p>", some);

        var empty = await Render("<div>@if (Items.Count > 0) { <p>has</p> } @else { <p>none</p> }</div>",
            new { Items = new int[0] });
        Assert.Contains("<p>none</p>", empty);
    }

    [Fact]
    public async Task Length_is_typed_and_usable_in_a_condition()
    {
        var content = await Render("<div>@if (Name.Length > 3) { <b>long</b> }</div>", new { Name = "abcd" });

        Assert.Contains("<b>long</b>", content);
    }

    [Fact]
    public async Task A_real_data_property_wins_over_the_computed_member()
    {
        // The object has an explicit Count field — it must not be shadowed by the child-count computation.
        var content = await Render("<p>{{ Order.Count }}</p>", new { Order = new { Count = 99, Items = new[] { 1, 2 } } });

        Assert.Contains("<p>99</p>", content);
    }
}
