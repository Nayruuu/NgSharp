# Remove AngleSharp — purpose-built HTML template parser (option B)

**Date:** 2026-07-17 · **Branch:** `parser/remove-anglesharp`

## Goal

Drop the AngleSharp dependency and replace it with a purpose-built, lightweight HTML
*template* tokenizer + tree-builder. NOT a full HTML5 parser: no error-recovery, no
foster-parenting, no implied-end-tags, no adoption-agency. Templates are authored, so we
require well-formedness (fail fast) and keep the one real edge — **structurally correct,
escaped output** — via tag-balance validation, while shedding the heavy dep and its
browser semantics (the `<table>` foster-parenting footgun disappears).

## AngleSharp surface to replace (measured)

1. **Parse** — `Template/TemplateParser.cs`: `HtmlParser().ParseDocument()`, `INode`,
   `IElement`, `INodeList`, `NodeType`, `.ChildNodes`. The core swap.
2. **Render-time** — `Template/TemplateRenderer.cs`:
   - L137: `ParseDocument("").CreateElement(tag)` — builds a host `IElement` for pipes/directives.
   - L176: `ParseDocument(raw).Body.InnerHtml` — normalizes the `[html]` binding's injected HTML.
   - L268 `BridgeDirectives` — re-parses a rendered element to `IElement`, lets `IDirective.Apply` mutate it, re-serializes.
3. **Public API leak** — `IPipe.Transform(IElement, …)`, `IDirective.Apply(…, IElement, …)`,
   `IComponent.Render(IElement)` + built-in pipes/directives all expose AngleSharp's `IElement`.

## Dead-code opportunity (verify first)

In v2, structural `[if]/[for]/[not-empty]` are `TemplateNodes` and `[html]/[attr.x]/[style.x]`
are `BindingNodes` — so the 1.0.x directive classes (`ForDirective` ~260 LOC of DOM cloning,
`IfDirective`, `NotEmptyDirective`, `StyleDirective`, `HtmlDirective`, `AttributeDirective`)
may never be invoked. If confirmed dead, **delete** them instead of porting — removes most of
the `IElement` surface for free.

## Status (2026-07-17)

DONE — AngleSharp is fully removed (package dropped, `AngleSharp.dll` no longer ships, 292 tests green).
Phases 0–5 below are complete except two deferred, separately-scoped follow-ups:
- **MinifyHtml `<pre>`/`<script>` footgun fix** — implemented then reverted: it's a real fix but it
  changes prod output (preserves `<style>`/`<script>` whitespace instead of collapsing it), so it's
  broken out as its own decision rather than bundled with the (prod-byte-safe) AngleSharp removal.
- **AOT hardening** — `<IsAotCompatible>`, a `net8.0` TFM, and replacing the reflection in
  `RenderComponent`/`ConvertValue` with source-gen. Bigger task, not required to drop AngleSharp.

## Phases (each keeps the golden corpus + full test suite green + prod-diff clean)

- **Phase 0 — Safety net (this commit):** characterization/golden harness capturing current
  AngleSharp output over a rich in-repo corpus (every construct at prod-like complexity). Plus a
  local-only diff script pointed at the ProxAfficheApis prod templates (never committed) as an
  extra regression oracle before finalizing.
- **Phase 1 — Tokenizer (TDD):** char-scanner → tokens (TagOpen/Attr/TagOpenEnd/TagClose/Text/
  Comment). Handles quoted/unquoted/bracketed attr names, **void elements**, **rawtext**
  (`<script>`/`<style>`), comments. Entities pass through verbatim.
- **Phase 2 — Tree-builder:** stack-based → emits the *existing* `ElementNode`/`TextNode`/… so
  the renderer is unchanged. Reuse `BuildElement` attribute classification (feed it our attr list).
  Swap `TemplateParser` behind a flag; keep AngleSharp path alive for A/B diffing.
- **Phase 3 — Render-time:** replace the three `TemplateRenderer` AngleSharp uses (or redesign so
  they're unneeded).
- **Phase 4 — API decoupling:** replace `IElement` in `IPipe`/`IDirective`/`IComponent` with our
  own light element abstraction (or `NgElement`); migrate built-ins. Breaking change (few external
  users; his own `tva`/`siret` pipes read the value, not the element).
- **Phase 5 — Remove dep:** drop the `AngleSharp` PackageReference, verify trim/AOT-clean
  (`<IsAotCompatible>`), and fix the `MinifyHtml` `<pre>`/`<script>` footgun using rawtext boundaries.

## Risks to border with tests

Void elements, rawtext (`<script>`/`<style>`), attribute edge cases (unquoted, bracketed,
boolean), self-closing, and any place where AngleSharp's normalization differed from verbatim
pass-through. The Phase-0 diff against 8,500 lines of prod templates is what catches these.
