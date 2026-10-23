using System.Buffers;
using System.Text;

namespace Lissic.Filters.BuiltIn;


/// <summary>
/// Replaces all instances of the specified needle with the replacement text.
/// </summary>
[FilterName("replace")]
public class ReplaceFilter(string needle, string replacement) : IFilterFactory
{
    private readonly ReadOnlyMemory<byte> needle = Encoding.UTF8.GetBytes(needle);
    private readonly ReadOnlyMemory<byte> replacement = Encoding.UTF8.GetBytes(replacement);

    public static ITemplateFilter Create(ReadOnlySpan<string> args) => new ReplaceFilter(args[0], args[1]);
    public void Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output)
    {
        int index = 0;
        while (index < input.Length)
        {
            int foundIndex = input[index..].IndexOf(needle.Span);
            if (foundIndex == -1)
            {
                output.Write(input[index..]);
                break;
            }
            output.Write(input[index..(index + foundIndex)]);
            output.Write(replacement.Span);
            index += foundIndex + needle.Span.Length;
        }
    }
}