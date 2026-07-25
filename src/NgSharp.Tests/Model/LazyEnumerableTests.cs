using System.Collections;

using NgSharp;

namespace NgSharp.Tests.Model;

// MakeLazy materializes a non-IList enumerable ONCE at wrap time: Count-then-items over the live
// source would re-enumerate it n+1 times (n+1 queries for an IQueryable), and a one-shot iterator
// would silently render empty items after its first pass.
public class LazyEnumerableTests
{
    [Fact]
    public void Non_IList_Enumerable_Is_Enumerated_Exactly_Once_For_A_Full_For()
    {
        var items = new CountingEnumerable();

        var html = HtmlBuilder.Create().BuildFromTemplate(
            "<ul><li [for]=\"Items\">{{ Name }}</li></ul>", new { Items = items });

        Assert.Contains("<li>a</li>", html);
        Assert.Contains("<li>b</li>", html);
        Assert.Contains("<li>c</li>", html);
        Assert.Equal(1, items.GetEnumeratorCalls);
    }

    [Fact]
    public void One_Shot_Enumerable_Renders_All_Its_Items()
    {
        var html = HtmlBuilder.Create().BuildFromTemplate(
            "<ul><li [for]=\"Items\">{{ Name }}</li></ul>", new { Items = new OneShotEnumerable() });

        Assert.Contains("<li>first</li>", html);
        Assert.Contains("<li>second</li>", html);
    }

    private sealed class Row
    {
        public string? Name { get; set; }
    }

    // Counts GetEnumerator calls — Count plus each item read used to cost one full enumeration EACH.
    private sealed class CountingEnumerable : IEnumerable<Row>
    {
        public int GetEnumeratorCalls;

        public IEnumerator<Row> GetEnumerator()
        {
            GetEnumeratorCalls++;
            yield return new Row { Name = "a" };
            yield return new Row { Name = "b" };
            yield return new Row { Name = "c" };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    // Yields only on the FIRST pass — a second enumeration observes nothing (like a consumed reader).
    private sealed class OneShotEnumerable : IEnumerable<Row>
    {
        private bool _consumed;

        public IEnumerator<Row> GetEnumerator()
        {
            if (_consumed)
            {
                yield break;
            }

            _consumed = true;
            yield return new Row { Name = "first" };
            yield return new Row { Name = "second" };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
