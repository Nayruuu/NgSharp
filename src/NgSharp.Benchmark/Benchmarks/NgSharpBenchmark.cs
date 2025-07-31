using System.Text.Json;

using NgSharp;
using RazorLight;

using BenchmarkDotNet.Attributes;

namespace FluentQL.Benchmark.Benchmarks;

[MemoryDiagnoser]
[WarmupCount(5)]
[IterationCount(10)]
public class NgSharpBenchmark
{
    private object model;
    private RazorLightEngine razor;

    private string ngSharpTemplate;
    private string razorlightTemplate;
    
    [GlobalSetup]
    public void Setup()
    {
        this.razor = new RazorLightEngineBuilder()
            .UseEmbeddedResourcesProject(typeof(NgSharpBenchmark))
            .UseMemoryCachingProvider()
            .Build();
        
        this.ngSharpTemplate = File.ReadAllText("Templates/big-test.ngsharp.html");
        this.razorlightTemplate = File.ReadAllText("Templates/big-test.razorlight.cshtml");

        this.model = new
        {
            Title = "hello world",
            Date = new DateTime(2023, 12, 31),
            MyAddedClass = "added__class",
            MyCustomLink = "https://ng-sharp.net",
            MyHtmlData = "<span style='color: red'>That text is red</span>",
            MyInitialWeight = "initial",
            MyBoldWeight = "bold",
            CssClass = "bg",
            User = new
            {
                Name = "Stephane",
                IsActive = true,
                Location = "Paris",
                Zoom = 12
            },
            Items = new[]
            {
                new { Amount = 1200 },
                new { Amount = 1200000 }
            },
            MyArray = Enumerable.Range(1, 10)
                .Select(i => new
                {
                    Id = i,
                    Name = $"John {i}",
                    Tasks = Enumerable.Range(1, 3)
                        .Select(j => new
                        {
                            TaskId = j + 10,
                            TaskName = $"Task {j + 10}"
                        })
                })
                .ToArray()
        };
    }
    
    [Benchmark(OperationsPerInvoke = 1000)]
    public async Task<string> NgSharp()
    {
        var obj = ToJsonElement(model);

        return await HtmlBuilder.Default.BuildFromTemplateAsync(ngSharpTemplate, obj);
    }
    
    [Benchmark(OperationsPerInvoke = 1000)]
    public async Task<string> RazorLight()
    {
        return await razor.CompileRenderStringAsync(Guid.NewGuid().ToString(), razorlightTemplate, model);
    }
    
    private static JsonElement ToJsonElement(object obj)
    {
        var json = JsonSerializer.Serialize(obj);

        using var doc = JsonDocument.Parse(json);

        return doc.RootElement.Clone();
    }
}