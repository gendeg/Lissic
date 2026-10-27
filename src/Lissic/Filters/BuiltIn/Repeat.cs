using System.Buffers;

namespace Lissic.Filters.BuiltIn;


/// <summary>
/// Repeats the input the specified number of times.
/// </summary>
[FilterName("repeat")]
public class RepeatFilter : IFilterFactory
{
    private readonly int _count;

    public RepeatFilter(string count)
    {
        if (int.TryParse(count, out _count))
        {
            if (_count < 0) _count = 0;
        }
        else
            _count = 0;
    }

    public static ITemplateFilter Create(ReadOnlySpan<string> args) => new RepeatFilter(args[0]);
    public int Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output)
    {
        for (int i = 0; i < _count; i++)
        {
            output.Write(input);
        }
        return input.Length * _count;
    }
}