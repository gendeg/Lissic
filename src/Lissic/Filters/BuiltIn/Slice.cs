using System.Buffers;
using System.Text;

namespace Lissic.Filters.BuiltIn;


/// <summary>
/// Slices the input starting from the specified start index and for the specified length.  Negative start indices count from the end of the input.
/// </summary>
[FilterName("slice")]
public class SliceFilter : IFilterFactory
{
    private readonly int _start;
    private readonly int _length;

    public SliceFilter(string start, string length)
    {
        int.TryParse(start, out _start);
        int.TryParse(length, out _length);
    }

    public static ITemplateFilter Create(ReadOnlySpan<string> args) => new SliceFilter(args[0], args[1]);
    public void Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output)
    {
        int startValue = _start;
        int lengthValue = Math.Max(0, _length);

        int idx = 0;
        int totalBytesConsumed = 0;
        int startByteIndex;
        int bytesConsumed;

        if (startValue >= 0)
        {
            while (idx != startValue)
            {
                if (Rune.DecodeFromUtf8(input[totalBytesConsumed..], out Rune _, out bytesConsumed) != OperationStatus.Done)
                    break;
                idx++;
                totalBytesConsumed += bytesConsumed;
            }
            startByteIndex = totalBytesConsumed;
        }
        else
        {
            totalBytesConsumed = input.Length;
            while (idx != startValue)
            {
                if (Rune.DecodeLastFromUtf8(input[..totalBytesConsumed], out _, out bytesConsumed) != OperationStatus.Done)
                    break;
                idx--;
                totalBytesConsumed -= bytesConsumed;
            }
            startByteIndex = totalBytesConsumed;
        }

        idx = 0;
        while (idx != lengthValue)
        {
            if (Rune.DecodeFromUtf8(input[totalBytesConsumed..], out _, out bytesConsumed) != OperationStatus.Done)
                break;
            idx++;
            totalBytesConsumed += bytesConsumed;
        }

        output.Write(input[startByteIndex..totalBytesConsumed]);
    }
}