using System.Buffers;
using System.Text;

namespace Lissic.Filters.BuiltIn;


/// <summary>
/// Replaces the last instance of the specified needle with the replacement text.
/// </summary>
[FilterName("replacelast")]
public class ReplaceLastFilter(string needle, string replacement) : IFilterFactory
{
    private readonly ReadOnlyMemory<byte> needle = Encoding.UTF8.GetBytes(needle);
    private readonly ReadOnlyMemory<byte> replacement = Encoding.UTF8.GetBytes(replacement);

    public static ITemplateFilter Create(ReadOnlySpan<string> args) => new ReplaceLastFilter(args[0], args[1]);
    public int Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output)
    {
        int foundIndex = input.LastIndexOf(needle.Span);
        if (foundIndex == -1)
        {
            output.Write(input);
            return input.Length;
        }
        output.Write(input[..foundIndex]);
        output.Write(replacement.Span);
        output.Write(input[(foundIndex + needle.Span.Length)..]);
        return input.Length - needle.Span.Length + replacement.Span.Length;
    }
}