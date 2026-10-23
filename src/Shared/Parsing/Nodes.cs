using System.Buffers;

namespace Lissic.Parsing;



internal interface ITemplateNode {}


internal readonly struct BranchNode(string branchName, Dictionary<string, ITemplateNode[]> branchEntries, bool required) : ITemplateNode
{
    public ReadOnlySpan<char> BranchName => branchName;
    public IReadOnlyDictionary<string, ITemplateNode[]> BranchEntries => branchEntries;
    public bool Required => required;
}


internal readonly struct FilterCall(string name, string[] args)
{
    public string Name => name;
    public string[] Args => args;
}


#if !IN_SOURCE_GENERATOR
internal readonly struct FilteredVariableNode(VariableNode variable, ITemplateFilter[] filters) : ITemplateNode
{
    public ITemplateFilter[] Filters => filters;
    public string Name => variable.Name;
    public bool Required => variable.Required;
}
#endif


internal readonly struct HtmlNode(byte[] html) : ITemplateNode
{
    public ReadOnlyMemory<byte> Html => html;

    public void WriteTo(IBufferWriter<byte> writer)
    {
        writer.Write(html.AsSpan());
    }
}


internal readonly struct PartNode(string name, bool required) : ITemplateNode
{
    public string Name => name;
    public bool Required => required;
}


internal readonly struct VariableNode(string name, bool required, FilterCall[]? filters = null) : ITemplateNode
{
    public FilterCall[] Filters => filters ?? [];
    public string Name => name;
    public bool Required => required;
}


internal readonly struct LoopNode(string name, bool required) : ITemplateNode
{
    public string Name => name;
    public bool Required => required;
}
