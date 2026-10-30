using Scriban;
using Scriban.Runtime;

namespace Benchmarks;

public class ScribanBenchmarks : BaseBenchmarks
{
    private Template _scribanTemplate;

    public ScribanBenchmarks()
    {
        _scribanTemplate = Template.ParseLiquid(LiquidProductTemplate);
    }

    public override string Render()
    {
        var scriptObject = new ScriptObject { { "products", Products } };
        return _scribanTemplate.Render(scriptObject);
    }
}
