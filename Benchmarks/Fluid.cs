using Fluid;

namespace Benchmarks;

public class FluidBenchmarks : BaseBenchmarks
{
    private readonly TemplateOptions _options = new() { OutputBufferSize = 40 * 1024};
    private readonly FluidParser _parser  = new();
    private readonly IFluidTemplate _fluidTemplate;

    public FluidBenchmarks() : base()
    {
        _options.ModelNamesComparer = StringComparers.CamelCase;
        _parser.TryParse(LiquidProductTemplate, out _fluidTemplate, out var _);
    }

    public override string Render()
    {
        var context = new TemplateContext(_options).SetValue("products", Products);
        return _fluidTemplate.Render(context);
    }
}
