using System.Buffers;
using System.Text;

namespace Lissic.Filters.BuiltIn;


/// <summary>
/// Trims whitespace from the start and end of the input.
/// </summary>
[FilterName("trim")]
public class TrimFilter : IFilterFactory
{
    public static ITemplateFilter Create(ReadOnlySpan<string> args) => new TrimFilter();
    public int Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output)
    {
        int start = 0;
        int end = input.Length;

        while (start < end && Rune.DecodeFromUtf8(input[start..end], out Rune currentRune, out int bytesConsumed) == OperationStatus.Done && Rune.IsWhiteSpace(currentRune))
        {
            start += bytesConsumed;
        }

        while (end > start && Rune.DecodeLastFromUtf8(input[..end], out Rune currentRune, out int bytesConsumed) == OperationStatus.Done && Rune.IsWhiteSpace(currentRune))
        {
            end -= bytesConsumed;
        }

        output.Write(input[start..end]);
        return end - start;
    }
}