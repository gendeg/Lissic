using System.Buffers;
using System.Text;

namespace Lissic.Filters.BuiltIn;


/// <summary>
/// Converts the input to uppercase.
/// </summary>
[FilterName("upper")]
public class UpperFilter : IFilterFactory
{
    public static ITemplateFilter Create(ReadOnlySpan<string> args) => new UpperFilter();
    public void Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output)
    {
        Span<byte> outSpan = output.GetSpan(input.Length * 2);
        Span<byte> buffer = stackalloc byte[4];
        int totalRead = 0;
        int totalWritten = 0;

        while (totalRead < input.Length)
        {
            OperationStatus status = Rune.DecodeFromUtf8(input[totalRead..], out Rune currentRune, out int bytesConsumed);
            if (status != OperationStatus.Done)
            {
                input[totalRead..].CopyTo(outSpan);
                totalWritten += input.Length - totalRead;
                break;
            }

            if (Rune.IsLower(currentRune))
            {
                currentRune = Rune.ToUpperInvariant(currentRune);
                int bytesWritten = currentRune.EncodeToUtf8(buffer);
                buffer[..bytesWritten].CopyTo(outSpan);
                outSpan = outSpan[bytesWritten..];
                totalRead += bytesConsumed;
                totalWritten += bytesWritten;
            }
            else
            {
                input[totalRead..(totalRead + bytesConsumed)].CopyTo(outSpan);
                outSpan = outSpan[bytesConsumed..];
                totalRead += bytesConsumed;
                totalWritten += bytesConsumed;
            }
        }
        output.Advance(totalWritten);
    }
}