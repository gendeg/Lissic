using Lissic.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace LissicGen;


internal static class TemplateSourceWriter
{
    public static string Generate(TemplateRecord templateRecord, ITemplateNode[] templateNodes, bool ignoreRequiredProperties)
    {

        (string properties, string argFields, string branchKeys, string loops, string parts, string variableStrings, string streamProperties)
            = ParseNodes(templateNodes, templateRecord, ignoreRequiredProperties);

        string visibility = (templateRecord.TargetClass as INamedTypeSymbol)?.DeclaredAccessibility.ToString().ToLower() ?? "public";

        StringBuilder sb = new();

        sb.Append($$"""
#nullable enable
using Lissic;
using Lissic.Tools;
using System;
using System.Buffers;

namespace {{templateRecord.Namespace}};
{{GetParentHeader(templateRecord)}}
{{visibility}} partial class {{templateRecord.ClassName}} : LissicTemplate
{
{{properties}}{{argFields}}
    public override string Get__BranchKey(ReadOnlySpan<char> keyName) => keyName switch
    {
{{branchKeys}}        _ => ""
    };

    public override LoopValue? Get__Loop(ReadOnlySpan<char> name)
    {
        switch (name)
        {
{{loops}}            default: return null;
        }
    }

    public override LissicTemplate? Get__Part(ReadOnlySpan<char> name) => name switch
    {
{{parts}}        _ => null
    };

    public override string? Get__VariableString(ReadOnlySpan<char> name)
    {
        switch (name)
        {
{{variableStrings}}            default: return null;
        }
    }

    public override int Stream__Variable(ReadOnlySpan<char> variableName, IBufferWriter<byte> writer)
    {
        switch (variableName)
        {
{{streamProperties}}            default: return -1;
        }
    }
}
{{GetParentFooter(templateRecord)}}
""");

        return sb.ToString();
    }

    internal static string GetParentHeader(TemplateRecord templateRecord)
    {
        if (templateRecord.NestedTypes.Length == 0)
        {
            return "";
        }

        StringBuilder sb = new("\n");
        foreach (INamedTypeSymbol nestedType in templateRecord.NestedTypes)
        {
            sb.Append($"{GetTypeSignature((nestedType.DeclaringSyntaxReferences[0].GetSyntax() as TypeDeclarationSyntax)!)} {{ ");
        }
        return sb.ToString();
    }

    internal static string GetParentFooter(TemplateRecord templateRecord)
    {
        return new string('}', templateRecord.NestedTypes.Length) + "\n";
    }

    internal static (string returnString, string streamVariable, string argField) GetVariableStrings(VariableNode node, TemplateRecord templateRecord)
    {
        int formatterIndex = templateRecord.FormattedProperties.FindIndex(prop => prop.VariableName == node.Name);

        if (formatterIndex < 0) // No formatter, write the value directly
        {
            return ($"            case \"{node.Name}\": return {node.Name}.ToString();",
                    $"            case \"{node.Name}\": if ({node.Name}.IsEmpty) return -1; return {node.Name}.WriteTo(writer);",
                    "");
        }

        FormattedVariableRecord formatter = templateRecord.FormattedProperties[formatterIndex];
        string formatterName = GetFullTypeName(formatter);
        string argsRef = "[]";
        string argField = "";

        if (formatter.Args.Length > 0)
        {
            argsRef = $"__args_{node.Name}";
            argField = $"    private static readonly string[] {argsRef} = [{formatter.ArgsToString()}];";
        }

        string returnString = $$"""
            case "{{node.Name}}":
            {
                if ({{node.Name}}.IsEmpty) return string.Empty;
                using PooledByteWriter buffer = new();
                global::{{formatterName}}.Format({{node.Name}}, buffer, {{argsRef}});
                return buffer.GetString();
            }
""";
        string streamVariable = $"            case \"{node.Name}\": if ({node.Name}.IsEmpty) return -1; return global::{formatterName}.Format({node.Name}, writer, {argsRef});";

        return (returnString, streamVariable, argField);
    }


    internal static string GetFullTypeName(FormattedVariableRecord record)
    {
        string typeNamespace = record.FormatterType.ContainingNamespace.IsGlobalNamespace ? "" : record.FormatterType.ContainingNamespace + ".";
        return typeNamespace + record.FormatterType.Name;
    }

    private static (string name, bool required)? GetNodeProperties(ITemplateNode node) => node switch
    {
        BranchNode branch => (branch.BranchName.ToString(), branch.Required),
        LoopNode loop => (loop.Name, loop.Required),
        PartNode part => (part.Name, part.Required),
        VariableNode variable => (variable.Name, variable.Required),
        _ => null,
    };

    internal static string GetTypeSignature(TypeDeclarationSyntax typeDecl)
    {
        var modifierTokens = typeDecl.Modifiers.Select(m => m.Text).ToList();
        if (!modifierTokens.Contains("partial"))
            modifierTokens.Add("partial");

        string modifiersStr = string.Join(" ", modifierTokens) + " ";

        string keyword = typeDecl.Keyword.Text;       // e.g. "class", "struct", "record", "record struct"
        string identifier = typeDecl.Identifier.Text; // Name of the object
        string typeParams = typeDecl.TypeParameterList?.ToString() ?? string.Empty;

        string recordParams = string.Empty;
        if (typeDecl is RecordDeclarationSyntax recordDecl && recordDecl.ParameterList != null)
        {
            recordParams = recordDecl.ParameterList.ToString();
        }

        string constraints = typeDecl.ConstraintClauses.Any()
            ? " " + string.Join(" ", typeDecl.ConstraintClauses.Select(c => c.ToString()))
            : string.Empty;

        // e.g.  public partial  class  MyContainer<T>    (object foo)         where T : class"
        return $"{modifiersStr}{keyword} {identifier}{typeParams}{recordParams}{constraints}";
    }

    internal static (string properties, string argFields, string branchKeys, string loops, string parts, string variableStrings, string streamProperties)
        ParseNodes(ITemplateNode[] templateNodes, TemplateRecord templateRecord, bool irp)
    {
        StringBuilder sbProperties = new();
        StringBuilder sbArgFields = new();
        StringBuilder sbBranchKeys = new();
        StringBuilder sbLoops = new();
        StringBuilder sbParts = new();
        StringBuilder sbVariableStrings = new();
        StringBuilder sbStreamProperties = new();

        ITemplateNode[] allNodes = [.. EnumerateAllNodes(templateNodes)];

        Dictionary<string, bool> isRequired = [];
        foreach (ITemplateNode node in allNodes)
        {
            if (GetNodeProperties(node) is (string name, bool required))
                isRequired[name] = isRequired.TryGetValue(name, out bool alreadyRequired) ? alreadyRequired | required : required;
        }

        HashSet<string> declared = [];
        foreach (ITemplateNode node in allNodes)
        {
            if (GetNodeProperties(node) is not (string name, _) || !declared.Add(name))
                continue;

            string requiredModifier = isRequired[name] && !irp ? "required " : "";

            switch (node)
            {
                case BranchNode:
                    sbProperties.AppendLine($"    public {requiredModifier}BranchValue {name} {{ get; set; }}");
                    sbBranchKeys.AppendLine($"        \"{name}\" => {name}.Key,");
                    break;
                case LoopNode:
                    sbProperties.AppendLine($"    public {requiredModifier}LoopValue {name} {{ get; set; }}");
                    sbLoops.AppendLine($"            case \"{name}\": return {name};");
                    break;
                case PartNode:
                    sbProperties.AppendLine($"    public {requiredModifier}PartValue {name} {{ get; set; }}");
                    sbParts.AppendLine($"        \"{name}\" => {name}.Template,");
                    break;
                case VariableNode variableNode:
                    sbProperties.AppendLine($"    public {requiredModifier}VariableValue {name} {{ get; set; }}");
                    (string returnString, string streamVariable, string argField) = GetVariableStrings(variableNode, templateRecord);
                    if (argField.Length > 0)
                        sbArgFields.AppendLine(argField);
                    sbVariableStrings.AppendLine(returnString);
                    sbStreamProperties.AppendLine(streamVariable);
                    break;
            }
        }

        return (sbProperties.ToString(), sbArgFields.ToString(), sbBranchKeys.ToString(), sbLoops.ToString(), sbParts.ToString(), sbVariableStrings.ToString(), sbStreamProperties.ToString());
    }

    // Enumerates all template nodes, including nested nodes within branch entries
    internal static IEnumerable<ITemplateNode> EnumerateAllNodes(IEnumerable<ITemplateNode> nodes)
    {
        foreach (ITemplateNode node in nodes)
        {
            yield return node;

            if (node is BranchNode branch)
            {
                foreach (ITemplateNode[] entry in branch.BranchEntries.Values)
                {
                    foreach (ITemplateNode nested in EnumerateAllNodes(entry))
                        yield return nested;
                }
            }
        }
    }
}
