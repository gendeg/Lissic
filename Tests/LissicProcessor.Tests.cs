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
		FilterRegistry.Register<UpperTestFilter>("lp-upper");
		FilterRegistry.Register<AppendTestFilter>("lp-append");
		FilterRegistry.Register<DefaultTestFilter>("lp-default");
		FilterRegistry.Register<FailingTestFilter>("lp-fail");
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

	[Fact]
	public async Task RenderToString_RendersSelectedBranch()
	{
		using TestRig rig = await CreateRig("before{{branch.color[red=>@Red;blue=>@Blue;]}}after");
		TestTemplate template = new TestTemplate(0).SetBranchKey("color", "blue");

		Assert.Equal("beforeBlueafter", rig.Processor.RenderToString(template));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("green")]
	public async Task RenderToString_MissingOrUnknownBranchKeyOmitsBranch(string? key)
	{
		using TestRig rig = await CreateRig("before{{branch.color[red=>@Red;blue=>@Blue;]}}after");
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

	[Fact]
	public async Task RenderToString_AppliesFilterToVariable()
	{
		using TestRig rig = await CreateRig("[{{ name <lp-upper> }}]");

		Assert.Equal("[ADA]", rig.Processor.RenderToString(new TestTemplate(0).SetVariable("name", "ada")));
	}

	[Fact]
	public async Task RenderToString_FiltersAreNotCaseSensitive()
	{
		using TestRig rig = await CreateRig("[{{ name <LP-UPpER> }}]");

		Assert.Equal("[ADA]", rig.Processor.RenderToString(new TestTemplate(0).SetVariable("name", "ada")));
	}

	[Theory]
	[InlineData("[{{name<lp-upper>}}]", true)]
	[InlineData("[{{ name < lp-upper > }}]", true)]
	[InlineData("[{{ name < lp-append('a') > }}]", true)]
	[InlineData("[{{ name < lp-append( 'a' ) > }}]", true)]
	[InlineData("[{{name<lp-append ('a')>}}]", false)]
	public async Task RenderToString_FiltersHandleWhitespaceCorrectly(string template, bool willParse)
	{
		if (willParse)
		{
			using TestRig rig = await CreateRig(template);
			Assert.Equal("[ada", rig.Processor.RenderToString(new TestTemplate(0).SetVariable("name", "ada")).ToLower()[..4]);
		}

		else
		{
			await Assert.ThrowsAsync<TemplateParsingException>(async () => await CreateRig(template));
		}
	}

	[Fact]
	public async Task RenderToString_PassesFilterArguments()
	{
		using TestRig rig = await CreateRig("{{ name <lp-append(\"!?\")> }}");

		Assert.Equal("ada!?", rig.Processor.RenderToString(new TestTemplate(0).SetVariable("name", "ada")));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(4)]
	[InlineData(5)]
	public async Task RenderToString_AppliesFilterChainInOrder(int count)
	{
		string chain = string.Join(" | ", Enumerable.Range(1, count).Select(i => $"lp-append({i})"));
		using TestRig rig = await CreateRig($"{{{{ name <{chain}> }}}}");

		string result = rig.Processor.RenderToString(new TestTemplate(0).SetVariable("name", "ada"));

		Assert.Equal("ada" + string.Concat(Enumerable.Range(1, count)), result);
	}

	[Fact]
	public async Task RenderToString_FilterAfterFilterSeesPreviousOutput()
	{
		using TestRig rig = await CreateRig("{{ name <lp-append(\"x\") | lp-upper> }}");

		Assert.Equal("ADAX", rig.Processor.RenderToString(new TestTemplate(0).SetVariable("name", "ada")));
	}

	[Fact]
	public async Task RenderToString_SameVariableWithAndWithoutFilters()
	{
		using TestRig rig = await CreateRig("{{ name }} {{ name <lp-upper> }} {{ name }}");

		Assert.Equal("ada ADA ada", rig.Processor.RenderToString(new TestTemplate(0).SetVariable("name", "ada")));
	}

	[Fact]
	public async Task RenderToString_UnsetFilteredVariable_SkipsFiltersSilently()
	{
		using TestRig rig = await CreateRig("[{{ name <lp-append(\"!\")> }}][{{ other? <lp-append(\"!\")> }}]");

		Assert.Equal("[][]", rig.Processor.RenderToString(new TestTemplate(0)));
	}

	[Fact]
	public async Task RenderToString_ZeroLengthTextValue_StillRunsFilters()
	{
		using TestRig rig = await CreateRig("{{ name <lp-default(\"n/a\")> }}");

		Assert.Equal("n/a", rig.Processor.RenderToString(new TestTemplate(0).SetVariable("name", "")));
	}

	[Fact]
	public async Task RenderToString_UnknownFilterPassesValueThrough()
	{
		using TestRig rig = await CreateRig("{{ name <lp-upper | lp-not-registered> }}");

		Assert.Equal("ADA", rig.Processor.RenderToString(new TestTemplate(0).SetVariable("name", "ada")));
	}

	[Fact]
	public async Task RenderToString_FiltersApplyInsideBranchEntries()
	{
		using TestRig rig = await CreateRig("{{branch.color[red=>name <lp-upper>;blue=>@Blue;]}}");
		TestTemplate template = new TestTemplate(0).SetBranchKey("color", "red").SetVariable("name", "ada");

		Assert.Equal("ADA", rig.Processor.RenderToString(template));
	}

	[Fact]
	public async Task RenderToString_FiltersApplyInPartsAndLoops()
	{
		using TestRig rig = await CreateRig("{{part.header}}|{{loop.items}}", "[{{ title <lp-upper> }}]");
		TestTemplate template = new TestTemplate(0)
			.SetPart("header", new TestTemplate(1).SetVariable("title", "head"))
			.SetLoop("items", new TestTemplate(1).SetVariable("title", "one"), new TestTemplate(1).SetVariable("title", "two"));

		Assert.Equal("[HEAD]|[ONE][TWO]", rig.Processor.RenderToString(template));
	}

	[Fact]
	public async Task Initialize_FilterFactoryThatThrows_FailsWithFilterAndTemplateNames()
	{
		TemplateParsingException exception = await Assert.ThrowsAsync<TemplateParsingException>(
			() => CreateRig("{{ name <lp-fail(\"x\")> }}"));

		Assert.Contains("lp-fail", exception.Message);
		Assert.Contains("template-0", exception.Message);
		Assert.Contains("bad filter argument", exception.Message);
	}

	[Fact]
	public async Task RenderToString_LargeFiltersAreRendered()
	{
		using TestRig rig = await CreateRig("{{ name <lp-append(1) | lp-append(2) | lp-append(3)> }}");
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
				"{{ name }}|{{ name <lp-upper> }}|{{ name <lp-append(\"-\") | lp-upper | lp-append(\"!\")> }}|{{part.p}}|{{loop.items}}|{{branch.color[red=>name <lp-upper>;]}}",
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
			Assert.Equal("ada|ADA|ADA-!|[ada]|[ada][ada]|ADA", Encoding.UTF8.GetString(writer.WrittenSpan));

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
	public Task RenderToBuffer_CountsLiteralVariableAndFilterBytesAtThreshold() =>
		RunWithOptions(nameof(RenderToBuffer_CountsLiteralVariableAndFilterBytesAtThreshold), new LissicOptions
		{
			OutputSizeThreshold = 12
		}, async () =>
		{
			using TestRig rig = await CreateRig("A{{plain}}/{{ value <lp-append(\"!\")> }}Z");
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
			using TestRig rig = await CreateRig("A{{plain}}/{{ value <lp-append(\"!\")> }}Z");
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

			using TestRig emptyBranchRig = await CreateRig("{{branch.color[red=>@Red;blue=>@Blue;]}}");
			Assert.Equal("<!-- ERROR: Branch key for 'color' is empty.  No branch key was provided or the value is empty. -->",
				RenderToText(emptyBranchRig.Processor, new TestTemplate(0)));

			using TestRig unknownBranchRig = await CreateRig("{{branch.color[red=>@Red;blue=>@Blue;]}}");
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

			using TestRig emptyBranchRig = await CreateRig("{{branch.color[red=>@Red;blue=>@Blue;]}}");
			TemplateRenderException emptyBranchException = Assert.Throws<TemplateRenderException>(
				() => RenderToText(emptyBranchRig.Processor, new TestTemplate(0)));
			Assert.Contains("Branch key for 'color' is empty", emptyBranchException.Message);

			using TestRig unknownBranchRig = await CreateRig("{{branch.color[red=>@Red;blue=>@Blue;]}}");
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
			using TestRig rig = await CreateRig("{{branch.color[red=>@Red;blue=>@Blue;]}}");
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
			using TestRig rig = await CreateRig("{{branch.color[red=>@Red;blue=>@Blue;]}}");
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
			using TestRig unsetRig = await CreateRig("[{{ name <lp-upper> }}]");
			Assert.Equal("[<!-- ERROR: Missing value for variable 'name' -->]",
				RenderToText(unsetRig.Processor, new TestTemplate(0)));

			using TestRig optionalRig = await CreateRig("[{{ name? <lp-upper> }}][{{ other? }}]");
			Assert.Equal("[][]", RenderToText(optionalRig.Processor, new TestTemplate(0)));

			using TestRig unknownRig = await CreateRig("{{ name <lp-nope> }}");
			Assert.Equal("<!-- ERROR: Filter 'lp-nope' not found -->ada",
				RenderToText(unknownRig.Processor, new TestTemplate(0).SetVariable("name", "ada")));
		});

	[Fact]
	public Task RenderToBuffer_ThrowExceptionReportsUnsetAndUnknownFilteredValues() =>
		RunWithOptions(nameof(RenderToBuffer_ThrowExceptionReportsUnsetAndUnknownFilteredValues), new LissicOptions
		{
			UnknownTagDebugMode = UnknownTagDebugMode.ThrowException
		}, async () =>
		{
			using TestRig unsetRig = await CreateRig("{{ name <lp-upper> }}");
			TemplateRenderException unsetException = Assert.Throws<TemplateRenderException>(
				() => RenderToText(unsetRig.Processor, new TestTemplate(0)));
			Assert.Contains("Missing value for variable 'name'", unsetException.Message);

			using TestRig optionalRig = await CreateRig("{{ name? <lp-upper> }}");
			Assert.Equal(string.Empty, RenderToText(optionalRig.Processor, new TestTemplate(0)));

			using TestRig unknownRig = await CreateRig("{{ name <lp-nope> }}");
			TemplateRenderException unknownException = Assert.Throws<TemplateRenderException>(
				() => RenderToText(unknownRig.Processor, new TestTemplate(0).SetVariable("name", "ada")));
			Assert.Contains("Filter 'lp-nope' not found", unknownException.Message);
		});


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