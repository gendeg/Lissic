

namespace Benchmarks;


public abstract class BaseBenchmarks
{
    protected List<Item> Products = new(ProductCount);

    protected const int ProductCount = 100;

    protected const string Lorem = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum";

    public BaseBenchmarks()
    {
        for (int i = 0; i < ProductCount; i++)
        {
            var product = new Item("Name" + i, i, Lorem);
            Products.Add(product);
        }
    }


    public static bool CheckBenchmark(string result)
    {
        if (string.IsNullOrEmpty(result) ||
            !result.Contains("<h2>Name0</h2>") ||
            !result.Contains($"<h2>Name{ProductCount - 1}</h2>") ||
            !result.Contains($"Lorem ipsum ...") ||
            !result.Contains($"Only 0") ||
            !result.Contains($"Only {ProductCount - 1}"))
        {
            return false;
        }
        return true;
    }


    public abstract string Render();

    public const string LiquidProductTemplate = """
<ul id='products'>
{% for product in products %}
    <li>
      <h2>{{ product.name }}</h2>
           Only {{ product.price }}
           {{ product.description | truncate: 15 }}
    </li>
{% endfor %}
</ul>

""";

    public const string MustacheProductTemplate = """
<ul id='products'>
{{#products}}
    <li>
      <h2>{{ name }}</h2>
           Only {{ price }}
           {{#truncate 15}}{{description}}{{/truncate}}
    </li>
{{/products}}
</ul>

""";
}


public class Item(string name, float price, string description)
{
    public string Name { get; set; } = name;
    public float Price { get; set; } = price;
    public string Description { get; set; } = description;
}
