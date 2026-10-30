using Scriban;
using Scriban.Runtime;

namespace Benchmarks;

public class ScribanBenchmarks : BaseBenchmarks
{
    private Template _scribanTemplate;

    public ScribanBenchmarks()
    {
        _scribanTemplate = Template.ParseLiquid(LiquidProductTemplate);
        _catalogTemplate = Template.ParseLiquid(ComplexTemplates.LiquidCatalogTemplate);
    }

    private readonly Template _catalogTemplate;

    public override string RenderCatalog()
    {
        var scriptObject = new ScriptObject();
        scriptObject.Import(CatalogData, renamer: m => char.ToLowerInvariant(m.Name[0]) + m.Name[1..]);
        var context = new LiquidTemplateContext { LoopLimit = 0, MemberRenamer = m => char.ToLowerInvariant(m.Name[0]) + m.Name[1..] };
        context.PushGlobal(scriptObject);
        return _catalogTemplate.Render(context);
    }

    public override string Render()
    {
        var scriptObject = new ScriptObject { { "products", Products } };
        return _scribanTemplate.Render(scriptObject);
    }
}
