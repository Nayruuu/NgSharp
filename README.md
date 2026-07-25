# NgSharp

**An interpreted, Angular-style HTML template engine for .NET.** It brings `{{ }}` interpolation, pipes, directives, server components, and `@if`/`@else`/`@for` + `[if]`/`[for]`/`[not-empty]` control flow, rendering structurally correct, HTML-escaped output.

Because it interprets templates instead of compiling them to code, NgSharp starts instantly and runs where Razor-based engines can't: Native AOT, trimming, Azure Functions, C# scripts, short-lived processes. Zero third-party dependencies: a purpose-built HTML parser (no AngleSharp), nothing to Roslyn-compile at first use.

[![NuGet](https://img.shields.io/nuget/v/NgSharp.svg)](https://www.nuget.org/packages/NgSharp)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

📖 **[Full documentation & live examples →](https://nayruuu.github.io/NgSharp/)**

---

## Why NgSharp

- **No runtime code generation** — nothing to Roslyn-compile or IL-emit, so its cold start is instant and it stays Native-AOT / trim safe.
- **Zero third-party dependencies** — only `System.Text.Json`; targets `netstandard2.1` and `net8.0`.
- **Angular-style templates** — interpolation, pipes, `[attr.x]` / `[class.x]` / `[style.x]` / `[html]` and bare `[prop]` bindings, block + attribute control flow, comparisons, logical `&&` / `||` / `!`, arithmetic `+ - * / %`, safe-navigation `?.`, ternary.
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

var builder = HtmlBuilder.Create();   // a NEW builder, pre-loaded with the built-in pipes

var html = builder.BuildFromTemplate(
    "<ul>@for (u of Users) {<li>{{ u.Name | upper }}</li>}</ul>",
    new { Users = new[] { new { Name = "ada" }, new { Name = "linus" } } });

// → <ul><li>ADA</li><li>LINUS</li></ul>
```

Rendering is CPU-bound, so `BuildFromTemplate` is synchronous — there is nothing to await and no async overload. `HtmlBuilder.Create()` returns a *new* builder on every call, so register your pipes/directives/components on the one instance you keep.

Everything tunable rides one optional `TemplateOptions` record — dialect, strict mode, culture, resource caps — on every render and compile call:

```csharp
builder.BuildFromTemplate(template, model, new TemplateOptions
{
    Mode = TemplateMode.Text,               // dialect (compile-time; default Html)
    Strict = true,                          // fail loud (compile-time gate + render-time checks)
    Culture = new CultureInfo("fr-FR"),     // pipe formatting (render-time; default ambient)
    Limits = new RenderLimits(),            // resource caps (render-time; default none)
});
```

Rendering the same template many times? Compile it once: the AST is folded and cached, and it's safe to render concurrently:

```csharp
var tpl = builder.Compile("<p>Hello, {{ Name }}!</p>");
tpl.Render(new { Name = "Ada" });
tpl.Render(new { Name = "Linus" });
```

The model can be an `object` (read via reflection), a `System.Text.Json.JsonElement` (reflection-free, the AOT / trimming path), or a pre-built `NgElement` (the hot path). Values are HTML-escaped automatically. Lazy reads evaluate each template reference independently: a one-shot enumerable (a live `DataReader`, a non-replayable LINQ source) is consumed per reference, so materialize it (`ToList()`) if the template reads it more than once.

### Rendering to a `TextWriter` — and honest async

A compiled template also renders straight into any `TextWriter` — a `StringWriter`, a file, an HTTP response. Both forms take the same models and the same options:

```csharp
tpl.Render(model, writer);   // sync — same output, written to the sink

await using var writer = new StreamWriter(response.Body);
await tpl.RenderAsync(model, writer, cancellationToken: ct);
```

`RenderAsync` is honest about where the async lives. The walk is CPU-bound and stays synchronous; the `await` is the write to your writer — the real I/O.

And the sink is atomic. Nothing reaches the writer until the render has fully succeeded: a throwing render (strict miss, exceeded cap) writes **zero** characters. What the sink saves is the final output string.

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

<!-- arithmetic, string concat, unary !, safe navigation, bare property bindings -->
<p>{{ First + ' ' + Last }} — total {{ Qty * Price }}</p>
@if (!Archived && User?.IsActive) { <span>active</span> }
<img [src]="AvatarUrl" [alt]="Name">

<!-- control flow: @if / @else if / @else chains, @for loops, @switch selection -->
@if (User.Age >= 18) { <b>adult</b> } @else { <b>minor</b> }
@for (u of Users) { <li>{{ u.Name }} — {{ CompanyName }}</li> }
@if (Stock >= 100) { <span>plenty</span> } @else if (Stock > 0) { <span>low</span> } @else { <span>out</span> }
@switch (Status) { @case ('open') { <b>open</b> } @case ('done') { <i>done</i> } @default { <u>other</u> } }

<!-- loop variables — inside any @for / [for]: $index (0-based), $count, $first, $last -->
@for (p of Products) { <li [class.first]="$first" [class.last]="$last">{{ $index + 1 }}/{{ $count }} — {{ p.Name }}</li> }

<!-- reusable fragments + transparent grouping -->
<ng-template #card><div class="kpi">{{ Name }} — {{ Budget | number:'C0' }}</div></ng-template>
@render(card, Departments[0])
<ng-container [if]="Ready"><p>no wrapper element in the output</p></ng-container>

<!-- server component -->
<user-card [name]="User.Name"></user-card>
```

Expressions support paths, safe-navigation `?.`, array indices, the computed members `.Count` / `.Length`, comparisons `== != < > <= >=`, logical `&&` / `||` / `!`, arithmetic `+ - * / %` (`+` also concatenates strings), ternary and pipes. Truthiness is strict: only a real boolean is truthy. `<script>` / `<style>` content is raw text: interpolations inside render unescaped, everything else is preserved verbatim.

`@switch (Expr)` evaluates its expression **once** and renders the first `@case (…)` whose value equals it — exactly the `==` operator's equality — falling back to `@default`, then to nothing (a matchless switch is not an error, strict mode included). Between the switch's braces only `@case` / `@default` and whitespace are legal; anything else never renders, and `Validate` flags it. Both dialects support the block, text mode included.

### Built-in pipes

Thirteen pipes ship on every `HtmlBuilder.Create()`:

| Pipe | Example | Notes |
|---|---|---|
| `date` | `{{ CreatedAt \| date:'yyyy-MM-dd' }}` | .NET date format strings, current culture. |
| `number` | `{{ Price \| number:'N2' }}` | .NET numeric format strings, current culture. |
| `currency` | `{{ Price \| currency:'EUR' }}` | Current-culture `C` format with the currency pinned by ISO code — `12,50 €` under fr-FR, `€12.50` under en-US. Without argument: the plain current-culture format. |
| `largeNumber` | `{{ Views \| largeNumber }}` | Magnitude suffixes — `1500` becomes `1.5K`. |
| `upper` | `{{ Name \| upper }}` | Uppercases (current culture). |
| `lower` | `{{ Name \| lower }}` | Lowercases (current culture). |
| `titlecase` | `{{ Title \| titlecase }}` | Capitalizes each word, lowercases the rest — `HELLO world` → `Hello World`. |
| `default` | `{{ Nickname \| default:'—' }}` | Substitutes the argument for null/undefined or a blank string. `false` and `0` are values — kept. |
| `truncate` | `{{ Summary \| truncate:80 }}` | Caps at N characters, `…` included as the last one; N defaults to 50. |
| `join` | `{{ Tags \| join:' - ' }}` | Joins a collection's items; the separator defaults to `', '`. Items format like interpolations. |
| `pad` | `{{ Id \| pad:6 }}` | Left-pads with `0` to the width — `42` becomes `000042`. |
| `image` | `[src]="Logo \| image"` | `ImageData` → data URI (below). |
| `json` | `{{ Name \| json }}` | The complete JSON literal, recursive (below). |

The `image` pipe reads an `ImageData`-shaped value (`FileName` gives the MIME type, `FileContent` the raw bytes) and emits a data URI: bare on an `<img>` `[src]`, wrapped in `url(…)` on any other element. The `json` pipe is **recursive**: an object or array serializes whole (nested objects, arrays and scalars included) as one valid JSON literal, not just scalar values.

### The attribute idiom

Control flow also comes as attributes, scoped to a single element. The condition or the loop rides the host tag, and the element itself is what gets kept, dropped or repeated, with no braces around the markup:

```html
<tr [for]="Lines"><td>{{ $index + 1 }}</td><td>{{ Label }}</td></tr>
<span [if]="Stock >= 100">plenty</span><span [else-if]="Stock > 0">low</span><span [else]="">out</span>
<ul [not-empty]="Items"> … </ul>
<p [empty]="Items">Nothing yet.</p>
```

It is a distinct idiom, not a synonym for the blocks:

| Attribute | Block form | Notes |
|---|---|---|
| `<span [if]="expr">…</span>` | `@if (expr) { <span>…</span> }` | Same output; the attribute keeps or drops its host element. |
| `<span [else-if]="expr">` + `<span [else]="">` | `@else if (expr) { … }` + `@else { … }` | The attribute chain runs across immediate sibling elements. |
| `<tr [for]="Lines">…</tr>` | `@for (Lines) { <tr>…</tr> }` | Both make each item the context: `{{ Name }}` reads the item directly. |
| — | `@for (x of Lines) { … }` | Block-only. The item answers to `x` (`{{ x.Name }}`); bare names resolve on the outer scope. |
| `$index` `$count` `$first` `$last` | identical | The loop variables work the same in both forms; the nearest enclosing loop wins. |
| `<ul [not-empty]="Items">` | — | Attribute-only: renders the element when the collection is non-empty. No block equivalent. |
| `<p [empty]="Items">` | — | Attribute-only: the dual of `[not-empty]` — renders the element when the collection is empty or absent. No block equivalent. |

The context rule is the one to internalize. `[for]` switches the context implicitly: inside the repeated element, `{{ Name }}` is the current item's `Name`, and outer fields stay reachable through the scope chain. The named block `@for (x of Lines)` does the opposite — the item answers only to `x`, and a bare name resolves on the outer scope (Angular semantics).

Prefer the attribute idiom when the markup is the point: print and PDF documents dense with tables, where `<tr [for]="Lines">` repeats the row without adding a single brace. The realistic-benchmark templates, mirrors of production documents, are written in it.

---

## Text mode — beyond HTML

The same engine renders non-HTML output (plain-text emails, JSON, CSV) with `TemplateMode.Text` in the options:

```csharp
var text = new TemplateOptions { Mode = TemplateMode.Text };

var body = builder.BuildFromTemplate(
    "Hello {{ Name }},\nyour total is {{ Total | number:'C2' }}.\n@if (Premium) { Thanks for your loyalty! }",
    model,
    text);

var row = builder.Compile("{\"name\":{{ Name | json }},\"score\":{{ Score }}}", text);
```

Text mode keeps interpolations, pipes, expressions and the `@if` / `@else` / `@for` blocks, and emits everything raw: no HTML escaping, so a `number` pipe's non-breaking group separator stays a real character.

Bare interpolations write **machine literals**: booleans come out `true` / `false` and numbers culture-invariant — `3.14` even on a fr-FR thread. That's what keeps your JSON JSON, whatever the server culture.

Pipes are the opposite, by design: they format for humans, with the current culture. `{{ Total | number:'C2' }}` in a fr-FR email says `12,50 €`.

For string values in JSON templates, the `json` pipe emits the complete JSON literal: quoted and escaped, quotes, backslashes and newlines included:

```csharp
// "name":{{ Name | json }}   — the quotes come from the pipe
// Name = "He said \"hi\""    → "name":"He said \"hi\""
```

### Whitespace control — `{{-` and `-}}`

Text output is unforgiving: every space and line break in the template lands in the email. Scriban/Liquid-style trim markers fix that, resolved *at parse time*: zero render cost, and templates without markers are byte-identical:

- `{{- expr }}` trims the whitespace (line breaks included) that *precedes* the interpolation, up to the previous interpolation, block or tag boundary.
- `{{ expr -}}` trims the whitespace that *follows*, same boundary rule.
- `{{- -}}` is the empty *whitespace eater*: renders nothing, trims both sides.

A marker is only active **flush against its braces with a space on the expression side**, so negation and subtraction are never captured: `{{ -X }}` and `{{-X }}` both mean minus-X, and `{{ A - B }}` stays a subtraction (`-}}` needs a space before the dash, `{{-` a space after it).

The `@if` / `@for` braces carry no trim syntax of their own. The eater covers the classic email need, making the block's own lines vanish cleanly in both branches:

```
Bonjour {{ Name }},
@if (Vip) {
{{- -}}
Merci pour votre fidélité !
}{{- -}}
Cordialement
```

```
Bonjour Alice,                     Bonjour Alice,
Merci pour votre fidélité !        Cordialement
Cordialement
```

Markers work in HTML mode too (escaping unchanged), and only ever trim *template* whitespace: a value's own spaces always survive.

A few grammar edges to know:

- **Braces scope blocks.** An unpaired `{` or `}` in static text skews `@if` / `@for` block matching; every block language shares this trait, Razor included. Escape a lone brace as an interpolated literal: `{{ '}' }}`.

- **Don't glue an interpolation to a block brace.** `@if (x) {{ X }}` parses as block-open plus a literal brace run; write `@if (x) { {{ X }} }`.

- **String data in JSON templates wants the `json` pipe.** A raw `{{ Name }}` between hand-written quotes breaks on the first `"` or newline in the data; `{{ Name | json }}` never does.

Tags, components, directives and `<ng-template>` are HTML-mode concepts and don't apply: in text mode they are just characters, output verbatim. A literal `@` (email addresses…) passes through untouched. The default everywhere remains `TemplateMode.Html`, so existing code is unaffected.

Text mode has its own benchmark arena: the realistic quote model exported as strict-valid JSON (5.7 KB of output), ported byte-identically to Fluid, Handlebars and Scriban and gated by a `JsonDocument.Parse` check. Pure text is the native terrain of these engines — and NgSharp is still first on every cell, time and allocations, cold and warm:

| Engine | Cold (parse + render) | Warm (render only) | Warm alloc |
|---|---|---|---|
| **NgSharp** *(Text)* | 52 µs | 37 µs | 40 KB |
| Handlebars.Net | ~13,000 µs *(codegen)* | 46 µs | 61 KB |
| Fluid | 94 µs | 62 µs | 73 KB |
| Scriban | 391 µs | 310 µs | 357 KB |

One porting anecdote: Handlebars.Net cannot lex the `}}}` a minified JSON template produces at every object close; its port needs a dedicated helper just to emit the closing brace.

---

## Per-render culture

Pipes format with the *current culture* by default. To serve multiple locales from one process (one invoice in `fr-FR`, the next in `de-DE`), set `TemplateOptions.Culture` on any `BuildFromTemplate` call or on `CompiledTemplate.Render`:

```csharp
using System.Globalization;

var invoice = builder.Compile(template);

var french = invoice.Render(model, new TemplateOptions { Culture = new CultureInfo("fr-FR") });   // 1 234,50 €
var german = invoice.Render(model, new TemplateOptions { Culture = new CultureInfo("de-DE") });   // 1.234,50 €
```

The engine swaps `CultureInfo.CurrentCulture` / `CurrentUICulture` around the render and restores them in a `finally`: thread-local, so concurrent renders on other threads are unaffected, and a `null` culture keeps the ambient one. A culture given to `Compile` becomes the compiled template's *default* culture, still overridable per render. Text-mode bare interpolations stay culture-invariant either way (machine literals); only pipe formatting follows the culture.

---

## Extend it

Three interfaces: implement one, register it on a builder, use it in templates. Each contract lives in its own namespace: `NgSharp.Pipes` (`IPipe`), `NgSharp.Directives` (`IDirective`, `DirectiveElement`), `NgSharp.Components` (`IComponent`).

```csharp
using NgSharp;
using NgSharp.Pipes;
using NgSharp.Directives;
using NgSharp.Components;

// Pipe:  {{ value | lower }}
public sealed class LowerPipe : IPipe
{
    public string PipeName => "lower";
    public string Transform(string tagName, NgElement value, string argument)
        => value.GetString()?.ToLowerInvariant() ?? string.Empty;
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

Pipes and directives are plain interface calls (reflection-free). Component property binding uses reflection; `RegisterComponent<T>()` carries the trimmer annotations, so your component's constructor and properties are preserved automatically under trimming / Native AOT.

Each registration also has an *instance* overload, the DI-friendly path for extensions carrying constructor-injected configuration or services:

```csharp
builder.RegisterPipe(new BrandPipe(options.BrandName));   // shared across renders — thread-safe/immutable
builder.RegisterDirective(new ThemeDirective(theme));     // same sharing contract
builder.RegisterComponent(new Badge());                   // prototype only — see below
```

A pipe or directive instance is **shared by every render** of the builder, potentially concurrently: make it thread-safe, ideally immutable. A component instance is a **prototype**: it contributes its `ComponentName` and its concrete type, but every render still activates a *fresh* instance through the public parameterless constructor and binds `[prop]` attributes on that fresh copy. Constructor state does not flow into renders.

One security boundary to know: a component's `Render()` output is **trusted raw HTML**. The engine injects it verbatim, without escaping, exactly like an `[html]` binding (an innerHTML-style trusted assignment). Escape any user-supplied data inside your component before embedding it in the returned markup (e.g. `System.Net.WebUtility.HtmlEncode`), or a `<script>` in a bound property lands in the page as a live tag.

Two caveats on component binding:

- **Complex-typed properties bound from a `JsonElement` model** deserialize through reflection-based System.Text.Json: under trimming, enable `JsonSerializerIsReflectionEnabledByDefault` and keep the element types rooted in your app (the annotations aren't transitive to element types). A binding that can't convert sets nothing, silently.
- **Any public settable property** of a registered component is bindable from the template (case-insensitive), and CLR models are handed to components by live reference: treat bound objects as read-only inside a component.

---

## Strict mode & validation

By default the engine is *lenient*: a mistyped path renders as an empty string, an unknown construct stays literal, and the document still ships. Perfect for production resilience, terrible for finding out *why* an invoice cell is blank. Two opt-in tools close that gap.

### `Validate` — catch template mistakes before they ship

`Validate` parses the template and reports everything the lenient renderer would swallow silently — without throwing, with a character `Position` into the source:

```csharp
var diagnostics = builder.Validate(template);            // IReadOnlyList<TemplateDiagnostic>

foreach (var d in diagnostics)
    Console.WriteLine(d);
// Error [position 23]: '@for (x in Items)' uses 'in' — did you mean '@for (x of Items)'? …
// Error [position 87]: Unclosed interpolation '{{' — no matching '}}', so it renders as literal text.
```

It flags, as **errors**: unclosed `{{` interpolations, empty or unparsable expressions, `@for (x in …)` (NgSharp loops use `of`: the classic slip that renders an empty block), unclosed `@if` / `@for` blocks, orphan `@else` / `[else-if]` / `[else]` branches, and malformed pipe segments (`{{ X | }}`, `{{ X | number: }}`). As **warnings**: pipes not registered on this builder, interpolations kept literal because their body spans a line break, dashed tags (`<user-card>`) not registered as components on this builder (if it *is* a component, register it before `Compile`; a genuine custom element renders verbatim and can ignore the warning), statically always-false `@if` / `[if]` conditions (a string or number literal, or an arithmetic result — strict truthiness never coerces, so the body can never render), and division or modulo by a literal zero (the lenient render always yields `0`).

Validation runs against the builder's registrations, so register your pipes, components and directives first. An empty list means the template is clean: one `Assert.Empty(builder.Validate(template))` per template makes a complete CI gate.

### Strict rendering — missing data fails loud

`TemplateOptions.Strict = true` on `Compile` (or any `BuildFromTemplate` call) turns silent holes into exceptions. `HtmlBuilder.Create(strict: true)` makes strict the builder's default; an explicit `Strict` in a call's options still wins:

```csharp
var builder = HtmlBuilder.Create(strict: true);            // strict everywhere on this builder
var tpl = builder.Compile(template);                       // throws if Validate finds errors

tpl.Render(model);
// NgSharpException: Strict mode: the path 'Customer.Nane' was not found in the model …

builder.Compile(template, new TemplateOptions { Strict = false });   // explicit per-call override: lenient
```

The rules are precise:

- A path that **does not exist** in the model throws `NgSharpException`, naming the path.
- A property that **is present with a null value** renders empty, as always: strict distinguishes *absent* from *null*.
- Paths guarded with `?.` (`{{ Delivery?.Date }}`) are declared optional and never throw.
- An `@if` / `[if]` condition that evaluates to a **non-boolean non-null** value throws, naming the condition: the Angular habit of `*ngIf="items.length"` finally fails loud instead of silently rendering nothing (strict truthiness: only real booleans are truthy). Null and `?.`-guarded conditions stay silently falsy.
- **Division or modulo by zero** throws, naming the expression (non-strict keeps rendering `0`).
- An unknown pipe throws at render, strict or not (it always has).
- A strict `Compile` runs `Validate` first and refuses a template with errors.

The non-strict path is untouched: same output byte for byte, zero cost. The flag rides the render scope and is only read when a resolution fails.

### Strict by convention

Strict mode earns its keep when it is the *default*, not an option someone remembers. A ten-line team wrapper makes it structural: one static class owns the app's only builder, configured strict once, and every render in the app goes through it.

```csharp
public static class Templates
{
    public static readonly HtmlBuilder Builder = CreateBuilder();

    public static readonly TemplateOptions Legacy = new TemplateOptions { Strict = false };

    private static HtmlBuilder CreateBuilder()
    {
        var builder = HtmlBuilder.Create(strict: true);   // strict is the house default
        // register your pipes, directives and components here — once

        return builder;
    }
}
```

New templates compile through `Templates.Builder.Compile(template)` and are strict without anyone thinking about it. The templates you inherited keep rendering through `Templates.Builder.Compile(oldTemplate, Templates.Legacy)` — a visible, deliberate exception.

That is the intended posture: lenient mode is for legacy templates; strict is the convention for new ones.

---

## Untrusted templates

Everything above assumes the template is yours. When templates come from users or tenants, opt into `RenderLimits`: resource caps enforced during the render, off (and free) by default:

```csharp
var capped = new TemplateOptions
{
    Limits = new RenderLimits(maxOutputChars: 500_000, maxLoopIterations: 5_000, maxRenderDepth: 20),
};

builder.Compile(template).Render(model, capped);         // compiled path
builder.BuildFromTemplate(template, model, capped);      // one-shot path
```

Exceeding any cap throws `NgSharpException` with a `Render limit exceeded: …` message. Omitting the options (or passing `RenderLimits.None`) enforces nothing and costs nothing: the default render path is unchanged, byte for byte. Limits given to `Compile` become the compiled template's default caps, still overridable per render.

**What the caps cover:** unbounded output (`MaxOutputChars`), oversized loops (`MaxLoopIterations`, checked per loop against the collection's count), and runaway `@render` fragment recursion (`MaxRenderDepth`, which generalizes the engine's built-in depth-50 guard and throws instead of silently truncating).

**What they don't — this is not a sandbox.** The expression language is bounded by design (no method calls, no arbitrary code: paths, comparisons, arithmetic, pipes), but a hostile *model* stays out of scope: the data you bind is your code's responsibility. So are the two trusted-raw-HTML doors, `[html]` bindings and component `Render()` output: an untrusted template must not be given components or data that emit unescaped user input, and interpolations inside `<script>`/`<style>` render unescaped by design.

---

## Performance

NgSharp *interprets* — no code generation, no compile cliff. The V3 engine (single-pass fused parser, lazy zero-copy model reads, monomorphic inline caches, span-formatted pipes) puts it ahead of every engine measured, warm and cold, in time and in allocations. A 96-item product catalogue across six .NET engines, byte-identical output (Apple M1 Max, .NET 10):

| Engine | Cold (first render) | Warm (steady state) | Warm alloc |
|---|---|---|---|
| **NgSharp** | 32 µs | 25 µs | 33 KB |
| RazorLight | ~29,000 µs *(Roslyn compile)* | 37 µs | 98 KB |
| Handlebars.Net | ~10,000 µs *(codegen)* | 48 µs | 90 KB |
| Fluid | 56 µs | 67 µs | 53 KB |
| Scriban | 168 µs | 153 µs | 242 KB |
| Stubble | 86 µs | — | — |

On the feature-complete showcase document (90 KB of output: fragments, components, `@switch`, the `[empty]`/`[not-empty]` guards, custom directives and the built-in pipe set, rendered byte-identically by NgSharp, Handlebars, RazorLight and Fluid), the gap widens. NgSharp renders warm in 297 µs / 228 KB, the compiled engines take 361–396 µs at ~2.2× the allocations, and Fluid, the other interpreted engine, takes 526 µs at 2.9×. Cold, the compiled engines pay their cliff in full: 45.5 ms for Handlebars, 131 ms and 12 MB for RazorLight, where NgSharp needs 457 µs.

Realistic print/PDF documents (a pipe-heavy quote, a product sheet, a card grid, ported byte-identically to Fluid, Handlebars and Scriban) tell the same story on every cell: the 31 KB quote renders cold in 106 µs and warm in 71 µs / 75 KB, ahead of compiled Handlebars (106 µs / 163 KB) with no compile cliff to amortize. Full tables and methodology: [benchmarks →](https://nayruuu.github.io/NgSharp/#benchmarks)

Don't take the repo's word for it: CI re-runs the four **byte-identity gates** on every push (every engine port must render the same bytes, or the build goes red), and a public [benchmarks workflow](.github/workflows/benchmarks.yml) (`workflow_dispatch`, plus a monthly schedule) re-runs all four BenchmarkDotNet suites on a neutral GitHub runner and uploads the raw results as an artifact. Absolute numbers shift with the hardware; the *ordering* is the reproducible claim.

---

## When to use it

Reach for NgSharp when you need **HTML-correct, escaped output with a compile-free cold start and zero dependencies**: Native AOT, trimming, serverless, C# scripts, short-lived processes, or generating PDFs, emails and server-side HTML. For a full MVC view engine integrated into ASP.NET's pipeline, Razor remains the native choice; for pure template rendering, the benchmarks above have NgSharp ahead of the compiled engines, warm as well as cold.

---

## Roadmap

- [x] Pipes, directives and server components
- [x] `@if` / `@else` / `@for` + `[if]` / `[for]` / `[not-empty]` control flow
- [x] Per-template compile & AST caching (partial evaluation, no codegen)
- [x] Reflection-free `JsonElement` path for Native AOT / trimming
- [x] Zero third-party dependencies (AngleSharp removed)
- [x] Reusable template fragments (`<ng-template>` + `@render`) and transparent `ng-container`
- [x] V3 engine: fused single-pass parser, lazy model reads, inline caches, span-formatted pipes
- [x] Strict mode & template validation (`Validate` diagnostics, `TemplateOptions.Strict` compile/render)
- [x] Streaming output (`TextWriter` sinks) + honest async: `RenderAsync` awaits the writer — the walk stays CPU-synchronous, and a throwing render writes nothing (atomic)
- [x] Validate covers every silent-failure trap; parser hardened by structural fuzzing
- [ ] NuGet publication
- *Not planned:* incremental streaming (mid-walk flushes) — it would break the atomic-output contract (see [Versioning promise](#versioning-promise))

---

## Migrating from 2.x

3.0 is a breaking release. Eight changes, each with its remedy:

- **`HtmlBuilder.Default` is removed.** It was a factory disguised as a singleton: every access returned a *new* builder, so registrations made on one access were silently lost on the next. Replace it with `HtmlBuilder.Create()` and keep the instance you register on.

- **`HtmlBuilder.MinifyHtml` is removed.** Rendering now emits your template verbatim, and the opt-in minifier went with it. It was a four-line `Regex` utility, trivial to copy into your own code if you still want it.

- **`HtmlBuilder.Token` is removed.** It resolved a path against a context, falling back to parsing the token as a literal. Compose the two calls it wrapped yourself: `content.SelectToken(path)`, then `NgElement.Parse(literal)` when the result's `IsUndefined` is true.

- **`BuildFromTemplateAsync` is removed.** Rendering is CPU-bound and completes synchronously — the wrappers only returned `Task.FromResult`. Replace `await builder.BuildFromTemplateAsync(...)` with `builder.BuildFromTemplate(...)` (drop the `await`). Need async? `RenderAsync(model, writer, cancellationToken)` awaits real I/O.

- **`FromObject` / `FromJson` lose their `parent` / `key` parameters.** The engine never used them: delete the extra arguments and the calls compile again.

- **`FromJson` reads the `JsonElement` lazily.** There is no document store anymore: rendering reads your `JsonDocument` in place. Keep it undisposed for the life of the render, or pass `json.Clone()` if it may be disposed first.

- **`NgElement.Key` always returns `string.Empty`.** Lazy nodes no longer record their position in the parent. If you need property names, enumerate `Properties`: its keys are the names.

- **The built-in pipes are `sealed`** (`DatePipe`, `NumberPipe`, `UpperPipe`, `LargeNumberPipe`, `ImagePipe`). Inheriting to tweak one no longer compiles: register your own pipe under a different name, or wrap a built-in instance by composition and delegate to it.

---

## Versioning promise

The 1.x → 3.0 churn was the engine finding its shape. That phase is over — **the API churn ends here.**

- **3.x is stable.** No breaking change to the public API without a major version bump — semver, applied literally.

- **Deprecate before delete.** Anything scheduled for removal ships `[Obsolete]` first, with a message naming its replacement, for at least one minor release before a major removes it.

- **Output is part of the contract.** Rendered bytes only change behind an explicit opt-in or a major; the golden-locked test suite is what holds that line.

- **The boundary is drawn.** Streaming output (`TextWriter` sinks) and honest async are **delivered** — in the one form that keeps the output contract: the walk renders fully, then the writer receives the final output in a single awaited write. A throwing render writes nothing (atomic). What stays excluded is *incremental* rendering — flushing mid-walk, while the tree is still being evaluated — because it would sacrifice exactly that atomicity: a failing render would leave half a document in your sink. No version is promised for it.

---

## Tooling

**VS Code extension** — [`tooling/vscode-ngsharp/`](tooling/vscode-ngsharp/) highlights NgSharp templates inside plain `.html` files (interpolations, pipes, `@if`/`@for`/`@render` blocks, `[if]`/`[for]` bindings, `<ng-template>` fragments) and adds an `ngsharp-text` language for text-mode templates (`.ngt`, `*.ng.txt`), plus snippets for the common constructs. Install instructions in its [README](tooling/vscode-ngsharp/README.md).

---

## Contributing

Pull requests are welcome: build and share your own pipes, directives or components.

## License

MIT — free to use and modify.
