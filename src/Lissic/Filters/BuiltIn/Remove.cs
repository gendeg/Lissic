using System.Buffers;
using System.Text;

namespace Lissic.Filters.BuiltIn;


/// <summary>
/// Removes all instances of the specified needle from the input.
/// </summary>
[FilterName("remove")]
public class RemoveFilter(string needle) : IFilterFactory
{
    private readonly ReadOnlyMemory<byte> needle = Encoding.UTF8.GetBytes(needle);

    public static ITemplateFilter Create(ReadOnlySpan<string> args) => new RemoveFilter(args[0]);
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
            index += foundIndex + needle.Span.Length;
        }
    }
}