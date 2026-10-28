using System.Buffers;

namespace Lissic;


/// <summary>
/// A text transformation applied inline in a template, e.g. <c>{{ Name &lt;upper | pad("10") &gt; }}</c>.
/// Input and output are UTF-8; no escaping is applied before or after (with the exception of <c>htmlTextSafe</c> and <c>htmlAttrSafe</c> filters).
/// Returns the number of bytes written to the output.
/// </summary>
public interface ITemplateFilter
{
    int Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output);
}


/// <summary>
/// Implemented by a filter type to build itself from template arguments; called once per reference when the template is cached.
/// </summary>
public interface IFilterFactory : ITemplateFilter
{
    static abstract ITemplateFilter Create(ReadOnlySpan<string> args);
}


/// <summary>
/// Stands in for a filter name that isn't registered; passes the value through unchanged.
/// </summary>
internal sealed class UnknownFilter(string name) : ITemplateFilter
{
    public int Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output)
    {
        UnknownTagDebug.Handle(output, $"Filter '{name}' not found");
        output.Write(input);
        return input.Length;
    }
}
