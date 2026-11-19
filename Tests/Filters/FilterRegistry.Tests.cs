using System.Buffers;
using System.Text;

namespace Lissic.Tests.Filters;


public class FilterRegistryTests
{
    private sealed class SuffixFilter(string suffix) : IFilterFactory
    {
        public string Suffix => suffix;

        public static ITemplateFilter Create(ReadOnlySpan<string> args) => new SuffixFilter(args.Length > 0 ? args[0] : "");

        public int Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output)
        {
            output.Write(input);
            output.Write(Encoding.UTF8.GetBytes(suffix));
            return input.Length + Encoding.UTF8.GetBytes(suffix).Length;
        }
    }

    [Fact]
    public void TryCreate_RegisteredName_PassesArgsToFactory()
    {
        FilterRegistry.Register<SuffixFilter>("registry_test_suffix");

        Assert.True(FilterRegistry.TryCreate("registry_test_suffix", ["!"], out ITemplateFilter? filter));

        ArrayBufferWriter<byte> output = new();
        filter.Apply("hi"u8, output);
        Assert.Equal("hi!", Encoding.UTF8.GetString(output.WrittenSpan));
    }

    [Fact]
    public void TryCreate_IsCaseInsensitive()
    {
        FilterRegistry.Register<SuffixFilter>("registry_test_case");

        Assert.True(FilterRegistry.TryCreate("REGISTRY_TEST_CASE", [], out _));
    }

    [Fact]
    public void TryCreate_UnknownName_ReturnsFalse()
    {
        Assert.False(FilterRegistry.TryCreate("registry_test_missing", [], out ITemplateFilter? filter));
        Assert.Null(filter);
    }

    [Fact]
    public void TryCreate_BuildsANewInstancePerCall()
    {
        FilterRegistry.Register<SuffixFilter>("registry_test_instances");

        FilterRegistry.TryCreate("registry_test_instances", ["a"], out ITemplateFilter? first);
        FilterRegistry.TryCreate("registry_test_instances", ["b"], out ITemplateFilter? second);

        Assert.NotSame(first, second);
        Assert.Equal("a", ((SuffixFilter)first!).Suffix);
        Assert.Equal("b", ((SuffixFilter)second!).Suffix);
    }

    [Fact]
    public void Register_SameNameAgain_ReplacesTheFactory()
    {
        FilterRegistry.Register<SuffixFilter>("registry_test_replace");
        FilterRegistry.Register<OtherFilter>("registry_test_replace");

        FilterRegistry.TryCreate("registry_test_replace", [], out ITemplateFilter? filter);

        Assert.IsType<OtherFilter>(filter);
    }

    // A filter name has to be usable in a template: a letter or '_' followed by letters, digits or '_'
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("registry-test-hyphen")]
    [InlineData("1registry_test_digit")]
    [InlineData("registry test space")]
    [InlineData(" registry_test_leading")]
    [InlineData("registry_test_trailing ")]
    [InlineData("registry.test.dot")]
    [InlineData("registry_test(paren)")]
    [InlineData("registry_test|pipe")]
    [InlineData("registry_test?")]
    [InlineData("part.registry_test")]
    public void Register_InvalidName_ThrowsAndDoesNotRegister(string name)
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(() => FilterRegistry.Register<SuffixFilter>(name));

        Assert.Equal("name", ex.ParamName);
        Assert.False(FilterRegistry.TryCreate(name, [], out _));
    }

    [Fact]
    public void Register_NullName_Throws()
    {
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => FilterRegistry.Register<SuffixFilter>(null!));

        Assert.Equal("name", ex.ParamName);
    }

    [Theory]
    [InlineData("registry_test_valid")]
    [InlineData("_registry_test_underscore")]
    [InlineData("RegistryTestPascal")]
    [InlineData("registry_test_2")]
    [InlineData("régistry_test_letter")]
    public void Register_ValidName_IsRegistered(string name)
    {
        FilterRegistry.Register<SuffixFilter>(name);

        Assert.True(FilterRegistry.TryCreate(name, [], out _));
    }

    private sealed class OtherFilter : IFilterFactory
    {
        public static ITemplateFilter Create(ReadOnlySpan<string> args) => new OtherFilter();
        public int Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output) { return 0; }
    }
}
