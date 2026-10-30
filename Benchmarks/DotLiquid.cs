using DotLiquid;

namespace Benchmarks;

public class DotLiquidBenchmarks : BaseBenchmarks
{
    private readonly Template _dotLiquidTemplate;

    public DotLiquidBenchmarks()
    {
        _dotLiquidTemplate = Template.Parse(LiquidProductTemplate);
        _dotLiquidTemplate.MakeThreadSafe();
    }

    private Hash MakeProducts()
    {
        return Hash.FromAnonymousObject(new
        {
            products = Products.Select(product => new Hash()
            {
                ["name"] = product.Name,
                ["price"] = product.Price,
                ["description"] = product.Description
            }).ToList()
        });
    }

    public override string Render()
    {
        var products = MakeProducts();
        return _dotLiquidTemplate.Render(products);
    }
}
