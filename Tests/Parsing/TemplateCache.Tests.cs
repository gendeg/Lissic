using System.Buffers;
using System.Text;
using Lissic.Parsing;

namespace Lissic.Tests.TemplateCache;


public class TemplateCacheTests
{
	static TemplateCacheTests()
	{
		FilterRegistry.Register<NoOpFilter>("tc-noop");
	}

	[Fact]
	public async Task Initialize_VariableWithFilters_IsResolvedToFilteredNode()
	{
		Lissic.Parsing.TemplateCache cache = new();

		await cache.Initialize(new TestManifest(new TemplateEntry(TemplateKind.Literal, "a {{ name <tc-noop | tc-noop(1)> }} b", "filtered")));

		FilteredVariableNode node = Assert.IsType<FilteredVariableNode>(cache.Get(0)[1]);
		Assert.Equal("name", node.Name);
		Assert.True(node.Required);
		Assert.Equal(2, node.Filters.Length);
		Assert.All(node.Filters, filter => Assert.IsType<NoOpFilter>(filter));
		Assert.Equal(["1"], ((NoOpFilter)node.Filters[1]).Args);
	}

	[Fact]
	public async Task Initialize_VariableWithoutFilters_StaysAPlainVariableNode()
	{
		Lissic.Parsing.TemplateCache cache = new();

		await cache.Initialize(new TestManifest(new TemplateEntry(TemplateKind.Literal, "{{ name }}", "plain")));

		Assert.IsType<VariableNode>(Assert.Single(cache.Get(0)));
	}

	[Fact]
	public async Task Initialize_FiltersInsideBranchEntries_AreResolved()
	{
		Lissic.Parsing.TemplateCache cache = new();

		await cache.Initialize(new TestManifest(new TemplateEntry(TemplateKind.Literal, "{{branch.color[red=>name <tc-noop>;]}}", "branch")));

		BranchNode branch = Assert.IsType<BranchNode>(Assert.Single(cache.Get(0)));
		ITemplateNode[] entry = branch.BranchEntries["red"];
		Assert.IsType<FilteredVariableNode>(Assert.Single(entry));
	}

	[Fact]
	public async Task Initialize_UnknownFilter_ResolvesToPassThroughPlaceholder()
	{
		Lissic.Parsing.TemplateCache cache = new();

		await cache.Initialize(new TestManifest(new TemplateEntry(TemplateKind.Literal, "{{ name <tc-not-registered> }}", "unknown")));

		FilteredVariableNode node = Assert.IsType<FilteredVariableNode>(Assert.Single(cache.Get(0)));
		Assert.IsType<UnknownFilter>(Assert.Single(node.Filters));
	}

	[Fact]
	public async Task ReloadPathTemplates_ResolvesFiltersInReloadedFiles()
	{
		using TemporaryTemplates templates = new();
		string filePath = templates.PathFor("filtered.html");
		await File.WriteAllTextAsync(filePath, "{{ name }}", TestContext.Current.CancellationToken);
		TestManifest manifest = new(new TemplateEntry(TemplateKind.File, filePath, "filtered"));
		Lissic.Parsing.TemplateCache cache = new();
		await cache.Initialize(manifest);

		await File.WriteAllTextAsync(filePath, "{{ name <tc-noop> }}", TestContext.Current.CancellationToken);
		cache.ReloadPathTemplates(manifest, TestContext.Current.CancellationToken);

		Assert.IsType<FilteredVariableNode>(Assert.Single(cache.Get(0)));
	}

	[Fact]
	public async Task Initialize_MixedEntries_ReadsFilesAndParsesAllTemplates()
	{
		using TemporaryTemplates templates = new();
		string filePath = templates.PathFor("header.html");
		await File.WriteAllTextAsync(filePath, "Header {{name}}!", TestContext.Current.CancellationToken);

		TestManifest manifest = new(
			new(TemplateKind.Literal, "Hello {{person}}!", "greeting"),
			new(TemplateKind.File, filePath, "header"),
			new(TemplateKind.Literal, "Goodbye", "farewell"));
		Lissic.Parsing.TemplateCache cache = new();

		await cache.Initialize(manifest);

		AssertTemplateNodes(cache.Get(0), "Hello ", "person", "!");
		AssertTemplateNodes(cache.Get(1), "Header ", "name", "!");
		HtmlNode farewell = Assert.IsType<HtmlNode>(Assert.Single(cache.Get(2)));
		Assert.Equal("Goodbye", Encoding.UTF8.GetString(farewell.Html.Span));
	}

	[Fact]
	public async Task Initialize_MissingFile_PropagatesFileNotFoundException()
	{
		using TemporaryTemplates templates = new();
		string missingPath = templates.PathFor("missing.html");
		Lissic.Parsing.TemplateCache cache = new();

		await Assert.ThrowsAsync<FileNotFoundException>(() =>
			cache.Initialize(new TestManifest(new TemplateEntry(TemplateKind.File, missingPath, "missing"))));
	}

	[Fact]
	public async Task Initialize_MalformedFileTemplate_PropagatesTemplateParsingException()
	{
		using TemporaryTemplates templates = new();
		string filePath = templates.PathFor("invalid.html");
		await File.WriteAllTextAsync(filePath, "{{part", TestContext.Current.CancellationToken);
		Lissic.Parsing.TemplateCache cache = new();

		await Assert.ThrowsAsync<TemplateParsingException>(() =>
			cache.Initialize(new TestManifest(new TemplateEntry(TemplateKind.File, filePath, "invalid"))));
	}

	[Fact]
	public async Task Initialize_UnknownEntryKind_ThrowsArgumentOutOfRangeException()
	{
		Lissic.Parsing.TemplateCache cache = new();
		TemplateEntry invalidEntry = new((TemplateKind)int.MaxValue, "ignored", "invalid");

		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			cache.Initialize(new TestManifest(invalidEntry)));
	}

	[Fact]
	public async Task ReloadPathTemplates_RefreshesFileEntriesAndLeavesLiteralEntriesUnchanged()
	{
		using TemporaryTemplates templates = new();
		string fixturePath = Path.GetFullPath(Path.Combine(
			AppContext.BaseDirectory, "..", "..", "..", "Templates", "reload-test.html"));
		string filePath = templates.PathFor("reload-test.html");
		File.Copy(fixturePath, filePath);

		TestManifest manifest = new(
			new(TemplateKind.Literal, "Static", "literal"),
			new(TemplateKind.File, "reload-test.html", "reload-test"))
		{
			TemplateSourceRoot = templates.RootDirectory,
			TemplateRoot = templates.RootDirectory
		};
		Lissic.Parsing.TemplateCache cache = new();
		await cache.Initialize(manifest);

		await File.WriteAllTextAsync(filePath, "After", TestContext.Current.CancellationToken);
		cache.ReloadPathTemplates(manifest, TestContext.Current.CancellationToken);

		HtmlNode literal = Assert.IsType<HtmlNode>(Assert.Single(cache.Get(0)));
		HtmlNode file = Assert.IsType<HtmlNode>(Assert.Single(cache.Get(1)));
		Assert.Equal("Static", Encoding.UTF8.GetString(literal.Html.Span));
		Assert.Equal("After", Encoding.UTF8.GetString(file.Html.Span));
	}

	[Fact]
	public async Task ReloadPathTemplates_BeforeInitialization_ThrowsInvalidOperationException()
	{
		Lissic.Parsing.TemplateCache cache = new();
		TestManifest manifest = new();

		Assert.Throws<InvalidOperationException>(
			() => cache.ReloadPathTemplates(manifest, TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task ReloadPathTemplatesAsync_WhenLaterFileFails_PropagatesIOExceptionAndKeepsSuccessfulUpdates()
	{
		using TemporaryTemplates templates = new();
		string firstPath = templates.PathFor("first.html");
		string secondPath = templates.PathFor("second.html");
		await File.WriteAllTextAsync(firstPath, "First before", TestContext.Current.CancellationToken);
		await File.WriteAllTextAsync(secondPath, "Second before", TestContext.Current.CancellationToken);

		TestManifest manifest = new(
			new(TemplateKind.File, firstPath, "first"),
			new(TemplateKind.File, secondPath, "second"));
		Lissic.Parsing.TemplateCache cache = new();
		await cache.Initialize(manifest);

		await File.WriteAllTextAsync(firstPath, "First after", TestContext.Current.CancellationToken);
		File.Delete(secondPath);

		Assert.Throws<FileNotFoundException>(
			() => cache.ReloadPathTemplates(manifest, TestContext.Current.CancellationToken));
		Assert.Equal("First after", Encoding.UTF8.GetString(Assert.IsType<HtmlNode>(Assert.Single(cache.Get(0))).Html.Span));
		Assert.Equal("Second before", Encoding.UTF8.GetString(Assert.IsType<HtmlNode>(Assert.Single(cache.Get(1))).Html.Span));
	}

	[Fact]
	public async Task ReloadPathTemplatesAsync_WhenLaterFileFailsToParse_PropagatesParsingExceptionAndKeepsSuccessfulUpdates()
	{
		using TemporaryTemplates templates = new();
		string firstPath = templates.PathFor("first.html");
		string secondPath = templates.PathFor("second.html");
		await File.WriteAllTextAsync(firstPath, "First before", TestContext.Current.CancellationToken);
		await File.WriteAllTextAsync(secondPath, "Second before", TestContext.Current.CancellationToken);

		TestManifest manifest = new(
			new(TemplateKind.File, firstPath, "first"),
			new(TemplateKind.File, secondPath, "second"));
		Lissic.Parsing.TemplateCache cache = new();
		await cache.Initialize(manifest);

		await File.WriteAllTextAsync(firstPath, "First after", TestContext.Current.CancellationToken);
		await File.WriteAllTextAsync(secondPath, "{{part", TestContext.Current.CancellationToken);

		Assert.Throws<TemplateParsingException>(
			() => cache.ReloadPathTemplates(manifest, TestContext.Current.CancellationToken));
		Assert.Equal("First after", Encoding.UTF8.GetString(Assert.IsType<HtmlNode>(Assert.Single(cache.Get(0))).Html.Span));
		Assert.Equal("Second before", Encoding.UTF8.GetString(Assert.IsType<HtmlNode>(Assert.Single(cache.Get(1))).Html.Span));
	}

	[Fact]
	public async Task ReloadPathTemplatesAsync_WhenCanceled_PropagatesCancellation()
	{
		using TemporaryTemplates templates = new();
		string filePath = templates.PathFor("header.html");
		await File.WriteAllTextAsync(filePath, "Header", TestContext.Current.CancellationToken);

		TestManifest manifest = new(new TemplateEntry(TemplateKind.File, filePath, "header"));
		Lissic.Parsing.TemplateCache cache = new();
		await cache.Initialize(manifest);

		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.ThrowsAny<OperationCanceledException>(
			() => cache.ReloadPathTemplates(manifest, cancellation.Token));
	}

	private static void AssertTemplateNodes(ITemplateNode[] nodes, string before, string variableName, string after)
	{
		Assert.Equal(3, nodes.Length);
		Assert.Equal(before, Encoding.UTF8.GetString(Assert.IsType<HtmlNode>(nodes[0]).Html.Span));
		Assert.Equal(variableName, Assert.IsType<VariableNode>(nodes[1]).Name);
		Assert.Equal(after, Encoding.UTF8.GetString(Assert.IsType<HtmlNode>(nodes[2]).Html.Span));
	}

	private sealed class NoOpFilter(string[] args) : IFilterFactory
	{
		public string[] Args => args;

		public static ITemplateFilter Create(ReadOnlySpan<string> args) => new NoOpFilter(args.ToArray());

		public void Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output) => output.Write(input);
	}

	private sealed class TestManifest(params TemplateEntry[] entries) : ITemplateManifest
	{
		public int Count => entries.Length;
		public string TemplateSourceRoot { get; init; } = string.Empty;
		public string TemplateRoot { get; init; } = string.Empty;

		public TemplateEntry Get(int index) => entries[index];
	}

	private sealed class TemporaryTemplates : IDisposable
	{
		private readonly string directory = Path.Combine(
			Path.GetTempPath(), Guid.NewGuid().ToString("N"));

		public TemporaryTemplates() => Directory.CreateDirectory(directory);
		public string RootDirectory => directory;

		public string PathFor(string fileName) => Path.Combine(directory, fileName);

		public void Dispose() => Directory.Delete(directory, recursive: true);
	}
}
