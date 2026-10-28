using System.Buffers;
using System.Text;

namespace Lissic.Filters.BuiltIn;


/// <summary>
/// Removes the first instance of the specified needle from the input.
/// </summary>
[FilterName("removeFirst")]
public class RemoveFirstFilter(string needle) : IFilterFactory
{
    private readonly ReadOnlyMemory<byte> needle = Encoding.UTF8.GetBytes(needle);

    public static ITemplateFilter Create(ReadOnlySpan<string> args) => new RemoveFirstFilter(args[0]);
    public int Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output)
    {
        int foundIndex = input.IndexOf(needle.Span);
        if (foundIndex == -1)
        {
            output.Write(input);
            return input.Length;
        }
        output.Write(input[..foundIndex]);
        output.Write(input[(foundIndex + needle.Span.Length)..]);
        return input.Length - needle.Span.Length;
    }
}