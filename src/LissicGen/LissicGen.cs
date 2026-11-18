using Lissic.Parsing;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace LissicGen;


[Generator]
public class LissicTemplateGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<TemplateRecord?> pathTemplates = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Lissic.TemplatePathAttribute",
                predicate: (_, _) => true,
                transform: (ctx, _) => GetTemplateRecord(ctx, TemplateType.Path)
            );

        IncrementalValuesProvider<TemplateRecord?> textTemplates = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Lissic.TemplateTextAttribute",
                predicate: (_, _) => true,
                transform: (ctx, _) => GetTemplateRecord(ctx, TemplateType.Text)
            );

        IncrementalValuesProvider<FilterRecord?> filters = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Lissic.FilterNameAttribute",
                predicate: (_, _) => true,
                transform: (ctx, _) => {
                    string? name = ctx.Attributes[0].ConstructorArguments[0].Value?.ToString();
                    if (ctx.TargetSymbol is not INamedTypeSymbol namedTypeSymbol || string.IsNullOrEmpty(name))
                        return (FilterRecord?)null;
                    return new FilterRecord(namedTypeSymbol, name!);
                }
            );

        IncrementalValueProvider<ImmutableArray<FilterRecord?>> allFilters = filters.Collect()
            .Combine(context.CompilationProvider.Select((compilation, _) => FindReferencedFilters(compilation)))
            .Select((pair, _) =>
            {
                // Filters declared in this project override same-named ones from referenced assemblies.
                HashSet<string> localNames = new(
                    pair.Left.Where(filter => filter.HasValue).Select(filter => filter!.Value.Name),
                    StringComparer.OrdinalIgnoreCase);
                return pair.Left.AddRange(pair.Right.Where(filter => !localNames.Contains(filter!.Value.Name)));
            });

        IncrementalValueProvider<string> templateRoot = context.AnalyzerConfigOptionsProvider
            .Select((options, _) =>
            {
                options.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory", out string? projectDir);
                options.GlobalOptions.TryGetValue("build_property.LissicTemplateRoot", out string? root);
                return Path.Combine(projectDir ?? "", root ?? "Templates");
            });

        IncrementalValueProvider<string> templateSourceRoot = context.AnalyzerConfigOptionsProvider
            .Select((options, _) =>
            {
                options.GlobalOptions.TryGetValue("build_property.LissicTemplateSourceRoot", out string? srcRoot);
                options.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory", out string? projectDir);
                if (srcRoot is not null && srcRoot != "_")
                    return Path.GetFullPath(Path.Combine(projectDir ?? "", srcRoot)).Replace('\\', '/');

                options.GlobalOptions.TryGetValue("build_property.LissicTemplateRoot", out string? tmplRoot);
                options.GlobalOptions.TryGetValue("build_property.OutDir", out string? outDir);

                if (string.IsNullOrEmpty(projectDir) || string.IsNullOrEmpty(outDir))
                    return "";

                string sourceAbs = Path.GetFullPath(Path.Combine(projectDir, tmplRoot ?? "Templates")).Replace('\\', '/');
                string outDirAbs = Path.GetFullPath(outDir).Replace('\\', '/');

                int idx = 0;
                while (idx < sourceAbs.Length && idx < outDirAbs.Length && char.ToLowerInvariant(sourceAbs[idx]) == char.ToLowerInvariant(outDirAbs[idx]))
                    idx++;
                while (idx > 0 && sourceAbs[idx] != '/')
                    idx--;

                string commonPrefix = sourceAbs.Substring(0, idx);
                int extraOutDirs = 0;
                for (int i = idx + 1; i < outDirAbs.Length; i++)
                    if (outDirAbs[i] == '/')
                        extraOutDirs++;

                string relativePath = "";
                for (int i = 0; i < extraOutDirs; i++)
                    relativePath = Path.Combine(relativePath, "..");
                relativePath = Path.Combine(relativePath, sourceAbs.Substring(idx + 1)).Replace('\\', '/');

                return relativePath;
            });

        IncrementalValueProvider<bool> ignoreRequiredProperties = context.AnalyzerConfigOptionsProvider
            .Select((options, _) =>
            {
                options.GlobalOptions.TryGetValue("build_property.LissicIgnoreRequiredProperties", out string? enforce);
                return bool.TryParse(enforce, out bool result) && result;
            });

        var additionalFilesByPath = context.AdditionalTextsProvider.Collect().Select((files, _) =>
            {
                var builder = ImmutableDictionary.CreateBuilder<string, AdditionalText>(StringComparer.OrdinalIgnoreCase);

                foreach (AdditionalText file in files)
                {
                    string key = Path.GetFullPath(file.Path).Replace('\\', '/');
                    if (!builder.ContainsKey(key))
                        builder.Add(key, file);
                }
                return builder.ToImmutable();
            });

        var filterNames = allFilters.Select((found, _) =>
            found.Where(filter => filter.HasValue)
                .Select(filter => filter!.Value.Name)
                .ToImmutableHashSet(StringComparer.OrdinalIgnoreCase));

        //                       Left                      Right
        var settings = ignoreRequiredProperties.Combine(filterNames);
        //                    Left.Left                Left.Right                 Right.Left         Right.Right
        var pathWithFiles = pathTemplates.Combine(additionalFilesByPath).Combine(templateRoot.Combine(settings));
        //                        Left               Right
        var textWithOptions = textTemplates.Combine(settings);
        //                   Left.Left                      Left.Right                       Right.Left             Right.Right
        var allTemplates = pathTemplates.Collect().Combine(textTemplates.Collect()).Combine(templateRoot.Combine(templateSourceRoot));

        context.RegisterSourceOutput(pathWithFiles, (srcContext, values) =>
            GeneratePathSource(srcContext, values.Left.Left, values.Left.Right, values.Right.Left, values.Right.Right.Left, values.Right.Right.Right));
        context.RegisterSourceOutput(textWithOptions, (srcContext, values) => 
            GenerateTextSource(srcContext, values.Left, values.Right.Left, values.Right.Right));
        context.RegisterSourceOutput(allFilters, GenerateFilterRegistration);
        context.RegisterSourceOutput(allTemplates, (srcContext, templates) =>
        {
            TemplateRecord[] manifestLog = templates.Left.Left
                .Concat(templates.Left.Right)
                .Where(record => record.HasValue)
                .Select(record => record!.Value)
                .ToArray();
            Array.Sort(manifestLog);
            GenerateManifest(srcContext, manifestLog, templates.Right.Left, templates.Right.Right);
        });
    }

    static ImmutableArray<FilterRecord?> FindReferencedFilters(Compilation compilation)
    {
        ImmutableArray<FilterRecord?>.Builder found = ImmutableArray.CreateBuilder<FilterRecord?>();

        foreach (MetadataReference reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly)
                continue;

            // Only Lissic itself or assemblies built on it can declare filters
            if (assembly.Name != "Lissic" && !assembly.Modules.Any(module => module.ReferencedAssemblies.Any(r => r.Name == "Lissic")))
                continue;

            CollectFilters(assembly.GlobalNamespace, found);
        }

        return found.ToImmutable();
    }

    static void CollectFilters(INamespaceOrTypeSymbol container, ImmutableArray<FilterRecord?>.Builder found)
    {
        foreach (ISymbol member in container.GetMembers())
        {
            if (member is INamespaceSymbol ns)
            {
                CollectFilters(ns, found);
                continue;
            }

            if (member is not INamedTypeSymbol type || type.DeclaredAccessibility != Accessibility.Public)
                continue;

            foreach (AttributeData attribute in type.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() == "Lissic.FilterNameAttribute"
                    && attribute.ConstructorArguments.Length > 0
                    && attribute.ConstructorArguments[0].Value?.ToString() is { Length: > 0 } name)
                {
                    found.Add(new FilterRecord(type, name));
                }
            }

            CollectFilters(type, found);
        }
    }

    static bool IsAccessible(INamedTypeSymbol type)
    {
        while (type is not null)
        {
            if (type.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
                return false;
            type = type.ContainingType;
        }
        return true;
    }

    static void GenerateFilterRegistration(SourceProductionContext context, ImmutableArray<FilterRecord?> filters)
    {
        // Ordered like the registration writer so the same type wins a duplicate name every build
        IEnumerable<FilterRecord> ordered = filters
            .Where(filter => filter.HasValue)
            .Select(filter => filter!.Value)
            .OrderBy(filter => filter.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(filter => filter.FilterType.ToDisplayString(), StringComparer.Ordinal);

        List<FilterRecord> valid = [];
        foreach (FilterRecord filter in ordered)
        {
            Location? location = filter.FilterType.Locations.FirstOrDefault();

            if (!filter.FilterType.AllInterfaces.Any(i => i.ToDisplayString() == "Lissic.IFilterFactory"))
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.FilterIsNotAFactory, location, filter.FilterType.Name));
            else if (!IsAccessible(filter.FilterType))
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.FilterIsNotAccessible, location, filter.FilterType.Name));
            else if (valid.Any(other => string.Equals(other.Name, filter.Name, StringComparison.OrdinalIgnoreCase)))
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.DuplicateFilterName, location, filter.Name, filter.FilterType.Name));
            else
                valid.Add(filter);
        }

        context.AddSource("Lissic.Filters.g.cs", FilterRegistrationWriter.Generate([.. valid]));
    }

    static void GenerateManifest(SourceProductionContext context, TemplateRecord[] manifestLog, string templateRoot, string templateSourceRoot)
    {
        string sourceCode = TemplateManifestSourceWriter.Generate(manifestLog, templateRoot, templateSourceRoot);

        context.AddSource("Lissic.Manifest.g.cs", sourceCode);
    }

    static void GeneratePathSource(
        SourceProductionContext context, 
        TemplateRecord? templateRecord, 
        ImmutableDictionary<string, AdditionalText> files, 
        string templateRoot, 
        bool ignoreRequiredProperties,
        ImmutableHashSet<string> filterNames)
    {
        if (templateRecord is null)
            return;

        Location? location = templateRecord.Value.TargetClass.Locations.FirstOrDefault();

        string key = Path.GetFullPath(Path.Combine(templateRoot, templateRecord.Value.Content)).Replace('\\', '/');
        if (!files.TryGetValue(key, out AdditionalText? sourceFile))
        {
            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.MissingTemplateFile, location, key));
            return;
        }

        string sourceText = sourceFile?.GetText()?.ToString() ?? "";
        if (sourceText == "")
            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.EmptyTemplateFile, location, key));

        TemplateRecord textTemplateRecord = new(
            sourceText,
            templateRecord.Value.TargetClass,
            TemplateType.Path);
        textTemplateRecord.FormattedProperties.AddRange(templateRecord.Value.FormattedProperties);
        textTemplateRecord.LoopLiteralNames.AddRange(templateRecord.Value.LoopLiteralNames);

        GenerateTextSource(context, textTemplateRecord, ignoreRequiredProperties, filterNames);
        return;
    }

    static void GenerateTextSource(
        SourceProductionContext context, 
        TemplateRecord? templateRecord, 
        bool ignoreRequiredProperties,
        ImmutableHashSet<string> filterNames)
    {
        if (templateRecord is null)
            return;

        foreach (IGrouping<string, FormattedVariableRecord> duplicateFormatters in templateRecord.Value.FormattedProperties
            .GroupBy(formatted => formatted.VariableName)
            .Where(group => group.Count() > 1))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.DuplicateFormatter,
                templateRecord.Value.TargetClass.Locations.FirstOrDefault(),
                duplicateFormatters.Key,
                templateRecord.Value.ClassName));
        }

        if (!TryParseTemplate(context, templateRecord.Value, filterNames, out ITemplateNode[] nodes))
            return;

        string sourceCode = TemplateSourceWriter.Generate(templateRecord.Value, nodes, ignoreRequiredProperties);

        context.AddSource($"Lissic.{templateRecord.Value.TemplateRefName}.g.cs", sourceCode);
    }

    TemplateRecord? GetTemplateRecord(GeneratorAttributeSyntaxContext context, TemplateType expectedType)
    {
        TemplateRecord? templateRecord = null;
        List<FormattedVariableRecord> formattedProperties = [];
        List<LoopLiteralNameRecord> loopLiteralNames = [];

        ImmutableArray<AttributeData> attributeArray = context.TargetSymbol.GetAttributes();

        foreach (AttributeData attribute in attributeArray)
        {
            string fullName = attribute.AttributeClass!.ToDisplayString();
            int ltIndex = fullName.LastIndexOf('<');
            if (ltIndex >= 0)
                fullName = fullName[..ltIndex];

            switch (fullName)
            {
                case "Lissic.FormattedVariableAttribute":
                    string[] args = attribute.ConstructorArguments.Length > 1 && !attribute.ConstructorArguments[1].IsNull
                        ? attribute.ConstructorArguments[1].Values.Select(v => v.Value as string ?? "").ToArray()
                        : [];
                    ITypeSymbol formatterType = attribute.AttributeClass.TypeArguments[0];
                    string refName = attribute.ConstructorArguments[0].Value as string ?? "";

                    formattedProperties.Add(new FormattedVariableRecord(args, formatterType, refName));
                    break;
    
                case "Lissic.LoopLiteralNameAttribute":
                    int index = attribute.ConstructorArguments[0].Value as int? ?? 0;
                    refName = attribute.ConstructorArguments[1].Value as string ?? "";

                    loopLiteralNames.Add(new LoopLiteralNameRecord(index, refName));
                    break;

                case "Lissic.TemplatePathAttribute":
                    if (expectedType != TemplateType.Path)
                        break;
                    
                    string content = attribute.ConstructorArguments[0].Value as string ?? "";
                    templateRecord = new TemplateRecord(content, context.TargetSymbol, TemplateType.Path);
                    break;

                case "Lissic.TemplateTextAttribute":
                    if (expectedType != TemplateType.Text)
                        break;
                    
                    content = attribute.ConstructorArguments[0].Value as string ?? "";
                    templateRecord = new TemplateRecord(content, context.TargetSymbol, TemplateType.Text);
                    break;
            }
        }

        if (templateRecord is not null)
        {
            templateRecord.Value.FormattedProperties.AddRange(formattedProperties);
            templateRecord.Value.LoopLiteralNames.AddRange(loopLiteralNames);
        }

        return templateRecord;
    }

    static bool TryParseTemplate(SourceProductionContext context, TemplateRecord record, ImmutableHashSet<string> filterNames, out ITemplateNode[] nodes)
    {
        Location? location = record.TargetClass.Locations.FirstOrDefault();

        try
        {
            nodes = LissicParser.Parse(record.Content, record.TemplateRefName, isSourceGen: true);
        }
        catch (TemplateParsingException ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InvalidTemplate, location, ex.Message));
            nodes = [];
            return false;
        }

        foreach (VariableNode variable in TemplateSourceWriter.EnumerateAllNodes(nodes).OfType<VariableNode>())
        {
            foreach (FilterCall filter in variable.Filters)
            {
                if (!filterNames.Contains(filter.Name))
                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.UnknownFilter, location, filter.Name, variable.Name, record.TemplateRefName));
            }
        }

        return true;
    }
}
