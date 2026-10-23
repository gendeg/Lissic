using System.Buffers;
using System.Text;

namespace Lissic.Filters.BuiltIn;


/// <summary>
/// Truncates the input to the specified maximum length.  Negative lengths remove characters from the end.
/// </summary>
[FilterName("truncate")]
public class TruncateFilter : IFilterFactory
{
    private readonly int _maxLength;

    public TruncateFilter(string maxLength)
    {
        int.TryParse(maxLength, out _maxLength);
    }

    public static ITemplateFilter Create(ReadOnlySpan<string> args) => new TruncateFilter(args[0]);
    public void Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output)
    {
        int idx = 0;
        int totalBytesConsumed = 0;

        if (_maxLength >= 0)
        {
            while (idx != _maxLength)
            {
                if (Rune.DecodeFromUtf8(input[totalBytesConsumed..], out Rune _, out int bytesConsumed) != OperationStatus.Done)
                    break;
                idx++;
                totalBytesConsumed += bytesConsumed;
            }
        }
        else
        {
            totalBytesConsumed = input.Length;
            while (idx != _maxLength)
            {
                if (Rune.DecodeLastFromUtf8(input[..totalBytesConsumed], out _, out int bytesConsumed) != OperationStatus.Done)
                    break;
                idx--;
                totalBytesConsumed -= bytesConsumed;
            }
        }

        output.Write(input[..totalBytesConsumed]);
    }
}