
using NgSharp;

namespace NgSharp.Tests.Model;

// Built-in computed members on NgElement: Count (child/array count) and Length (string length),
// usable in template paths and — the real point — typed in conditions like [if]="Items.Count > 0".
public class ComputedMembersTests
{
    [Fact]
    public void Count_of_an_array_is_its_element_count()
    {
        var content = Render("<p>{{ Items.Count }}</p>", new { Items = new[] { 10, 20, 30 } });

        Assert.Contains("<p>3</p>", content);
    }

    [Fact]
    public void Length_of_a_string_is_its_character_count()
    {
        var content = Render("<p>{{ Name.Length }}</p>", new { Name = "hello" });

        Assert.Contains("<p>5</p>", content);
    }

    [Fact]
    public void Count_at_the_root_context()
    {
        var content = Render("<ul>@for (Items) { <li>{{ Tags.Count }}</li> }</ul>",
            new { Items = new[] { new { Tags = new[] { "a", "b" } }, new { Tags = new[] { "x" } } } });

        Assert.Contains("<li>2</li>", content);
        Assert.Contains("<li>1</li>", content);
    }

    [Fact]
    public void Count_is_typed_and_usable_in_a_condition()
    {
        var some = Render("<div>@if (Items.Count > 0) { <p>has</p> } @else { <p>none</p> }</div>",
            new { Items = new[] { 1 } });
        Assert.Contains("<p>has</p>", some);

        var empty = Render("<div>@if (Items.Count > 0) { <p>has</p> } @else { <p>none</p> }</div>",
            new { Items = new int[0] });
        Assert.Contains("<p>none</p>", empty);
    }

    [Fact]
    public void Length_is_typed_and_usable_in_a_condition()
    {
        var content = Render("<div>@if (Name.Length > 3) { <b>long</b> }</div>", new { Name = "abcd" });

        Assert.Contains("<b>long</b>", content);
    }

    [Fact]
    public void A_real_data_property_wins_over_the_computed_member()
    {
        var content = Render("<p>{{ Order.Count }}</p>", new { Order = new { Count = 99, Items = new[] { 1, 2 } } });

        Assert.Contains("<p>99</p>", content);
    }

    private static string Render(string tpl, object model)
        => HtmlBuilder.Create().BuildFromTemplate(tpl, model);
}
