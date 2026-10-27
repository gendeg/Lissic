using System.Buffers;
using System.Text;

namespace Lissic.Filters.BuiltIn;


/// <summary>
/// Prepends the specified text to the input.
/// </summary>
[FilterName("prepend")]
public class PrependFilter(string text) : IFilterFactory
{
    private readonly ReadOnlyMemory<byte> text = Encoding.UTF8.GetBytes(text);

    public static ITemplateFilter Create(ReadOnlySpan<string> args) => new PrependFilter(args[0]);
    public int Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output)
    {
        output.Write(text.Span);
        output.Write(input);
        return text.Length + input.Length;
    }
}