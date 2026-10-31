using HandlebarsDotNet;

namespace Benchmarks;

public class Catalog
{
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string FooterNote { get; set; } = "";
    public List<Category> Categories { get; set; } = [];
}

public class Category
{
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Summary { get; set; } = "";
    public bool IsFeatured { get; set; }
    public bool HasProducts { get; set; }
    public List<Product> Products { get; set; } = [];
}

public class Product
{
    public string Sku { get; set; } = "";
    public string Title { get; set; } = "";
    public string Brand { get; set; } = "";
    public float Price { get; set; }
    public float ListPrice { get; set; }
    public string Description { get; set; } = "";
    public bool IsNew { get; set; }
    public bool OnSale { get; set; }
    public bool InStock { get; set; }
    public bool HasTags { get; set; }
    public bool HasVariants { get; set; }
    public bool HasReviews { get; set; }
    public List<Tag> Tags { get; set; } = [];
    public List<Variant> Variants { get; set; } = [];
    public List<Review> Reviews { get; set; } = [];
}

public class Tag
{
    public string Label { get; set; } = "";
    public bool IsLast { get; set; }
}

public class Variant
{
    public string VariantSku { get; set; } = "";
    public string Color { get; set; } = "";
    public string Size { get; set; } = "";
    public bool VariantAvailable { get; set; }
}

public class Review
{
    public string Author { get; set; } = "";
    public string ReviewTitle { get; set; } = "";
    public string Body { get; set; } = "";
    public string Rating { get; set; } = "";
    public bool Verified { get; set; }
}

public static class ComplexBenchmarkData
{
    public const int CategoryCount = 8;
    public const int ProductsPerCategory = 12;

    private static readonly string[] Colors = ["red", "green", "blue"];
    private static readonly string[] Sizes = ["Small", "Medium", "Large"];

    public static Catalog Create(string lorem)
    {
        var catalog = new Catalog
        {
            Title = "Summer Catalog",
            Subtitle = lorem,
            FooterNote = lorem,
        };

        for (int c = 0; c < CategoryCount; c++)
        {
            var category = new Category
            {
                Name = "category" + c,
                Slug = "category-number-" + c,
                Summary = lorem,
                IsFeatured = c % 3 == 0,
                HasProducts = true,
            };

            for (int p = 0; p < ProductsPerCategory; p++)
            {
                int n = c * ProductsPerCategory + p;
                var product = new Product
                {
                    Sku = "SKU-" + n,
                    Title = "Product" + n,
                    Brand = "Brand" + (n % 5),
                    Price = n,
                    ListPrice = n + 10,
                    Description = lorem,
                    IsNew = n % 4 == 0,
                    OnSale = n % 2 == 0,
                    InStock = n % 5 != 0,
                    HasTags = true,
                    HasVariants = true,
                    HasReviews = true,
                };

                for (int t = 0; t < 4; t++)
                    product.Tags.Add(new Tag { Label = "tag" + t, IsLast = t == 3 });

                for (int v = 0; v < 3; v++)
                    product.Variants.Add(new Variant
                    {
                        VariantSku = "SKU-" + n + "-" + v,
                        Color = Colors[v],
                        Size = Sizes[v],
                        VariantAvailable = (n + v) % 3 != 0,
                    });

                for (int r = 0; r < 3; r++)
                    product.Reviews.Add(new Review
                    {
                        Author = "Reviewer" + r,
                        ReviewTitle = "Review title " + r,
                        Body = lorem,
                        Rating = (3 + r % 3) + " of 5",
                        Verified = r % 2 == 0,
                    });

                category.Products.Add(product);
            }

            catalog.Categories.Add(category);
        }

        return catalog;
    }

    public static bool CheckBenchmark(string result)
    {
        int last = CategoryCount * ProductsPerCategory - 1;
        return !string.IsNullOrEmpty(result)
            && result.Contains("<h1>SUMMER CATALOG</h1>")
            && result.Contains("<h3>Product0</h3>")
            && result.Contains($"<h3>Product{last}</h3>")
            && result.Contains("Browse: category number 0")
            && result.Contains("Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempo...")
            && result.Contains("Reviewer2");
    }

    // Block helpers needed by the Mustache/Handlebars template: truncate, upper, lower, capitalize, replace.
    public static void RegisterHelpers(IHandlebars handlebars)
    {
        handlebars.RegisterHelper("truncate", (output, options, context, arguments) =>
        {
            var text = options.Template();
            int length = Convert.ToInt32(arguments[0]);
            output.WriteSafeString(text.Length > length ? string.Concat(text.AsSpan(0, Math.Max(0, length - 3)), "...") : text);
        });
        handlebars.RegisterHelper("upper", (output, options, context, arguments) =>
            output.WriteSafeString(options.Template().ToUpperInvariant()));
        handlebars.RegisterHelper("lower", (output, options, context, arguments) =>
            output.WriteSafeString(options.Template().ToLowerInvariant()));
        handlebars.RegisterHelper("capitalize", (output, options, context, arguments) =>
        {
            var text = options.Template();
            output.WriteSafeString(text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..]);
        });
        handlebars.RegisterHelper("replace", (output, options, context, arguments) =>
            output.WriteSafeString(options.Template().Replace($"{arguments[0]}", $"{arguments[1]}")));
    }
}

public static class ComplexTemplates
{
    public const string LiquidCatalogTemplate = """
<div id='catalog'>
  <header>
    <h1>{{ title | upcase }}</h1>
    <p class='subtitle'>{{ subtitle | truncate: 60 }}</p>
  </header>
{% for category in categories %}
  <section class='category{% if category.isFeatured %} featured{% endif %}' id='{{ category.slug }}'>
    <h2>{{ category.name | capitalize }}</h2>
    <p class='crumb'>{{ category.slug | replace: "-", " " | prepend: "Browse: " }}</p>
    <p class='summary'>{{ category.summary | truncate: 40 }}</p>
{% if category.hasProducts %}
    <ul class='products'>
{% for product in category.products %}
      <li class='product{% if product.isNew %} new{% endif %}' data-sku='{{ product.sku | downcase }}'>
        <h3>{{ product.title }}</h3>
        <span class='brand'>{{ product.brand | upcase }}</span>
{% if product.onSale %}
        <p class='price sale'>Now {{ product.price }} <del>Was {{ product.listPrice }}</del></p>
{% else %}
        <p class='price'>Only {{ product.price }}</p>
{% endif %}
{% if product.inStock %}
        <span class='stock in'>In stock</span>
{% else %}
        <span class='stock out'>Sold out</span>
{% endif %}
        <p class='description'>{{ product.description | truncate: 80 }}</p>
{% if product.hasTags %}
        <p class='tags'>
{% for tag in product.tags %}
          <a href='/tag/{{ tag.label | downcase }}'>{{ tag.label | upcase }}</a>{% if tag.isLast %}{% else %},{% endif %}
{% endfor %}
        </p>
{% endif %}
{% if product.hasVariants %}
        <table class='variants'>
{% for variant in product.variants %}
          <tr class='{% if variant.variantAvailable %}available{% else %}unavailable{% endif %}'>
            <td>{{ variant.variantSku | downcase }}</td>
            <td>{{ variant.color | capitalize }}</td>
            <td>{{ variant.size | upcase }}</td>
            <td>{% if variant.variantAvailable %}Yes{% else %}No{% endif %}</td>
          </tr>
{% endfor %}
        </table>
{% endif %}
{% if product.hasReviews %}
        <div class='reviews'>
{% for review in product.reviews %}
          <blockquote>
            <h4>{{ review.reviewTitle | capitalize }}</h4>
            <span class='rating'>{{ review.rating }}</span>{% if review.verified %} <em>Verified</em>{% endif %}
            <p>{{ review.body | truncate: 50 }}</p>
            <cite>{{ review.author }}</cite>
          </blockquote>
{% endfor %}
        </div>
{% endif %}
      </li>
{% endfor %}
    </ul>
{% else %}
    <p class='empty'>No products</p>
{% endif %}
  </section>
{% endfor %}
  <footer>{{ footerNote | truncate: 40 }}</footer>
</div>

""";

    public const string MustacheCatalogTemplate = """
<div id='catalog'>
  <header>
    <h1>{{#upper}}{{title}}{{/upper}}</h1>
    <p class='subtitle'>{{#truncate 60}}{{subtitle}}{{/truncate}}</p>
  </header>
{{#categories}}
  <section class='category{{#isFeatured}} featured{{/isFeatured}}' id='{{slug}}'>
    <h2>{{#capitalize}}{{name}}{{/capitalize}}</h2>
    <p class='crumb'>Browse: {{#replace "-" " "}}{{slug}}{{/replace}}</p>
    <p class='summary'>{{#truncate 40}}{{summary}}{{/truncate}}</p>
{{#hasProducts}}
    <ul class='products'>
{{#products}}
      <li class='product{{#isNew}} new{{/isNew}}' data-sku='{{#lower}}{{sku}}{{/lower}}'>
        <h3>{{title}}</h3>
        <span class='brand'>{{#upper}}{{brand}}{{/upper}}</span>
{{#onSale}}
        <p class='price sale'>Now {{price}} <del>Was {{listPrice}}</del></p>
{{/onSale}}
{{^onSale}}
        <p class='price'>Only {{price}}</p>
{{/onSale}}
{{#inStock}}
        <span class='stock in'>In stock</span>
{{/inStock}}
{{^inStock}}
        <span class='stock out'>Sold out</span>
{{/inStock}}
        <p class='description'>{{#truncate 80}}{{description}}{{/truncate}}</p>
{{#hasTags}}
        <p class='tags'>
{{#tags}}
          <a href='/tag/{{#lower}}{{label}}{{/lower}}'>{{#upper}}{{label}}{{/upper}}</a>{{^isLast}},{{/isLast}}
{{/tags}}
        </p>
{{/hasTags}}
{{#hasVariants}}
        <table class='variants'>
{{#variants}}
          <tr class='{{#variantAvailable}}available{{/variantAvailable}}{{^variantAvailable}}unavailable{{/variantAvailable}}'>
            <td>{{#lower}}{{variantSku}}{{/lower}}</td>
            <td>{{#capitalize}}{{color}}{{/capitalize}}</td>
            <td>{{#upper}}{{size}}{{/upper}}</td>
            <td>{{#variantAvailable}}Yes{{/variantAvailable}}{{^variantAvailable}}No{{/variantAvailable}}</td>
          </tr>
{{/variants}}
        </table>
{{/hasVariants}}
{{#hasReviews}}
        <div class='reviews'>
{{#reviews}}
          <blockquote>
            <h4>{{#capitalize}}{{reviewTitle}}{{/capitalize}}</h4>
            <span class='rating'>{{rating}}</span>{{#verified}} <em>Verified</em>{{/verified}}
            <p>{{#truncate 50}}{{body}}{{/truncate}}</p>
            <cite>{{author}}</cite>
          </blockquote>
{{/reviews}}
        </div>
{{/hasReviews}}
      </li>
{{/products}}
    </ul>
{{/hasProducts}}
{{^hasProducts}}
    <p class='empty'>No products</p>
{{/hasProducts}}
  </section>
{{/categories}}
  <footer>{{#truncate 40}}{{footerNote}}{{/truncate}}</footer>
</div>

""";
}
