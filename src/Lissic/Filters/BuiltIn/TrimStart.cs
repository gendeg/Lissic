using System.Buffers;
using System.Text;

namespace Lissic.Filters.BuiltIn;


/// <summary>
/// Trims whitespace from the start of the input.
/// </summary>
[FilterName("trimStart")]
public class TrimStartFilter : IFilterFactory
{
    public static ITemplateFilter Create(ReadOnlySpan<string> args) => new TrimStartFilter();
    public int Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output)
    {
        int start = 0;
        int end = input.Length;

        while (start < end && Rune.DecodeFromUtf8(input[start..end], out Rune currentRune, out int bytesConsumed) == OperationStatus.Done && Rune.IsWhiteSpace(currentRune))
        {
            start += bytesConsumed;
        }

        output.Write(input[start..end]);
        return end - start;
    }
}