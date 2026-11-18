using Microsoft.CodeAnalysis;

namespace Tests.Lissic.SourceGen;

// Source generator tests for the 0.2.0 syntax changes:
// - New syntax generates the same properties as the old syntax
// - Tags inside '_' default options and parentheses are declared
// - Invalid new syntax reports LISSIC005
// - Deprecated syntax still generates source but reports LISSIC009 (DeprecatedSyntax, Warning)
//   for ';' delimiters, '@' literals, '<...>' filters, and unquoted string filter arguments


public class LissicGeneratorV020Tests
{
    private const string TemplateHeader = """
using Lissic;
using System;
using System.Buffers;

namespace Tests;

[FilterName("known")]
public class KnownFilter : IFilterFactory
{
    public static ITemplateFilter Create(ReadOnlySpan<string> args) => new KnownFilter();
    public int Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output) { return 0; }
}

""";

    private const string DeprecatedSyntaxId = "LISSIC009";

    private static GeneratorRunResult RunTemplate(string templateText)
        => SnapshotRunner.Run($$$"""
{{{TemplateHeader}}}
[TemplateText(@"{{{Escape(templateText)}}}")]
public partial class WelcomeMessage { }
""").GetRunResult().Results[0];

    private static string GetTemplateSource(string templateText)
        => SnapshotRunner.GetSource($$$"""
{{{TemplateHeader}}}
[TemplateText(@"{{{Escape(templateText)}}}")]
public partial class WelcomeMessage { }
""", "Lissic.WelcomeMessage.g.cs");

    // Template text is embedded in a C# verbatim string, so quotes must be doubled
    private static string Escape(string templateText) => templateText.Replace("\"", "\"\"");

    private static Diagnostic[] Deprecations(string templateText)
        => RunTemplate(templateText).Diagnostics.Where(d => d.Id == DeprecatedSyntaxId).ToArray();


    /* New syntax: property generation */

    [Fact]
    public void NewSyntaxBranch_DeclaresNodesInsideOptionsAndDefault()
    {
        string source = GetTemplateSource(@"{{ branch.color [red => name, blue => part.sidebar, green => ""<b>{{ extra? }}</b>"", _ => '{{ loop.fallback }}'] }}");

        Assert.Contains("public required BranchValue color { get; set; }", source);
        Assert.Contains("public required VariableValue name { get; set; }", source);
        Assert.Contains("public required PartValue sidebar { get; set; }", source);
        Assert.Contains("public VariableValue extra { get; set; }", source);
        Assert.Contains("public required LoopValue fallback { get; set; }", source);
    }

    [Fact]
    public void NewSyntaxBranch_DefaultCommandIsDeclared()
    {
        string source = GetTemplateSource(@"{{ branch.color [red => ""R"", _ => (fallbackName | known)] }}");

        Assert.Contains("public required VariableValue fallbackName { get; set; }", source);
    }

    [Fact]
    public void NewSyntaxNestedBranches_AreDeclared()
    {
        string source = GetTemplateSource(@"{{ branch.color [red => ""<div>{{ branch.shade [dark => """"Dark"""", _ => name] }}</div>""] }}");

        Assert.Contains("BranchValue color { get; set; }", source);
        Assert.Contains("BranchValue shade { get; set; }", source);
        Assert.Contains("VariableValue name { get; set; }", source);
    }
/*
    [Theory]
    [InlineData(@"{{ branch.outer [a => branch.inner [x => name, y => part.side, _ => '{{ extra? }}'], _ => (loop.items)] }}")]
    [InlineData(@"{{ branch.outer [a => (branch.inner [x => name, y => part.side, _ => '{{ extra? }}']), _ => (loop.items)] }}")]
    public void BranchAsDirectAction_DeclaresNestedBranchAndContents(string template)
    {
        string source = GetTemplateSource(template);

        Assert.Contains("public required BranchValue outer { get; set; }", source);
        Assert.Contains("public required BranchValue inner { get; set; }", source);
        Assert.Contains("public required VariableValue name { get; set; }", source);
        Assert.Contains("public required PartValue side { get; set; }", source);
        Assert.Contains("public VariableValue extra { get; set; }", source);
        Assert.Contains("public required LoopValue items { get; set; }", source);
    }

    [Fact]
    public void BranchAsDirectAction_OptionalInnerBranch()
    {
        string source = GetTemplateSource(@"{{ branch.outer [a => branch.inner? [x => ""X""]] }}");

        Assert.Contains("public BranchValue inner { get; set; }", source);
    }

    [Fact]
    public void BranchAsDirectAction_UnknownFilterInInnerBranchIsReported()
    {
        GeneratorRunResult result = RunTemplate(@"{{ branch.outer [a => branch.inner [x => Name | nope]] }}");

        Assert.Contains(result.Diagnostics, d => d.Id == "LISSIC006" && d.GetMessage().Contains("'nope'"));
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == DeprecatedSyntaxId);
    }
*/
    [Fact]
    public void ParenthesizedTags_AreDeclared()
    {
        string source = GetTemplateSource(@"{{ (part.header) }} {{ (loop.items?) }} {{ (name | known) }} {{ (plain) }} {{ (branch.b [x => ""X""]) }}");

        Assert.Contains("public required PartValue header { get; set; }", source);
        Assert.Contains("public LoopValue items { get; set; }", source);
        Assert.Contains("public required VariableValue name { get; set; }", source);
        Assert.Contains("public required VariableValue plain { get; set; }", source);
        Assert.Contains("public required BranchValue b { get; set; }", source);
    }

    [Fact]
    public void QuotedLiteralText_IsNotDeclaredAsProperties()
    {
        string source = GetTemplateSource(@"{{ branch.color [red => ""notAProperty"", blue => 'alsoNot'] }}");

        Assert.DoesNotContain("notAProperty", source);
        Assert.DoesNotContain("alsoNot", source);
    }

    [Fact]
    public void NewAndOldSyntax_GenerateEquivalentProperties()
    {
        string oldSource = GetTemplateSource(@"{{ Name <known(""x"")> }} {{ branch.color[red=>part.side;blue=>@<b>{{ extra? }}</b>;] }}");
        string newSource = GetTemplateSource(@"{{ Name | known(""x"") }} {{ branch.color[red => part.side, blue => ""<b>{{ extra? }}</b>""] }}");

        Assert.Equal(oldSource, newSource);
    }

    [Fact]
    public void NewSyntaxTemplate_Compiles()
    {
        Assert.Empty(SnapshotRunner.GetCompilationErrors($$$"""
{{{TemplateHeader}}}
[TemplateText(@"{{ (Name | known(5, ""x"")) }} {{ branch.color [red => part.side, _ => '{{ extra? }}'] }} {{ (loop.items) }}")]
public partial class WelcomeMessage { }
"""));
    }


    /* New syntax: filter diagnostics */

    [Fact]
    public void NewSyntax_KnownFilters_ReportNoDiagnostics()
    {
        Assert.Empty(RunTemplate(@"{{ Name | KNOWN(""x"") | Known }} {{ (Other | known(1, -2.5)) }} {{ branch.b [a => Third | known, _ => """"] }}").Diagnostics);
    }

    [Fact]
    public void NewSyntax_UnknownFilter_WarnsPerUseButStillGeneratesSource()
    {
        GeneratorRunResult result = RunTemplate(@"{{ Name | known | nope }} {{ branch.color [red => Other | missing(1), _ => (Third | gone)] }}");

        Diagnostic[] unknown = result.Diagnostics.Where(d => d.Id == "LISSIC006").ToArray();
        Assert.Equal(3, unknown.Length);
        Assert.All(unknown, d => Assert.Equal(DiagnosticSeverity.Warning, d.Severity));
        Assert.Contains(unknown, d => d.GetMessage().Contains("'nope'"));
        Assert.Contains(unknown, d => d.GetMessage().Contains("'missing'"));
        Assert.Contains(unknown, d => d.GetMessage().Contains("'gone'"));
        Assert.Contains(result.GeneratedSources, source => source.HintName == "Lissic.WelcomeMessage.g.cs");
    }


    /* New syntax: parse errors */

    [Theory]
    [InlineData(@"{{ Name <known> | known }}")]
    [InlineData(@"{{ branch.c [a => x; b => y,] }}")]
    [InlineData(@"{{ branch.c [a=>@A; b => ""B""] }}")]
    [InlineData(@"{{ branch.c [a => ""A"", a => ""B""] }}")]
    [InlineData(@"{{ branch.c [a=>@A;a=>@B;] }}")]
    [InlineData(@"{{ branch.c [_ => ""A"", _ => ""B""] }}")]
    [InlineData(@"{{ Name | known(abc) }}")]
    [InlineData(@"{{ Name | known(10, left) }}")]
    [InlineData(@"{{ (Name | known }}")]
    [InlineData(@"{{ branch.c [a => ""unclosed] }}")]
    [InlineData(@"{{ part.p | known }}")]
    [InlineData(@"{{ (Name) | known }}")]
    [InlineData(@"{{ (Name | known) | known }}")]
    [InlineData(@"{{ branch.c [a => (Name) | known] }}")]
    [InlineData(@"{{ branch.c [a => branch.d [x => ""X"", b => ""B""] }}")]
    [InlineData(@"{{ branch.c [a => branch.d, b => ""B""] }}")]
    public void MalformedNewSyntax_ReportsErrorAndGeneratesNoTemplateSource(string template)
    {
        // Note: templates are embedded in a C# verbatim string, so "" in the InlineData becomes "" in source, which is " in the template
        GeneratorRunResult result = RunTemplate(template);

        Diagnostic diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "LISSIC005");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("WelcomeMessage", diagnostic.GetMessage());
        Assert.DoesNotContain(result.GeneratedSources, source => source.HintName == "Lissic.WelcomeMessage.g.cs");
    }

    // Inputs that once threw non-parsing exceptions inside the parser.  These must report LISSIC005
    // rather than crash the generator (a crash surfaces as result.Exception / CS8785).
    [Theory]
    [InlineData(@"text {{")]
    [InlineData(@"text {{   ")]
    [InlineData(@"{{(}}")]
    [InlineData(@"{{Name|known(""a""}}")]
    [InlineData(@"{{Name|known('a'}}")]
    [InlineData(@"{{ branch.c [a => Name|known(""a""] }}")]
    public void MalformedTemplate_ReportsErrorInsteadOfCrashing(string template)
    {
        GeneratorRunResult result = RunTemplate(template);

        Assert.Null(result.Exception);
        Diagnostic diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "LISSIC005");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("WelcomeMessage", diagnostic.GetMessage());
    }

    // Whether empty tags are allowed is undecided, so only the generator crash is checked
    [Theory]
    [InlineData(@"{{}}")]
    [InlineData(@"{{ }}")]
    [InlineData(@"{{ () }}")]
    [InlineData(@"{{ branch.c [a => (), b => y] }}")]
    public void EmptyTagsAndParentheses_DoNotCrashGenerator(string template)
    {
        Assert.Null(RunTemplate(template).Exception);
    }

    [Theory]
    [InlineData(@"{{ Name | known( }}")]
    [InlineData(@"{{Name|known(}}")]
    [InlineData(@"{{Name|known(""a|""}}")]
    [InlineData(@"{{Name|known('|'}}")]
    public void FilterArgsCutOffAtEndOfTag_ReportErrorInsteadOfCrashing(string template)
    {
        GeneratorRunResult result = RunTemplate(template);

        Assert.Null(result.Exception);
        Assert.Single(result.Diagnostics, d => d.Id == "LISSIC005");
    }

    [Theory]
    [InlineData(@"{{ branch.c [a => (Name) , b => (part.p) ] }}")]
    [InlineData(@"{{ branch.c [a => (Name | known) , _ => (loop.l) ] }}")]
    [InlineData(@"{{ branch.c [a => (""text"") , _ => ('other')] }}")]
    public void ParenthesizedActionsAndLiterals_ReportNoDiagnostics(string template)
    {
        Assert.Empty(RunTemplate(template).Diagnostics);
    }

    // A literal mis-parsed as a variable produces no generator diagnostic, only uncompilable generated code
    [Theory]
    [InlineData(@"{{ branch.c [a => ""text"", _ => 'other'] }}")]
    [InlineData(@"{{ branch.c [a => (""text""), _ => ('other')] }}")]
    [InlineData(@"{{ branch.c [a => (""Hi {{ Name }}""), _ => (""""), ] }}")]
    [InlineData(@"{{ branch.c [a => (""don't get confused ,""), b => (part.p)] }}")]
    public void ParenthesizedLiterals_GeneratedCodeCompiles(string template)
    {
        Diagnostic[] errors = SnapshotRunner.GetCompilationErrors($$$"""
{{{TemplateHeader}}}
[TemplateText(@"{{{Escape(template)}}}")]
public partial class WelcomeMessage { }
""");

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(@"{{ branch.c [a => x] }}<script>f(a, b => b)</script>")]
    [InlineData(@"{{ branch.c [a => ""x""] }} a, b => c")]
    [InlineData(@"{{ branch.c [a => x, _ => """"] }} a, b => c")]
    public void CommaAndFatArrowAfterBranch_ReportNoDiagnostics(string template)
    {
        Assert.Empty(RunTemplate(template).Diagnostics);
    }

    [Theory]
    [InlineData(@"{{ Name | known(""}}"") }}")]
    [InlineData(@"{{ Name | known('}}') }}")]
    [InlineData(@"{{ (Name | known(""a }} b"")) }}")]
    [InlineData(@"{{ branch.c [a => Name | known(""}}""), _ => """"] }}")]
    public void ClosingTagInsideQuotedArg_ReportsNoDiagnostics(string template)
    {
        Assert.Empty(RunTemplate(template).Diagnostics);
    }


    /* Deprecated syntax: LISSIC009 */

    [Theory]
    [InlineData(@"{{ Name }}")]
    [InlineData(@"{{ Name | known }}")]
    [InlineData(@"{{ Name | known(""x"", 'y', 5, -1.5) }}")]
    [InlineData(@"{{ (Name | known) }}")]
    [InlineData(@"{{ part.p }} {{ loop.l }} {{ (part.q) }}")]
    [InlineData(@"{{ branch.c [a => x, b => part.p, c => ""text"", d => 'text', _ => """"] }}")]
    [InlineData(@"{{ branch.c [a => ""x; y @z <w>""] }}")]
    [InlineData(@"{{ branch.c [a => ""{{ branch.d [b => """"B""""] }}""] }}")]
    [InlineData(@"{{ branch.c [a => branch.d [b => ""B"", _ => x], _ => (branch.e [c => y])] }}")]
    public void NewSyntax_ReportsNoDeprecation(string template)
    {
        Assert.Empty(Deprecations(template));
    }

    // Asserts no diagnostics at all (not just no LISSIC009) so a parse error can't make these pass trivially
    [Theory]
    [InlineData(@"{{ branch.c [a => x] }} Hello; world")]
    [InlineData(@"{{ branch.c [a => x, _ => """"] }}<style>p { color: red; }</style>")]
    [InlineData(@"{{ branch.c [a => x, b => ""x;y""] }}")]
    [InlineData(@"{{ branch.c [a => x, b => 'x;y'] }}")]
    [InlineData(@"{{ branch.c [a => Name | known("";""), _ => """"] }}")]
    public void NewSyntax_SemicolonsOutsideOptionDelimiters_ReportNoDiagnostics(string template)
    {
        Assert.Empty(RunTemplate(template).Diagnostics);
    }

    [Theory]
    [InlineData(@"{{ Name <known> }}")]
    [InlineData(@"{{ Name <known(""x"")> }}")]
    [InlineData(@"{{ Name <known(x)> }}")]
    [InlineData(@"{{ branch.c[a=>x;] }}")]
    [InlineData(@"{{ branch.c[a=>@x;] }}")]
    [InlineData(@"{{ branch.c[_=>@x;] }}")]
    [InlineData(@"{{ branch.c[a=>Name <known>;] }}")]
    // Deprecated syntax nested inside a new syntax literal is still reported
    [InlineData(@"{{ branch.c [a => ""{{ Name <known> }}""] }}")]
    [InlineData(@"{{ branch.c [a => ""{{ branch.d[b=>@B;] }}""] }}")]
    public void DeprecatedSyntax_ReportsWarningAndStillGeneratesSource(string template)
    {
        GeneratorRunResult result = RunTemplate(template);

        Diagnostic[] deprecations = result.Diagnostics.Where(d => d.Id == DeprecatedSyntaxId).ToArray();
        Assert.NotEmpty(deprecations);
        Assert.All(deprecations, d => Assert.Equal(DiagnosticSeverity.Warning, d.Severity));
        Assert.All(deprecations, d => Assert.Contains("WelcomeMessage", d.GetMessage()));
        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains(result.GeneratedSources, source => source.HintName == "Lissic.WelcomeMessage.g.cs");
    }

    [Fact]
    public void DeprecatedSyntax_DescriptorHasExpectedMetadata()
    {
        Diagnostic diagnostic = Assert.Single(Deprecations(@"{{ Name <known> }}"));

        Assert.Equal(DeprecatedSyntaxId, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Descriptor.DefaultSeverity);
        Assert.True(diagnostic.Descriptor.IsEnabledByDefault);
    }

    [Fact]
    public void DeprecatedSyntax_UnquotedStringArgument_IsReportedInAdditionToAngleBrackets()
    {
        int quoted = Deprecations(@"{{ Name <known(""x"")> }}").Length;
        int unquoted = Deprecations(@"{{ Name <known(x)> }}").Length;

        Assert.True(unquoted > quoted, $"Unquoted string argument should add a deprecation (quoted: {quoted}, unquoted: {unquoted}).");
    }

    [Fact]
    public void DeprecatedSyntax_UnquotedNumberArgument_IsNotReportedAsAnUnquotedString()
    {
        int quoted = Deprecations(@"{{ Name <known(""5"", ""x"")> }}").Length;
        int number = Deprecations(@"{{ Name <known(5, ""x"")> }}").Length;
        int negativeDecimal = Deprecations(@"{{ Name <known(-1.5, ""x"")> }}").Length;

        Assert.True(quoted > 0, "'<...>' filters should be reported as deprecated.");
        Assert.Equal(quoted, number);
        Assert.Equal(quoted, negativeDecimal);
    }

    [Fact]
    public void DeprecatedSyntax_AtLiteral_IsReportedInAdditionToSemicolons()
    {
        int command = Deprecations(@"{{ branch.c[a=>x;] }}").Length;
        int literal = Deprecations(@"{{ branch.c[a=>@x;] }}").Length;

        Assert.True(literal > command, $"'@' literal should add a deprecation (command: {command}, literal: {literal}).");
    }

    [Fact]
    public void DeprecatedSyntax_EachDeprecatedTagIsReported()
    {
        int one = Deprecations(@"{{ Name <known> }}").Length;
        int two = Deprecations(@"{{ Name <known> }} {{ Other <known> }}").Length;
        int mixed = Deprecations(@"{{ Name <known> }} {{ Other | known }}").Length;

        Assert.True(one > 0, "'<...>' filters should be reported as deprecated.");
        Assert.Equal(one * 2, two);
        Assert.Equal(one, mixed);
    }

    [Fact]
    public void DeprecatedSyntax_DoesNotAffectOtherDiagnostics()
    {
        GeneratorRunResult result = RunTemplate(@"{{ Name <known | nope> }}");

        Assert.Contains(result.Diagnostics, d => d.Id == DeprecatedSyntaxId);
        Assert.Single(result.Diagnostics, d => d.Id == "LISSIC006");
    }

    [Fact]
    public void DeprecatedSyntax_PathTemplate_IsReported()
    {
        GeneratorDriver driver = SnapshotRunner.Run($$$"""
{{{TemplateHeader}}}
[TemplatePath("old.html")]
public partial class WelcomeMessage { }
""", templateFiles: new Dictionary<string, string> { ["old.html"] = "{{ Name <known> }} {{ branch.c[a=>@A;] }}" });

        GeneratorRunResult result = driver.GetRunResult().Results[0];
        Assert.Contains(result.Diagnostics, d => d.Id == DeprecatedSyntaxId);
        Assert.Contains(result.GeneratedSources, source => source.HintName == "Lissic.WelcomeMessage.g.cs");
    }

    [Fact]
    public void NewSyntax_PathTemplate_ReportsNoDiagnostics()
    {
        GeneratorDriver driver = SnapshotRunner.Run($$$"""
{{{TemplateHeader}}}
[TemplatePath("new.html")]
public partial class WelcomeMessage { }
""", templateFiles: new Dictionary<string, string> { ["new.html"] = "{{ Name | known }} {{ branch.c [a => \"A\", _ => 'D'] }}" });

        Assert.Empty(driver.GetRunResult().Results[0].Diagnostics);
    }
}
