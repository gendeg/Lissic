using System.Buffers;
using System.Text;

namespace Lissic.Tests;


public class TemplateBaseTests
{
	[Fact]
	public void StreamVariable_MissingValue_ReturnsFalseAndWritesNothing()
	{
		ArrayBufferWriter<byte> writer = new();

		int wrote = new TestTemplate().Stream_Variable("missing", writer);

		Assert.Equal(-1, wrote);
		Assert.Equal(0, writer.WrittenCount);
	}

	private sealed class TestTemplate : LissicTemplate
	{
		private readonly Dictionary<string, string> branchKeys = [];
		private readonly Dictionary<string, LissicTemplate> parts = [];
		private readonly Dictionary<string, string> properties = [];
		private readonly Dictionary<string, LoopValue> loops = [];

		public override int Template_Index => 0;

		public TestTemplate SetBranchKey(string name, string value)
		{
			branchKeys[name] = value;
			return this;
		}

		public TestTemplate SetPart(string name, LissicTemplate value)
		{
			parts[name] = value;
			return this;
		}

		public TestTemplate SetVariable(string name, string value)
		{
			properties[name] = value;
			return this;
		}

		public TestTemplate SetLoop(string name, params TestTemplate[] children)
		{
			loops[name] = new LoopValue([..children]);
			return this;
		}

		public override string Get_BranchKey(ReadOnlySpan<char> keyName) =>
			branchKeys.TryGetValue(keyName.ToString(), out string? value)
				? value
				: "";

		public override LissicTemplate? Get_Part(ReadOnlySpan<char> name) =>
			parts.TryGetValue(name.ToString(), out LissicTemplate? value)
				? value
				: null;

		public override string? Get_VariableString(ReadOnlySpan<char> name) =>
			properties.TryGetValue(name.ToString(), out string? value)
				? value
				: null;

		public override LoopValue? Get_Loop(ReadOnlySpan<char> name)
		{
			if (loops.TryGetValue(name.ToString(), out LoopValue value))
				return value;

			return null;
		}

		public override int Stream_Variable(ReadOnlySpan<char> name, IBufferWriter<byte> writer)
		{
			if (!properties.TryGetValue(name.ToString(), out string? value))
				return -1;

			writer.Write(Encoding.UTF8.GetBytes(value));
			return Encoding.UTF8.GetBytes(value).Length;
		}
	}
}