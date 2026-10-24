//HintName: Lissic.Manifest.g.cs
#nullable enable
using Lissic.Parsing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace Lissic
{
    public sealed partial class TemplateManifest : ITemplateManifest
    {
        public int Count => 1;

        public TemplateEntry Get(int index) => index switch
        {
            0 => new(TemplateKind.Literal, @"Hello {{ Name }}, welcome to {{ City? }}!
It's time to do the thing! {{part.thingPart}}
Also, think about these:
{{loop.itemsForLoop}}

Well, I have more things to test, so...
{{ testVal <upper> }}
{{ testVal2 <lower(""lowerArg"") | pad(""42"", ""down"")> }}

As always, {{ branch.farewellType[like=>@take care!;dislike=>@goodbye!;]}} ", "WelcomeMessage"),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

        public string TemplateRoot => @"Templates";
        public string TemplateSourceRoot => @"";
    }

    public static class ServiceCollectionExtensions
    {
        public static Task<IServiceCollection> AddLissicAsync(this IServiceCollection services, Action<LissicOptions>? configureAction = null)
        {
            LissicOptions options = new();
            configureAction?.Invoke(options);
            return AddLissicAsync(services, options);
        }

        public static async Task<IServiceCollection> AddLissicAsync(this IServiceCollection services, LissicOptions options)
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

namespace Tests.FakeSpace
{
public partial class Templates { 
    public partial class WelcomeMessage
    {
        public override int Template__Index => 0;
    }
}
}
