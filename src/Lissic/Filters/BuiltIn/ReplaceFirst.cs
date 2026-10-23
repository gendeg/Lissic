using System.Buffers;
using System.Text;

namespace Lissic.Filters.BuiltIn;


/// <summary>
/// Replaces the first instance of the specified needle with the replacement text.
/// </summary>
[FilterName("replacefirst")]
public class ReplaceFirstFilter(string needle, string replacement) : IFilterFactory
{
    private readonly ReadOnlyMemory<byte> needle = Encoding.UTF8.GetBytes(needle);
    private readonly ReadOnlyMemory<byte> replacement = Encoding.UTF8.GetBytes(replacement);

    public static ITemplateFilter Create(ReadOnlySpan<string> args) => new ReplaceFirstFilter(args[0], args[1]);
    public void Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output)
    {
        int foundIndex = input.IndexOf(needle.Span);
        if (foundIndex == -1)
        {
            output.Write(input);
            return;
        }
        output.Write(input[..foundIndex]);
        output.Write(replacement.Span);
        output.Write(input[(foundIndex + needle.Span.Length)..]);
    }
}