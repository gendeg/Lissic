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
        int.TryParse(count, out _count);
    }

    public static ITemplateFilter Create(ReadOnlySpan<string> args) => new RepeatFilter(args[0]);
    public void Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output)
    {
        for (int i = 0; i < _count; i++)
        {
            output.Write(input);
        }
    }
}