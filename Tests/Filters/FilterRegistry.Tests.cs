using System.Buffers;
using System.Text;

namespace Lissic.Tests.Filters;


public class FilterRegistryTests
{
    private sealed class SuffixFilter(string suffix) : IFilterFactory
    {
        public string Suffix => suffix;

        public static ITemplateFilter Create(ReadOnlySpan<string> args) => new SuffixFilter(args.Length > 0 ? args[0] : "");

        public void Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output)
        {
            output.Write(input);
            output.Write(Encoding.UTF8.GetBytes(suffix));
        }
    }

    [Fact]
    public void TryCreate_RegisteredName_PassesArgsToFactory()
    {
        FilterRegistry.Register<SuffixFilter>("registry-test-suffix");

        Assert.True(FilterRegistry.TryCreate("registry-test-suffix", ["!"], out ITemplateFilter? filter));

        ArrayBufferWriter<byte> output = new();
        filter.Apply("hi"u8, output);
        Assert.Equal("hi!", Encoding.UTF8.GetString(output.WrittenSpan));
    }

    [Fact]
    public void TryCreate_IsCaseInsensitive()
    {
        FilterRegistry.Register<SuffixFilter>("registry-test-case");

        Assert.True(FilterRegistry.TryCreate("REGISTRY-TEST-CASE", [], out _));
    }

    [Fact]
    public void TryCreate_UnknownName_ReturnsFalse()
    {
        Assert.False(FilterRegistry.TryCreate("registry-test-missing", [], out ITemplateFilter? filter));
        Assert.Null(filter);
    }

    [Fact]
    public void TryCreate_BuildsANewInstancePerCall()
    {
        FilterRegistry.Register<SuffixFilter>("registry-test-instances");

        FilterRegistry.TryCreate("registry-test-instances", ["a"], out ITemplateFilter? first);
        FilterRegistry.TryCreate("registry-test-instances", ["b"], out ITemplateFilter? second);

        Assert.NotSame(first, second);
        Assert.Equal("a", ((SuffixFilter)first!).Suffix);
        Assert.Equal("b", ((SuffixFilter)second!).Suffix);
    }

    [Fact]
    public void Register_SameNameAgain_ReplacesTheFactory()
    {
        FilterRegistry.Register<SuffixFilter>("registry-test-replace");
        FilterRegistry.Register<OtherFilter>("registry-test-replace");

        FilterRegistry.TryCreate("registry-test-replace", [], out ITemplateFilter? filter);

        Assert.IsType<OtherFilter>(filter);
    }

    private sealed class OtherFilter : IFilterFactory
    {
        public static ITemplateFilter Create(ReadOnlySpan<string> args) => new OtherFilter();
        public void Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output) { }
    }
}
