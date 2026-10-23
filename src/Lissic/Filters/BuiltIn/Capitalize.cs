using System.Buffers;
using System.Text;

namespace Lissic.Filters.BuiltIn;


/// <summary>
/// Capitalizes the first letter of the input. If the first character is already uppercase, the input remains unchanged.
/// </summary>
[FilterName("capitalize")]
public class CapitalizeFilter : IFilterFactory
{
    public static ITemplateFilter Create(ReadOnlySpan<string> args) => new CapitalizeFilter();
    public void Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output)
    {
        Rune currentRune = default;
        Rune? upperRune = null;
        int totalConsumed = 0;
        int bytesConsumed = 0;

        while (totalConsumed < input.Length && Rune.DecodeFromUtf8(input[totalConsumed..], out currentRune, out bytesConsumed) == OperationStatus.Done)
        {
            if (Rune.IsLetter(currentRune))
            {
                upperRune = Rune.ToUpperInvariant(currentRune);
                break;
            }
            totalConsumed += bytesConsumed;
        }

        if (upperRune is null || upperRune == currentRune)
        {
            output.Write(input);
            return;
        }

        output.Write(input[..totalConsumed]);

        Span<byte> buffer = stackalloc byte[4];
        int runeByteCount = upperRune.Value.EncodeToUtf8(buffer);
        output.Write(buffer[..runeByteCount]);

        output.Write(input[(totalConsumed + bytesConsumed)..]); // use byte count from the removed Rune, in case upper and lower case differ in byte length
    }
}