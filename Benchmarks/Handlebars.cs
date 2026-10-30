using HandlebarsDotNet;

namespace Benchmarks;

public class HandlebarsBenchmarks : BaseBenchmarks
{
    private readonly HandlebarsTemplate<object, object> _handlebarsTemplate;

    public HandlebarsBenchmarks()
    {
        _handlebarsTemplate = CompileTemplate(MustacheProductTemplate);
    }

    private static HandlebarsTemplate<object, object> CompileTemplate(string template)
    {
        var handlebars = Handlebars.Create();

        using (handlebars.Configure())
        {
            handlebars.RegisterHelper("truncate", (output, options, context, arguments) =>
            {
                const string ellipsisStr = "...";
                var inputStr = options.Template();
                var length = Convert.ToInt32(arguments.Length > 0 ? arguments[0] : 50);
                var l = Math.Max(0, length - ellipsisStr.Length);
                var concat = string.Concat(inputStr.AsSpan().Slice(0, l), ellipsisStr);
                output.WriteSafeString(concat);
            });

            return handlebars.Compile(template);
        }
    }

    private readonly HandlebarsTemplate<object, object> _catalogTemplate = CompileCatalog();

    private static HandlebarsTemplate<object, object> CompileCatalog()
    {
        var handlebars = Handlebars.Create();
        ComplexBenchmarkData.RegisterHelpers(handlebars);
        return handlebars.Compile(ComplexTemplates.MustacheCatalogTemplate);
    }

    public override string RenderCatalog() => _catalogTemplate(CatalogData);

    public override string Render()
    {
        return _handlebarsTemplate(new
        {
            products = Products
        });
    }
}
