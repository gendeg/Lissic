using System.Buffers;
using System.Text;

namespace Lissic.Filters.BuiltIn;


/// <summary>
/// Appends the specified text to the input.
/// </summary>
[FilterName("append")]
public class AppendFilter(string text) : IFilterFactory
{
    private readonly ReadOnlyMemory<byte> text = Encoding.UTF8.GetBytes(text);

    public static ITemplateFilter Create(ReadOnlySpan<string> args) => new AppendFilter(args[0]);
    public void Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output)
    {
        output.Write(input);
        output.Write(text.Span);
    }
}