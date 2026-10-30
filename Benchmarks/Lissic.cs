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

        catalog = BuildCatalog(CatalogData);
    }

    protected CatalogLayout catalog;
    protected PooledByteWriter catalogWriter = new(1000000);

    private static CatalogLayout BuildCatalog(Catalog source)
    {
        List<LissicTemplate> categories = [];
        foreach (var category in source.Categories)
        {
            List<LissicTemplate> products = [];
            foreach (var product in category.Products)
            {
                List<LissicTemplate> tags = [];
                foreach (var tag in product.Tags)
                {
                    tags.Add(new CatalogTag { label = tag.Label, isLast = tag.IsLast.ToString() });
                }

                List<LissicTemplate> variants = [];
                foreach (var variant in product.Variants)
                {
                    variants.Add(new CatalogVariant
                    {
                        variantSku = variant.VariantSku,
                        color = variant.Color,
                        size = variant.Size,
                        variantAvailable = variant.VariantAvailable.ToString()
                    });
                }

                List<LissicTemplate> reviews = [];
                foreach (var review in product.Reviews)
                {
                    reviews.Add(new CatalogReview
                    {
                        reviewTitle = review.ReviewTitle,
                        rating = review.Rating,
                        verified = review.Verified.ToString(),
                        body = review.Body,
                        author = review.Author
                    });
                }

                products.Add(new CatalogProduct
                {
                    isNew = product.IsNew.ToString(),
                    sku = product.Sku,
                    title = product.Title,
                    brand = product.Brand,
                    onSale = product.OnSale.ToString(),
                    price = product.Price,
                    listPrice = product.ListPrice,
                    inStock = product.InStock.ToString(),
                    description = product.Description,
                    hasTags = product.HasTags.ToString(),
                    tagList = new CatalogTagList { tags = tags },
                    hasVariants = product.HasVariants.ToString(),
                    variantTable = new CatalogVariantTable { variants = variants },
                    hasReviews = product.HasReviews.ToString(),
                    reviewList = new CatalogReviewList { reviews = reviews }
                });
            }

            categories.Add(new CatalogCategory
            {
                isFeatured = category.IsFeatured.ToString(),
                slug = category.Slug,
                name = category.Name,
                summary = category.Summary,
                hasProducts = category.HasProducts.ToString(),
                productList = new CatalogProductList { products = products }
            });
        }

        return new CatalogLayout
        {
            title = source.Title,
            subtitle = source.Subtitle,
            categories = categories,
            footerNote = source.FooterNote
        };
    }

    public override string Render()
    {
        return lissicProcessor.RenderToString(outer, 32000);
    }

    public override string RenderCatalog()
    {
        return lissicProcessor.RenderToString(catalog, 1000000);
    }

    public void RenderToBuffer()
    {
        bufferWriter.Reset();
        lissicProcessor.RenderToBuffer(outer, bufferWriter);
    }

    public void RenderCatalogToBuffer()
    {
        catalogWriter.Reset();
        lissicProcessor.RenderToBuffer(catalog, catalogWriter);
    }
}

[TemplateText("""
<div id='catalog'>
  <header>
    <h1>{{ title <upper> }}</h1>
    <p class='subtitle'>{{ subtitle <truncate(57) | append("...")> }}</p>
  </header>
{{ loop.categories }}  <footer>{{ footerNote <truncate(37) | append("...")> }}</footer>
</div>
""")]
public partial class CatalogLayout {}

[TemplateText("""
  <section class='category{{ branch.isFeatured [True=>@ featured; False=>@;] }}' id='{{ slug }}'>
    <h2>{{ name <capitalize> }}</h2>
    <p class='crumb'>Browse: {{ slug <replace("-", " ")> }}</p>
    <p class='summary'>{{ summary <truncate(37) | append("...")> }}</p>
{{ branch.hasProducts [True=>part.productList; False=>@    <p class='empty'>No products</p>;] }}
  </section>

""")]
public partial class CatalogCategory {}

[TemplateText("""
    <ul class='products'>
{{ loop.products }}    </ul>
""")]
public partial class CatalogProductList {}

[TemplateText("""
      <li class='product{{ branch.isNew [True=>@ new; False=>@;] }}' data-sku='{{ sku <lower> }}'>
        <h3>{{ title }}</h3>
        <span class='brand'>{{ brand <upper> }}</span>
        {{ branch.onSale [True=>@<p class='price sale'>Now {{ price }} <del>Was {{ listPrice }}</del></p>; False=>@<p class='price'>Only {{ price }}</p>;] }}
        {{ branch.inStock [True=>@<span class='stock in'>In stock</span>; False=>@<span class='stock out'>Sold out</span>;] }}
        <p class='description'>{{ description <truncate(77) | append("...")> }}</p>
{{ branch.hasTags [True=>part.tagList; False=>@;] }}
{{ branch.hasVariants [True=>part.variantTable; False=>@;] }}
{{ branch.hasReviews [True=>part.reviewList; False=>@;] }}
      </li>

""")]
public partial class CatalogProduct {}

[TemplateText("""
        <p class='tags'>
{{ loop.tags }}        </p>
""")]
public partial class CatalogTagList {}

[TemplateText("""
          <a href='/tag/{{ label <lower> }}'>{{ label <upper> }}</a>{{ branch.isLast [True=>@; False=>@,;] }}

""")]
public partial class CatalogTag {}

[TemplateText("""
        <table class='variants'>
{{ loop.variants }}        </table>
""")]
public partial class CatalogVariantTable {}

[TemplateText("""
          <tr class='{{ branch.variantAvailable [True=>@available; False=>@unavailable;] }}'>
            <td>{{ variantSku <lower> }}</td>
            <td>{{ color <capitalize> }}</td>
            <td>{{ size <upper> }}</td>
            <td>{{ branch.variantAvailable [True=>@Yes; False=>@No;] }}</td>
          </tr>

""")]
public partial class CatalogVariant {}

[TemplateText("""
        <div class='reviews'>
{{ loop.reviews }}        </div>
""")]
public partial class CatalogReviewList {}

[TemplateText("""
          <blockquote>
            <h4>{{ reviewTitle <capitalize> }}</h4>
            <span class='rating'>{{ rating }}</span>{{ branch.verified [True=>@ <em>Verified</em>; False=>@;] }}
            <p>{{ body <truncate(47) | append("...")> }}</p>
            <cite>{{ author }}</cite>
          </blockquote>

""")]
public partial class CatalogReview {}

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
