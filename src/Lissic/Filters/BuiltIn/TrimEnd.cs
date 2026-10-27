using System.Buffers;
using System.Text;

namespace Lissic.Filters.BuiltIn;


/// <summary>
/// Trims whitespace from the end of the input.
/// </summary>
[FilterName("trimend")]
public class TrimEndFilter : IFilterFactory
{
    public static ITemplateFilter Create(ReadOnlySpan<string> args) => new TrimEndFilter();
    public int Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output)
    {
        int start = 0;
        int end = input.Length;

        while (end > start && Rune.DecodeLastFromUtf8(input[..end], out Rune currentRune, out int bytesConsumed) == OperationStatus.Done && Rune.IsWhiteSpace(currentRune))
        {
            end -= bytesConsumed;
        }

        output.Write(input[start..end]);
        return end - start;
    }
}