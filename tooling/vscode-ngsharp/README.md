# NgSharp Templates for VS Code

Syntax highlighting and snippets for [NgSharp](https://github.com/Nayruuu/NgSharp) — the interpreted, Angular-style HTML template engine for .NET.

The extension does two things:

1. **Injects NgSharp highlighting into regular HTML files.** Your templates stay `.html` — interpolations, pipes, bindings and blocks light up on top of the normal HTML colors.
2. **Adds an `ngsharp-text` language** for text-mode templates (plain-text emails, JSON, CSV): files named `*.ngt` or `*.ng.txt` are picked up automatically; for any other `.txt`, switch via *Change Language Mode → NgSharp Text Template*.

No compiler, no language server, no settings — grammar and snippets only.

## What gets highlighted

In an HTML template, everything NgSharp adds to HTML:

```html
<!-- interpolation: {{ }} delimiters, expression operators, pipe names, pipe arguments -->
<h1>{{ Title | upper }}</h1>
<p>{{ CreatedAt | date:'yyyy-MM-dd' }} — {{ Qty * Price | number:'C2' }}</p>
<p>{{ !Archived && User?.IsActive ? 'active' : 'inactive' }}</p>

<!-- structural attributes as control-flow keywords -->
<span [if]="Stock >= 100">plenty</span>
<span [else-if]="Stock > 0">low</span>
<span [else]="">out</span>
<li [for]="Items">{{ Name }}</li>
<ul [not-empty]="Items">…</ul>

<!-- bindings — the expression inside the quotes is tokenized, not left as a plain string -->
<a [attr.href]="Url" [class.active]="IsCurrent" [style.color]="Color">{{ Label }}</a>
<div [html]="TrustedMarkup"></div>
<img [src]="AvatarUrl" [alt]="Name">

<!-- block control flow, with 'of' as a keyword and $index & co as language variables -->
@if (User.Age >= 18) { <b>adult</b> } @else { <b>minor</b> }
@for (u of Users) { <li>{{ $index + 1 }}/{{ $count }} — {{ u.Name }}</li> }

<!-- fragments: the #reference and the @render call that consumes it -->
<ng-template #card><div class="kpi">{{ Name }} — {{ Budget | number:'C0' }}</div></ng-template>
@render(card, Departments[0])
<ng-container [if]="Ready"><p>no wrapper element</p></ng-container>
```

Broken down, the grammar colorizes:

- `{{ … }}` delimiters (trim-marker variants `{{-` / `-}}` included) and the whole expression inside: string and number literals, `true` / `false` / `null`, comparison / logical / arithmetic operators, safe-navigation `?.`, ternary `? :`
- **pipes** — the `|` separator and the pipe name (`upper`, `date`, `number`, `json`, your own) plus `:'argument'`
- **loop variables** `$index`, `$count`, `$first`, `$last` as language constants
- `[if]` / `[for]` / `[else-if]` / `[else]` / `[not-empty]` as **control-flow keywords**, `[attr.x]` / `[class.x]` / `[style.x]` / `[html]` and bare `[prop]` as **binding attributes** — and in every one of them, the quoted value is highlighted as an expression, not a string
- `@if` / `@else if` / `@else` / `@switch` / `@case` / `@default` / `@for` / `@render` blocks, the `of` keyword, and the block braces in the usual layouts
- `<ng-template>` / `<ng-container>` tags and the `#name` template reference

In an `ngsharp-text` file the same expression and block highlighting applies to raw text — tags are just characters there, exactly like the engine treats them:

```
Hello {{ Name }},

your total is {{ Total | number:'C2' }}.
@if (Premium) {
Thanks for your loyalty!
}
@for (line of Lines) {
- {{ line.Label }}: {{ line.Amount | number:'C2' }}
}
```

## Snippets

Every snippet lives under the `ng-` prefix — type `ng-` to see the whole catalog. The literal syntax works as a prefix too (`@for`, `[if]`, …).

HTML templates: `ng-for`, `ng-if`, `ng-if-else`, `ng-switch`, `ng-if-attr`, `ng-for-attr`, `ng-else-if-attr`, `ng-template`, `ng-render`, `ng-container`, `ng-component`.
Text templates: `ng-for`, `ng-if`, `ng-if-else`, `ng-switch`, `ng-pipe`, `ng-json`.

## Install

The extension is not on the marketplace yet — install it from this folder:

```bash
cd tooling/vscode-ngsharp
npx --yes @vscode/vsce package          # produces ngsharp-0.1.0.vsix
code --install-extension ngsharp-0.1.0.vsix
```

Or, without packaging, symlink the folder into your extensions directory and reload VS Code:

```bash
ln -s "$(pwd)/tooling/vscode-ngsharp" ~/.vscode/extensions/nayruuu.ngsharp-0.1.0
```

## Honest limits

- This is a TextMate grammar: it colorizes, it does not parse. A closing block `}` is only recognized in the common layouts (alone on its line, or right before `@else` / `@case` / `@default`) — a brace glued to other text stays plain.
- `#name` references are highlighted inside `<ng-template …>` tags, where they mean something to the engine.
- No IntelliSense, no diagnostics — for template validation, use `builder.Validate(template)` in a test (see the NgSharp README).

## License

MIT, same as NgSharp.
