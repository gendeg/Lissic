using Fluid;

namespace Benchmarks;

public class FluidBenchmarks : BaseBenchmarks
{
    private readonly TemplateOptions _options = new() { OutputBufferSize = 40 * 1024};
    private readonly FluidParser _parser  = new();
    private readonly IFluidTemplate _fluidTemplate;
    private readonly IFluidTemplate _catalogTemplate;

    public FluidBenchmarks() : base()
    {
        _options.ModelNamesComparer = StringComparers.CamelCase;
        _parser.TryParse(LiquidProductTemplate, out _fluidTemplate, out var _);
        _parser.TryParse(ComplexTemplates.LiquidCatalogTemplate, out _catalogTemplate, out var _);
    }

    public override string RenderCatalog()
    {
        var context = new TemplateContext(CatalogData, new TemplateOptions
        {
            OutputBufferSize = 200 * 1024,
            ModelNamesComparer = StringComparers.CamelCase
        });
        return _catalogTemplate.Render(context);
    }

    public override string Render()
    {
        var context = new TemplateContext(_options).SetValue("products", Products);
        return _fluidTemplate.Render(context);
    }
}
