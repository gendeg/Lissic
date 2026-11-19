using Lissic.Filters.BuiltIn;
using Lissic.Parsing;
using Microsoft.Extensions.DependencyInjection;
using System.Buffers;
using System.Diagnostics;
using System.Text;

namespace Lissic.Tests.LissicProcessorTests;


public class LissicProcessorTests
{
	static LissicProcessorTests()
	{
		FilterRegistry.Register<UpperTestFilter>("lp_upper");
		FilterRegistry.Register<AppendTestFilter>("lp_append");
		FilterRegistry.Register<DefaultTestFilter>("lp_default");
		FilterRegistry.Register<FailingTestFilter>("lp_fail");
	}

	[Fact]
	public async Task RenderToBuffer_WritesHtmlInOrderAndPreservesUtf8()
	{
		using TestRig rig = await CreateRig("Hello, 世界!");
		TestTemplate template = new(0);
		ArrayBufferWriter<byte> writer = new();

		rig.Processor.RenderToBuffer(template, writer);

		Assert.Equal("Hello, 世界!", Encoding.UTF8.GetString(writer.WrittenSpan));
	}

	[Fact]
	public async Task RenderToString_RendersTemplate()
	{
		using TestRig rig = await CreateRig("Hello, {{name}}!");
		TestTemplate template = new TestTemplate(0).SetVariable("name", "Ada");

		Assert.Equal("Hello, Ada!", rig.Processor.RenderToString(template));
	}

	[Fact]
	public async Task RenderToString_AppliesDateFormatterAttribute()
	{
		DateFormatterTemplate template = new()
		{
			Date = new DateTimeOffset(2024, 3, 4, 5, 6, 7, TimeSpan.Zero)
		};
		string[] templates = Enumerable.Repeat(string.Empty, template.Template_Index + 1).ToArray();
		templates[template.Template_Index] = "{{ Date }}";
		using TestRig rig = await CreateRig(templates);

		Assert.Equal("03-04-24 A.D.", rig.Processor.RenderToString(template));
	}

	[Fact]
	public async Task RenderToString_EmptyTemplate_ReturnsEmptyString()
	{
		using TestRig rig = await CreateRig(string.Empty);

		Assert.Equal(string.Empty, rig.Processor.RenderToString(new TestTemplate(0)));
	}

	[Fact]
	public async Task RenderToBuffer_RendersVariableBetweenHtml()
	{
		using TestRig rig = await CreateRig("before{{value}}after");
		TestTemplate template = new TestTemplate(0).SetVariable("value", "<raw & text>");
		ArrayBufferWriter<byte> writer = new();

		rig.Processor.RenderToBuffer(template, writer);

		Assert.Equal("before<raw & text>after", Encoding.UTF8.GetString(writer.WrittenSpan));
	}

	[Fact]
	public async Task RenderToBuffer_MissingVariableIsSkippedWithDefaultConfiguration()
	{
		using TestRig rig = await CreateRig("before{{missing}}after");
		ArrayBufferWriter<byte> writer = new();

		rig.Processor.RenderToBuffer(new TestTemplate(0), writer);

		Assert.Equal("beforeafter", Encoding.UTF8.GetString(writer.WrittenSpan));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("green")]
	public async Task RenderToString_MissingOrUnknownBranchKeyOmitsBranch(string? key)
	{
		using TestRig rig = await CreateRig("before{{branch.color[red=>'Red',blue=>'Blue']}}after");
		TestTemplate template = new(0);
		if (key is not null)
			template.SetBranchKey("color", key);

		Assert.Equal("beforeafter", rig.Processor.RenderToString(template));
	}

	[Fact]
	public async Task RenderToString_RendersPartUsingPartTemplateValues()
	{
		using TestRig rig = await CreateRig("A{{part.header}}Z", "[{{title}}]");
		TestTemplate part = new TestTemplate(1).SetVariable("title", "Child");
		TestTemplate template = new TestTemplate(0).SetPart("header", part);

		Assert.Equal("A[Child]Z", rig.Processor.RenderToString(template));
	}

	[Fact]
	public async Task RenderToString_MissingPartIsOmitted()
	{
		using TestRig rig = await CreateRig("before{{part.missing}}after");

		Assert.Equal("beforeafter", rig.Processor.RenderToString(new TestTemplate(0)));
	}

	[Fact]
	public async Task RenderToString_RendersLoopChildrenInOrder()
	{
		using TestRig rig = await CreateRig("{{loop.items}}", "{{value}}");
		TestTemplate first = new TestTemplate(1).SetVariable("value", "one");
		TestTemplate second = new TestTemplate(1).SetVariable("value", "two");
		TestTemplate template = new TestTemplate(0).SetLoop("items", first, second);

		Assert.Equal("onetwo", rig.Processor.RenderToString(template));
	}

	[Fact]
	public async Task RenderToString_RepeatedUseOfSameChildIsNotACycle()
	{
		using TestRig rig = await CreateRig("{{loop.items}}", "item");
		TestTemplate child = new(1);
		TestTemplate template = new TestTemplate(0).SetLoop("items", child, child);

		Assert.Equal("itemitem", rig.Processor.RenderToString(template));
	}

	[Fact]
	public async Task RenderToString_MissingLoopIsOmitted()
	{
		using TestRig rig = await CreateRig("before{{loop.items}}after");

		Assert.Equal("beforeafter", rig.Processor.RenderToString(new TestTemplate(0)));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(4)]
	[InlineData(5)]
	public async Task RenderToString_AppliesFilterChainInOrder(int count)
	{
		string chain = string.Join(" | ", Enumerable.Range(1, count).Select(i => $"lp_append({i})"));
		using TestRig rig = await CreateRig($"{{{{ name | {chain} }}}}");

		string result = rig.Processor.RenderToString(new TestTemplate(0).SetVariable("name", "ada"));

		Assert.Equal("ada" + string.Concat(Enumerable.Range(1, count)), result);
	}

	[Fact]
	public async Task RenderToString_SameVariableWithAndWithoutFilters()
	{
		using TestRig rig = await CreateRig("{{ name }} {{ name | lp_upper }} {{ name }}");

		Assert.Equal("ada ADA ada", rig.Processor.RenderToString(new TestTemplate(0).SetVariable("name", "ada")));
	}

	[Fact]
	public async Task RenderToString_UnsetFilteredVariable_SkipsFiltersSilently()
	{
		using TestRig rig = await CreateRig("[{{ name | lp_append(\"!\") }}][{{ other? | lp_append(\"!\") }}]");

		Assert.Equal("[][]", rig.Processor.RenderToString(new TestTemplate(0)));
	}

	[Fact]
	public async Task RenderToString_ZeroLengthTextValue_StillRunsFilters()
	{
		using TestRig rig = await CreateRig("{{ name | lp_default(\"n/a\") }}");

		Assert.Equal("n/a", rig.Processor.RenderToString(new TestTemplate(0).SetVariable("name", "")));
	}

	[Fact]
	public async Task RenderToString_UnknownFilterPassesValueThrough()
	{
		using TestRig rig = await CreateRig("{{ name | lp_upper | lp_not_registered }}");

		Assert.Equal("ADA", rig.Processor.RenderToString(new TestTemplate(0).SetVariable("name", "ada")));
	}

	[Fact]
	public async Task RenderToString_FiltersApplyInPartsAndLoops()
	{
		using TestRig rig = await CreateRig("{{part.header}}|{{loop.items}}", "[{{ title | lp_upper }}]");
		TestTemplate template = new TestTemplate(0)
			.SetPart("header", new TestTemplate(1).SetVariable("title", "head"))
			.SetLoop("items", new TestTemplate(1).SetVariable("title", "one"), new TestTemplate(1).SetVariable("title", "two"));

		Assert.Equal("[HEAD]|[ONE][TWO]", rig.Processor.RenderToString(template));
	}

	[Fact]
	public async Task Initialize_FilterFactoryThatThrows_FailsWithFilterAndTemplateNames()
	{
		TemplateParsingException exception = await Assert.ThrowsAsync<TemplateParsingException>(
			() => CreateRig("{{ name | lp_fail(\"x\") }}"));

		Assert.Contains("lp_fail", exception.Message);
		Assert.Contains("template-0", exception.Message);
		Assert.Contains("bad filter argument", exception.Message);
	}

	[Fact]
	public async Task RenderToString_LargeFiltersAreRendered()
	{
		using TestRig rig = await CreateRig("{{ name | lp_append(1) | lp_append(2) | lp_append(3) }}");
		string value = new('x', 300_000);
		TestTemplate template = new TestTemplate(0).SetVariable("name", value);

		// Twice: the first render grows the pooled buffers past the retention cap, the second runs on fresh ones.
		for (int i = 0; i < 2; i++)
			Assert.Equal(value + "123", rig.Processor.RenderToString(template, 300_100));
	}

	[Fact]
	public Task RenderToBuffer_AfterWarmUp_DoesNotAllocate() =>
		RunWithOptions(nameof(RenderToBuffer_AfterWarmUp_DoesNotAllocate), new LissicOptions
		{
			EnableCycleDetection = false
		}, async () =>
		{
			using TestRig rig = await CreateRig(
				"{{ name }}|{{ name | lp_upper }}|{{ name | lp_append(\"-\") | lp_upper | lp_append(\"!\") }}|{{part.p}}|{{loop.items}}|{{branch.color[red=>name | lp_upper]}}|{{branch.shape[round => \"O\", _ => \"D\"]}}",
				"[{{ name }}]");
			AllocationFreeTemplate template = new(0)
			{
				Part = new AllocationFreeTemplate(1),
				Items = new LoopValue([new AllocationFreeTemplate(1), new AllocationFreeTemplate(1)])
			};
			ArrayBufferWriter<byte> writer = new(1024);

			for (int i = 0; i < 10; i++)
			{
				writer.ResetWrittenCount();
				rig.Processor.RenderToBuffer(template, writer);
			}
			Assert.Equal("ada|ADA|ADA-!|[ada]|[ada][ada]|ADA|D", Encoding.UTF8.GetString(writer.WrittenSpan));

			long before = GC.GetAllocatedBytesForCurrentThread();
			for (int i = 0; i < 1000; i++)
			{
				writer.ResetWrittenCount();
				rig.Processor.RenderToBuffer(template, writer);
			}
			long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

			Assert.Equal(0, allocated);
		});

	[Fact]
	public Task RenderToString_DirectPartCycleThrowsTemplateRenderException() =>
		RunWithOptions(nameof(RenderToString_DirectPartCycleThrowsTemplateRenderException), new LissicOptions
		{
			EnableCycleDetection = true
		}, async () =>
		{
			using TestRig rig = await CreateRig("{{part.self}}");
			TestTemplate template = new(0);
			template.SetPart("self", template);

			TemplateRenderException exception = Assert.Throws<TemplateRenderException>(
				() => rig.Processor.RenderToString(template));

			Assert.Contains("Cycle detected", exception.Message);
		});

	[Fact]
	public Task RenderToString_IndirectPartCycleThrowsTemplateRenderException() =>
		RunWithOptions(nameof(RenderToString_IndirectPartCycleThrowsTemplateRenderException), new LissicOptions
		{
            EnableCycleDetection = true
		}, async () =>
		{
            using TestRig rig = await CreateRig("{{part.next}}", "{{part.next}}");
			TestTemplate first = new(0);
			TestTemplate second = new(1);
			first.SetPart("next", second);
			second.SetPart("next", first);

			TemplateRenderException exception = Assert.Throws<TemplateRenderException>(
                () => rig.Processor.RenderToString(first));

			Assert.Contains("Cycle detected", exception.Message);
		});

	[Fact]
	public async Task RenderToString_AtMaximumRecursionDepthSucceeds()
	{
		using TestRig rig = await CreateRig("{{part.next}}");
		TestTemplate root = CreatePartChain(64);

		rig.Processor.RenderToString(root);
	}

	[Fact]
	public async Task RenderToString_AboveMaximumRecursionDepthThrows()
	{
		using TestRig rig = await CreateRig("{{part.next}}");
		TestTemplate root = CreatePartChain(65);

		TemplateRenderException exception = Assert.Throws<TemplateRenderException>(
			() => rig.Processor.RenderToString(root));

		Assert.Contains("Maximum template recursion depth", exception.Message);
	}

	[Fact]
	public async Task RenderToString_ConcurrentCallsUseIndependentCycleState()
	{
		using TestRig rig = await CreateRig("{{value}}");
		TestTemplate template = new TestTemplate(0).SetVariable("value", "parallel");

		string[] results = await Task.WhenAll(Enumerable.Range(0, 32)
			.Select(_ => Task.Run(() => rig.Processor.RenderToString(template))));

		Assert.All(results, result => Assert.Equal("parallel", result));
	}

	[Fact]
	public Task RenderToBuffer_DisabledCycleDetectionStillHonorsRecursionLimit() =>
		RunWithOptions(nameof(RenderToBuffer_DisabledCycleDetectionStillHonorsRecursionLimit), new LissicOptions
		{
			EnableCycleDetection = false,
			RecursionDepthLimit = 3
		}, async () =>
		{
			using TestRig rig = await CreateRig("{{part.self}}");
			TestTemplate template = new(0);
			template.SetPart("self", template);

			TemplateRenderException exception = Assert.Throws<TemplateRenderException>(
				() => RenderToText(rig.Processor, template));

			Assert.Contains("Maximum template recursion depth of 3", exception.Message);
			Assert.DoesNotContain("Cycle detected", exception.Message);
		});

	[Fact]
	public Task RenderToBuffer_UsesConfiguredRecursionDepth() =>
		RunWithOptions(nameof(RenderToBuffer_UsesConfiguredRecursionDepth), new LissicOptions
		{
			RecursionDepthLimit = 2
		}, async () =>
		{
			using TestRig rig = await CreateRig("{{part.next}}");

			RenderToText(rig.Processor, CreatePartChain(2));

			TemplateRenderException exception = Assert.Throws<TemplateRenderException>(
				() => RenderToText(rig.Processor, CreatePartChain(3)));

			Assert.Contains("Maximum template recursion depth of 2", exception.Message);
		});

	[Fact]
	public Task RenderToBuffer_StopsWhenOutputSizeThresholdIsExceeded() =>
		RunWithOptions(nameof(RenderToBuffer_StopsWhenOutputSizeThresholdIsExceeded), new LissicOptions
		{
			OutputSizeThreshold = 3
		}, async () =>
		{
				Assert.Equal(3UL, LissicConfig.OutputSizeThreshold);
			using TestRig rig = await CreateRig("ab{{value}}tail");
			TestTemplate template = new TestTemplate(0).SetVariable("value", "cde");
			ArrayBufferWriter<byte> writer = new();

			TemplateRenderException exception = Assert.Throws<TemplateRenderException>(
				() => rig.Processor.RenderToBuffer(template, writer));

			Assert.Contains("Output size exceeded the threshold of 3 bytes", exception.Message);
			Assert.Equal("abcde", Encoding.UTF8.GetString(writer.WrittenSpan));
		});

	[Fact]
	public Task RenderToBuffer_AllowsOutputAtTheSizeThreshold() =>
		RunWithOptions(nameof(RenderToBuffer_AllowsOutputAtTheSizeThreshold), new LissicOptions
		{
			OutputSizeThreshold = 5
		}, async () =>
		{
			using TestRig rig = await CreateRig("abcde");
			ArrayBufferWriter<byte> writer = new();

			rig.Processor.RenderToBuffer(new TestTemplate(0), writer);

			Assert.Equal("abcde", Encoding.UTF8.GetString(writer.WrittenSpan));
		});

	[Fact]
	public Task RepeatFilter_EnforcesOutputSizeThreshold() =>
		RunWithOptions(nameof(RepeatFilter_EnforcesOutputSizeThreshold), new LissicOptions
		{
			OutputSizeThreshold = 3
		}, () =>
		{
			ArrayBufferWriter<byte> output = new();
			RepeatFilter atThreshold = new("3");

			Assert.Equal(3, atThreshold.Apply("x"u8, output));
			Assert.Equal("xxx", Encoding.UTF8.GetString(output.WrittenSpan));

			output.Clear();
			RepeatFilter aboveThreshold = new("4");
			TemplateRenderException exception = Assert.Throws<TemplateRenderException>(
				() => aboveThreshold.Apply("x"u8, output));

			Assert.Contains("Repeat filter will exceed the threshold of 3 bytes", exception.Message);
			Assert.Equal(0, output.WrittenCount);
			return Task.CompletedTask;
		});

	[Fact]
	public Task RenderToBuffer_CountsLiteralVariableAndFilterBytesAtThreshold() =>
		RunWithOptions(nameof(RenderToBuffer_CountsLiteralVariableAndFilterBytesAtThreshold), new LissicOptions
		{
			OutputSizeThreshold = 12
		}, async () =>
		{
			using TestRig rig = await CreateRig("A{{plain}}/{{ value | lp_append(\"!\") }}Z");
			TestTemplate template = new TestTemplate(0).SetVariable("plain", "世界").SetVariable("value", "é");
			ArrayBufferWriter<byte> writer = new();
			const string expected = "A世界/é!Z";

			rig.Processor.RenderToBuffer(template, writer);

			Assert.Equal(expected, Encoding.UTF8.GetString(writer.WrittenSpan));
			Assert.Equal(12, writer.WrittenCount);
		});

	[Fact]
	public Task RenderToBuffer_ThrowsWhenTrackedMixedOutputExceedsThresholdByOneByte() =>
		RunWithOptions(nameof(RenderToBuffer_ThrowsWhenTrackedMixedOutputExceedsThresholdByOneByte), new LissicOptions
		{
			OutputSizeThreshold = 11
		}, async () =>
		{
			using TestRig rig = await CreateRig("A{{plain}}/{{ value | lp_append(\"!\") }}Z");
			TestTemplate template = new TestTemplate(0).SetVariable("plain", "世界").SetVariable("value", "é");
			ArrayBufferWriter<byte> writer = new();
			const string expected = "A世界/é!Z";

			TemplateRenderException exception = Assert.Throws<TemplateRenderException>(
				() => rig.Processor.RenderToBuffer(template, writer));

			Assert.Contains("Output size exceeded the threshold of 11 bytes", exception.Message);
			Assert.Equal(expected, Encoding.UTF8.GetString(writer.WrittenSpan));
			Assert.Equal(12, writer.WrittenCount);
		});

	[Fact]
	public Task AddLissicAsync_RegistersInitializedCacheAndProcessor() =>
		RunInChildProcess(nameof(AddLissicAsync_RegistersInitializedCacheAndProcessor), async () =>
		{
			ServiceCollection services = new();
			await services.AddLissicAsync();

			using ServiceProvider provider = services.BuildServiceProvider();
			using IServiceScope scope = provider.CreateScope();
			Lissic.Parsing.TemplateCache cache = provider.GetRequiredService<Lissic.Parsing.TemplateCache>();
			LissicProcessor processor = scope.ServiceProvider.GetRequiredService<LissicProcessor>();

			Assert.Same(cache, provider.GetRequiredService<Lissic.Parsing.TemplateCache>());
			cache.ReloadPathTemplates(new TemplateManifest());
			Assert.NotNull(processor);
		});

	[Fact]
	public Task RenderToBuffer_InsertErrorTextReportsMissingNodeKinds() =>
		RunWithOptions(nameof(RenderToBuffer_InsertErrorTextReportsMissingNodeKinds), new LissicOptions
		{
			UnknownTagDebugMode = UnknownTagDebugMode.InsertErrorText,
			UnknownBranchKeyDebugMode = UnknownBranchKeyDebugMode.InsertErrorText
		}, async () =>
		{
			using TestRig variableRig = await CreateRig("{{missing}}");
			Assert.Equal("<!-- ERROR: Missing value for variable 'missing' -->",
				RenderToText(variableRig.Processor, new TestTemplate(0)));

			using TestRig emptyBranchRig = await CreateRig("{{branch.color[red=>'Red',blue=>'Blue']}}");
			Assert.Equal("<!-- ERROR: Branch key for 'color' is empty.  No branch key was provided or the value is empty. -->",
				RenderToText(emptyBranchRig.Processor, new TestTemplate(0)));

			using TestRig unknownBranchRig = await CreateRig("{{branch.color[red=>'Red',blue=>'Blue']}}");
			TestTemplate unknownBranch = new TestTemplate(0).SetBranchKey("color", "green");
			Assert.Equal("<!-- ERROR: No option found for branch key 'green' in branch 'color' -->",
				RenderToText(unknownBranchRig.Processor, unknownBranch));

			using TestRig partRig = await CreateRig("{{part.missing}}");
			Assert.Equal("<!-- ERROR: Part 'missing' not found -->",
				RenderToText(partRig.Processor, new TestTemplate(0)));

			using TestRig loopRig = await CreateRig("{{loop.items}}");
			Assert.Equal("<!-- ERROR: Loop 'items' not found -->",
				RenderToText(loopRig.Processor, new TestTemplate(0)));
		});

	[Fact]
	public Task RenderToBuffer_ThrowExceptionReportsMissingNodeKinds() =>
		RunWithOptions(nameof(RenderToBuffer_ThrowExceptionReportsMissingNodeKinds), new LissicOptions
		{
			UnknownTagDebugMode = UnknownTagDebugMode.ThrowException,
			UnknownBranchKeyDebugMode = UnknownBranchKeyDebugMode.ThrowException
		}, async () =>
		{
			using TestRig variableRig = await CreateRig("{{missing}}");
			TemplateRenderException variableException = Assert.Throws<TemplateRenderException>(
				() => RenderToText(variableRig.Processor, new TestTemplate(0)));
			Assert.Contains("Missing value for variable 'missing'", variableException.Message);

			using TestRig emptyBranchRig = await CreateRig("{{branch.color[red=>'Red',blue=>'Blue']}}");
			TemplateRenderException emptyBranchException = Assert.Throws<TemplateRenderException>(
				() => RenderToText(emptyBranchRig.Processor, new TestTemplate(0)));
			Assert.Contains("Branch key for 'color' is empty", emptyBranchException.Message);

			using TestRig unknownBranchRig = await CreateRig("{{branch.color[red=>'Red',blue=>'Blue']}}");
			TestTemplate unknownBranch = new TestTemplate(0).SetBranchKey("color", "green");
			TemplateRenderException unknownBranchException = Assert.Throws<TemplateRenderException>(
				() => RenderToText(unknownBranchRig.Processor, unknownBranch));
			Assert.Contains("No option found for branch key 'green'", unknownBranchException.Message);

			using TestRig partRig = await CreateRig("{{part.missing}}");
			TemplateRenderException partException = Assert.Throws<TemplateRenderException>(
				() => RenderToText(partRig.Processor, new TestTemplate(0)));
			Assert.Contains("Part 'missing' not found", partException.Message);

			using TestRig loopRig = await CreateRig("{{loop.items}}");
			TemplateRenderException loopException = Assert.Throws<TemplateRenderException>(
				() => RenderToText(loopRig.Processor, new TestTemplate(0)));
			Assert.Contains("Loop 'items' not found", loopException.Message);
		});

	[Fact]
	public Task RenderToBuffer_InsertErrorTextDoesNotReportResolvedBranch() =>
		RunWithOptions(nameof(RenderToBuffer_InsertErrorTextDoesNotReportResolvedBranch), new LissicOptions
		{
			UnknownTagDebugMode = UnknownTagDebugMode.InsertErrorText
		}, async () =>
		{
			using TestRig rig = await CreateRig("{{branch.color[red=>'Red',blue=>'Blue']}}");
			TestTemplate template = new TestTemplate(0).SetBranchKey("color", "blue");

			Assert.Equal("Blue", RenderToText(rig.Processor, template));
		});

	[Fact]
	public Task RenderToBuffer_InsertErrorTextDoesNotReportResolvedPart() =>
		RunWithOptions(nameof(RenderToBuffer_InsertErrorTextDoesNotReportResolvedPart), new LissicOptions
		{
			UnknownTagDebugMode = UnknownTagDebugMode.InsertErrorText
		}, async () =>
		{
			using TestRig rig = await CreateRig("{{part.header}}", "Header");
			TestTemplate template = new TestTemplate(0).SetPart("header", new TestTemplate(1));

			Assert.Equal("Header", RenderToText(rig.Processor, template));
		});

	[Fact]
	public Task RenderToBuffer_ThrowExceptionAllowsResolvedBranch() =>
		RunWithOptions(nameof(RenderToBuffer_ThrowExceptionAllowsResolvedBranch), new LissicOptions
		{
			UnknownTagDebugMode = UnknownTagDebugMode.ThrowException
		}, async () =>
		{
			using TestRig rig = await CreateRig("{{branch.color[red=>'Red',blue=>'Blue']}}");
			TestTemplate template = new TestTemplate(0).SetBranchKey("color", "blue");

			Assert.Equal("Blue", RenderToText(rig.Processor, template));
		});

	[Fact]
	public Task RenderToBuffer_ThrowExceptionAllowsResolvedPart() =>
		RunWithOptions(nameof(RenderToBuffer_ThrowExceptionAllowsResolvedPart), new LissicOptions
		{
			UnknownTagDebugMode = UnknownTagDebugMode.ThrowException
		}, async () =>
		{
			using TestRig rig = await CreateRig("{{part.header}}", "Header");
			TestTemplate template = new TestTemplate(0).SetPart("header", new TestTemplate(1));

			Assert.Equal("Header", RenderToText(rig.Processor, template));
		});

	[Fact]
	public Task RenderToBuffer_InsertErrorTextReportsUnsetAndUnknownFilteredValues() =>
		RunWithOptions(nameof(RenderToBuffer_InsertErrorTextReportsUnsetAndUnknownFilteredValues), new LissicOptions
		{
			UnknownTagDebugMode = UnknownTagDebugMode.InsertErrorText
		}, async () =>
		{
			using TestRig unsetRig = await CreateRig("[{{ name | lp_upper }}]");
			Assert.Equal("[<!-- ERROR: Missing value for variable 'name' -->]",
				RenderToText(unsetRig.Processor, new TestTemplate(0)));

			using TestRig optionalRig = await CreateRig("[{{ name? | lp_upper }}][{{ other? }}]");
			Assert.Equal("[][]", RenderToText(optionalRig.Processor, new TestTemplate(0)));

			using TestRig unknownRig = await CreateRig("{{ name | lp_nope }}");
			Assert.Equal("<!-- ERROR: Filter 'lp_nope' not found -->ada",
				RenderToText(unknownRig.Processor, new TestTemplate(0).SetVariable("name", "ada")));
		});

	[Fact]
	public Task RenderToBuffer_ThrowExceptionReportsUnsetAndUnknownFilteredValues() =>
		RunWithOptions(nameof(RenderToBuffer_ThrowExceptionReportsUnsetAndUnknownFilteredValues), new LissicOptions
		{
			UnknownTagDebugMode = UnknownTagDebugMode.ThrowException
		}, async () =>
		{
			using TestRig unsetRig = await CreateRig("{{ name | lp_upper }}");
			TemplateRenderException unsetException = Assert.Throws<TemplateRenderException>(
				() => RenderToText(unsetRig.Processor, new TestTemplate(0)));
			Assert.Contains("Missing value for variable 'name'", unsetException.Message);

			using TestRig optionalRig = await CreateRig("{{ name? | lp_upper }}");
			Assert.Equal(string.Empty, RenderToText(optionalRig.Processor, new TestTemplate(0)));

			using TestRig unknownRig = await CreateRig("{{ name | lp_nope }}");
			TemplateRenderException unknownException = Assert.Throws<TemplateRenderException>(
				() => RenderToText(unknownRig.Processor, new TestTemplate(0).SetVariable("name", "ada")));
			Assert.Contains("Filter 'lp_nope' not found", unknownException.Message);
		});


	/* Branches: comma delimiter and quoted literals */

	[Theory]
	[InlineData("before{{branch.color[red => \"Red\", blue => \"Blue\"]}}after")]
	[InlineData("before{{branch.color[red => 'Red', blue => 'Blue',]}}after")]
	[InlineData("before{{ branch.color [\n\tred => \"Red\",\n\tblue => 'Blue'\n] }}after")]
	[InlineData("before{{ (branch.color [red => \"Red\", blue => \"Blue\"]) }}after")]
	public async Task RenderToString_RendersSelectedBranch(string text)
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
	public async Task RenderToString_QuotedLiteralText(string text, string expected)
	{
		using TestRig rig = await CreateRig(text);

		Assert.Equal(expected, rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("q", "a")));
	}

	[Fact]
	public async Task RenderToString_LiteralTagsAreRendered()
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
	public async Task RenderToString_NestedBranches(string text)
	{
		using TestRig rig = await CreateRig(text);
		TestTemplate dark = new TestTemplate(0).SetBranchKey("color", "red").SetBranchKey("shade", "dark");
		TestTemplate light = new TestTemplate(0).SetBranchKey("color", "red").SetBranchKey("shade", "light").SetVariable("name", "Ada");

		Assert.Equal("<div>Dark</div>", rig.Processor.RenderToString(dark));
		Assert.Equal("<div>Ada</div>", rig.Processor.RenderToString(light));
	}

	[Fact]
	public async Task RenderToString_CommandActions()
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
	public async Task RenderToString_DefaultOptionUsedForUnmatchedKeys(string key, string expected)
	{
		using TestRig rig = await CreateRig("[{{branch.color[red => \"R\", blue => \"B\", _ => \"D\"]}}]");

		Assert.Equal($"[{expected}]", rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("color", key)));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	public async Task RenderToString_DefaultOptionUsedForEmptyOrUnsetKeys(string? key)
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
	public async Task RenderToString_DefaultOptionPositionDoesNotMatter(string key, string expected)
	{
		using TestRig rig = await CreateRig("{{branch.color[_ => \"D\", red => \"R\"]}}");

		Assert.Equal(expected, rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("color", key)));
	}

	[Fact]
	public async Task RenderToString_DefaultOnlyBranch()
	{
		using TestRig rig = await CreateRig("{{branch.color[_ => \"D\"]}}");

		Assert.Equal("D", rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("color", "anything")));
		Assert.Equal("D", rig.Processor.RenderToString(new TestTemplate(0)));
	}

	[Fact]
	public async Task RenderToString_DefaultOptionCanBeACommand()
	{
		using TestRig rig = await CreateRig("{{branch.color[red => \"R\", _ => (name | lp_upper)]}}|{{branch.shape[round => \"O\", _ => part.p]}}", "[part]");
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
	public async Task RenderToString_ProposalExample(string key, string expected)
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
	public async Task RenderToString_QuotedUnderscoreIsALiteralKeyNotADefault(string key, string expected)
	{
		using TestRig rig = await CreateRig("{{branch.color[\"_\" => \"Q\"]}}");

		Assert.Equal(expected, rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("color", key)));
	}

	[Fact]
	public Task RenderToBuffer_InsertErrorText_DefaultSuppressesMissingKeyErrors() =>
		RunWithOptions(nameof(RenderToBuffer_InsertErrorText_DefaultSuppressesMissingKeyErrors), new LissicOptions
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
	public Task RenderToBuffer_ThrowException_DefaultSuppressesMissingKeyErrors() =>
		RunWithOptions(nameof(RenderToBuffer_ThrowException_DefaultSuppressesMissingKeyErrors), new LissicOptions
		{
			UnknownTagDebugMode = UnknownTagDebugMode.ThrowException,
			UnknownBranchKeyDebugMode = UnknownBranchKeyDebugMode.ThrowException
		}, async () =>
		{
			using TestRig rig = await CreateRig("{{branch.color[red => \"Red\", _ => \"Default\"]}}");

			Assert.Equal("Default", RenderToText(rig.Processor, new TestTemplate(0)));
			Assert.Equal("Default", RenderToText(rig.Processor, new TestTemplate(0).SetBranchKey("color", "green")));
		});


	/* Branches: other branches as direct actions

	[Theory]
	[InlineData("a", "x", "X")]
	[InlineData("a", "y", "Ada")]
	[InlineData("a", "other", "ID")]
	[InlineData("a", null, "ID")]
	[InlineData("b", "x", "B")]
	[InlineData("other", "x", "OD")]
	[InlineData(null, "x", "OD")]
	public async Task RenderToString_BranchAsDirectAction(string? outerKey, string? innerKey, string expected)
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
	public async Task RenderToString_BranchAsDirectAction_WithFiltersAndNoInnerDefault()
	{
		using TestRig rig = await CreateRig("[{{ branch.outer [a => branch.inner [x => name | lp_append(\", \") | lp_upper, y => (name | lp_append(\"!\"))], b => \"B\"] }}]");

		Assert.Equal("[ADA, ]", rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("outer", "a").SetBranchKey("inner", "x").SetVariable("name", "ada")));
		Assert.Equal("[ada!]", rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("outer", "a").SetBranchKey("inner", "y").SetVariable("name", "ada")));
		Assert.Equal("[]", rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("outer", "a").SetBranchKey("inner", "z").SetVariable("name", "ada")));
	}

	[Fact]
	public async Task RenderToString_BranchAsDirectAction_DeeplyNested()
	{
		using TestRig rig = await CreateRig("{{ branch.a [x => branch.b [y => branch.c [z => \"Deep\", _ => \"C\"], _ => \"B\"], _ => \"A\"] }}");

		Assert.Equal("Deep", rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("a", "x").SetBranchKey("b", "y").SetBranchKey("c", "z")));
		Assert.Equal("C", rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("a", "x").SetBranchKey("b", "y")));
		Assert.Equal("B", rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("a", "x")));
		Assert.Equal("A", rig.Processor.RenderToString(new TestTemplate(0)));
	}

	[Fact]
	public Task RenderToBuffer_InsertErrorText_BranchAsDirectActionReportsInnerKey() =>
		RunWithOptions(nameof(RenderToBuffer_InsertErrorText_BranchAsDirectActionReportsInnerKey), new LissicOptions
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
	[InlineData("{{ branch.status [member => name | lp_upper | lp_append(\", member\"), _ => \"not a member\" ] }}")]
	[InlineData("{{ branch.status [member => (name | lp_upper | lp_append(\", member\")), _ => \"not a member\" ] }}")]
	[InlineData("{{ branch.status [member => name|lp_upper|lp_append(', member'),_=>'not a member'] }}")]
	public async Task RenderToString_FilteredBranchActions(string text)
	{
		using TestRig rig = await CreateRig(text);

		Assert.Equal("ADA, member", rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("status", "member").SetVariable("name", "ada")));
		Assert.Equal("not a member", rig.Processor.RenderToString(new TestTemplate(0).SetBranchKey("status", "guest").SetVariable("name", "ada")));
	}

	[Fact]
	public async Task RenderToString_FiltersApplyInsideDefaultLiteral()
	{
		using TestRig rig = await CreateRig("{{ branch.s [a => \"A\", _ => '{{ name | lp_upper }}'] }}");

		Assert.Equal("ADA", rig.Processor.RenderToString(new TestTemplate(0).SetVariable("name", "ada")));
	}


	/* Filters: pipe syntax */

	[Theory]
	[InlineData("[{{ name | lp_upper }}]")]
	[InlineData("[{{name|lp_upper}}]")]
	[InlineData("[{{ name | LP_UPpER }}]")]
	[InlineData("[{{ (name | lp_upper) }}]")]
	[InlineData("[{{ name\n  | lp_upper\n}}]")]
	public async Task RenderToString_AppliesPipeFilter(string text)
	{
		using TestRig rig = await CreateRig(text);

		Assert.Equal("[ADA]", rig.Processor.RenderToString(new TestTemplate(0).SetVariable("name", "ada")));
	}

	[Theory]
	[InlineData("{{ name | lp_append(\"x\") | lp_upper }}", "ADAX")]
	[InlineData("{{ name | lp_upper | lp_append(\"x\") }}", "ADAx")]
	[InlineData("{{ name | lp_append(-1.5) }}", "ada-1.5")]
	[InlineData("{{ name | lp_append (\"!\") }}", "ada!")]
	[InlineData("{{ name | lp_append( \"!\" ) }}", "ada!")]
	[InlineData("{{ name | lp_append(\" the great\") }}", "ada the great")]
	[InlineData("{{ name | lp_append(\"\"\"\") }}", "ada\"")]
	[InlineData("{{ name | lp_append('''') }}", "ada'")]
	[InlineData("{{ name | lp_append(\"say \"\"hi\"\"\") }}", "adasay \"hi\"")]
	[InlineData("{{ name | lp_append(\"a, b | c) d\") }}", "adaa, b | c) d")]
	public async Task RenderToString_PipeFilterChainsAndArgs(string text, string expected)
	{
		using TestRig rig = await CreateRig(text);

		Assert.Equal(expected, rig.Processor.RenderToString(new TestTemplate(0).SetVariable("name", "ada")));
	}

	/* Parentheses */

	[Fact]
	public async Task RenderToString_ParenthesizedTags()
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
	public async Task RenderToString_ParenthesesOutsideTagsAreText()
	{
		using TestRig rig = await CreateRig("({{ name }}) ({{ (name) }})");

		Assert.Equal("(ada) (ada)", rig.Processor.RenderToString(new TestTemplate(0).SetVariable("name", "ada")));
	}


	/* Parse errors surface when the cache is initialized */

	[Theory]
	[InlineData("{{ branch.c [a => x; b => y,] }}")]
	[InlineData("{{ branch.c [a => \"A\", a => \"B\"] }}")]
	[InlineData("{{ branch.c [_ => \"A\", _ => \"B\"] }}")]
	[InlineData("{{ name | lp_append(abc) }}")]
	[InlineData("{{ (name | lp_upper }}")]
	[InlineData("{{ branch.c [a => \"unclosed] }}")]
	[InlineData("{{ (name) | lp_upper }}")]
	[InlineData("{{ (name | lp_upper) | lp_upper }}")]
	[InlineData("{{ branch.c [a => (name) | lp_upper] }}")]
	[InlineData("{{ branch.c [a => branch.d [x => \"X\", b => \"B\"] }}")]
	[InlineData("{{ branch.c [a => branch.d, b => \"B\"] }}")]
	public async Task Initialize_InvalidSyntaxThrows(string text)
	{
		await Assert.ThrowsAsync<TemplateParsingException>(async () => await CreateRig(text));
	}


    /* Helper methods for creating test templates and rigs */

	private static string RenderToText(LissicProcessor processor, LissicTemplate template)
	{
		ArrayBufferWriter<byte> writer = new();
		processor.RenderToBuffer(template, writer);
		return Encoding.UTF8.GetString(writer.WrittenSpan);
	}

	private static Task RunWithOptions(string testName, LissicOptions options, Func<Task> test)
	{
		if (Environment.GetEnvironmentVariable("LISSIC_PROCESSOR_OPTION_PROBE") == testName)
		{
			LissicConfigurator.SetOptions(options);
			Assert.Equal(options.UnknownTagDebugMode, LissicConfig.UnknownTagDebugMode);
			return test();
		}

		return RunInChildProcess(testName);
	}

	private static Task RunInChildProcess(string testName, Func<Task> test)
	{
		if (Environment.GetEnvironmentVariable("LISSIC_PROCESSOR_INTEGRATION_PROBE") == testName)
			return test();

		return RunInChildProcessCore(testName);
	}

	private static async Task RunInChildProcess(string testName)
		=> await RunInChildProcessCore(testName);

	private static async Task RunInChildProcessCore(string testName)
	{
		ProcessStartInfo startInfo = new(Environment.ProcessPath!)
		{
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true
		};
		startInfo.ArgumentList.Add("--filter");
		startInfo.ArgumentList.Add($"FullyQualifiedName~{testName}");
		startInfo.Environment["LISSIC_PROCESSOR_OPTION_PROBE"] = testName;
		startInfo.Environment["LISSIC_PROCESSOR_INTEGRATION_PROBE"] = testName;
		startInfo.WorkingDirectory = AppContext.BaseDirectory;

		using Process process = Process.Start(startInfo)!;
		CancellationToken cancellationToken = TestContext.Current.CancellationToken;
		Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
		Task<string> standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
		await process.WaitForExitAsync(cancellationToken);
		string standardOutput = await standardOutputTask;
		string standardError = await standardErrorTask;

		Assert.True(process.ExitCode == 0, $"Child test process failed.\n{standardOutput}\n{standardError}");
	}

	private static TestTemplate CreatePartChain(int length)
	{
		TestTemplate root = new(0);
		TestTemplate current = root;
		for (int index = 1; index < length; index++)
		{
			TestTemplate next = new(0);
			current.SetPart("next", next);
			current = next;
		}
		return root;
	}

	private static async Task<TestRig> CreateRig(params string[] templates)
	{
		TemplateEntry[] entries = templates
			.Select((text, index) => new TemplateEntry(TemplateKind.Literal, text, $"template-{index}"))
			.ToArray();
		Lissic.Parsing.TemplateCache cache = new();
		await cache.Initialize(new TestManifest(entries));
#pragma warning disable CA2000 // Dispose objects before losing scope : Will be disposed by TestRig
        return new TestRig(new LissicProcessor(cache));
#pragma warning restore CA2000
    }

	private sealed class UpperTestFilter : IFilterFactory
	{
		public static ITemplateFilter Create(ReadOnlySpan<string> args) => new UpperTestFilter();

		public int Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output)
		{
			Span<byte> span = output.GetSpan(input.Length);
			for (int i = 0; i < input.Length; i++)
				span[i] = input[i] is >= (byte)'a' and <= (byte)'z' ? (byte)(input[i] - 32) : input[i];
			output.Advance(input.Length);
			return input.Length;
		}
	}

	private sealed class AppendTestFilter(byte[] suffix) : IFilterFactory
	{
		public static ITemplateFilter Create(ReadOnlySpan<string> args) =>
			new AppendTestFilter(Encoding.UTF8.GetBytes(args.Length > 0 ? args[0] : ""));

		public int Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output)
		{
			output.Write(input);
			output.Write(suffix);
			return input.Length + suffix.Length;
		}
	}

	private sealed class DefaultTestFilter(byte[] fallback) : IFilterFactory
	{
		public static ITemplateFilter Create(ReadOnlySpan<string> args) =>
			new DefaultTestFilter(Encoding.UTF8.GetBytes(args.Length > 0 ? args[0] : ""));

		public int Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output)
		{
			output.Write(input.IsEmpty ? fallback.AsSpan() : input);
			return input.IsEmpty ? fallback.Length : input.Length;
		}
	}

	private sealed class FailingTestFilter : IFilterFactory
	{
		public static ITemplateFilter Create(ReadOnlySpan<string> args) => throw new ArgumentException("bad filter argument");

		public int Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output) { return 0; }
	}

	private sealed class AllocationFreeTemplate(int index) : LissicTemplate
	{
		private static readonly byte[] NameBytes = "ada"u8.ToArray();

		public override int Template_Index => index;
		public LissicTemplate? Part { get; init; }
		public LoopValue? Items { get; init; }

		public override int Stream_Variable(ReadOnlySpan<char> name, IBufferWriter<byte> writer)
		{
			if (name is not "name")
				return -1;

			writer.Write(NameBytes);
			return NameBytes.Length;
		}

		public override LissicTemplate? Get_Part(ReadOnlySpan<char> name) => name is "p" ? Part : null;

		public override LoopValue? Get_Loop(ReadOnlySpan<char> name) => name is "items" ? Items : null;

		public override string Get_BranchKey(ReadOnlySpan<char> keyName) =>
			keyName is "color" ? "red" : "";

		public override string? Get_VariableString(ReadOnlySpan<char> name) => throw new NotImplementedException();
	}

	private sealed record TestRig(LissicProcessor Processor) : IDisposable
	{
		public void Dispose() => Processor.Dispose();
	};

	private sealed class TestManifest(TemplateEntry[] entries) : ITemplateManifest
	{
		public int Count => entries.Length;
		public string TemplateSourceRoot => string.Empty;
		public string TemplateRoot => string.Empty;
		public TemplateEntry Get(int index) => entries[index];
	}

	private sealed class TestTemplate(int index) : LissicTemplate
	{
		private readonly Dictionary<string, string> properties = [];
		private readonly Dictionary<string, string> branchKeys = [];
		private readonly Dictionary<string, LissicTemplate> parts = [];
		private readonly Dictionary<string, LoopValue> loops = [];

		public override int Template_Index => index;

		public TestTemplate SetVariable(string name, string value)
		{
			properties[name] = value;
			return this;
		}

		public TestTemplate SetBranchKey(string name, string key)
		{
			branchKeys[name] = key;
			return this;
		}

		public TestTemplate SetPart(string name, LissicTemplate part)
		{
			parts[name] = part;
			return this;
		}

		public TestTemplate SetLoop(string name, params TestTemplate[] children)
		{
			loops[name] = new LoopValue([..children]);
			return this;
		}

		public override string Get_BranchKey(ReadOnlySpan<char> keyName) =>
			branchKeys.GetValueOrDefault(keyName.ToString()) ?? "";

		public override LissicTemplate? Get_Part(ReadOnlySpan<char> name) =>
			parts.GetValueOrDefault(name.ToString());

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

		public override string? Get_VariableString(ReadOnlySpan<char> name) => throw new NotImplementedException();
	}
}

[FormattedVariable<DateFormatter>("Date", ["MM-dd-yy g"])]
[TemplateText("{{ Date }}")]
public partial class DateFormatterTemplate { }