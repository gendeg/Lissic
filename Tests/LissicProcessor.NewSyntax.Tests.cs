using Lissic.Parsing;
using System.Buffers;
using System.Text;

namespace Lissic.Tests.LissicProcessorTests;

// Rendering tests for the 0.2.0 syntax changes.  Shares the rig, filters, and child-process option
// helpers with LissicProcessorTests (lp-upper, lp-append, lp-default are registered there).

public partial class LissicProcessorTests
{
	/* Branches: comma delimiter and quoted literals */

	[Theory]
	[InlineData("before{{branch.color[red => \"Red\", blue => \"Blue\"]}}after")]
	[InlineData("before{{branch.color[red => 'Red', blue => 'Blue',]}}after")]
	[InlineData("before{{ branch.color [\n\tred => \"Red\",\n\tblue => 'Blue'\n] }}after")]
	[InlineData("before{{ (branch.color [red => \"Red\", blue => \"Blue\"]) }}after")]
	public async Task V020_RenderToString_RendersSelectedBranch(string text)
	{
		using TestRig rig = await CreateRig(text);
		TestTemplate template = new TestTemplate(0).SetBranchKey("color", "blue");

		Assert.Equal("beforeBlueafter", rig.Processor.RenderToString(template));
	}

	[Theory]
	[InlineData("{{branch.q[a => \"say \"\"hi\"\"\"]}}", "say \"hi\"")]
	[InlineData("{{branch.q[a => 'it''s']}}", "it's")]
	[InlineData("{{branch.q[a => 'say \"hi\"']}}", "say \"hi\"")]
	[InlineData("{{branch.q[a => \" spaced \"]}}", " spaced ")]
	[InlineData("{{branch.q[a => \"a, b; c ] d => e }} f\"]}}", "a, b; c ] d => e }} f")]
	[InlineData("{{branch.q[a => \"\"]}}", "")]
	public async Task V020_RenderToString_QuotedLiteralText(string text, string expected)
	{
		using TestRig rig = await CreateRig(text);

		Assert.Equal(expected, rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("q", "a")));
	}

	[Fact]
	public async Task V020_RenderToString_LiteralTagsAreRendered()
	{
		using TestRig rig = await CreateRig("{{branch.q[a => \"Hi {{ name }}, {{ part.p }}!\"]}}", "[{{ title }}]");
		TestTemplate template = new TestTemplate(0)
			.SetBranchKey("q", "a")
			.SetVariable("name", "Ada")
			.SetPart("p", new TestTemplate(1).SetVariable("title", "T"));

		Assert.Equal("Hi Ada, [T]!", rig.Processor.RenderToString(template));
	}

	[Theory]
	[InlineData("{{branch.color[red => \"<div>{{ branch.shade [dark => \"\"Dark\"\", light => name] }}</div>\"]}}")]
	[InlineData("{{branch.color[red => '<div>{{ branch.shade [dark => ''Dark'', light => name] }}</div>']}}")]
	[InlineData("{{branch.color[red => '<div>{{ branch.shade [dark => \"Dark\", light => name] }}</div>']}}")]
	[InlineData("{{branch.color[red => \"<div>{{ branch.shade[dark=>@Dark;light=>name;] }}</div>\"]}}")]
	public async Task V020_RenderToString_NestedBranches(string text)
	{
		using TestRig rig = await CreateRig(text);
		TestTemplate dark = new TestTemplate(0).SetBranchKey("color", "red").SetBranchKey("shade", "dark");
		TestTemplate light = new TestTemplate(0).SetBranchKey("color", "red").SetBranchKey("shade", "light").SetVariable("name", "Ada");

		Assert.Equal("<div>Dark</div>", rig.Processor.RenderToString(dark));
		Assert.Equal("<div>Ada</div>", rig.Processor.RenderToString(light));
	}

	[Fact]
	public async Task V020_RenderToString_CommandActions()
	{
		using TestRig rig = await CreateRig("{{branch.o[a => part.p, b => loop.l, c => name]}}", "[{{ v }}]");
		TestTemplate part = new TestTemplate(0).SetBranchKey("o", "a").SetPart("p", new TestTemplate(1).SetVariable("v", "P"));
		TestTemplate loop = new TestTemplate(0).SetBranchKey("o", "b")
			.SetLoop("l", new TestTemplate(1).SetVariable("v", "1"), new TestTemplate(1).SetVariable("v", "2"));
		TestTemplate variable = new TestTemplate(0).SetBranchKey("o", "c").SetVariable("name", "Ada");

		Assert.Equal("[P]", rig.Processor.RenderToString(part));
		Assert.Equal("[1][2]", rig.Processor.RenderToString(loop));
		Assert.Equal("Ada", rig.Processor.RenderToString(variable));
	}


	/* Branches: default option */

	[Theory]
	[InlineData("red", "R")]
	[InlineData("blue", "B")]
	[InlineData("green", "D")]
	[InlineData("Red", "D")]
	[InlineData("_", "D")]
	public async Task V020_RenderToString_DefaultOptionUsedForUnmatchedKeys(string key, string expected)
	{
		using TestRig rig = await CreateRig("[{{branch.color[red => \"R\", blue => \"B\", _ => \"D\"]}}]");

		Assert.Equal($"[{expected}]", rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("color", key)));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public async Task V020_RenderToString_DefaultOptionUsedForEmptyOrUnsetKeys(string? key)
	{
		using TestRig rig = await CreateRig("[{{branch.color[red => \"R\", _ => \"D\"]}}]");
		TestTemplate template = new(0);
		if (key is not null)
			template.SetBranchKey("color", key);

		Assert.Equal("[D]", rig.Processor.RenderToString(template));
	}

	[Theory]
	[InlineData("red", "R")]
	[InlineData("other", "D")]
	public async Task V020_RenderToString_DefaultOptionPositionDoesNotMatter(string key, string expected)
	{
		using TestRig rig = await CreateRig("{{branch.color[_ => \"D\", red => \"R\"]}}");

		Assert.Equal(expected, rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("color", key)));
	}

	[Fact]
	public async Task V020_RenderToString_DefaultOnlyBranch()
	{
		using TestRig rig = await CreateRig("{{branch.color[_ => \"D\"]}}");

		Assert.Equal("D", rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("color", "anything")));
		Assert.Equal("D", rig.Processor.RenderToString(new TestTemplate(0)));
	}

	[Fact]
	public async Task V020_RenderToString_DefaultOptionCanBeACommand()
	{
		using TestRig rig = await CreateRig("{{branch.color[red => \"R\", _ => (name | lp-upper)]}}|{{branch.shape[round => \"O\", _ => part.p]}}", "[part]");
		TestTemplate template = new TestTemplate(0)
			.SetBranchKey("color", "green")
			.SetBranchKey("shape", "square")
			.SetVariable("name", "ada")
			.SetPart("p", new TestTemplate(1));

		Assert.Equal("ADA|[part]", rig.Processor.RenderToString(template));
	}

	[Theory]
	[InlineData("gold", "<b>Gold!</b>")]
	[InlineData("member", "Welcome back")]
	[InlineData("none", "We look forward to seeing you!  Join the \"Local Lions\" at example.com")]
	[InlineData("", "We look forward to seeing you!  Join the \"Local Lions\" at example.com")]
	public async Task V020_RenderToString_ProposalExample(string key, string expected)
	{
		string[] variants =
		[
			"{{ branch.memberStatus [ gold => part.goldMessage, member => memberMessage, _ => 'We look forward to seeing you!  Join the \"Local Lions\" at {{ signUpLink }}' ] }}",
			"{{ branch.memberStatus [ gold => part.goldMessage, member => memberMessage, _ => \"We look forward to seeing you!  Join the \"\"Local Lions\"\" at {{ signUpLink }}\" ] }}",
			"""
			{{ branch.memberStatus [
			    gold => part.goldMessage,
			    member => memberMessage,
			    _ => "We look forward to seeing you!  Join the ""Local Lions"" at {{ signUpLink }}"
			] }}
			""",
		];

		foreach (string text in variants)
		{
			using TestRig rig = await CreateRig(text, "<b>Gold!</b>");
			TestTemplate template = new TestTemplate(0)
				.SetBranchKey("memberStatus", key)
				.SetPart("goldMessage", new TestTemplate(1))
				.SetVariable("memberMessage", "Welcome back")
				.SetVariable("signUpLink", "example.com");

			Assert.Equal(expected, rig.Processor.RenderToString(template));
		}
	}

	[Theory]
	[InlineData("\"_\"", "Q")]
	[InlineData("_", "")]
	[InlineData("other", "")]
	public async Task V020_RenderToString_QuotedUnderscoreIsALiteralKeyNotADefault(string key, string expected)
	{
		using TestRig rig = await CreateRig("{{branch.color[\"_\" => \"Q\"]}}");

		Assert.Equal(expected, rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("color", key)));
	}

	[Theory]
	[InlineData("_", "U")]
	[InlineData("other", "")]
	[InlineData("", "")]
	public async Task V020_RenderToString_DeprecatedSyntaxUnderscoreIsNotADefault(string key, string expected)
	{
		using TestRig rig = await CreateRig("{{branch.color[_=>@U;red=>@R;]}}");

		Assert.Equal(expected, rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("color", key)));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("green")]
	public async Task V020_RenderToString_NoDefault_MissingOrUnknownKeyOmitsBranch(string? key)
	{
		using TestRig rig = await CreateRig("before{{branch.color[red => \"Red\", blue => \"Blue\"]}}after");
		TestTemplate template = new(0);
		if (key is not null)
			template.SetBranchKey("color", key);

		Assert.Equal("beforeafter", rig.Processor.RenderToString(template));
	}

	[Fact]
	public Task V020_RenderToBuffer_InsertErrorText_NoDefault_ReportsMissingKeys() =>
		RunWithOptions(nameof(V020_RenderToBuffer_InsertErrorText_NoDefault_ReportsMissingKeys), new LissicOptions
		{
			UnknownTagDebugMode = UnknownTagDebugMode.InsertErrorText,
			UnknownBranchKeyDebugMode = UnknownBranchKeyDebugMode.InsertErrorText
		}, async () =>
		{
			using TestRig rig = await CreateRig("{{branch.color[red => \"Red\", blue => \"Blue\"]}}");

			Assert.Equal("<!-- ERROR: Branch key for 'color' is empty.  No branch key was provided or the value is empty. -->",
				RenderToText(rig.Processor, new TestTemplate(0)));
			Assert.Equal("<!-- ERROR: No option found for branch key 'green' in branch 'color' -->",
				RenderToText(rig.Processor, new TestTemplate(0).SetBranchKey("color", "green")));
		});

	[Fact]
	public Task V020_RenderToBuffer_InsertErrorText_DefaultSuppressesMissingKeyErrors() =>
		RunWithOptions(nameof(V020_RenderToBuffer_InsertErrorText_DefaultSuppressesMissingKeyErrors), new LissicOptions
		{
			UnknownTagDebugMode = UnknownTagDebugMode.InsertErrorText,
			UnknownBranchKeyDebugMode = UnknownBranchKeyDebugMode.InsertErrorText
		}, async () =>
		{
			using TestRig rig = await CreateRig("{{branch.color[red => \"Red\", _ => \"Default\"]}}");

			Assert.Equal("Default", RenderToText(rig.Processor, new TestTemplate(0)));
			Assert.Equal("Default", RenderToText(rig.Processor, new TestTemplate(0).SetBranchKey("color", "")));
			Assert.Equal("Default", RenderToText(rig.Processor, new TestTemplate(0).SetBranchKey("color", "green")));
			Assert.Equal("Red", RenderToText(rig.Processor, new TestTemplate(0).SetBranchKey("color", "red")));
		});

	[Fact]
	public Task V020_RenderToBuffer_ThrowException_DefaultSuppressesMissingKeyErrors() =>
		RunWithOptions(nameof(V020_RenderToBuffer_ThrowException_DefaultSuppressesMissingKeyErrors), new LissicOptions
		{
			UnknownTagDebugMode = UnknownTagDebugMode.ThrowException,
			UnknownBranchKeyDebugMode = UnknownBranchKeyDebugMode.ThrowException
		}, async () =>
		{
			using TestRig rig = await CreateRig("{{branch.color[red => \"Red\", _ => \"Default\"]}}");

			Assert.Equal("Default", RenderToText(rig.Processor, new TestTemplate(0)));
			Assert.Equal("Default", RenderToText(rig.Processor, new TestTemplate(0).SetBranchKey("color", "green")));
		});

	[Fact]
	public Task V020_RenderToBuffer_ThrowException_NoDefault_StillThrows() =>
		RunWithOptions(nameof(V020_RenderToBuffer_ThrowException_NoDefault_StillThrows), new LissicOptions
		{
			UnknownTagDebugMode = UnknownTagDebugMode.ThrowException,
			UnknownBranchKeyDebugMode = UnknownBranchKeyDebugMode.ThrowException
		}, async () =>
		{
			using TestRig rig = await CreateRig("{{branch.color[red => \"Red\", blue => \"Blue\"]}}");

			Assert.Contains("Branch key for 'color' is empty", Assert.Throws<TemplateRenderException>(
				() => RenderToText(rig.Processor, new TestTemplate(0))).Message);
			Assert.Contains("No option found for branch key 'green'", Assert.Throws<TemplateRenderException>(
				() => RenderToText(rig.Processor, new TestTemplate(0).SetBranchKey("color", "green"))).Message);
		});

	[Fact]
	public Task V020_RenderToBuffer_InsertErrorText_DeprecatedUnderscoreKeyIsNotADefault() =>
		RunWithOptions(nameof(V020_RenderToBuffer_InsertErrorText_DeprecatedUnderscoreKeyIsNotADefault), new LissicOptions
		{
			UnknownBranchKeyDebugMode = UnknownBranchKeyDebugMode.InsertErrorText
		}, async () =>
		{
			using TestRig rig = await CreateRig("{{branch.color[_=>@U;]}}");

			Assert.Equal("<!-- ERROR: No option found for branch key 'green' in branch 'color' -->",
				RenderToText(rig.Processor, new TestTemplate(0).SetBranchKey("color", "green")));
		});


	/* Branches: other branches as direct actions */

	[Theory]
	[InlineData("a", "x", "X")]
	[InlineData("a", "y", "Ada")]
	[InlineData("a", "other", "ID")]
	[InlineData("a", null, "ID")]
	[InlineData("b", "x", "B")]
	[InlineData("other", "x", "OD")]
	[InlineData(null, "x", "OD")]
	public async Task V020_RenderToString_BranchAsDirectAction(string? outerKey, string? innerKey, string expected)
	{
		string[] variants =
		[
			"{{ branch.outer [a => branch.inner [x => \"X\", y => name, _ => \"ID\"], b => \"B\", _ => \"OD\"] }}",
			"{{ branch.outer [a => (branch.inner [x => \"X\", y => name, _ => \"ID\"]), b => \"B\", _ => \"OD\"] }}",
			"{{ branch.outer [_ => \"OD\", b => \"B\", a => switch.inner [_ => \"ID\", y => name, x => \"X\",],] }}",
		];

		foreach (string text in variants)
		{
			using TestRig rig = await CreateRig(text);
			TestTemplate template = new TestTemplate(0).SetVariable("name", "Ada");
			if (outerKey is not null)
				template.SetBranchKey("outer", outerKey);
			if (innerKey is not null)
				template.SetBranchKey("inner", innerKey);

			Assert.Equal(expected, rig.Processor.RenderToString(template));
		}
	}

	[Fact]
	public async Task V020_RenderToString_BranchAsDirectAction_WithFiltersAndNoInnerDefault()
	{
		using TestRig rig = await CreateRig("[{{ branch.outer [a => branch.inner [x => name | lp-append(\", \") | lp-upper, y => (name | lp-append(\"!\"))], b => \"B\"] }}]");

		Assert.Equal("[ADA, ]", rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("outer", "a").SetBranchKey("inner", "x").SetVariable("name", "ada")));
		Assert.Equal("[ada!]", rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("outer", "a").SetBranchKey("inner", "y").SetVariable("name", "ada")));
		Assert.Equal("[]", rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("outer", "a").SetBranchKey("inner", "z").SetVariable("name", "ada")));
	}

	[Fact]
	public async Task V020_RenderToString_BranchAsDirectAction_DeeplyNested()
	{
		using TestRig rig = await CreateRig("{{ branch.a [x => branch.b [y => branch.c [z => \"Deep\", _ => \"C\"], _ => \"B\"], _ => \"A\"] }}");

		Assert.Equal("Deep", rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("a", "x").SetBranchKey("b", "y").SetBranchKey("c", "z")));
		Assert.Equal("C", rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("a", "x").SetBranchKey("b", "y")));
		Assert.Equal("B", rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("a", "x")));
		Assert.Equal("A", rig.Processor.RenderToString(new TestTemplate(0)));
	}

	[Fact]
	public Task V020_RenderToBuffer_InsertErrorText_BranchAsDirectActionReportsInnerKey() =>
		RunWithOptions(nameof(V020_RenderToBuffer_InsertErrorText_BranchAsDirectActionReportsInnerKey), new LissicOptions
		{
			UnknownBranchKeyDebugMode = UnknownBranchKeyDebugMode.InsertErrorText
		}, async () =>
		{
			using TestRig rig = await CreateRig("{{ branch.outer [a => branch.inner [x => \"X\"]] }}");

			Assert.Equal("<!-- ERROR: No option found for branch key 'green' in branch 'inner' -->",
				RenderToText(rig.Processor, new TestTemplate(0).SetBranchKey("outer", "a").SetBranchKey("inner", "green")));
		});


	/* Branches: filtered actions */

	[Theory]
	[InlineData("{{ branch.status [member => name | lp-upper | lp-append(\", member\"), _ => \"not a member\" ] }}")]
	[InlineData("{{ branch.status [member => (name | lp-upper | lp-append(\", member\")), _ => \"not a member\" ] }}")]
	[InlineData("{{ branch.status [member => name|lp-upper|lp-append(', member'),_=>'not a member'] }}")]
	public async Task V020_RenderToString_FilteredBranchActions(string text)
	{
		using TestRig rig = await CreateRig(text);

		Assert.Equal("ADA, member", rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("status", "member").SetVariable("name", "ada")));
		Assert.Equal("not a member", rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("status", "guest").SetVariable("name", "ada")));
	}

	[Fact]
	public async Task V020_RenderToString_FiltersApplyInsideDefaultLiteral()
	{
		using TestRig rig = await CreateRig("{{ branch.s [a => \"A\", _ => '{{ name | lp-upper }}'] }}");

		Assert.Equal("ADA", rig.Processor.RenderToString(new TestTemplate(0).SetVariable("name", "ada")));
	}


	/* Filters: pipe syntax */

	[Theory]
	[InlineData("[{{ name | lp-upper }}]")]
	[InlineData("[{{name|lp-upper}}]")]
	[InlineData("[{{ name | LP-UPpER }}]")]
	[InlineData("[{{ (name | lp-upper) }}]")]
	[InlineData("[{{ name\n  | lp-upper\n}}]")]
	public async Task V020_RenderToString_AppliesPipeFilter(string text)
	{
		using TestRig rig = await CreateRig(text);

		Assert.Equal("[ADA]", rig.Processor.RenderToString(new TestTemplate(0).SetVariable("name", "ada")));
	}

	[Theory]
	[InlineData("{{ name | lp-append(\"x\") | lp-upper }}", "ADAX")]
	[InlineData("{{ name | lp-upper | lp-append(\"x\") }}", "ADAx")]
	[InlineData("{{ name | lp-append(1) | lp-append(2) | lp-append(3) }}", "ada123")]
	[InlineData("{{ name | lp-append(-1.5) }}", "ada-1.5")]
	[InlineData("{{ name | lp-append (\"!\") }}", "ada!")]
	[InlineData("{{ name | lp-append( \"!\" ) }}", "ada!")]
	[InlineData("{{ name | lp-append(\" the great\") }}", "ada the great")]
	[InlineData("{{ name | lp-append(\"\"\"\") }}", "ada\"")]
	[InlineData("{{ name | lp-append('''') }}", "ada'")]
	[InlineData("{{ name | lp-append(\"say \"\"hi\"\"\") }}", "adasay \"hi\"")]
	[InlineData("{{ name | lp-append(\"a, b | c) d\") }}", "adaa, b | c) d")]
	public async Task V020_RenderToString_PipeFilterChainsAndArgs(string text, string expected)
	{
		using TestRig rig = await CreateRig(text);

		Assert.Equal(expected, rig.Processor.RenderToString(new TestTemplate(0).SetVariable("name", "ada")));
	}

	[Fact]
	public async Task V020_RenderToString_SameVariableWithMixedFilterStylesAcrossTags()
	{
		using TestRig rig = await CreateRig("{{ name }} {{ name <lp-upper> }} {{ name | lp-upper }} {{ (name) }}");

		Assert.Equal("ada ADA ADA ada", rig.Processor.RenderToString(new TestTemplate(0).SetVariable("name", "ada")));
	}

	[Fact]
	public async Task V020_RenderToString_UnsetPipeFilteredVariable_SkipsFiltersSilently()
	{
		using TestRig rig = await CreateRig("[{{ name | lp-append(\"!\") }}][{{ (other? | lp-append(\"!\")) }}]");

		Assert.Equal("[][]", rig.Processor.RenderToString(new TestTemplate(0)));
	}

	[Fact]
	public async Task V020_RenderToString_PipeFiltersApplyInPartsAndLoops()
	{
		using TestRig rig = await CreateRig("{{ (part.header) }}|{{ (loop.items) }}", "[{{ title | lp-upper }}]");
		TestTemplate template = new TestTemplate(0)
			.SetPart("header", new TestTemplate(1).SetVariable("title", "head"))
			.SetLoop("items", new TestTemplate(1).SetVariable("title", "one"), new TestTemplate(1).SetVariable("title", "two"));

		Assert.Equal("[HEAD]|[ONE][TWO]", rig.Processor.RenderToString(template));
	}

	[Fact]
	public async Task V020_RenderToString_DeprecatedFilterSyntaxStillRenders()
	{
		using TestRig rig = await CreateRig("{{ name <lp-append(x) | lp-upper> }}|{{ name < lp-append (\"y\") > }}");

		Assert.Equal("ADAX|aday", rig.Processor.RenderToString(new TestTemplate(0).SetVariable("name", "ada")));
	}

	[Fact]
	public Task V020_RenderToBuffer_InsertErrorTextReportsUnknownPipeFilter() =>
		RunWithOptions(nameof(V020_RenderToBuffer_InsertErrorTextReportsUnknownPipeFilter), new LissicOptions
		{
			UnknownTagDebugMode = UnknownTagDebugMode.InsertErrorText
		}, async () =>
		{
			using TestRig rig = await CreateRig("{{ name | lp-nope }}");
			Assert.Equal("<!-- ERROR: Filter 'lp-nope' not found -->ada",
				RenderToText(rig.Processor, new TestTemplate(0).SetVariable("name", "ada")));
		});


	/* Parentheses */

	[Fact]
	public async Task V020_RenderToString_ParenthesizedTags()
	{
		using TestRig rig = await CreateRig("{{ (name) }}|{{ (part.p) }}|{{ (loop.l) }}|{{ (branch.b [x => \"X\"]) }}|{{ (missing?) }}", "[{{ v }}]");
		TestTemplate template = new TestTemplate(0)
			.SetVariable("name", "ada")
			.SetPart("p", new TestTemplate(1).SetVariable("v", "P"))
			.SetLoop("l", new TestTemplate(1).SetVariable("v", "1"), new TestTemplate(1).SetVariable("v", "2"))
			.SetBranchKey("b", "x");

		Assert.Equal("ada|[P]|[1][2]|X|", rig.Processor.RenderToString(template));
	}

	[Fact]
	public async Task V020_RenderToString_ParenthesesOutsideTagsAreText()
	{
		using TestRig rig = await CreateRig("({{ name }}) ({{ (name) }})");

		Assert.Equal("(ada) (ada)", rig.Processor.RenderToString(new TestTemplate(0).SetVariable("name", "ada")));
	}


	/* Parse errors surface when the cache is initialized */

	[Theory]
	[InlineData("{{ branch.c [a => x; b => y,] }}")]
	[InlineData("{{ branch.c [a=>@A; b => \"B\"] }}")]
	[InlineData("{{ name <lp-upper> | lp-upper }}")]
	[InlineData("{{ branch.c [a => \"A\", a => \"B\"] }}")]
	[InlineData("{{ branch.c [a=>@A;a=>@B;] }}")]
	[InlineData("{{ branch.c [_ => \"A\", _ => \"B\"] }}")]
	[InlineData("{{ name | lp-append(abc) }}")]
	[InlineData("{{ (name | lp-upper }}")]
	[InlineData("{{ branch.c [a => \"unclosed] }}")]
	[InlineData("{{ (name) | lp-upper }}")]
	[InlineData("{{ (name | lp-upper) | lp-upper }}")]
	[InlineData("{{ branch.c [a => (name) | lp-upper] }}")]
	[InlineData("{{ branch.c [a => branch.d [x => \"X\", b => \"B\"] }}")]
	[InlineData("{{ branch.c [a => branch.d, b => \"B\"] }}")]
	public async Task V020_Initialize_InvalidNewSyntaxThrows(string text)
	{
		await Assert.ThrowsAsync<TemplateParsingException>(async () => await CreateRig(text));
	}


	/* Allocation-free rendering is preserved with the new syntax */

	[Fact]
	public Task V020_RenderToBuffer_NewSyntaxAfterWarmUp_DoesNotAllocate() =>
		RunWithOptions(nameof(V020_RenderToBuffer_NewSyntaxAfterWarmUp_DoesNotAllocate), new LissicOptions
		{
			EnableCycleDetection = false
		}, async () =>
		{
			using TestRig rig = await CreateRig(
				"{{ name | lp-upper }}|{{ (name | lp-append(\"-\") | lp-upper) }}|{{ (part.p) }}|{{branch.color[red => name | lp-upper, _ => \"D\"]}}|{{branch.shape[round => \"O\", _ => \"D\"]}}",
				"[{{ name }}]");
			AllocationFreeTemplate template = new(0) { Part = new AllocationFreeTemplate(1) };
			ArrayBufferWriter<byte> writer = new(1024);

			for (int i = 0; i < 10; i++)
			{
				writer.ResetWrittenCount();
				rig.Processor.RenderToBuffer(template, writer);
			}
			Assert.Equal("ADA|ADA-|[ada]|ADA|D", Encoding.UTF8.GetString(writer.WrittenSpan));

			long before = GC.GetAllocatedBytesForCurrentThread();
			for (int i = 0; i < 1000; i++)
			{
				writer.ResetWrittenCount();
				rig.Processor.RenderToBuffer(template, writer);
			}
			long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

			Assert.Equal(0, allocated);
		});
}
