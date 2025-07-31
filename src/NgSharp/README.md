# NgSharp

**NgSharp** is a fast, modular, and extensible HTML rendering engine for .NET, built on top
of [AngleSharp](https://anglesharp.github.io/) and inspired by Angular’s philosophy.  
It lets you generate HTML using templates with **pipes**, **directives**, and **custom components**, ideal for rendering
**PDFs, emails, or server-side HTML**.

[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![NuGet](https://img.shields.io/nuget/v/FluentGraphQL.svg)](https://www.nuget.org/packages/NgSharp)

---

## 🚀 Features

- ✅ Angular-style syntax (`*if`: `[if]`, `*for`: `[for]`, `| pipe`)
- 🔧 Fully extensible: add your own **pipes**, **directives**, or **components**
- ⚡ Blazing fast (~4–5 µs per render)
- 🧱 Supports custom components (e.g., static Google Maps)
- 🧪 Snapshot-style HTML testing support
- 🧩 No MVC or Razor dependency

---

## 🧩 Template syntax

```html
<!-- Pipes -->
<span>
    {{ MyDate | date: 'dd/MM/yyyy' }} <!-- Can use same format as DateTime.ToString() -->
</span>

<span>
    {{ MyNumber | number: 'N0' }} <!-- Can use same format as Number.ToString() -->
</span>

<span>
    {{ MyText | upper }}
</span>


<!-- Directives -->
<div [html]="MyHtmlContent"></div>

<a [attr.href]="MyLink"></a>

<span [attr.class]="MyAddedClass" class="initial__class"></span>

<span [style.color]="MySpanColor"></span>


<!-- Structural Directives -->
<div [if]="User.IsActive">
    Welcome {{ User.Name | upper }}
</div>

<div [for]="Items">
    {{ Amount | largeNumber }}
</div>

<div [not-empty]="Items">
    <div [for]="Items">
        {{ Amount | largeNumber }}
    </div>
</div>

<!-- Custom Components -->
<map [points]="MapPoints" [Width]="MapWidth" [Height]="MapHeight"></map>
```

---

## 📊 Benchmarks

| Engine      | Mean time | Memory allocation |
|-------------|-----------|-------------------|
| **NgSharp** | ~4.6 µs   | ~840 B            |
| RazorLight  | ~61.4 µs  | ~4.1 KB           |

NgSharp is up to **13× faster** and **5× lighter** than RazorLight in pure HTML rendering scenarios.

---

## 🔌 Custom Pipes

NgSharp allows you to build custom pipes with logic inside.

```csharp
public class LowerCasePipe : IPipe
{
    public string PipeName => "lower";
    
    public string Transform(IElement childElement, NgElement value, string argument)
    {
        return value.GetString()?.ToLower();
    }
}
```

## 🔌 Custom Directives

NgSharp allows you to build custom directives with logic inside.

```csharp
public class HiddenDirective : IDirective
{
    public string DirectiveName => "hidden";
    
    public bool ApplyWhileParsing => false;

    public void Apply(HtmlBuilder builder, string directiveName, IElement childElement, NgElement content, Dictionary<string, string> optionalArguments = null)
    {
        var booleanValue = content.GetBoolean();

        if (booleanValue == true)
        {
            childElement.SetAttribute("hidden", string.Empty);
        }
    }
}
```

---

## 🔌 Custom components

NgSharp allows you to build custom components with logic inside.  
See the MapComponent for more informations

```csharp
public class CustomComponent : IComponent
{
    public string ComponentName => "custom-component";
        
    public string ComponentText { get; set; }

    public void Render(IElement element)
    {
        var htmlParser = new HtmlParser();

        var image = 
            $"<div>" +
            $"{ComponentText}" +
            $"</div>";

        var node = htmlParser.ParseFragment(image, element);

        element.Parent.InsertBefore(node.First(), element);
        element.Parent.RemoveElement(element);
    }
}
```

---

## 🛠️ Installation

📦 Coming soon to NuGet  
(For now, clone this repository and reference `NgSharp.csproj`)

---

## ⚙️ Usage

```csharp
var builder = HtmlBuilder.Default;
var templateModel = new
{
    FirstProperty = "MyFirstProperty"
};

var result = builder.Render(templateHtml, templateModel);
```

---

## 🔁 RazorLight vs NgSharp

|                   | RazorLight         | NgSharp              |
|-------------------|--------------------|----------------------|
| Syntax            | Razor (.cshtml)    | HTML + Angular-style |
| Rendering mode    | Compiled (Roslyn)  | Interpreted parsing  |
| Performance       | ⚠️ slow (uncached) | ⚡ extremely fast     |
| Custom logic      | ❌ difficult        | ✅ simple and modular |
| Component support | ❌ no               | ✅ yes                |
| Ideal for         | MVC views          | PDF, email, SSR API  |

---

## 📦 Roadmap

- [x] Pipe + directive system
- [x] Custom component architecture
- [x] HTML-based snapshot tests
- [x] NuGet publication
- [ ] Precompiled template support
- [ ] Multi pipes parsing
- [ ] Condition parsing improved
- [ ] Custom template in html, like ng-template for angular

---

## 🤝 Contributing

Pull requests are welcome!  
You can build and share your own pipes, directives or components too.

---

## 📄 License

MIT – free to use and modify.
