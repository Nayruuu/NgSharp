using NgSharp.Pipes;
using NgSharp.Components;
using NgSharp.Tests.CustomElements;

using System.Globalization;
using System.Runtime.InteropServices;

namespace NgSharp.Tests;

public class Tests
{
    private readonly HtmlBuilder htmlBuilder;

    public Tests()
    {
        this.htmlBuilder = HtmlBuilder.Default;
        
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("fr-FR");
        CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("fr-FR");
    }
    
    #region Pipes
    [Fact]
    public async Task Should_Replace_DateTime_With_Format_Provided_Using_Date_Pipe()
    {
        var template = File.ReadAllText("Templates/pipes/date-pipe/date.pipe.html");
        var resultTemplate = File.ReadAllText("Templates/pipes/date-pipe/date.pipe.result.html");

        var obj = new
        {
            NullDate = (DateTime?)null,
            OneDate = new DateTime(2014, 03, 13, 23, 33, 32)
        };
        
        var content = await this.htmlBuilder.BuildFromTemplateAsync(template, obj);

        Assert.Equal(HtmlBuilder.MinifyHtml(resultTemplate), content);
    }

    [Fact]
    public async Task Should_Set_Image_Data_Using_Image_Pipe()
    {
        var imageContent = File.ReadAllBytes("Templates/pipes/image-pipe/image.jpeg");
        
        var template = File.ReadAllText("Templates/pipes/image-pipe/image.pipe.html");
        var resultTemplate = File.ReadAllText("Templates/pipes/image-pipe/image.pipe.result.html");

        resultTemplate = resultTemplate.Replace("##IMAGEDATA##", Convert.ToBase64String(imageContent));

        var obj = new
        {
            NullDate = (ImageData?)null,
            ImageData = new ImageData()
            {
                FileName = "image.jpeg",
                FileContent = imageContent
            }
        };
        
        var content = await this.htmlBuilder.BuildFromTemplateAsync(template, obj);

        Assert.Equal(
            HtmlBuilder
                .MinifyHtml(resultTemplate)
                .Replace("##IMAGEDATA##", Convert.ToBase64String(imageContent)), 
            content);
    }

    [Fact]
    public async Task Should_Replace_String_With_Big_Number_Output_Using_Large_Number_Pipe()
    {
        var template = File.ReadAllText("Templates/pipes/large-number/large-number.pipe.html");
        var resultTemplate = File.ReadAllText("Templates/pipes/large-number/large-number.pipe.result.html");
            
        var obj = new 
        {
            NullNumber = (int?)null,
            ThousandNumber = 1_200,
            MillionNumber = 1_200_000,
            BillionNumber = 1_200_000_000,
            TeraNumber = 1_200_000_000_000,
            QuantiNumber = 1_200_000_000_000_000
        };

        var content = await this.htmlBuilder.BuildFromTemplateAsync(template, obj);

        Assert.Equal(HtmlBuilder.MinifyHtml(resultTemplate), content);
    }
    
    [Fact]
    public async Task Should_Replace_String_With_Number_Output_Using_Number_Pipe()
    {
        var template = File.ReadAllText("Templates/pipes/number/number.pipe.html");
        var resultTemplate = File.ReadAllText("Templates/pipes/number/number.pipe.result.html");
            
        var obj = new 
        {
            NullNumber = (decimal?)null,
            IntNumber = 100,
            OneNumber = 1.23232M,
        };

        var content = await this.htmlBuilder.BuildFromTemplateAsync(template, obj);

        Assert.Equal(HtmlBuilder.MinifyHtml(resultTemplate), content);
    }
    
    [Fact]
    public async Task Should_Replace_String_With_UPPERCASE_Output_Using_Upper_Pipe()
    {
        var template = File.ReadAllText("Templates/pipes/upper/upper.pipe.html");
        var resultTemplate = File.ReadAllText("Templates/pipes/upper/upper.pipe.result.html");
            
        var obj = new 
        {
            NullName = (string)null,
            FullName = "Jean-Paul Goat hier"
        };

        var content = await this.htmlBuilder.BuildFromTemplateAsync(template, obj);

        Assert.Equal(HtmlBuilder.MinifyHtml(resultTemplate), content);
    }
    #endregion
    
    #region Directives
    [Fact]
    public async Task Should_Apply_Attr_Directive()
    {
        var template = File.ReadAllText("Templates/directives/html/attr/attr.directive.html");
        var resultTemplate = File.ReadAllText("Templates/directives/html/attr/attr.directive.result.html");
            
        var obj = new 
        {
            MyAddedClass = "added__class",
            MyCustomLink = "https://ng-sharp.net"
        };

        var content = await this.htmlBuilder.BuildFromTemplateAsync(template, obj);

        Assert.Equal(HtmlBuilder.MinifyHtml(resultTemplate), content);
    }
    
    [Fact]
    public async Task Should_Apply_Html_Directive()
    {
        var template = File.ReadAllText("Templates/directives/html/html/html.directive.html");
        var resultTemplate = File.ReadAllText("Templates/directives/html/html/html.directive.result.html");
            
        var obj = new 
        {
            MyHtmlData = "<span style='color: red'>That text is red</span>"
        };

        var content = await this.htmlBuilder.BuildFromTemplateAsync(template, obj);

        Assert.Equal(HtmlBuilder.MinifyHtml(resultTemplate), content);
    }
    
    [Fact]
    public async Task Should_Apply_Style_Directive()
    {
        var template = File.ReadAllText("Templates/directives/html/style/style.directive.html");
        var resultTemplate = File.ReadAllText("Templates/directives/html/style/style.directive.result.html");
            
        var obj = new 
        {
            MyInitialWeight = "initial",
            MyBoldWeight = "bold"
        };

        var content = await this.htmlBuilder.BuildFromTemplateAsync(template, obj);

        Assert.Equal(HtmlBuilder.MinifyHtml(resultTemplate), content);
    }
    #endregion
    
    #region Structural Directives
    [Fact]
    public async Task Should_Apply_If_Structural_Directive()
    {
        var template = File.ReadAllText("Templates/directives/structural/if/if.directive.html");
        var resultTemplate = File.ReadAllText("Templates/directives/structural/if/if.directive.result.html");
            
        var obj = new 
        {
            ShouldDisplay = true
        };

        var content = await this.htmlBuilder.BuildFromTemplateAsync(template, obj);

        Assert.Equal(HtmlBuilder.MinifyHtml(resultTemplate), content);
    }
    
    [Fact]
    public async Task Should_Apply_For_Structural_Directive()
    {
        var template = File.ReadAllText("Templates/directives/structural/for/for.directive.html");
        var resultTemplate = File.ReadAllText("Templates/directives/structural/for/for.directive.result.html");
            
        var obj = new 
        {
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

        var content = await this.htmlBuilder.BuildFromTemplateAsync(template, obj);

        Assert.Equal(HtmlBuilder.MinifyHtml(resultTemplate), content);
    }
    
    [Fact]
    public async Task Should_Apply_Not_Empty_Structural_Directive()
    {
        var template = File.ReadAllText("Templates/directives/structural/not-empty/not-empty.directive.html");
        var resultTemplate = File.ReadAllText("Templates/directives/structural/not-empty/not-empty.directive.result.html");
            
        var obj = new 
        {
            MyArray = Enumerable.Range(1, 10)
                .Select(i => new
                {
                    Id = i,
                    Name = $"John {i}",
                })
                .ToArray(),
            MyTasks = Array.Empty<object>()
        };

        var content = await this.htmlBuilder.BuildFromTemplateAsync(template, obj);

        Assert.Equal(HtmlBuilder.MinifyHtml(resultTemplate), content);
    }
    #endregion
    
    #region Custom Elements
    [Fact]
    public async Task Should_Replace_String_With_lowercase_Output_Using_Custom_Lower_Pipe()
    {
        this.htmlBuilder.RegisterPipe<LowerCasePipe>();
        
        var template = File.ReadAllText("Templates/pipes/lower/lower.pipe.html");
        var resultTemplate = File.ReadAllText("Templates/pipes/lower/lower.pipe.result.html");
            
        var obj = new 
        {
            FullName = "Jean-Paul Goat hier"
        };

        var content = await this.htmlBuilder.BuildFromTemplateAsync(template, obj);

        Assert.Equal(HtmlBuilder.MinifyHtml(resultTemplate), content);
    }
    
    [Fact]
    public async Task Should_Add_Custom_Hidden_Attribute()
    {
        this.htmlBuilder.RegisterDirective<HiddenDirective>();
        
        var template = File.ReadAllText("Templates/directives/html/hidden/hidden.directive.html");
        var resultTemplate = File.ReadAllText("Templates/directives/html/hidden/hidden.directive.result.html");
            
        var obj = new 
        {
            IsHidden = true
        };

        var content = await this.htmlBuilder.BuildFromTemplateAsync(template, obj);

        Assert.Equal(HtmlBuilder.MinifyHtml(resultTemplate), content);
    }

    [Fact]
    public async Task Should_Add_Custom_Component()
    {
        this.htmlBuilder.RegisterComponent<CustomComponent>();
        
        var template = File.ReadAllText("Templates/components/custom.component.html");
        var resultTemplate = File.ReadAllText("Templates/components/custom.component.result.html");
            
        var obj = new 
        {
            MyComponentText = "This is my custom component text"
        };

        var content = await this.htmlBuilder.BuildFromTemplateAsync(template, obj);

        Assert.Equal(HtmlBuilder.MinifyHtml(resultTemplate), content);
    }
    #endregion
    
    #region Big Test
    [SkippableFact]
    public async Task Should_Parse_Big_One()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "SkiaSharp not supported on CI");
        
        var icon = File.ReadAllBytes("Templates/big-test-marker-icon.webp");
        
        var template = File.ReadAllText("Templates/big-test.html");
        var resultTemplate = File.ReadAllText("Templates/big-test.result.html");

        var obj = new
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
                    ShouldDisplayTask = i % 2 == 0,
                    Tasks = Enumerable.Range(1, 3)
                        .Select(j => new
                        {
                            TaskId = j + 10,
                            TaskName = $"Task {j + 10}"
                        })
                })
                .ToArray(),
            ApiKey = "MyApiKey",
            Icon = icon,
            MapPoints = new List<MapPoint>()
            {
                new(48.8566, 2.3522),
                new(48.8600, 2.3419),
                new(48.8530, 2.3499)
            }
        };

        var content = await this.htmlBuilder.BuildFromTemplateAsync(template, obj);

        Assert.Equal(HtmlBuilder.MinifyHtml(resultTemplate), content);
    }
    #endregion
}