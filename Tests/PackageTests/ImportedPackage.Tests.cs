using Lissic.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lissic.Package.Tests;

public partial class ImportedPackageTests
{
    readonly ServiceProvider services;

    public ImportedPackageTests()
    {
        ServiceCollection serviceCollection = new();
        serviceCollection.AddLissicAsync().Wait();
        services = serviceCollection.BuildServiceProvider();
    }

    [Fact]
    public void CanFindTemplates()
    {
        TestTemplate template = new() { 
            name = "John"
        };

        Assert.Equal("John", template.name.ToString());
    }

    [Fact]
    public void CanHandleFullPathTemplates()
    {
        TestFullPathTemplate template = new() { 
            name = "John"
        };

        Assert.Equal("John", template.name.ToString());
    }

    [Fact]
    public void CanHandleOptionalProperties()
    {
        TestTemplate template = new() { 
            name = "John"
        };

        Assert.Equal("John", template.name.ToString());
        Assert.Equal(VariableValueType.Empty, template.myName.Kind);
    }

    [Fact]
    public void CanRenderTemplate()
    {
        TestTemplate template = new() { 
            name = "John",
            myName = "Jane"
        };

        LissicProcessor processor = services.GetRequiredService<LissicProcessor>();

        string rendered = processor.RenderToString(template);
        Assert.Equal("Hello, John!  This is Jane!", rendered);
    }

    [Fact]
    public void CanUseInternalTemplate()
    {
        TestInternalTemplate template = new() { 
            name = "John"
        };

        LissicProcessor processor = services.GetRequiredService<LissicProcessor>();

        string rendered = processor.RenderToString(template);
        Assert.Equal("Hello, John!", rendered);
    }

    [Fact]
    public void CanUsePrivateTemplate()
    {
        TestPrivateTemplate template = new() { 
            name = "John"
        };

        LissicProcessor processor = services.GetRequiredService<LissicProcessor>();

        string rendered = processor.RenderToString(template);
        Assert.Equal("Hello, John!", rendered);
    }

    [Fact]
    public void CanUseBuiltInFilter()
    {
        TestFilterTemplate template = new() { 
            name = "John"
        };

        LissicProcessor processor = services.GetRequiredService<LissicProcessor>();

        string rendered = processor.RenderToString(template);
        Assert.Equal("Hello, JOHN!", rendered);
    }

    [Fact]
    public void CanUseBuiltInFormatter()
    {
        TestFormatterTemplate template = new() { 
            date = new DateTimeOffset(2024, 6, 26, 0, 0, 0, TimeSpan.Zero)
        };

        LissicProcessor processor = services.GetRequiredService<LissicProcessor>();

        string rendered = processor.RenderToString(template);
        Assert.Equal("Today is 2024-06-26", rendered);
    }

    [Fact]
    public void CanUseLoopTemplate()
    {
        List<LissicTemplate> loopItems = [];
        for (int i = 0; i < 3; i++)
        {
            loopItems.Add(new TestLoopInnerTemplate { item = i });
        }

        TestLoopTemplate template = new() { 
            things = loopItems
        };

        LissicProcessor processor = services.GetRequiredService<LissicProcessor>();

        string rendered = processor.RenderToString(template);
        Assert.Equal("Loop Test:\nItem: 0\nItem: 1\nItem: 2\n", rendered);
    }

    [Fact]
    public void CanUseMixedTemplateLoop()
    {
        List<LissicTemplate> loopItems = [];
        loopItems.Add(new TestTemplate { name = "John", myName = "Jane" });

        for (int i = 0; i < 3; i++)
        {
            loopItems.Add(new TestLoopInnerTemplate { item = i });
        }

        TestLoopTemplate template = new() { 
            things = loopItems
        };

        LissicProcessor processor = services.GetRequiredService<LissicProcessor>();

        string rendered = processor.RenderToString(template);
        Assert.Equal("Loop Test:\nHello, John!  This is Jane!Item: 0\nItem: 1\nItem: 2\n", rendered);
    }

    #if DEBUG
    [Fact]
    public void CanHotReloadPathTemplates()
    {
        TestTemplate template = new() { 
            name = "John",
            myName = "Jane"
        };

        LissicProcessor processor = services.GetRequiredService<LissicProcessor>();
        TemplateCache cache = services.GetRequiredService<TemplateCache>();
        cache.ReloadPathTemplates(new TemplateManifest(), TestContext.Current.CancellationToken);

        string rendered = processor.RenderToString(template);
        Assert.Equal("Greetings, John!  My name is Jane.", rendered);
    }
    #endif

    protected const string Lorem = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum";

    [Fact]
    public void CanRenderNestedTemplates()
    {
            LissicProcessor processor = services.GetRequiredService<LissicProcessor>();

            List<LissicTemplate> templates = [];
            for (var i = 0; i < 100; i++)
            {
                templates.Add(new BenchInner()
                {
                    name = "Name" + i,
                    price = (float)i,
                    description = Lorem
                });
            }
            BenchOuter outer = new()
            {
                inner = templates
            };

            string rendered = processor.RenderToString(outer);
            Assert.Contains("<ul id='products'>", rendered);
            Assert.Contains("""
    <li>
      <h2>Name95</h2>
           Only 95
           Lorem ipsum dol
    </li>
""", rendered);
    }


    [TemplatePath("test.html")]
    public partial class TestTemplate {}

    [TemplatePath("empty.html")]
#pragma warning disable LISSIC008 // Verify empty template warning, as double check on LissicGen tests
    public partial class TestEmptyTemplate {}
#pragma warning restore LISSIC008

    [TemplatePath("../../Templates/header.html")]
    public partial class TestOutsideDirectory {}

    [TemplateText("Hello, {{ name <upper> }}!")]
    public partial class TestFilterTemplate {}

    [FormattedVariable<DateFormatter>("date", ["yyyy-MM-dd"])]
    [TemplateText("Today is {{ date }}")]
    public partial class TestFormatterTemplate {}

    [TemplateText("Hello, {{ name }}!")]
    internal partial class TestInternalTemplate {}

    [TemplateText("Hello, {{ name }}!")]
    private partial class TestPrivateTemplate {}

    [TemplateText("Loop Test:\n{{ loop.things }}")]
    public partial class TestLoopTemplate {}

    [TemplateText("Item: {{ item }}\n")]
    public partial class TestLoopInnerTemplate {}

    [TemplateText("""
<ul id='products'>
  {{ loop.inner }}
</ul>
""")]
    public partial class BenchOuter {}

    [TemplateText("""
    <li>
      <h2>{{ name }}</h2>
           Only {{ price }}
           {{ description <truncate(15)> }}
    </li>
""")]
    public partial class BenchInner {}
}
