using System.Buffers;
using System.Text;

namespace Lissic.Filters.BuiltIn;


/// <summary>
/// Removes the last instance of the specified needle from the input.
/// </summary>
[FilterName("removeLast")]
public class RemoveLastFilter(string needle) : IFilterFactory
{
    private readonly ReadOnlyMemory<byte> needle = Encoding.UTF8.GetBytes(needle);

    public static ITemplateFilter Create(ReadOnlySpan<string> args) => new RemoveLastFilter(args[0]);
    public int Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output)
    {
        int foundIndex = input.LastIndexOf(needle.Span);
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