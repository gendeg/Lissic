using System.Buffers;
using System.Text;
using Lissic.Filters.BuiltIn;

namespace Lissic.Tests.Filters;

public class BuiltInFiltersTests
{
	[Theory]
	[InlineData("hello", "!", "hello!")]
	[InlineData("", "tail", "tail")]
	[InlineData("世界", "!", "世界!")]
	public void Append_AddsTextAfterInput(string input, string text, string expected) =>
		AssertApply(input, new AppendFilter(text), expected);

	[Theory]
	[InlineData("hello", "pre-", "pre-hello")]
	[InlineData("", "head", "head")]
	[InlineData("世界", "前", "前世界")]
	public void Prepend_AddsTextBeforeInput(string input, string text, string expected) =>
		AssertApply(input, new PrependFilter(text), expected);

	[Theory]
	[InlineData("hello world", "Hello world")]
	[InlineData("...hello", "...Hello")]
	[InlineData("Already Upper", "Already Upper")]
	[InlineData("123", "123")]
	[InlineData("éCOLE", "ÉCOLE")]
	[InlineData("", "")]
	public void Capitalize_UppercasesFirstLetter(string input, string expected) =>
		AssertApply(input, new CapitalizeFilter(), expected);

	[Theory]
	[InlineData("HELLO World", "hello world")]
	[InlineData("Already lower", "already lower")]
	[InlineData("123ABC", "123abc")]
	[InlineData("ÉCOLE", "école")]
	[InlineData("", "")]
	public void Lower_ConvertsTextToLowercase(string input, string expected) =>
		AssertApply(input, new LowerFilter(), expected);

	[Theory]
	[InlineData("hello WORLD", "HELLO WORLD")]
	[InlineData("Already Upper", "ALREADY UPPER")]
	[InlineData("123abc", "123ABC")]
	[InlineData("école", "ÉCOLE")]
	[InlineData("", "")]
	public void Upper_ConvertsTextToUppercase(string input, string expected) =>
		AssertApply(input, new UpperFilter(), expected);

	[Theory]
	[InlineData("one two two", "two", "one  ")]
	[InlineData("aaaa", "aa", "")]
	[InlineData("hello", "x", "hello")]
	[InlineData("世界世界", "世界", "")]
	[InlineData("", "x", "")]
	public void Remove_RemovesEveryOccurrence(string input, string needle, string expected) =>
		AssertApply(input, new RemoveFilter(needle), expected);

	[Theory]
	[InlineData("one two two", "two", "one  two")]
	[InlineData("aaaa", "aa", "aa")]
	[InlineData("hello", "x", "hello")]
	[InlineData("same", "same", "")]
	[InlineData("", "x", "")]
	public void RemoveFirst_RemovesOnlyTheFirstOccurrence(string input, string needle, string expected) =>
		AssertApply(input, new RemoveFirstFilter(needle), expected);

	[Theory]
	[InlineData("one two two", "two", "one two ")]
	[InlineData("aaaa", "aa", "aa")]
	[InlineData("hello", "x", "hello")]
	[InlineData("same", "same", "")]
	[InlineData("", "x", "")]
	public void RemoveLast_RemovesOnlyTheLastOccurrence(string input, string needle, string expected) =>
		AssertApply(input, new RemoveLastFilter(needle), expected);

	[Theory]
	[InlineData("one two two", "two", "X", "one X X")]
	[InlineData("aaaa", "aa", "b", "bb")]
	[InlineData("hello", "x", "!", "hello")]
	[InlineData("abc", "b", "", "ac")]
	[InlineData("", "x", "!", "")]
	public void Replace_ReplacesEveryOccurrence(string input, string needle, string replacement, string expected) =>
		AssertApply(input, new ReplaceFilter(needle, replacement), expected);

	[Theory]
	[InlineData("one two two", "two", "X", "one X two")]
	[InlineData("aaaa", "aa", "b", "baa")]
	[InlineData("hello", "x", "!", "hello")]
	[InlineData("abc", "b", "", "ac")]
	[InlineData("", "x", "!", "")]
	public void ReplaceFirst_ReplacesOnlyTheFirstOccurrence(string input, string needle, string replacement, string expected) =>
		AssertApply(input, new ReplaceFirstFilter(needle, replacement), expected);

	[Theory]
	[InlineData("one two two", "two", "X", "one two X")]
	[InlineData("aaaa", "aa", "b", "aab")]
	[InlineData("hello", "x", "!", "hello")]
	[InlineData("abc", "b", "", "ac")]
	[InlineData("", "x", "!", "")]
	public void ReplaceLast_ReplacesOnlyTheLastOccurrence(string input, string needle, string replacement, string expected) =>
		AssertApply(input, new ReplaceLastFilter(needle, replacement), expected);

	[Theory]
	[InlineData("abc", "1", "abc")]
	[InlineData("x", "3", "xxx")]
	[InlineData("abc", "0", "")]
	[InlineData("abc", "-2", "")]
	[InlineData("", "3", "")]
	public void Repeat_RepeatsInputTheRequestedNumberOfTimes(string input, string count, string expected) =>
		AssertApply(input, new RepeatFilter(count), expected);

	[Theory]
	[InlineData("abcdef", "2", "3", "cde")]
	[InlineData("abcdef", "3", "3", "def")]
	[InlineData("abcdef", "6", "2", "")]
    [InlineData("abcdef", "8", "1", "")]
	[InlineData("abcdef", "2", "0", "")]
	[InlineData("abcdef", "-2", "4", "ef")]
    [InlineData("abcdef", "-5", "4", "bcde")]
    [InlineData("abcdef", "-10", "4", "abcd")]
    [InlineData("世界🙂世界", "1", "3", "界🙂世")]
    [InlineData("世界🙂世界", "-4", "1", "界")]
	[InlineData("", "0", "0", "")]
	public void Slice_ReturnsTheRequestedByteRange(string input, string start, string length, string expected) =>
		AssertApply(input, new SliceFilter(start, length), expected);

	[Theory]
	[InlineData("  hello \t", "hello")]
	[InlineData("\n\t", "")]
	[InlineData(" hello", "hello")]
	[InlineData("hello ", "hello")]
	[InlineData("hello", "hello")]
	[InlineData("　hello ", "hello")]
	[InlineData("", "")]
	public void Trim_RemovesWhitespaceFromBothEnds(string input, string expected) =>
		AssertApply(input, new TrimFilter(), expected);

	[Theory]
	[InlineData("hello \t", "hello")]
	[InlineData(" \n", "")]
	[InlineData(" hello", " hello")]
	[InlineData("hello", "hello")]
	[InlineData("hello ", "hello")]
	[InlineData("", "")]
	public void TrimEnd_RemovesWhitespaceFromTheEndOnly(string input, string expected) =>
		AssertApply(input, new TrimEndFilter(), expected);

	[Theory]
	[InlineData(" \thello", "hello")]
	[InlineData("\n ", "")]
	[InlineData("hello ", "hello ")]
	[InlineData("hello", "hello")]
	[InlineData(" hello", "hello")]
	[InlineData("", "")]
	public void TrimStart_RemovesWhitespaceFromTheStartOnly(string input, string expected) =>
		AssertApply(input, new TrimStartFilter(), expected);

	[Theory]
	[InlineData("abcdef", "3", "abc")]
	[InlineData("abcdef", "6", "abcdef")]
	[InlineData("abcdef", "0", "")]
	[InlineData("abcdef", "10", "abcdef")]
    [InlineData("世界🙂世界", "3", "世界🙂")]
    [InlineData("世界🙂世界", "-3", "世界")]
    [InlineData("abcdef", "-10", "")]
	[InlineData("", "3", "")]
	public void Truncate_LimitsInputToTheMaximumLength(string input, string maxLength, string expected) =>
		AssertApply(input, new TruncateFilter(maxLength), expected);

	[Fact]
	public void BuiltInFilters_DoNotAllocateDuringApply()
	{
		ITemplateFilter[] filters =
		[
			new AppendFilter("!"),
			new CapitalizeFilter(),
			new LowerFilter(),
			new PrependFilter("!"),
			new RemoveFilter("a"),
			new RemoveFirstFilter("a"),
			new RemoveLastFilter("a"),
			new RepeatFilter("3"),
			new ReplaceFilter("a", "b"),
			new ReplaceFirstFilter("a", "b"),
			new ReplaceLastFilter("a", "b"),
			new SliceFilter("0", "8"),
			new TrimFilter(),
			new TrimEndFilter(),
			new TrimStartFilter(),
			new TruncateFilter("8"),
			new UpperFilter()
		];
		byte[] input = Encoding.UTF8.GetBytes(" alPha beta ");
		ArrayBufferWriter<byte>[] outputs = filters.Select(_ => new ArrayBufferWriter<byte>(256)).ToArray();
		const int iterations = 100;

		for (int filterIndex = 0; filterIndex < filters.Length; filterIndex++)
		{
			for (int iteration = 0; iteration < 10; iteration++)
			{
				outputs[filterIndex].Clear();
				filters[filterIndex].Apply(input, outputs[filterIndex]);
			}
		}

		for (int filterIndex = 0; filterIndex < filters.Length; filterIndex++)
		{
			string filterName = filters[filterIndex].GetType().Name;
			long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

			for (int iteration = 0; iteration < iterations; iteration++)
			{
				outputs[filterIndex].Clear();
				filters[filterIndex].Apply(input, outputs[filterIndex]);
			}

			long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
			Assert.True(allocated == 0, $"{filterName} allocated {allocated} bytes during Apply.");
		}
	}

	private static void AssertApply(string input, ITemplateFilter filter, string expected)
	{
		ArrayBufferWriter<byte> output = new();
		int bytesWritten = filter.Apply(Encoding.UTF8.GetBytes(input), output);
		int expectedByteCount = Encoding.UTF8.GetByteCount(expected);
		Assert.Equal(expectedByteCount, bytesWritten);
		Assert.Equal(bytesWritten, output.WrittenCount);
		Assert.Equal(expected, Encoding.UTF8.GetString(output.WrittenSpan));
	}
}
