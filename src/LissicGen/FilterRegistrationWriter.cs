using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Text;

namespace LissicGen;


internal static class FilterRegistrationWriter
{
    public static string Generate(FilterRecord[] filters)
    {
        // The first type declared for a name wins; sorted so the output is stable between builds.
        IEnumerable<FilterRecord> uniqueFilters = filters
            .OrderBy(filter => filter.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(filter => filter.FilterType.ToDisplayString(), StringComparer.Ordinal)
            .GroupBy(filter => filter.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First());

        StringBuilder registrations = new();
        foreach (FilterRecord filter in uniqueFilters)
        {
            string typeName = filter.FilterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            registrations.AppendLine($"        global::Lissic.FilterRegistry.Register<{typeName}>({SymbolDisplay.FormatLiteral(filter.Name, quote: true)});");
        }

        return $$"""
#nullable enable
namespace Lissic;

internal static class Lissic_FilterRegistration
{
    internal static void Register()
    {
{{registrations}}    }
}
""";
    }
}
