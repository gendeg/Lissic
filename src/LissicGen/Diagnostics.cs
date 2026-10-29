using Microsoft.CodeAnalysis;

namespace LissicGen;


internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor DuplicateFormatter = new(
        "LISSIC001",
        "Variable has more than one formatter",
        "Variable '{0}' on template '{1}' has more than one [FormattedVariable] attribute; only one formatter per variable is supported",
        "Design", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor FilterIsNotAFactory = new(
        "LISSIC002",
        "Filter does not implement IFilterFactory",
        "Type '{0}' has [FilterName] but does not implement Lissic.IFilterFactory",
        "Design", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateFilterName = new(
        "LISSIC003",
        "Duplicate filter name",
        "Filter name '{0}' is already used by another type; '{1}' is ignored",
        "Design", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor FilterIsNotAccessible = new(
        "LISSIC004",
        "Filter is not accessible",
        "Filter type '{0}' must be public or internal, including its containing types, to be registered",
        "Design", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidTemplate = new(
        "LISSIC005",
        "Template could not be parsed",
        "{0}",
        "Design", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnknownFilter = new(
        "LISSIC006",
        "Unknown filter",
        "Filter '{0}' on Variable '{1}' in template '{2}' is not declared with [FilterName] in this project",
        "Design", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingTemplateFile = new(
        "LISSIC007",
        "Missing template file",
        "Template file '{0}' could not be found",
        "Usage", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EmptyTemplateFile = new(
        "LISSIC008",
        "Empty template file",
        "Template file '{0}' is empty",
        "Usage", DiagnosticSeverity.Info, isEnabledByDefault: true);
}
