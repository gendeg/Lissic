using Lissic.Parsing;
using System.Text;

namespace Lissic.Tests.Parsing;


public class ParseTemplateTests
{
    [Fact]
    public void Parse_PlainText_ReturnsSingleHtmlNode()
    {
        ITemplateNode[] nodes = LissicParser.Parse("Hello World", "TestTemplate");
        HtmlNode html = Assert.IsType<HtmlNode>(Assert.Single(nodes));
        Assert.Equal("Hello World", Encoding.UTF8.GetString(html.Html.Span));
    }

    [Fact]
    public void Parse_EmptyString_ReturnsNoNodes()
    {
        Assert.Empty(LissicParser.Parse("".AsSpan(), "TestTemplate"));
    }

    [Fact]
    public void Parse_CommandOnly_ReturnsSingleCommandNodeWithNoSurroundingHtml()
    {
        ITemplateNode[] nodes = LissicParser.Parse("{{part.name}}", "TestTemplate");
        PartNode cmd = Assert.IsType<PartNode>(Assert.Single(nodes));
        Assert.Equal("name", cmd.Name.ToString());
    }

    [Fact]
    public void Parse_TextCommandText_ReturnsThreeNodesInOrder()
    {
        ITemplateNode[] nodes = LissicParser.Parse("A{{x}}B", "TestTemplate");

        Assert.Equal(3, nodes.Length);
        Assert.Equal("A", Encoding.UTF8.GetString(Assert.IsType<HtmlNode>(nodes[0]).Html.Span));
        Assert.IsType<VariableNode>(nodes[1]);
        Assert.Equal("B", Encoding.UTF8.GetString(Assert.IsType<HtmlNode>(nodes[2]).Html.Span));
    }

    [Fact]
    public void Parse_ConsecutiveCommands_NoHtmlNodeBetween()
    {
        ITemplateNode[] nodes = LissicParser.Parse("{{a}}{{b}}", "TestTemplate");

        Assert.Equal(2, nodes.Length);
        Assert.All(nodes, n => Assert.IsType<VariableNode>(n));
    }

    [Fact]
    public void ParseTag_VariableWithoutFilters_HasNoFilters()
    {
        VariableNode variable = Assert.IsType<VariableNode>(LissicParser.ParseTag("name", "TestTemplate", 0));

        Assert.Empty(variable.Filters);
    }

    [Fact]
    public void ParseTag_SingleFilterWithoutArgs()
    {
        VariableNode variable = Assert.IsType<VariableNode>(LissicParser.ParseTag(" name <upper> ", "TestTemplate", 0));

        FilterCall filter = Assert.Single(variable.Filters);
        Assert.Equal("name", variable.Name);
        Assert.Equal("upper", filter.Name);
        Assert.Empty(filter.Args);
    }

    [Fact]
    public void ParseTag_FilterChain_KeepsOrderAndArgs()
    {
        VariableNode variable = Assert.IsType<VariableNode>(
            LissicParser.ParseTag("name <lower(\"x\") | pad(\"42\", 'down') | upper()>", "TestTemplate", 0));

        Assert.Collection(variable.Filters,
            f => { Assert.Equal("lower", f.Name); Assert.Equal(["x"], f.Args); },
            f => { Assert.Equal("pad", f.Name); Assert.Equal(["42", "down"], f.Args); },
            f => { Assert.Equal("upper", f.Name); Assert.Empty(f.Args); });
    }

    [Fact]
    public void ParseTag_BareArgsAreAccepted()
    {
        VariableNode variable = Assert.IsType<VariableNode>(LissicParser.ParseTag("name <pad(10, left)>", "TestTemplate", 0));

        Assert.Equal(["10", "left"], Assert.Single(variable.Filters).Args);
    }

    [Fact]
    public void ParseTag_QuotedArgsMayContainDelimiters()
    {
        VariableNode variable = Assert.IsType<VariableNode>(LissicParser.ParseTag("name <join(\"a,b\", 'c|d', \">\")>", "TestTemplate", 0));

        Assert.Equal(["a,b", "c|d", ">"], Assert.Single(variable.Filters).Args);
    }

    [Fact]
    public void ParseTag_OptionalVariableWithFilters()
    {
        VariableNode variable = Assert.IsType<VariableNode>(LissicParser.ParseTag("city? <trim>", "TestTemplate", 0));

        Assert.Equal("city", variable.Name);
        Assert.False(variable.Required);
        Assert.Equal("trim", Assert.Single(variable.Filters).Name);
    }

    [Theory]
    [InlineData("name <upper")]
    [InlineData("name <upper |>")]
    [InlineData("name <| upper>")]
    [InlineData("name <pad(1>")]
    [InlineData("name <pad(1,)>")]
    [InlineData("name <pad(,1)>")]
    [InlineData("name <pad(\"1)>")]
    [InlineData("name <pad(1) x>")]
    [InlineData("name <upper> trailing")]
    public void ParseTag_MalformedFilters_Throw(string tag)
    {
        Assert.Throws<TemplateParsingException>(() => LissicParser.ParseTag(tag, "TestTemplate", 0));
    }

    [Fact]
    public void Parse_RuntimeMode_ParsesFilters()
    {
        ITemplateNode[] nodes = LissicParser.Parse("a {{ name <upper> }} b", "TestTemplate");
        VariableNode variable = Assert.Single(nodes.OfType<VariableNode>());

        Assert.Equal("upper", Assert.Single(variable.Filters).Name);
    }

    [Fact]
    public void Parse_SourceGenMode_ParsesFiltersToo()
    {
        ITemplateNode[] nodes = LissicParser.Parse("a {{ name? <upper(\"x\")> }} b", "TestTemplate", isSourceGen: true);
        VariableNode variable = Assert.Single(nodes.OfType<VariableNode>());

        Assert.Equal("name", variable.Name);
        Assert.False(variable.Required);
        FilterCall filter = Assert.Single(variable.Filters);
        Assert.Equal("upper", filter.Name);
        Assert.Equal(["x"], filter.Args);
    }

    [Fact]
    public void Parse_SourceGenMode_ParsesFiltersInsideBranchEntries()
    {
        ITemplateNode[] nodes = LissicParser.Parse("{{branch.color[red=>name <upper>;]}}", "TestTemplate", isSourceGen: true);
        BranchNode branch = Assert.IsType<BranchNode>(Assert.Single(nodes));
        VariableNode variable = Assert.IsType<VariableNode>(Assert.Single(branch.BranchEntries["red"]));

        Assert.Equal("upper", Assert.Single(variable.Filters).Name);
    }

    [Theory]
    [InlineData("name", typeof(VariableNode))]
    [InlineData("part.name", typeof(PartNode))]
    [InlineData("subTemplate.name", typeof(PartNode))]
    [InlineData("loop.name", typeof(LoopNode))]
    [InlineData("forEach.name", typeof(LoopNode))]
    public void ParseTag_RecognizesAllCommandTypes(string command, Type expected)
    {
        ITemplateNode node = LissicParser.ParseTag(command.AsSpan(), "TestTemplate", 0);
        Assert.IsType(expected, node);
        switch (node)
        {
            case VariableNode variable:
                Assert.Equal("name", variable.Name.ToString());
                break;
            case PartNode part:
                Assert.Equal("name", part.Name.ToString());
                break;
            case LoopNode loop:
                Assert.Equal("name", loop.Name.ToString());
                break;
        }
    }

    [Fact]
    public void ParseTag_TypeIsCaseInsensitive()
    {
        ITemplateNode node = LissicParser.ParseTag("PART.name", "TestTemplate", 0);
        PartNode part = Assert.IsType<PartNode>(node);
        Assert.Equal("name", part.Name.ToString());
    }

    [Fact]
    public void ParseTag_TrimsWhitespaceAroundTypeAndValue_ButPreservesValueCase()
    {
        ITemplateNode node = LissicParser.ParseTag("  part.Name  ", "TestTemplate", 0);
        PartNode part = Assert.IsType<PartNode>(node);
        Assert.Equal("Name", part.Name.ToString());
    }

    [Fact]
    public void ParseTag_UnknownType_ThrowsWithTypeInMessage()
    {
        TemplateParsingException ex = Assert.Throws<TemplateParsingException>(
            () => LissicParser.ParseTag("bogus.name".AsSpan(), "TestTemplate", 0));
        Assert.Contains("bogus", ex.Message);
    }

    [Theory]
    [InlineData("name?", typeof(VariableNode))]
    [InlineData("part.name?", typeof(PartNode))]
    [InlineData("loop.name?", typeof(LoopNode))]
    public void ParseTag_IgnoresQuestionMark(string command, Type expected)
    {
        ITemplateNode node = LissicParser.ParseTag(command.AsSpan(), "TestTemplate", 0);
        Assert.IsType(expected, node);
        switch (node)
        {
            case VariableNode variable:
                Assert.Equal("name", variable.Name.ToString());
                break;
            case PartNode part:
                Assert.Equal("name", part.Name.ToString());
                break;
            case LoopNode loop:
                Assert.Equal("name", loop.Name.ToString());
                break;
        }
    }

    [Fact]
    public void Parse_UnclosedCommand_Throws()
    {
        Assert.Throws<TemplateParsingException>(() => LissicParser.Parse("{{name", "TestTemplate"));
    }

    [Fact]
    public void Parse_ConvertsSpanToHtmlNode()
    {
        ITemplateNode[] nodes = LissicParser.Parse("plain text", "TestTemplate");
        HtmlNode html = Assert.IsType<HtmlNode>(Assert.Single(nodes));
        Assert.Equal("plain text", Encoding.UTF8.GetString(html.Html.Span));
    }
}


public class ParseBranchTests
{
    [Fact]
    public void Parse_BranchWithLiteralOption_ProducesBranchNodeWithExpectedNameAndEntry()
    {
        ITemplateNode[] nodes = LissicParser.Parse("{{branch.color[red=>@Red;]}}", "TestTemplate");
        BranchNode branch = Assert.IsType<BranchNode>(Assert.Single(nodes));

        Assert.Equal("color", branch.BranchName.ToString());
        Assert.True(branch.BranchEntries.ContainsKey("red"));
    }

    [Fact]
    public void Parse_BranchWithLiteralOption_EntryContentMatchesLiteralText()
    {
        ITemplateNode[] nodes = LissicParser.Parse("{{branch.color[red=>@Red;]}}", "TestTemplate");
        BranchNode branch = Assert.IsType<BranchNode>(Assert.Single(nodes));

        ITemplateNode[] entry = branch.BranchEntries["red"];
        HtmlNode html = Assert.IsType<HtmlNode>(Assert.Single(entry));
        Assert.Equal("Red", Encoding.UTF8.GetString(html.Html.Span));
    }

    [Fact]
    public void Parse_BranchWithMultipleOptions_ProducesEntryForEach()
    {
        ITemplateNode[] nodes = LissicParser.Parse("{{branch.color[red=>@Red;blue=>@Blue;]}}", "TestTemplate");
        BranchNode branch = Assert.IsType<BranchNode>(Assert.Single(nodes));

        Assert.Equal(2, branch.BranchEntries.Count);
    }

    [Fact]
    public void Parse_BranchWithMultipleOptions_SecondEntryContentMatchesItsOwnLiteralText()
    {
        ITemplateNode[] nodes = LissicParser.Parse("{{branch.color[red=>@Red;blue=>@Blue;]}}", "TestTemplate");
        BranchNode branch = Assert.IsType<BranchNode>(Assert.Single(nodes));

        ITemplateNode[] entry = branch.BranchEntries["blue"];
        HtmlNode html = Assert.IsType<HtmlNode>(Assert.Single(entry));
        Assert.Equal("Blue", Encoding.UTF8.GetString(html.Html.Span));
    }

    [Fact]
    public void Parse_BranchOptionWithCommandValue_ParsesAsCommandNode()
    {
        ITemplateNode[] nodes = LissicParser.Parse("{{branch.color[red=>name;]}}", "TestTemplate");
        BranchNode branch = Assert.IsType<BranchNode>(Assert.Single(nodes));

        ITemplateNode[] entry = branch.BranchEntries["red"];
        VariableNode cmd = Assert.IsType<VariableNode>(Assert.Single(entry));
        Assert.Equal("name", cmd.Name);
    }

    [Fact]
    public void Parse_BranchWithCommands_ParsesCorrectly()
    {
        ITemplateNode[] nodes = LissicParser.Parse("{{branch.option[one=>part.red;two=>loop.blue;three=>yellow;]}}", "TestTemplate");
        BranchNode branch = Assert.IsType<BranchNode>(Assert.Single(nodes));
        Assert.Equal(3, branch.BranchEntries.Count);

        ITemplateNode[] one = branch.BranchEntries["one"];
        PartNode cmd = Assert.IsType<PartNode>(Assert.Single(one));
        Assert.Equal("red", cmd.Name);

        ITemplateNode[] two = branch.BranchEntries["two"];
        LoopNode cmd2 = Assert.IsType<LoopNode>(Assert.Single(two));
        Assert.Equal("blue", cmd2.Name);

        ITemplateNode[] three = branch.BranchEntries["three"];
        VariableNode cmd3 = Assert.IsType<VariableNode>(Assert.Single(three));
        Assert.Equal("yellow", cmd3.Name);
    }

    [Fact]
    public void Parse_BranchMissingOptionsBracket_Throws()
    {
        Assert.Throws<TemplateParsingException>(() => LissicParser.Parse("{{branch.color}}", "TestTemplate"));
    }

    [Fact]
    public async Task Parse_BranchLiteralWithUnclosedOuterTag_Throws()
    {
        Assert.Throws<TemplateParsingException>(() => LissicParser.Parse("{{branch.x[key=>@{{foo}}text;", "TestTemplate"));
    }

    [Fact]
    public void Parse_BranchMissingClosingOptionsBracket_Throws()
    {
        Assert.Throws<TemplateParsingException>(() => LissicParser.Parse("{{branch.color[red=>name;}}", "TestTemplate"));
    }

    [Fact]
    public void Parse_BranchUnexpectedContentAfterOptionsBracket_Throws()
    {
        Assert.Throws<TemplateParsingException>(() => LissicParser.Parse("{{branch.color[red=>name;]unexpected}}", "TestTemplate"));
    }

    [Theory]
    [InlineData("{{branch.[red=>name;]}}")]
    [InlineData("{{part.}}")]
    [InlineData("{{part.?}}")]
    public void Parse_EmptyBranchNameOrCommandValue_Throws(string template)
    {
        Assert.Throws<TemplateParsingException>(() => LissicParser.Parse(template, "TestTemplate"));
    }

    [Fact]
    public void Parse_BranchUnclosed_Throws()
    {
        Assert.Throws<TemplateParsingException>(() => LissicParser.Parse("{{branch.color[red=>@Red;]", "TestTemplate"));
    }

    [Fact]
    public void Parse_BranchOptionKey_TrimsWhitespace()
    {
        ITemplateNode[] nodes = LissicParser.Parse("{{branch.color[ red => name;]}}", "TestTemplate");
        BranchNode branch = Assert.IsType<BranchNode>(Assert.Single(nodes));

        Assert.True(branch.BranchEntries.ContainsKey("red"));
    }

    [Theory]
    [InlineData("{{branch.color[=>@Empty;]}}")]
    [InlineData("{{branch.color[   =>@Empty;]}}")]
    public void Parse_BranchOptionWithEmptyKey_Throws(string template)
    {
        Assert.Throws<TemplateParsingException>(() => LissicParser.Parse(template, "TestTemplate"));
    }

    [Fact]
    public void Parse_BranchLiteralOption_HandlesEscapedSemicolon()
    {
        ITemplateNode[] nodes = LissicParser.Parse("{{branch.color[red=>@a;;b;]}}", "TestTemplate");
        BranchNode branch = Assert.IsType<BranchNode>(Assert.Single(nodes));

        ITemplateNode[] entry = branch.BranchEntries["red"];
        HtmlNode html = Assert.IsType<HtmlNode>(Assert.Single(entry));
        Assert.Equal("a;b", Encoding.UTF8.GetString(html.Html.Span));
    }

    [Fact]
    public void Parse_BranchLiteralOption_EscapedSemicolonAloneRoundTrips()
    {
        ITemplateNode[] nodes = LissicParser.Parse("{{branch.color[red=>@a;;;]}}", "TestTemplate");
        BranchNode branch = Assert.IsType<BranchNode>(Assert.Single(nodes));

        ITemplateNode[] entry = branch.BranchEntries["red"];
        HtmlNode html = Assert.IsType<HtmlNode>(Assert.Single(entry));
        Assert.Equal("a;", Encoding.UTF8.GetString(html.Html.Span));
    }

    [Fact]
    public void Parse_PermissibleWhitespaceInCommandTrimsCorrectly()
    {
        ITemplateNode[] nodes = LissicParser.Parse("{{  branch.color  [red=>name;]}}", "TestTemplate");
        BranchNode branch = Assert.IsType<BranchNode>(Assert.Single(nodes));
        Assert.Equal("color", branch.BranchName);
    }

    [Theory]
    [InlineData("{{branch.color[ red => part.name;]}}", "red")]
    [InlineData("{{branch.color[ blue => part.name; ] }}", "blue")]
    [InlineData("{{ branch.color [ green => part.name ; ] }}", "green")]
    public void Parse_BranchOptions_TrimsWhitespaceCorrectly(string template, string expectedKey)
    {
        ITemplateNode[] nodes = LissicParser.Parse(template, "TestTemplate");
        BranchNode branch = Assert.IsType<BranchNode>(Assert.Single(nodes));

        Assert.Equal("color", branch.BranchName);
        Assert.True(branch.BranchEntries.ContainsKey(expectedKey));
        PartNode command = Assert.IsType<PartNode>(branch.BranchEntries[expectedKey][0]);
        Assert.Equal("name", command.Name);
    }

    [Theory]
    [InlineData("{{branch.color[ html =>@<div></div>; ] }}", "<div></div>")]
    [InlineData("{{branch.color[ html => @<div></div>; ] }}", "<div></div>")]
    [InlineData("{{branch.color[ html => @ <div></div> ; ] }}", " <div></div> ")]
    public void Parse_BranchOptions_TrimsLitteralWhitespaceCorrectly(string template, string expectedResult)
    {
        ITemplateNode[] nodes = LissicParser.Parse(template, "TestTemplate");
        BranchNode branch = Assert.IsType<BranchNode>(Assert.Single(nodes));
        Assert.True(branch.BranchEntries.ContainsKey("html"));
        HtmlNode html = Assert.IsType<HtmlNode>(branch.BranchEntries["html"][0]);
        Assert.Equal(expectedResult, Encoding.UTF8.GetString(html.Html.Span));
    }

    [Fact]
    public void Parse_TrimsWhitespaceCorrectly_MulitpleParts()
    {
        ITemplateNode[] nodes = LissicParser.Parse("{{ branch.color [ red => name ; ] }} some text here {{ branch.colorer [ blue => part.namer ; ] }}", "TestTemplate");
        BranchNode redBranch = Assert.IsType<BranchNode>(nodes[0]);
        HtmlNode textNode = Assert.IsType<HtmlNode>(nodes[1]);
        Assert.Equal(" some text here ", Encoding.UTF8.GetString(textNode.Html.Span));
        BranchNode blueBranch = Assert.IsType<BranchNode>(nodes[2]);

        Assert.Equal("color", redBranch.BranchName);
        Assert.True(redBranch.BranchEntries.ContainsKey("red"));
        VariableNode redCommand = Assert.IsType<VariableNode>(redBranch.BranchEntries["red"][0]);
        Assert.Equal("name", redCommand.Name);

        Assert.Equal("colorer", blueBranch.BranchName);
        Assert.True(blueBranch.BranchEntries.ContainsKey("blue"));
        PartNode blueCommand = Assert.IsType<PartNode>(blueBranch.BranchEntries["blue"][0]);
        Assert.Equal("namer", blueCommand.Name);
    }

    [Theory]
    [InlineData("{{branch .color[red=>part.name;]}}")]
    [InlineData("{{branch. color[red=>part.name;]}}")]
    [InlineData("{{branch.color[red=>part .name;]}}")]
    [InlineData("{{branch.color[red=>part. name;]}}")]
    public void Parse_ImpermissibleWhiteSpace_Throws(string template)
    {
        Assert.Throws<TemplateParsingException>(() => LissicParser.Parse(template, "TestTemplate"));
    }

    [Fact]
    public void Parse_EscapedCharacters_ParsedCorrectly()
    {
        ITemplateNode[] nodes = LissicParser.Parse("{{branch.color[ html =>@<div>@;;escaped;;;;</div>;]}}", "TestTemplate");
        BranchNode branch = Assert.IsType<BranchNode>(nodes[0]);
        Assert.True(branch.BranchEntries.ContainsKey("html"));
        HtmlNode html = Assert.IsType<HtmlNode>(branch.BranchEntries["html"][0]);
        Assert.Equal("<div>@;escaped;;</div>", Encoding.UTF8.GetString(html.Html.Span));
    }

    [Fact]
    public void Parse_NestedBranch_ParsedCorrectly()
    {
        ITemplateNode[] nodes = LissicParser.Parse("{{branch.color[red=>@<div>{{branch.shade[dark=>name;;]}}</div>;]}}", "TestTemplate");
        BranchNode branch = Assert.IsType<BranchNode>(nodes[0]);
        Assert.True(branch.BranchEntries.ContainsKey("red"));

        HtmlNode html = Assert.IsType<HtmlNode>(branch.BranchEntries["red"][0]);
        Assert.Equal("<div>", Encoding.UTF8.GetString(html.Html.Span));
        html = Assert.IsType<HtmlNode>(branch.BranchEntries["red"][2]);
        Assert.Equal("</div>", Encoding.UTF8.GetString(html.Html.Span));

        BranchNode darkBranch = Assert.IsType<BranchNode>(branch.BranchEntries["red"][1]);
        Assert.True(darkBranch.BranchEntries.ContainsKey("dark"));
        VariableNode darkCommand = Assert.IsType<VariableNode>(darkBranch.BranchEntries["dark"][0]);
        Assert.Equal("name", darkCommand.Name);
    }

    [Fact]
    public void ParseTag_IgnoresQuestionMarks()
    {
        ITemplateNode[] nodes = LissicParser.Parse("{{ branch.color? [ red => name? ; ] }} some text here {{ branch.colorer? [ blue => part.namer? ; ] }}", "TestTemplate");
        BranchNode redBranch = Assert.IsType<BranchNode>(nodes[0]);
        HtmlNode textNode = Assert.IsType<HtmlNode>(nodes[1]);
        Assert.Equal(" some text here ", Encoding.UTF8.GetString(textNode.Html.Span));
        BranchNode blueBranch = Assert.IsType<BranchNode>(nodes[2]);

        Assert.Equal("color", redBranch.BranchName);
        Assert.True(redBranch.BranchEntries.ContainsKey("red"));
        VariableNode redCommand = Assert.IsType<VariableNode>(redBranch.BranchEntries["red"][0]);
        Assert.Equal("name", redCommand.Name);

        Assert.Equal("colorer", blueBranch.BranchName);
        Assert.True(blueBranch.BranchEntries.ContainsKey("blue"));
        PartNode blueCommand = Assert.IsType<PartNode>(blueBranch.BranchEntries["blue"][0]);
        Assert.Equal("namer", blueCommand.Name);
    }
}


public class ReadOnlyMemoryComparerTests
{
    [Fact]
    public void Equals_SameByteContentFromDifferentArrays_ReturnsTrue()
    {
        ReadOnlyMemory<byte> a = Encoding.UTF8.GetBytes("red").AsMemory();
        ReadOnlyMemory<byte> b = Encoding.UTF8.GetBytes("red").AsMemory();
        Assert.True(ReadOnlyMemoryComparer.Instance.Equals(a, b));
    }

    [Fact]
    public void Equals_DifferentByteContent_ReturnsFalse()
    {
        ReadOnlyMemory<byte> a = Encoding.UTF8.GetBytes("red").AsMemory();
        ReadOnlyMemory<byte> b = Encoding.UTF8.GetBytes("blue").AsMemory();
        Assert.False(ReadOnlyMemoryComparer.Instance.Equals(a, b));
    }

    [Fact]
    public void GetHashCode_SameByteContentFromDifferentArrays_ProducesSameHash()
    {
        ReadOnlyMemory<byte> a = Encoding.UTF8.GetBytes("red").AsMemory();
        ReadOnlyMemory<byte> b = Encoding.UTF8.GetBytes("red").AsMemory();
        Assert.Equal(ReadOnlyMemoryComparer.Instance.GetHashCode(a), ReadOnlyMemoryComparer.Instance.GetHashCode(b));
    }
}
