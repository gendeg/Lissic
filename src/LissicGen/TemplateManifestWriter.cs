using System.Text;
using Microsoft.CodeAnalysis;

namespace LissicGen;


internal static class TemplateManifestSourceWriter
{
    const string ServiceCollectionExtensionsSource = """
    public static class ServiceCollectionExtensions
    {
        extension (IServiceCollection services)
        {
            public Task<IServiceCollection> AddLissicAsync(Action<LissicOptions>? configureAction = null)
            {
                LissicOptions options = new();
                configureAction?.Invoke(options);
                return AddLissicAsync(services, options);
            }

            public async Task<IServiceCollection> AddLissicAsync(LissicOptions options)
            {
                if (!LissicConfigurator.IsInitialized)
                    LissicConfigurator.SetOptions(options);

                Lissic__FilterRegistration.Register();

                TemplateCache templateCache = new();
                await templateCache.Initialize(new TemplateManifest());

                services.AddSingleton(templateCache);
                if (options.RegisterAsTransient)
                {
                    services.AddTransient<ILissicProcessor, LissicProcessor>();
                    services.AddTransient<LissicProcessor>();
                }
                else
                {
                    services.AddScoped<ILissicProcessor, LissicProcessor>();
                    services.AddScoped<LissicProcessor>();
                }

                return services;
            }
        }
    }
""";


    public static string Generate(TemplateRecord[] manifestLog, string templateRoot, string templateSourceRoot)
    {
        if (manifestLog.Length == 0)
            return $$"""
#nullable enable
using Lissic.Parsing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace Lissic;

public sealed partial class TemplateManifest : ITemplateManifest
{
    public int Count => 0;

    public TemplateEntry Get(int index)
    {
        throw new ArgumentOutOfRangeException(nameof(index));
    }

    public string TemplateRoot => @"{{templateRoot}}";
    public string TemplateSourceRoot => @"{{templateSourceRoot}}";
}

{{ServiceCollectionExtensionsSource}}
""";

        StringBuilder sb = new();

        sb.Append($$"""
#nullable enable
using Lissic.Parsing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace Lissic
{
    public sealed partial class TemplateManifest : ITemplateManifest
    {
        public int Count => {{manifestLog.Length}};

        public TemplateEntry Get(int index) => index switch
        {

""");

        for (int i = 0; i < manifestLog.Length; i++)
        {
            TemplateRecord template = manifestLog[i];
            sb.AppendLine($"            {i} => new(TemplateKind.{(template.Type == TemplateType.Path ? "File" : "Literal")}, @\"{template.Content.Replace("\"", "\"\"")}\", \"{template.TemplateRefName}\"),");
        }

        sb.Append($$"""
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

        public string TemplateRoot => @"{{templateRoot}}";
        public string TemplateSourceRoot => @"{{templateSourceRoot}}";
    }

{{ServiceCollectionExtensionsSource}}
}

""");

        for (int i = 0; i < manifestLog.Length; i++)
        {
            TemplateRecord template = manifestLog[i];
            string visibility = (template.TargetClass as INamedTypeSymbol)?.DeclaredAccessibility.ToString().ToLower() ?? "public";

            sb.Append($$"""

namespace {{template.Namespace}}
{{{TemplateSourceWriter.GetParentHeader(template)}}
    {{visibility}} partial class {{template.ClassName}}
    {
        public override int Template__Index => {{i}};
    }
{{TemplateSourceWriter.GetParentFooter(template)}}}

""");
        }

        return sb.ToString();
    }
}
