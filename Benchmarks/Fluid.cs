using Fluid;

namespace Benchmarks;

public class FluidBenchmarks : BaseBenchmarks
{
    private readonly FluidParser _parser  = new();
    private readonly IFluidTemplate _fluidTemplate;
    private readonly IFluidTemplate _catalogTemplate;

    public FluidBenchmarks() : base()
    {
        _parser.TryParse(LiquidProductTemplate, out _fluidTemplate, out var _);
        _parser.TryParse(ComplexTemplates.LiquidCatalogTemplate, out _catalogTemplate, out var _);
    }

    public override string RenderCatalog()
    {
        var context = new TemplateContext(CatalogData);
        return _catalogTemplate.Render(context);
    }

    public override string Render()
    {
        var context = new TemplateContext().SetValue("products", Products);
        return _fluidTemplate.Render(context);
    }
}
