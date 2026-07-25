using System.Text.Json;
using System.Collections.Concurrent;

using NgSharp;
using NgSharp.Pipes;

namespace NgSharp.Tests.Concurrency;

// Stress suite for the documented thread-safety invariants — the ones otherwise defended only by
// comments: the per-AST inline caches (PathExpression member-site memo, PipeExpression pipe memo)
// publish immutable holders through single atomic reference writes, RegisterPipe swaps the registry
// copy-on-write so an in-flight render keeps one coherent snapshot, strict-ness travels per call
// (never ambient state), and the parser's [ThreadStatic] FoldEmitter pool stays per-thread. Each
// test hammers one invariant from many threads and byte-compares against a sequential oracle.
public class ConcurrencyStressTests
{
    private const int THREADS = 8;

    // The four heterogeneous carriers hit the SAME template sites: two distinct POCO types (the
    // member-site memo must keep flipping types without ever serving one type's accessor to the
    // other), a dictionary, and a lazy JsonElement.
    private const string CardTemplate =
        "<div class=\"card\"><h1>{{ Name | upper }}</h1><p [if]=\"Vip\">VIP</p>"
        + "<ul><li [for]=\"Items\">{{ Label }}:{{ Qty }}</li></ul><footer>{{ Total }}</footer></div>";

    private sealed class CardA
    {
        public string Name { get; set; } = "";
        public bool Vip { get; set; }
        public List<RowA> Items { get; set; } = new();
        public int Total { get; set; }
    }

    private sealed class RowA
    {
        public string Label { get; set; } = "";
        public int Qty { get; set; }
    }

    private sealed class CardB
    {
        public string Name { get; set; } = "";
        public bool Vip { get; set; }
        public RowB[] Items { get; set; } = Array.Empty<RowB>();
        public int Total { get; set; }
    }

    private sealed class RowB
    {
        public string Label { get; set; } = "";
        public int Qty { get; set; }
    }

    // Kept alive for the whole run: FromJson reads the document lazily, in place.
    private static readonly JsonDocument JsonModel = JsonDocument.Parse(
        "{\"Name\":\"Dora & Sons\",\"Vip\":false,\"Items\":[{\"Label\":\"m\",\"Qty\":4},{\"Label\":\"n\",\"Qty\":5},{\"Label\":\"o\",\"Qty\":6}],\"Total\":3}");

    private static readonly object[] Models =
    {
        new CardA { Name = "Alice & Co", Vip = true, Items = new List<RowA> { new() { Label = "a", Qty = 1 }, new() { Label = "b", Qty = 2 } }, Total = 12 },
        new CardB { Name = "Bruno", Vip = false, Items = new[] { new RowB { Label = "x", Qty = 9 } }, Total = 7 },
        new Dictionary<string, object>
        {
            ["Name"] = "Carla",
            ["Vip"] = true,
            ["Items"] = new List<object> { new Dictionary<string, object> { ["Label"] = "k", ["Qty"] = 3 } },
            ["Total"] = 99,
        },
        JsonModel.RootElement,
    };

    [Fact]
    public void Heterogeneous_Models_Alternating_On_One_Compiled_Template_Render_Byte_Identical_Under_8_Threads()
    {
        // The oracle: each model rendered sequentially on its OWN fresh compile (fresh AST, cold
        // caches) — the concurrent outputs must match it byte for byte, whatever the cache churn.
        var expected = Models.Select(model => Render(HtmlBuilder.Create().Compile(CardTemplate), model)).ToArray();
        Assert.Contains("ALICE &amp; CO", expected[0]);
        Assert.Contains("<p>VIP</p>", expected[2]);
        Assert.DoesNotContain("VIP", expected[3]);

        var compiled = HtmlBuilder.Create().Compile(CardTemplate);
        var failures = RunOnThreads(threadIndex =>
        {
            for (var i = 0; i < 5000; i++)
            {
                // threadIndex offsets the phase so at any instant different threads hit different
                // model kinds on the same access sites.
                var pick = (threadIndex + i) % Models.Length;
                var html = Render(compiled, Models[pick]);

                if (html != expected[pick])
                {
                    return $"model #{pick} diverged on thread {threadIndex} iteration {i}";
                }
            }

            return null;
        });

        Assert.Empty(failures);
    }

    [Fact]
    public void ReRegistering_A_Pipe_During_Concurrent_Renders_Never_Loses_It_And_Never_Mixes_Implementations()
    {
        var builder = HtmlBuilder.Create();
        builder.RegisterPipe(new StampPipe("A"));

        // TWO uses of the pipe in one template: a render that straddled a registry swap incoherently
        // would show a mixed "A…B" output; the copy-on-write snapshot forbids it.
        var compiled = builder.Compile("<p>{{ Name | stamp }}|{{ Name | stamp }}</p>");
        var model = new { Name = "x" };

        var oldForm = "<p>Ax|Ax</p>";
        var newForm = "<p>Bx|Bx</p>";

        using var stop = new ManualResetEventSlim(false);
        var writer = new Thread(() =>
        {
            var flip = false;
            while (stop.IsSet == false)
            {
                builder.RegisterPipe(new StampPipe(flip ? "A" : "B"));
                flip = !flip;
                Thread.SpinWait(50);
            }
        });
        writer.Start();

        try
        {
            var failures = RunOnThreads(threadIndex =>
            {
                for (var i = 0; i < 3000; i++)
                {
                    // Any NgSharpException here (e.g. a transient 'Unknown pipe') fails the test via
                    // RunOnThreads' exception funnel.
                    var html = compiled.Render(model);

                    if (html != oldForm && html != newForm)
                    {
                        return $"mixed or corrupt output '{html}' on thread {threadIndex} iteration {i}";
                    }
                }

                return null;
            });

            Assert.Empty(failures);
        }
        finally
        {
            stop.Set();
            writer.Join();
        }
    }

    [Fact]
    public void Strict_And_Lenient_Renders_Of_The_Same_Compiled_Template_Interleave_Without_Bleeding()
    {
        // Strict-ness must travel with the CALL (options/compile snapshot), never through shared or
        // ambient state: a strict render throwing next to a lenient one must not make the lenient
        // one throw, nor the lenient one soften the strict one.
        var compiled = HtmlBuilder.Create().Compile("<b>{{ Name }}</b>");
        var strictOptions = new TemplateOptions { Strict = true };
        var withName = new { Name = "ok" };
        var noName = new { Other = 1 };

        var failures = RunOnThreads(threadIndex =>
        {
            for (var i = 0; i < 2000; i++)
            {
                switch ((threadIndex + i) % 4)
                {
                    case 0 when compiled.Render(withName) != "<b>ok</b>":
                        return $"lenient present-path diverged (thread {threadIndex}, iteration {i})";

                    case 1 when compiled.Render(withName, strictOptions) != "<b>ok</b>":
                        return $"strict present-path diverged (thread {threadIndex}, iteration {i})";

                    case 2 when compiled.Render(noName) != "<b></b>":
                        return $"lenient missing-path stopped rendering empty (thread {threadIndex}, iteration {i})";

                    case 3:
                        try
                        {
                            compiled.Render(noName, strictOptions);

                            return $"strict missing-path did NOT throw (thread {threadIndex}, iteration {i})";
                        }
                        catch (NgSharpException)
                        {
                            // The deterministic strict outcome.
                        }

                        break;
                }
            }

            return null;
        });

        Assert.Empty(failures);
    }

    [Fact]
    public void Concurrent_Compiles_Of_Nested_Templates_Keep_The_Thread_Local_Emitter_Pool_Coherent()
    {
        // Two templates with nested structural bodies: every compile nests FoldEmitters (each body
        // takes a pooled StringBuilder) and releases them stack-like back to the [ThreadStatic]
        // pool. Alternating templates on each thread re-uses the pooled builders across parses —
        // a stale or shared builder would corrupt the folded const runs.
        const string nested =
            "<section [if]=\"Vip\"><div [for]=\"Items\"><em [if]=\"Qty\">{{ Label }}</em></div></section>"
            + "<script>var a = 1 < 2 && {{ Total }} > 0;</script>";

        var model = Models[0];
        var expectedCard = Render(HtmlBuilder.Create().Compile(CardTemplate), model);
        var expectedNested = Render(HtmlBuilder.Create().Compile(nested), model);

        var builder = HtmlBuilder.Create();
        var failures = RunOnThreads(threadIndex =>
        {
            for (var i = 0; i < 250; i++)
            {
                var useNested = (threadIndex + i) % 2 == 0;
                var compiled = builder.Compile(useNested ? nested : CardTemplate);
                var html = Render(compiled, model);

                if (html != (useNested ? expectedNested : expectedCard))
                {
                    return $"compile #{i} on thread {threadIndex} produced a divergent program";
                }
            }

            return null;
        });

        Assert.Empty(failures);
    }

    [Fact]
    public void Concurrent_Writer_Renders_On_One_Compiled_Template_Stay_Byte_Identical()
    {
        // The oracle is the STRING render on a fresh compile: every per-thread StringWriter drain of
        // the shared CompiledTemplate must reproduce it byte for byte (the capacity-hint race is
        // benign, the pooled buffers are per-render).
        var expected = Models.Select(model => Render(HtmlBuilder.Create().Compile(CardTemplate), model)).ToArray();

        var compiled = HtmlBuilder.Create().Compile(CardTemplate);
        var failures = RunOnThreads(threadIndex =>
        {
            for (var i = 0; i < 2000; i++)
            {
                var pick = (threadIndex + i) % Models.Length;
                using var sink = new StringWriter();

                if (Models[pick] is JsonElement json)
                {
                    compiled.Render(json, sink);
                }
                else
                {
                    compiled.Render(Models[pick], sink);
                }

                if (sink.ToString() != expected[pick])
                {
                    return $"writer render of model #{pick} diverged on thread {threadIndex} iteration {i}";
                }
            }

            return null;
        });

        Assert.Empty(failures);
    }

    // Runs body on THREADS threads behind a barrier (maximum overlap); funnels the first failure
    // message per thread — and any exception — into the returned collection.
    private static IReadOnlyCollection<string> RunOnThreads(Func<int, string?> body)
    {
        var failures = new ConcurrentQueue<string>();
        using var barrier = new Barrier(THREADS);

        var threads = Enumerable.Range(0, THREADS).Select(threadIndex => new Thread(() =>
        {
            barrier.SignalAndWait();
            try
            {
                if (body(threadIndex) is { } failure)
                {
                    failures.Enqueue(failure);
                }
            }
            catch (Exception exception)
            {
                failures.Enqueue($"thread {threadIndex} threw {exception.GetType().Name}: {exception.Message}");
            }
        })).ToList();

        threads.ForEach(thread => thread.Start());
        threads.ForEach(thread => thread.Join());

        return failures;
    }

    // Dispatches to the overload matching the carrier — the same call shapes a real mixed workload uses.
    private static string Render(CompiledTemplate compiled, object model)
        => model is JsonElement json ? compiled.Render(json) : compiled.Render(model);

    private sealed class StampPipe : IPipe
    {
        private readonly string _stamp;

        public string PipeName => "stamp";

        public StampPipe(string stamp) => _stamp = stamp;

        public string Transform(string tagName, NgElement value, string argument) => _stamp + value.GetString();
    }
}
