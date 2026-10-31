using DotLiquid;

namespace Benchmarks;

public class DotLiquidBenchmarks : BaseBenchmarks
{
    private readonly Template _dotLiquidTemplate;

    public DotLiquidBenchmarks()
    {
        _dotLiquidTemplate = Template.Parse(LiquidProductTemplate);
        _dotLiquidTemplate.MakeThreadSafe();
        _catalogTemplate = Template.Parse(ComplexTemplates.LiquidCatalogTemplate);
        _catalogTemplate.MakeThreadSafe();
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

    private readonly Template _catalogTemplate;

    private Hash MakeCatalog()
    {
        return Hash.FromAnonymousObject(new
        {
            title = CatalogData.Title,
            subtitle = CatalogData.Subtitle,
            footerNote = CatalogData.FooterNote,
            categories = CatalogData.Categories.Select(c => new Hash
            {
                ["name"] = c.Name,
                ["slug"] = c.Slug,
                ["summary"] = c.Summary,
                ["isFeatured"] = c.IsFeatured,
                ["hasProducts"] = c.HasProducts,
                ["products"] = c.Products.Select(p => new Hash
                {
                    ["sku"] = p.Sku,
                    ["title"] = p.Title,
                    ["brand"] = p.Brand,
                    ["price"] = p.Price,
                    ["listPrice"] = p.ListPrice,
                    ["description"] = p.Description,
                    ["isNew"] = p.IsNew,
                    ["onSale"] = p.OnSale,
                    ["inStock"] = p.InStock,
                    ["hasTags"] = p.HasTags,
                    ["hasVariants"] = p.HasVariants,
                    ["hasReviews"] = p.HasReviews,
                    ["tags"] = p.Tags.Select(t => new Hash
                    {
                        ["label"] = t.Label,
                        ["isLast"] = t.IsLast
                    }).ToList(),
                    ["variants"] = p.Variants.Select(v => new Hash
                    {
                        ["variantSku"] = v.VariantSku,
                        ["color"] = v.Color,
                        ["size"] = v.Size,
                        ["variantAvailable"] = v.VariantAvailable
                    }).ToList(),
                    ["reviews"] = p.Reviews.Select(r => new Hash
                    {
                        ["author"] = r.Author,
                        ["reviewTitle"] = r.ReviewTitle,
                        ["body"] = r.Body,
                        ["rating"] = r.Rating,
                        ["verified"] = r.Verified
                    }).ToList()
                }).ToList()
            }).ToList()
        });
    }

    public override string RenderCatalog()
    {
        return _catalogTemplate.Render(MakeCatalog());
    }

    public override string Render()
    {
        var products = MakeProducts();
        return _dotLiquidTemplate.Render(products);
    }
}
