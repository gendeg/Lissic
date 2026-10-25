using DiffEngine;
using LissicGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Tests.Lissic.SourceGen;


public class LissicGeneratorSnapshotTests
{
    private const string FullTemplate = """
using Lissic;
using System;
using System.Buffers;

namespace Tests.Formatters
{
    public class LowerFormatter : IVariableFormatter
    {
        public static int Format(in VariableValue value, IBufferWriter<byte> output, ReadOnlySpan<string> args) { return 0; }
    }

    public class PadFormatter : IVariableFormatter
    {
        public static int Format(in VariableValue value, IBufferWriter<byte> output, ReadOnlySpan<string> args) { return 0; }
    }
}

namespace Tests.Filters
{
    [FilterName("upper")]
    public class UpperFilter : IFilterFactory
    {
        public static ITemplateFilter Create(ReadOnlySpan<string> args) => new UpperFilter();
        public void Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output) { }
    }

    [FilterName("lower")]
    public class LowerFilter : IFilterFactory
    {
        public static ITemplateFilter Create(ReadOnlySpan<string> args) => new LowerFilter();
        public void Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output) { }
    }

    [FilterName("pad")]
    public class PadFilter : IFilterFactory
    {
        public static ITemplateFilter Create(ReadOnlySpan<string> args) => new PadFilter();
        public void Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output) { }
    }
}

namespace Tests.FakeSpace
{
    using Tests.Formatters;
    
    public partial class Templates
    {
        [FormattedVariable<LowerFormatter>("City", ["trim"])]
        [FormattedVariable<PadFormatter>("Name", ["10", "left"])]
        [TemplateText(@"Hello {{ Name }}, welcome to {{ City? }}!
It's time to do the thing! {{part.thingPart}}
Also, think about these:
{{loop.itemsForLoop}}

Well, I have more things to test, so...
{{ testVal <upper> }}
{{ testVal2 <lower(""lowerArg"") | pad(""42"", ""down"")> }}

As always, {{ branch.farewellType[like=>@take care!;dislike=>@goodbye!;]}} ")]
    public partial class WelcomeMessage { }
    }
}

""";

    [Fact]
    public Task GeneratesTemplateExtensionsCorrectly() => SnapshotRunner.Verify(FullTemplate);

    [Fact]
    public void GeneratedCode_Compiles()
    {
        Assert.Empty(SnapshotRunner.GetCompilationErrors(FullTemplate));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GeneratedCode_Compiles_WithRequiredPropertiesOption(bool ignore)
    {
        Assert.Empty(SnapshotRunner.GetCompilationErrors("""
using Lissic;

namespace Tests.FakeSpace;

[TemplateText(@"Hello {{ Name }}!")]
public partial class WelcomeMessage { }
""", ignore));
    }

    [Fact]
    public void GeneratedCode_ContainsInternalTemplate()
    {
        string source = SnapshotRunner.GetSource("""
using Lissic;

namespace Tests.FakeSpace;

[TemplateText("Hello, {{ name }}!")]
internal partial class TestInternalTemplate {}
""", "Lissic.TestInternalTemplate.g.cs");

        Assert.Contains("internal partial class TestInternalTemplate", source);
    }

    [Fact]
    public void GeneratedCode_ContainsPrivateTemplate()
    {
        string source = SnapshotRunner.GetSource("""
using Lissic;

namespace Tests.FakeSpace;

[TemplateText("Hello, {{ name }}!")]
private partial class TestPrivateTemplate {}
""", "Lissic.TestPrivateTemplate.g.cs");

        Assert.Contains("private partial class TestPrivateTemplate", source);
    }

    [Fact]
    public void NodesInsideBranchEntries_AreDeclared()
    {
        string source = SnapshotRunner.GetSource("""
using Lissic;

namespace Tests.FakeSpace;

[TemplateText(@"{{ branch.color[red=>name;blue=>part.sidebar;green=>@<b>{{ extra? }}</b>;] }}")]
public partial class WelcomeMessage { }
""", "Lissic.WelcomeMessage.g.cs");

        Assert.Contains("public required BranchValue color { get; set; }", source);
        Assert.Contains("public required VariableValue name { get; set; }", source);
        Assert.Contains("public required PartValue sidebar { get; set; }", source);
        Assert.Contains("public VariableValue extra { get; set; }", source);
        Assert.Contains("case \"name\": if (name.IsEmpty) return -1; return name.WriteTo(writer);", source);
    }

    [Fact]
    public void NodesInsideNestedBranches_AreDeclared()
    {
        string source = SnapshotRunner.GetSource("""
using Lissic;

namespace Tests.FakeSpace;

[TemplateText(@"{{ branch.color[red=>@<div>{{ branch.shade[dark=>name;;] }}</div>;] }}")]
public partial class WelcomeMessage { }
""", "Lissic.WelcomeMessage.g.cs");

        Assert.Contains("BranchValue color { get; set; }", source);
        Assert.Contains("BranchValue shade { get; set; }", source);
        Assert.Contains("VariableValue name { get; set; }", source);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RequiredVariable_RespectsIgnoreOption(bool ignore)
    {
        string templateWithRequiredProp = """
using Lissic;
using System;

namespace Tests.FakeSpace;

[TemplateText(@"Hello {{ Name }}!")]
public partial class WelcomeMessage { }
""";

        string source = SnapshotRunner.GetSource(templateWithRequiredProp, "Lissic.WelcomeMessage.g.cs", ignore);
        if (ignore)
            Assert.Contains("public VariableValue Name { get; set; }", source);
        else
            Assert.Contains("public required VariableValue Name { get; set; }", source);
        return Task.CompletedTask;
    }

    [Fact]
    public void RepeatedReferences_DeclareEachNameOnce()
    {
        string source = SnapshotRunner.GetSource("""
using Lissic;

namespace Tests.FakeSpace;

[TemplateText(@"{{ Name }} and {{ Name <upper> }} and {{ Name }}")]
public partial class WelcomeMessage { }
""", "Lissic.WelcomeMessage.g.cs");

        Assert.Equal(1, CountOccurrences(source, "VariableValue Name { get; set; }"));
        Assert.Equal(1, CountOccurrences(source, "case \"Name\": if (Name.IsEmpty) return -1; return Name.WriteTo(writer);"));
    }

    [Fact]
    public void RepeatedReferences_RequiredWinsOverOptional()
    {
        string source = SnapshotRunner.GetSource("""
using Lissic;

namespace Tests.FakeSpace;

[TemplateText(@"{{ Name? }} and {{ Name }}")]
public partial class WelcomeMessage { }
""", "Lissic.WelcomeMessage.g.cs");

        Assert.Contains("public required VariableValue Name { get; set; }", source);
    }

    [Fact]
    public void FilterTypes_AreRegisteredByName()
    {
        string source = SnapshotRunner.GetSource("""
using Lissic;
using System;
using System.Buffers;

namespace Tests.Filters
{
    [FilterName("upper")]
    public class UpperFilter : IFilterFactory
    {
        public static ITemplateFilter Create(ReadOnlySpan<string> args) => new UpperFilter();
        public void Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output) { }
    }

    public class Outer
    {
        [FilterName("pad")]
        public class PadFilter : IFilterFactory
        {
            public static ITemplateFilter Create(ReadOnlySpan<string> args) => new PadFilter();
            public void Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output) { }
        }
    }
}
""", "Lissic.Filters.g.cs");

        Assert.Contains("Register<global::Tests.Filters.Outer.PadFilter>(\"pad\");", source);
        Assert.Contains("Register<global::Tests.Filters.UpperFilter>(\"upper\");", source);
        Assert.True(source.IndexOf("\"pad\"") < source.IndexOf("\"upper\""), "Registrations should be sorted by name.");
    }

    [Fact]
    public void MultipleFormattersOnOneVariable_ReportsError()
    {
        string[] ids = DiagnosticIds("""
using Lissic;
using System;
using System.Buffers;

namespace Tests.FakeSpace;

public class F : IVariableFormatter
{
    public static void Format(in VariableValue value, IBufferWriter<byte> output, ReadOnlySpan<string> args) { }
}

[FormattedVariable<F>("Name")]
[FormattedVariable<F>("Name", ["x"])]
[TemplateText(@"{{ Name }}")]
public partial class WelcomeMessage { }
""");

        Assert.Equal(["LISSIC001"], ids);
    }

    [Fact]
    public void FilterWithoutFactory_ReportsErrorAndIsNotRegistered()
    {
        GeneratorDriver driver = SnapshotRunner.Run("""
using Lissic;
using System;
using System.Buffers;

namespace Tests;

[FilterName("bad")]
public class BadFilter : ITemplateFilter
{
    public void Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output) { }
}
""");

        GeneratorRunResult result = driver.GetRunResult().Results[0];
        Assert.Equal(["LISSIC002"], result.Diagnostics.Select(d => d.Id));
        var fitlers = result.GeneratedSources.Where(source => source.HintName == "Lissic.Filters.g.cs").FirstOrDefault();
        Assert.DoesNotContain("BadFilter", fitlers.SourceText.ToString());
    }

    [Fact]
    public void DuplicateFilterName_WarnsAndKeepsTheFirstTypeByName()
    {
        GeneratorDriver driver = SnapshotRunner.Run("""
using Lissic;
using System;
using System.Buffers;

namespace Tests;

[FilterName("same")]
public class BFilter : IFilterFactory
{
    public static ITemplateFilter Create(ReadOnlySpan<string> args) => new BFilter();
    public void Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output) { }
}

[FilterName("SAME")]
public class AFilter : IFilterFactory
{
    public static ITemplateFilter Create(ReadOnlySpan<string> args) => new AFilter();
    public void Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output) { }
}
""");

        GeneratorRunResult result = driver.GetRunResult().Results[0];
        Assert.Equal(["LISSIC003"], result.Diagnostics.Select(d => d.Id));
        string registration = result.GeneratedSources.Single(source => source.HintName == "Lissic.Filters.g.cs").SourceText.ToString();
        Assert.Contains("Register<global::Tests.AFilter>", registration);
        Assert.DoesNotContain("BFilter", registration);
    }

    [Fact]
    public void PrivateNestedFilter_ReportsError()
    {
        string[] ids = DiagnosticIds("""
using Lissic;
using System;
using System.Buffers;

namespace Tests;

public class Outer
{
    [FilterName("hidden")]
    private class HiddenFilter : IFilterFactory
    {
        public static ITemplateFilter Create(ReadOnlySpan<string> args) => new HiddenFilter();
        public void Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output) { }
    }
}
""");

        Assert.Equal(["LISSIC004"], ids);
    }

    [Fact]
    public void ValidTemplateAndFilters_ReportNoDiagnostics()
    {
        Assert.Empty(DiagnosticIds(FullTemplate));
    }

    private const string TemplateHeader = """
using Lissic;
using System;
using System.Buffers;

namespace Tests;

[FilterName("known")]
public class KnownFilter : IFilterFactory
{
    public static ITemplateFilter Create(ReadOnlySpan<string> args) => new KnownFilter();
    public void Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output) { }
}

""";

    [Theory]
    [InlineData("{{ Name <upper }}")]
    [InlineData("{{ Name <known(1,)> }}")]
    [InlineData("{{ Name <known | > }}")]
    [InlineData("{{ Name <known> trailing }}")]
    [InlineData("{{ part")]
    public void MalformedTemplate_ReportsErrorAndGeneratesNoTemplateSource(string template)
    {
        GeneratorDriver driver = SnapshotRunner.Run($$$"""
{{{TemplateHeader}}}
[TemplateText(@"{{{template}}}")]
public partial class WelcomeMessage { }
""");

        GeneratorRunResult result = driver.GetRunResult().Results[0];
        Diagnostic diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "LISSIC005");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("WelcomeMessage", diagnostic.GetMessage());
        Assert.DoesNotContain(result.GeneratedSources, source => source.HintName == "Lissic.WelcomeMessage.g.cs");
    }

    [Fact]
    public void UnknownFilter_WarnsPerUseButStillGeneratesSource()
    {
        GeneratorDriver driver = SnapshotRunner.Run($$$"""
{{{TemplateHeader}}}
[TemplateText(@"{{ Name <known | nope> }} {{ branch.color[red=>Other <missing(1)>;] }}")]
public partial class WelcomeMessage { }
""");

        GeneratorRunResult result = driver.GetRunResult().Results[0];
        Diagnostic[] unknown = result.Diagnostics.Where(d => d.Id == "LISSIC006").ToArray();
        Assert.Equal(2, unknown.Length);
        Assert.All(unknown, d => Assert.Equal(DiagnosticSeverity.Warning, d.Severity));
        Assert.Contains("'nope'", unknown[0].GetMessage());
        Assert.Contains("'missing'", unknown[1].GetMessage());
        Assert.Contains(result.GeneratedSources, source => source.HintName == "Lissic.WelcomeMessage.g.cs");
    }

    [Fact]
    public void KnownFilters_AreMatchedCaseInsensitivelyWithoutDiagnostics()
    {
        string[] ids = DiagnosticIds($$$"""
{{{TemplateHeader}}}
[TemplateText(@"{{ Name <KNOWN(""x"") | Known> }}")]
public partial class WelcomeMessage { }
""");

        Assert.Empty(ids);
    }

    [Fact]
    public void PathTemplate_MissingFile_ReportsOnlyMissingDiagnostic()
    {
        GeneratorDriver driver = SnapshotRunner.Run($$$"""
{{{TemplateHeader}}}
[TemplatePath("missing.html")]
public partial class WelcomeMessage { }
""", templateFiles: new Dictionary<string, string> { ["other.html"] = "Hello" });

        GeneratorRunResult result = driver.GetRunResult().Results[0];
        Diagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("LISSIC007", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("missing.html", diagnostic.GetMessage());
        Assert.Empty(result.GeneratedSources.Where(source => source.HintName == "Lissic.WelcomeMessage.g.cs"));
    }

    [Fact]
    public void PathTemplate_EmptyFile_ReportsOnlyEmptyDiagnostic()
    {
        GeneratorDriver driver = SnapshotRunner.Run($$$"""
{{{TemplateHeader}}}
[TemplatePath("empty.html")]
public partial class WelcomeMessage { }
""", templateFiles: new Dictionary<string, string> { ["empty.html"] = "" });

        Diagnostic diagnostic = Assert.Single(driver.GetRunResult().Diagnostics);
        Assert.Equal("LISSIC008", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("empty.html", diagnostic.GetMessage());
    }

    [Fact]
    public void PathTemplate_ExistingFile_ReportsNoDiagnostics()
    {
        GeneratorDriver driver = SnapshotRunner.Run($$$"""
{{{TemplateHeader}}}
[TemplatePath("sub/hello.html")]
public partial class WelcomeMessage { }
""", templateFiles: new Dictionary<string, string> { ["sub/hello.html"] = "Hello" });

        GeneratorRunResult result = driver.GetRunResult().Results[0];
        Assert.Empty(result.Diagnostics);
        Assert.Contains(result.GeneratedSources, source => source.HintName == "Lissic.WelcomeMessage.g.cs");
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        for (int index = text.IndexOf(value, StringComparison.Ordinal); index != -1; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
            count++;
        return count;
    }
        
    private static string[] DiagnosticIds(string source) =>
        SnapshotRunner.Run(source).GetRunResult().Diagnostics.Select(d => d.Id).ToArray();

}


public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifySourceGenerators.Initialize();
        DiffTools.UseOrder(DiffTool.VisualStudioCode);
    }
}


public static class SnapshotRunner
{
    public static Task Verify(string template, bool ignoreRequired = false)
        => Verifier.Verify(Run(template, ignoreRequired));

    public static string GetSource(string template, string hintSuffix, bool ignoreRequired = false)
        => Run(template, ignoreRequired).GetRunResult().GeneratedTrees.Single(tree => tree.FilePath.EndsWith(hintSuffix)).ToString();

    public static GeneratorDriver Run(string template, bool ignoreRequired = false, IReadOnlyDictionary<string, string>? templateFiles = null)
        => Run(template, ignoreRequired, templateFiles, out _);

    public static Diagnostic[] GetCompilationErrors(string template, bool ignoreRequired = false)
    {
        Run(template, ignoreRequired, null, out Compilation output);
        return output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
    }

    private static GeneratorDriver Run(string template, bool ignoreRequired, IReadOnlyDictionary<string, string>? templateFiles, out Compilation outputCompilation)
    {
        IEnumerable<PortableExecutableReference> references =
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                // Don't load the source gen assembly because a real application won't have it either
                .Where(p => Path.GetFileNameWithoutExtension(p) != "LissicGen")
                .Select(p => MetadataReference.CreateFromFile(p));

        CSharpParseOptions parseOptions = new(preprocessorSymbols: ["DEBUG"]);
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(template, parseOptions);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "Tests",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        Dictionary<string, string> globalOptions = new()
        {
            ["build_property.LissicIgnoreRequiredProperties"] = ignoreRequired ? "true" : "false",
        };

        // Only set when files are supplied so existing snapshots keep their default manifest roots.
        List<AdditionalText> additionalTexts = [];
        if (templateFiles is not null)
        {
            string projectDir = Path.Combine(Path.GetTempPath(), "LissicGenTests");
            globalOptions["build_property.MSBuildProjectDirectory"] = projectDir;
            globalOptions["build_property.LissicTemplateRoot"] = "Templates";
            foreach (KeyValuePair<string, string> file in templateFiles)
                additionalTexts.Add(new InMemoryText(Path.Combine(projectDir, "Templates", file.Key), file.Value));
        }

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new LissicTemplateGenerator().AsSourceGenerator()],
            additionalTexts: additionalTexts,
            parseOptions: parseOptions,
            optionsProvider: new TestOptionsProvider(globalOptions));
        return driver.RunGeneratorsAndUpdateCompilation(compilation, out outputCompilation, out _);
    }
}


sealed class InMemoryText(string path, string content) : AdditionalText
{
    public override string Path { get; } = path;
    public override SourceText? GetText(CancellationToken cancellationToken = default) => SourceText.From(content);
}


sealed class TestOptions(Dictionary<string, string> values) : AnalyzerConfigOptions
{
    public static readonly TestOptions Empty = new([]);
    public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) => values.TryGetValue(key, out value);
}


sealed class TestOptionsProvider(Dictionary<string, string> global) : AnalyzerConfigOptionsProvider
{
    public override AnalyzerConfigOptions GlobalOptions { get; } = new TestOptions(global);
    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => TestOptions.Empty;
    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => TestOptions.Empty;
}
