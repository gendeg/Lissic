using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LissicGen;


public readonly record struct TemplateRecord : IComparable<TemplateRecord>
{
    public readonly string ClassName => TargetClass.Name;
    public readonly string Content;
    public readonly List<FormattedVariableRecord> FormattedProperties;
    public readonly List<LoopLiteralNameRecord> LoopLiteralNames;
    public readonly string Namespace;
    public readonly INamedTypeSymbol[] NestedTypes;
    public readonly ISymbol TargetClass;
    public readonly string TemplateRefName => TargetClass.Name;
    public readonly TemplateType Type;

    public TemplateRecord(string content, ISymbol targetClass, TemplateType type)
    {
        TargetClass = targetClass;
        Content = content;
        Type = type;
        Namespace = TargetClass.ContainingNamespace.IsGlobalNamespace ? "" : TargetClass.ContainingNamespace.ToDisplayString();
        FormattedProperties = [];
        LoopLiteralNames = [];

        ISymbol parent = TargetClass.ContainingType;
        int count = 0;
        while (parent is not null && parent is INamedTypeSymbol)
        {
            count++;
            parent = parent.ContainingType;
        }

        NestedTypes = new INamedTypeSymbol[count];
        parent = TargetClass.ContainingType;
        for (int i = count - 1; i >= 0; i--)
        {
            NestedTypes[i] = (INamedTypeSymbol)parent;
            parent = parent.ContainingType;
        }
    }

    public int CompareTo(TemplateRecord other)
    {
        return TemplateRefName.CompareTo(other.TemplateRefName);
    }

    public readonly bool Equals(TemplateRecord other)
    {
        return  TemplateRefName == other.TemplateRefName
            && SymbolEqualityComparer.Default.Equals(TargetClass, other.TargetClass)
            && Content == other.Content 
            && Type == other.Type
            && FormattedProperties.SequenceEqual(other.FormattedProperties) 
            && LoopLiteralNames.SequenceEqual(other.LoopLiteralNames);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(TemplateRefName);
        hashCode.Add(SymbolEqualityComparer.Default.GetHashCode(TargetClass));
        hashCode.Add(Content);
        hashCode.Add(Type);
        foreach (var formattedVariable in FormattedProperties)
        {
            hashCode.Add(formattedVariable);
        }
        foreach (var loopLiteralName in LoopLiteralNames)
        {
            hashCode.Add(loopLiteralName);
        }

        return hashCode.ToHashCode();
    }
}


public readonly record struct FormattedVariableRecord
{
    public readonly string[] Args;
    public readonly ITypeSymbol FormatterType;
    public readonly string VariableName;

    public FormattedVariableRecord(string[] args, ITypeSymbol formatterType, string variableName)
    {
        Args = args;
        FormatterType = formatterType;
        VariableName = variableName;
    }

    public readonly string ArgsToString()
    {
        if (Args == null || Args.Length == 0)
        {
            return "";
        }

        return string.Join(", ", Args.Select(arg => SymbolDisplay.FormatLiteral(arg, quote: true)));
    }

    public readonly bool Equals(FormattedVariableRecord other)
    {
        return VariableName == other.VariableName 
            && SymbolEqualityComparer.Default.Equals(FormatterType, other.FormatterType)
            && Args.SequenceEqual(other.Args);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(VariableName);
        hashCode.Add(FormatterType);
        foreach (var arg in Args)
        {
            hashCode.Add(arg);
        }

        return hashCode.ToHashCode();
    }
}


public readonly record struct FilterRecord
{
    public readonly INamedTypeSymbol FilterType;
    public readonly string Name;

    public FilterRecord(INamedTypeSymbol filterType, string name)
    {
        FilterType = filterType;
        Name = name;
    }
}


public readonly record struct LoopLiteralNameRecord
{
    public readonly int Index;
    public readonly string Name;

    public LoopLiteralNameRecord(int index, string name)
    {
        Index = index;
        Name = name;
    }
}


public enum TemplateType : byte
{
    Path,
    Text
}
