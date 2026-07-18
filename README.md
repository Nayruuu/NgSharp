# NgSharp

**An interpreted, Angular-style HTML template engine for .NET** — `{{ }}` interpolation, pipes, directives, server components, and `[if]`/`[for]`/`[not-empty]` + `@if`/`@else`/`@for` control flow, rendering structurally-correct, HTML-escaped output.

Because it **interprets** templates instead of compiling them to code, NgSharp starts instantly and runs where Razor-based engines can't: **Native AOT, trimming, Azure Functions, C# scripts, short-lived processes**. **Zero third-party dependencies** — a purpose-built HTML parser (no AngleSharp), nothing to Roslyn-compile at first use.

[![NuGet](https://img.shields.io/nuget/v/NgSharp.svg)](https://www.nuget.org/packages/NgSharp)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

📖 **[Full documentation & live examples →](https://nayruuu.github.io/NgSharp/)**

---

## Why NgSharp

- **No runtime code generation** — nothing to Roslyn-compile or IL-emit, so its cold start is instant and it stays **Native-AOT / trim safe**.
- **Zero third-party dependencies** — only `System.Text.Json`; targets `netstandard2.1` and `net8.0`.
- **Angular-style templates** — interpolation, pipes, `[attr.x]` / `[class.x]` / `[style.x]` / `[html]` bindings, block + attribute control flow, `&&` / `||` / comparisons, ternary.
- **Extensible** — your own pipes, directives and server components.
- **Fast & thread-safe** — immutable AST + stateless renderer; compile once, render many concurrently.

---

## Install

```bash
dotnet add package NgSharp
```

## Quick start

```csharp
using NgSharp;

var builder = HtmlBuilder.Default;

var html = await builder.BuildFromTemplateAsync(
    "<ul><li [for]=\"Users\">{{ Name | upper }}</li></ul>",
    new { Users = new[] { new { Name = "ada" }, new { Name = "linus" } } });

// → <ul><li>ADA</li><li>LINUS</li></ul>
```

Rendering the same template many times? Compile it once — the AST is folded and cached, and it's safe to render concurrently:

```csharp
var tpl = builder.Compile("<p>Hello, {{ Name }}!</p>");
tpl.Render(new { Name = "Ada" });
tpl.Render(new { Name = "Linus" });
```

The model can be an `object` (read via reflection), a `System.Text.Json.JsonElement` (reflection-free — the AOT / trimming path), or a pre-built `NgElement` (the hot path). Values are HTML-escaped automatically.

---

## Template syntax

```html
<!-- interpolation + pipes (current-culture formatting) -->
<h1>{{ Title | upper }}</h1>
<p>{{ CreatedAt | date:'yyyy-MM-dd' }} · {{ Price | number:'C0' }} · {{ Views | largeNumber }}</p>

<!-- attribute / class / style / raw-html bindings -->
<a [attr.href]="Url" [class.active]="IsCurrent">{{ Label }}</a>
<div [style.color]="Color"></div>
<div [html]="TrustedMarkup"></div>

<!-- structural directives (a single element) -->
<span [if]="InStock == true">in stock</span>
<li [for]="Items">{{ Name }}</li>
<ul [not-empty]="Items"> … </ul>

<!-- block control flow -->
@if (User.Age >= 18) { <b>adult</b> } @else { <b>minor</b> }
@for (Items) { <li>{{ Name }} — {{ Price | number:'C2' }}</li> }

<!-- server component -->
<user-card [name]="User.Name"></user-card>
```

Expressions support paths, array indices, the computed members `.Count` / `.Length`, comparisons `== != < > <= >=`, `&&` / `||`, ternary and pipes. Truthiness is strict — only a real boolean is truthy.

Built-in pipes: `date`, `number`, `largeNumber`, `upper`, `image`.

---

## Extend it

Three interfaces — implement one, register it on a builder, use it in templates.

```csharp
// Pipe:  {{ value | lower }}
public sealed class LowerPipe : IPipe
{
    public string PipeName => "lower";
    public string Transform(string tagName, NgElement value, string argument)
        => value.GetString()?.ToLowerInvariant();
}
builder.RegisterPipe<LowerPipe>();

// Directive:  [hidden]="expr"  — mutate the host element
public sealed class HiddenDirective : IDirective
{
    public string DirectiveName => "hidden";
    public void Apply(DirectiveElement element, NgElement content)
    {
        if (content.GetBoolean() == true) element.SetAttribute("hidden", "");
    }
}
builder.RegisterDirective<HiddenDirective>();

// Component:  <badge [count]="Total"></badge>
public sealed class Badge : IComponent
{
    public string ComponentName => "badge";
    public int Count { get; set; }   // bound from the [count] attribute
    public string Render() => $"<span class=\"badge\">{Count}</span>";
}
builder.RegisterComponent<Badge>();
```

Pipes and directives are plain interface calls (reflection-free); component property binding uses reflection, so preserve those members under trimming / Native AOT.

---

## Performance

NgSharp **interprets** — it never compiles to code, so it wins decisively on cold start and stays AOT-safe, while remaining competitive warm. Rendering a 96-item product catalogue across six .NET engines, byte-identical output (Apple M1 Max, .NET 10):

| Engine | Cold (first render) | Warm (steady state) |
|---|---|---|
| **NgSharp** | **~78 µs** | ~47 µs · **~26 µs** with a reused context |
| RazorLight | ~28,000 µs *(Roslyn compile)* | ~37 µs |
| Handlebars.Net | ~4,900 µs *(codegen)* | ~50 µs |
| Fluid | ~57 µs | ~48 µs |
| Scriban | ~176 µs | ~155 µs |

Cold, NgSharp is **tens to hundreds of times faster** than the engines that compile to code — exactly the tax that hurts in serverless, Native AOT and short-lived processes. The [full benchmarks](https://nayruuu.github.io/NgSharp/#benchmarks) include a feature-complete document rendered byte-identically by NgSharp, Handlebars and RazorLight.

---

## When to use it

Reach for NgSharp when you need **HTML-correct, escaped output with a compile-free cold start and zero dependencies** — Native AOT, trimming, serverless, C# scripts, short-lived processes, or generating **PDFs, emails and server-side HTML**. For a full MVC view engine, use Razor; for maximum warm-loop throughput on long-lived servers, the compiled engines edge it.

---

## Roadmap

- [x] Pipes, directives and server components
- [x] `[if]` / `[for]` / `[not-empty]` + `@if` / `@else` / `@for` control flow
- [x] Per-template compile & AST caching (partial evaluation, no codegen)
- [x] Reflection-free `JsonElement` path for Native AOT / trimming
- [x] Zero third-party dependencies (AngleSharp removed)
- [ ] NuGet publication
- [ ] Reusable template fragments (`ng-template`-style)

---

## Contributing

Pull requests are welcome — build and share your own pipes, directives or components.

## License

MIT — free to use and modify.
