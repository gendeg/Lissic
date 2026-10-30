using Lissic;
using Lissic.Parsing;
using Lissic.Tools;

namespace Benchmarks;

public class LissicBenchmarks : BaseBenchmarks
{
    protected LissicProcessor lissicProcessor;
    protected BenchOuter outer;
    protected PooledByteWriter bufferWriter = new(50000);

    public LissicBenchmarks()
    {
        Lissic_FilterRegistration.Register();
        TemplateCache cache = new();
        cache.Initialize(new TemplateManifest());
        lissicProcessor = new LissicProcessor(cache);

        List<LissicTemplate> templates = [];
        foreach (var product in Products)
        {
            templates.Add(new BenchInner()
            {
                name = product.Name,
                price = product.Price,
                description = product.Description
            });
        }
        outer = new()
        {
            inner = templates
        };
    }

    public override string Render()
    {
        return lissicProcessor.RenderToString(outer, 32000);
    }

    public void RenderToBuffer()
    {
        bufferWriter.Reset();
        lissicProcessor.RenderToBuffer(outer, bufferWriter);
    }
}

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
           {{ description <truncate(12) | append("...")> }}
    </li>
""")]
public partial class BenchInner {}
