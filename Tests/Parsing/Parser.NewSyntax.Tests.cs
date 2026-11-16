using Lissic.Parsing;
using System.Text;

namespace Lissic.Tests.Parsing;

// Tests for the 0.2.0 syntax changes:
// - Branches use ',' delimiter, '_' default, and "..." / '...' literals with ""/'' escapes
// - Filters use '|' instead of '<>', may have whitespace before '(', and string arguments must be quoted
// - Optional parentheses around any tag
// - Old syntax remains supported (deprecated) but may not be mixed with new syntax in the same tag
//
// These tests go through LissicParser.Parse rather than ParseTag/ParseBranch so they don't depend
// on how the parser is split internally.  The representation of the '_' default option is
// intentionally not asserted here; default behavior is verified through rendering tests.


public class ParseBranchV020Tests
{
    private static BranchNode ParseSingleBranch(string template)
        => Assert.IsType<BranchNode>(Assert.Single(LissicParser.Parse(template, "TestTemplate")));

    private static string HtmlText(ITemplateNode node)
        => Encoding.UTF8.GetString(Assert.IsType<HtmlNode>(node).Html.Span);

    private static string SingleHtmlText(ITemplateNode[] entry)
        => HtmlText(Assert.Single(entry));


    /* Delimiters and literals */

    [Theory]
    [InlineData("{{branch.color[red => \"Red\", blue => \"Blue\"]}}")]
    [InlineData("{{branch.color[red => 'Red', blue => 'Blue']}}")]
    [InlineData("{{branch.color[red => \"Red\", blue => 'Blue']}}")]
    [InlineData("{{branch.color[red=>\"Red\",blue=>\"Blue\"]}}")]
    [InlineData("{{ branch.color [ red => \"Red\" , blue => \"Blue\" ] }}")]
    [InlineData("{{branch.color[red => \"Red\", blue => \"Blue\",]}}")]
    [InlineData("{{branch.color[red => \"Red\", blue => \"Blue\" , ]}}")]
    [InlineData("{{branch.color[\n    red => \"Red\",\n    blue => \"Blue\",\n]}}")]
    [InlineData("{{branch.color[\r\n\tred => \"Red\",\r\n\tblue => \"Blue\"\r\n]}}")]
    public void Parse_CommaDelimitedQuotedLiterals_ProducesEntryForEach(string template)
    {
        BranchNode branch = ParseSingleBranch(template);

        Assert.Equal("color", branch.BranchName.ToString());
        Assert.Equal("Red", SingleHtmlText(branch.BranchEntries["red"]));
        Assert.Equal("Blue", SingleHtmlText(branch.BranchEntries["blue"]));
    }

    [Fact]
    public void Parse_SingleOptionWithoutTrailingComma()
    {
        BranchNode branch = ParseSingleBranch("{{branch.color[red => \"Red\"]}}");

        Assert.Equal("Red", SingleHtmlText(branch.BranchEntries["red"]));
    }

    [Fact]
    public void Parse_SwitchAlias_UsesNewSyntax()
    {
        BranchNode branch = ParseSingleBranch("{{switch.color[red => \"Red\", blue => \"Blue\"]}}");

        Assert.Equal("color", branch.BranchName.ToString());
        Assert.Equal(2, branch.BranchEntries.Count);
    }

    [Fact]
    public void Parse_CommandActions_ParseAsTheirNodeTypes()
    {
        BranchNode branch = ParseSingleBranch("{{branch.option[one => part.red, two => loop.blue, three => yellow, four => subTemplate.green, five => forEach.purple]}}");

        Assert.Equal(5, branch.BranchEntries.Count);
        Assert.Equal("red", Assert.IsType<PartNode>(Assert.Single(branch.BranchEntries["one"])).Name);
        Assert.Equal("blue", Assert.IsType<LoopNode>(Assert.Single(branch.BranchEntries["two"])).Name);
        Assert.Equal("yellow", Assert.IsType<VariableNode>(Assert.Single(branch.BranchEntries["three"])).Name);
        Assert.Equal("green", Assert.IsType<PartNode>(Assert.Single(branch.BranchEntries["four"])).Name);
        Assert.Equal("purple", Assert.IsType<LoopNode>(Assert.Single(branch.BranchEntries["five"])).Name);
    }

    [Fact]
    public void Parse_MixedCommandAndLiteralActions()
    {
        BranchNode branch = ParseSingleBranch("{{ branch.memberStatus [ gold => part.goldMessage, member => memberMessage, none => 'Join us!' ] }}");

        Assert.Equal("goldMessage", Assert.IsType<PartNode>(Assert.Single(branch.BranchEntries["gold"])).Name);
        Assert.Equal("memberMessage", Assert.IsType<VariableNode>(Assert.Single(branch.BranchEntries["member"])).Name);
        Assert.Equal("Join us!", SingleHtmlText(branch.BranchEntries["none"]));
    }

    [Fact]
    public void Parse_OptionalMarkers_InNewSyntax()
    {
        BranchNode branch = ParseSingleBranch("{{ branch.color? [ red => name?, blue => part.side? ] }}");

        Assert.False(branch.Required);
        VariableNode variable = Assert.IsType<VariableNode>(Assert.Single(branch.BranchEntries["red"]));
        Assert.Equal("name", variable.Name);
        Assert.False(variable.Required);
        PartNode part = Assert.IsType<PartNode>(Assert.Single(branch.BranchEntries["blue"]));
        Assert.Equal("side", part.Name);
        Assert.False(part.Required);
    }


    /* Quoting and escapes */

    [Theory]
    [InlineData("{{branch.q[a => \"say \"\"hi\"\"\"]}}", "say \"hi\"")]
    [InlineData("{{branch.q[a => 'it''s']}}", "it's")]
    [InlineData("{{branch.q[a => 'say \"hi\"']}}", "say \"hi\"")]
    [InlineData("{{branch.q[a => \"it's\"]}}", "it's")]
    [InlineData("{{branch.q[a => \"\"\"\"]}}", "\"")]
    [InlineData("{{branch.q[a => '''']}}", "'")]
    [InlineData("{{branch.q[a => \"''\"]}}", "''")]
    [InlineData("{{branch.q[a => '\"\"']}}", "\"\"")]
    public void Parse_QuotedLiteral_HandlesEscapes(string template, string expected)
    {
        BranchNode branch = ParseSingleBranch(template);

        Assert.Equal(expected, SingleHtmlText(branch.BranchEntries["a"]));
    }

    [Theory]
    [InlineData("{{branch.q[a => \"x, y\"]}}", "x, y")]
    [InlineData("{{branch.q[a => \"x ] y\"]}}", "x ] y")]
    [InlineData("{{branch.q[a => \"x => y\"]}}", "x => y")]
    [InlineData("{{branch.q[a => \"x; y\"]}}", "x; y")]
    [InlineData("{{branch.q[a => \"x;; y\"]}}", "x;; y")]
    [InlineData("{{branch.q[a => \"@x\"]}}", "@x")]
    [InlineData("{{branch.q[a => \"x | y\"]}}", "x | y")]
    [InlineData("{{branch.q[a => \"x <y> z\"]}}", "x <y> z")]
    [InlineData("{{branch.q[a => \"(x)\"]}}", "(x)")]
    [InlineData("{{branch.q[a => \"x }} y }} z\"]}}", "x }} y }} z")]
    public void Parse_QuotedLiteral_DelimitersAreLiteralText(string template, string expected)
    {
        BranchNode branch = ParseSingleBranch(template);

        Assert.Equal(expected, SingleHtmlText(branch.BranchEntries["a"]));
    }

    [Fact]
    public void Parse_QuotedLiteralContainingClosingTag_DoesNotEndBranch()
    {
        BranchNode branch = ParseSingleBranch("{{branch.greeting[a => \"x }} y\", b => part.sub]}}");

        Assert.Equal("x }} y", SingleHtmlText(branch.BranchEntries["a"]));
        Assert.Equal("sub", Assert.IsType<PartNode>(Assert.Single(branch.BranchEntries["b"])).Name);
    }

    [Theory]
    [InlineData("{{branch.q[a => \" <div></div> \"]}}", " <div></div> ")]
    [InlineData("{{branch.q[a =>\"<div></div>\"]}}", "<div></div>")]
    [InlineData("{{branch.q[a =>     \"<div></div>\"     ]}}", "<div></div>")]
    [InlineData("{{branch.q[a => \"line1\nline2\"]}}", "line1\nline2")]
    [InlineData("{{branch.q[a => '\t']}}", "\t")]
    public void Parse_QuotedLiteral_PreservesInsideWhitespaceAndIgnoresOutside(string template, string expected)
    {
        BranchNode branch = ParseSingleBranch(template);

        Assert.Equal(expected, SingleHtmlText(branch.BranchEntries["a"]));
    }

    [Theory]
    [InlineData("{{branch.q[a => \"\", b => \"B\"]}}")]
    [InlineData("{{branch.q[a => '', b => \"B\"]}}")]
    public void Parse_EmptyQuotedLiteral_IsValidAndEmpty(string template)
    {
        BranchNode branch = ParseSingleBranch(template);

        Assert.True(branch.BranchEntries.ContainsKey("a"));
        Assert.Empty(branch.BranchEntries["a"]);
        Assert.Equal("B", SingleHtmlText(branch.BranchEntries["b"]));
    }


    /* Tags inside literals */

    [Fact]
    public void Parse_QuotedLiteral_ParsesEmbeddedTags()
    {
        BranchNode branch = ParseSingleBranch("{{branch.q[a => \"Hi {{ name }}, see {{ part.footer }}!\"]}}");
        ITemplateNode[] entry = branch.BranchEntries["a"];

        Assert.Equal(5, entry.Length);
        Assert.Equal("Hi ", HtmlText(entry[0]));
        Assert.Equal("name", Assert.IsType<VariableNode>(entry[1]).Name);
        Assert.Equal(", see ", HtmlText(entry[2]));
        Assert.Equal("footer", Assert.IsType<PartNode>(entry[3]).Name);
        Assert.Equal("!", HtmlText(entry[4]));
    }

    [Fact]
    public void Parse_ProposalExample_ParsesAllOptions()
    {
        BranchNode branch = ParseSingleBranch(
            "{{ branch.memberStatus [ gold => part.goldMessage, member => memberMessage, _ => 'We look forward to seeing you!  Join the \"Local Lions\" at {{ signUpLink }}' ] }}");

        Assert.Equal("memberStatus", branch.BranchName.ToString());
        Assert.Equal("goldMessage", Assert.IsType<PartNode>(Assert.Single(branch.BranchEntries["gold"])).Name);
        Assert.Equal("memberMessage", Assert.IsType<VariableNode>(Assert.Single(branch.BranchEntries["member"])).Name);
    }

    [Fact]
    public void Parse_ProposalExampleWithDoubledQuotes_ParsesAllOptions()
    {
        BranchNode branch = ParseSingleBranch(
            "{{ branch.memberStatus [ gold => part.goldMessage, member => memberMessage, _ => \"We look forward to seeing you!  Join the \"\"Local Lions\"\" at {{ signUpLink }}\" ] }}");

        Assert.Equal("goldMessage", Assert.IsType<PartNode>(Assert.Single(branch.BranchEntries["gold"])).Name);
        Assert.Equal("memberMessage", Assert.IsType<VariableNode>(Assert.Single(branch.BranchEntries["member"])).Name);
    }

    [Theory]
    // Same quote type: inner quotes are doubled, unescaped first, and then parsed (same as ';;' today)
    [InlineData("{{branch.color[red => \"<div>{{ branch.shade [dark => \"\"Dark\"\", light => name] }}</div>\"]}}")]
    [InlineData("{{branch.color[red => '<div>{{ branch.shade [dark => ''Dark'', light => name] }}</div>']}}")]
    // Different quote type: no escaping needed
    [InlineData("{{branch.color[red => '<div>{{ branch.shade [dark => \"Dark\", light => name] }}</div>']}}")]
    [InlineData("{{branch.color[red => \"<div>{{ branch.shade [dark => 'Dark', light => name] }}</div>\"]}}")]
    // Inner tag may use the deprecated syntax, since it is a different tag
    [InlineData("{{branch.color[red => \"<div>{{ branch.shade [dark => @Dark; light => name;] }}</div>\"]}}")]
    public void Parse_NestedBranchInQuotedLiteral_ParsesCorrectly(string template)
    {
        BranchNode branch = ParseSingleBranch(template);
        ITemplateNode[] entry = branch.BranchEntries["red"];

        Assert.Equal(3, entry.Length);
        Assert.Equal("<div>", HtmlText(entry[0]));
        Assert.Equal("</div>", HtmlText(entry[2]));

        BranchNode shade = Assert.IsType<BranchNode>(entry[1]);
        Assert.Equal("shade", shade.BranchName.ToString());
        Assert.Equal("Dark", SingleHtmlText(shade.BranchEntries["dark"]));
        Assert.Equal("name", Assert.IsType<VariableNode>(Assert.Single(shade.BranchEntries["light"])).Name);
    }

    [Fact]
    public void Parse_DoublyNestedSameQuoteEscapes_ParsesCorrectly()
    {
        // Outer level doubles once, second level doubles again
        BranchNode branch = ParseSingleBranch(
            "{{branch.a[x => \"{{ branch.b [y => \"\"{{ branch.c [z => \"\"\"\"Deep\"\"\"\"] }}\"\"] }}\"]}}");

        BranchNode b = Assert.IsType<BranchNode>(Assert.Single(branch.BranchEntries["x"]));
        BranchNode c = Assert.IsType<BranchNode>(Assert.Single(b.BranchEntries["y"]));
        Assert.Equal("Deep", SingleHtmlText(c.BranchEntries["z"]));
    }


    /* Keys */

    [Theory]
    [InlineData("{{branch.q[ red => \"R\"]}}", "red")]
    [InlineData("{{branch.q[red=>\"R\"]}}", "red")]
    [InlineData("{{branch.q[\n  red   =>  \"R\"\n]}}", "red")]
    [InlineData("{{branch.q[two words => \"R\"]}}", "two words")]
    [InlineData("{{branch.q[True => \"R\"]}}", "True")]
    [InlineData("{{branch.q[42 => \"R\"]}}", "42")]
    public void Parse_BareKeys_AreTrimmedAndCaseSensitive(string template, string expectedKey)
    {
        BranchNode branch = ParseSingleBranch(template);

        Assert.Equal(expectedKey, Assert.Single(branch.BranchEntries.Keys));
    }

    [Theory]
    // Keys are always bare; quotes are part of the key text
    [InlineData("{{branch.q[\"_\" => \"R\"]}}", "\"_\"")]
    [InlineData("{{branch.q['red' => \"R\"]}}", "'red'")]
    [InlineData("{{branch.q[\"red\" => \"R\"]}}", "\"red\"")]
    public void Parse_QuotedKeys_KeepTheirQuotes(string template, string expectedKey)
    {
        BranchNode branch = ParseSingleBranch(template);

        Assert.Equal(expectedKey, Assert.Single(branch.BranchEntries.Keys));
    }

    [Fact]
    public void Parse_OldSyntaxUnderscore_IsARegularKey()
    {
        BranchNode branch = ParseSingleBranch("{{branch.q[_=>@Underscore;red=>@Red;]}}");

        Assert.Equal("Underscore", SingleHtmlText(branch.BranchEntries["_"]));
        Assert.Equal("Red", SingleHtmlText(branch.BranchEntries["red"]));
    }


    /* Default option */

    [Theory]
    [InlineData("{{branch.q[red => \"R\", _ => \"D\"]}}")]
    [InlineData("{{branch.q[_ => \"D\", red => \"R\"]}}")]
    [InlineData("{{branch.q[red => \"R\", _ => \"D\",]}}")]
    [InlineData("{{branch.q[red => \"R\", _ => name]}}")]
    [InlineData("{{branch.q[red => \"R\", _ => part.fallback]}}")]
    [InlineData("{{branch.q[red => \"R\", _ => (name | upper)]}}")]
    [InlineData("{{branch.q[red => \"R\",_=>\"D\"]}}")]
    public void Parse_DefaultOption_IsAccepted(string template)
    {
        BranchNode branch = ParseSingleBranch(template);

        Assert.Equal("R", SingleHtmlText(branch.BranchEntries["red"]));
    }

    [Fact]
    public void Parse_DefaultOnly_IsAccepted()
    {
        Assert.IsType<BranchNode>(Assert.Single(LissicParser.Parse("{{branch.q[_ => \"D\"]}}", "TestTemplate")));
    }


    /* Filters and parentheses inside branch actions */

    [Theory]
    [InlineData("{{ branch.status [member => name | upper | append(\", member\"), _ => \"not a member\" ] }}")]
    [InlineData("{{ branch.status [member => (name | upper | append(\", member\")), _ => \"not a member\" ] }}")]
    [InlineData("{{ branch.status [member => ( name | upper | append( \", member\" ) ), _ => \"not a member\" ] }}")]
    [InlineData("{{ branch.status [member => name|upper|append(\", member\"),_ => \"not a member\"] }}")]
    [InlineData("{{ branch.status [member => name | upper | append(', member'), _ => 'not a member' ] }}")]
    public void Parse_FilteredVariableAction_EndsAtCommaOutsideQuotesAndParentheses(string template)
    {
        BranchNode branch = ParseSingleBranch(template);
        VariableNode variable = Assert.IsType<VariableNode>(Assert.Single(branch.BranchEntries["member"]));

        Assert.Equal("name", variable.Name);
        Assert.Collection(variable.Filters,
            f => { Assert.Equal("upper", f.Name); Assert.Empty(f.Args); },
            f => { Assert.Equal("append", f.Name); Assert.Equal([", member"], f.Args); });
    }

    [Fact]
    public void Parse_FilterWithMultipleArgsAction_CommasInArgsDoNotEndOption()
    {
        BranchNode branch = ParseSingleBranch("{{ branch.s [a => name | slice(5, 30), b => name | replace(\"x\", \"y\")] }}");

        Assert.Equal(["5", "30"], Assert.Single(Assert.IsType<VariableNode>(Assert.Single(branch.BranchEntries["a"])).Filters).Args);
        Assert.Equal(["x", "y"], Assert.Single(Assert.IsType<VariableNode>(Assert.Single(branch.BranchEntries["b"])).Filters).Args);
    }

    [Fact]
    public void Parse_ParenthesizedTagActions_ParseAsTheirNodeTypes()
    {
        BranchNode branch = ParseSingleBranch("{{ branch.s [a => (part.header), b => (loop.items), c => (name), d => (part.side?)] }}");

        Assert.Equal("header", Assert.IsType<PartNode>(Assert.Single(branch.BranchEntries["a"])).Name);
        Assert.Equal("items", Assert.IsType<LoopNode>(Assert.Single(branch.BranchEntries["b"])).Name);
        Assert.Equal("name", Assert.IsType<VariableNode>(Assert.Single(branch.BranchEntries["c"])).Name);
        PartNode side = Assert.IsType<PartNode>(Assert.Single(branch.BranchEntries["d"]));
        Assert.Equal("side", side.Name);
        Assert.False(side.Required);
    }

    [Fact]
    public void Parse_FiltersInsideBranchEntries_SourceGenMode()
    {
        ITemplateNode[] nodes = LissicParser.Parse("{{branch.color[red => name | upper]}}", "TestTemplate", isSourceGen: true);
        BranchNode branch = Assert.IsType<BranchNode>(Assert.Single(nodes));
        VariableNode variable = Assert.IsType<VariableNode>(Assert.Single(branch.BranchEntries["red"]));

        Assert.Equal("upper", Assert.Single(variable.Filters).Name);
    }


    /* Branches as direct actions of other branches */

    [Theory]
    [InlineData("{{branch.outer[a => branch.inner [x => \"X\", y => name], b => \"B\"]}}")]
    [InlineData("{{branch.outer[a => (branch.inner [x => \"X\", y => name]), b => \"B\"]}}")]
    [InlineData("{{branch.outer[a => switch.inner [x => \"X\", y => name], b => \"B\"]}}")]
    [InlineData("{{branch.outer[a => branch.inner[x=>\"X\",y=>name],b=>\"B\"]}}")]
    [InlineData("{{branch.outer[a => branch.inner [x => \"X\", y => name,], b => \"B\",]}}")]
    [InlineData("{{ branch.outer [\n    a => branch.inner [\n        x => \"X\",\n        y => name,\n    ],\n    b => \"B\"\n] }}")]
    [InlineData("{{ (branch.outer [a => (branch.inner [x => \"X\", y => name]), b => \"B\"]) }}")]
    public void Parse_BranchAsDirectAction_ParsesNestedBranch(string template)
    {
        BranchNode outer = ParseSingleBranch(template);

        Assert.Equal("outer", outer.BranchName.ToString());
        Assert.Equal(2, outer.BranchEntries.Count);
        Assert.Equal("B", SingleHtmlText(outer.BranchEntries["b"]));

        BranchNode inner = Assert.IsType<BranchNode>(Assert.Single(outer.BranchEntries["a"]));
        Assert.Equal("inner", inner.BranchName.ToString());
        Assert.Equal(2, inner.BranchEntries.Count);
        Assert.Equal("X", SingleHtmlText(inner.BranchEntries["x"]));
        Assert.Equal("name", Assert.IsType<VariableNode>(Assert.Single(inner.BranchEntries["y"])).Name);
    }

    [Fact]
    public void Parse_BranchAsDirectAction_InnerDelimitersInQuotesDoNotEndOptions()
    {
        BranchNode outer = ParseSingleBranch("{{branch.outer[a => branch.inner [x => \"1, ] }} 2\", y => '=>'], b => \"B\"]}}");

        BranchNode inner = Assert.IsType<BranchNode>(Assert.Single(outer.BranchEntries["a"]));
        Assert.Equal("1, ] }} 2", SingleHtmlText(inner.BranchEntries["x"]));
        Assert.Equal("=>", SingleHtmlText(inner.BranchEntries["y"]));
        Assert.Equal("B", SingleHtmlText(outer.BranchEntries["b"]));
    }

    [Fact]
    public void Parse_BranchAsDirectAction_InnerFilteredActions()
    {
        BranchNode outer = ParseSingleBranch("{{branch.outer[a => branch.inner [x => name | append(\", \") | upper, y => (other | trim)], b => \"B\"]}}");

        BranchNode inner = Assert.IsType<BranchNode>(Assert.Single(outer.BranchEntries["a"]));
        VariableNode x = Assert.IsType<VariableNode>(Assert.Single(inner.BranchEntries["x"]));
        Assert.Collection(x.Filters,
            f => { Assert.Equal("append", f.Name); Assert.Equal([", "], f.Args); },
            f => Assert.Equal("upper", f.Name));
        Assert.Equal("trim", Assert.Single(Assert.IsType<VariableNode>(Assert.Single(inner.BranchEntries["y"])).Filters).Name);
    }

    [Theory]
    [InlineData("{{branch.outer[a => branch.inner [x => \"X\", _ => \"ID\"], _ => \"OD\"]}}")]
    [InlineData("{{branch.outer[_ => branch.inner [x => \"X\", _ => \"ID\"], a => \"A\"]}}")]
    public void Parse_BranchAsDirectAction_WithDefaults(string template)
    {
        BranchNode outer = ParseSingleBranch(template);

        Assert.Contains(outer.BranchEntries.Values, entry => entry.Length == 1 && entry[0] is BranchNode);
    }

    [Fact]
    public void Parse_BranchAsDirectAction_Optional()
    {
        BranchNode outer = ParseSingleBranch("{{branch.outer[a => branch.inner? [x => \"X\"]]}}");

        BranchNode inner = Assert.IsType<BranchNode>(Assert.Single(outer.BranchEntries["a"]));
        Assert.Equal("inner", inner.BranchName.ToString());
        Assert.False(inner.Required);
    }

    [Fact]
    public void Parse_BranchAsDirectAction_DeeplyNested()
    {
        BranchNode a = ParseSingleBranch("{{branch.a[x => branch.b [y => branch.c [z => \"Deep\"], w => \"W\"], v => \"V\"]}}");

        BranchNode b = Assert.IsType<BranchNode>(Assert.Single(a.BranchEntries["x"]));
        BranchNode c = Assert.IsType<BranchNode>(Assert.Single(b.BranchEntries["y"]));
        Assert.Equal("Deep", SingleHtmlText(c.BranchEntries["z"]));
        Assert.Equal("W", SingleHtmlText(b.BranchEntries["w"]));
        Assert.Equal("V", SingleHtmlText(a.BranchEntries["v"]));
    }

    [Fact]
    public void Parse_BranchAsDirectAction_SourceGenMode()
    {
        ITemplateNode[] nodes = LissicParser.Parse("{{branch.outer[a => branch.inner [x => name | upper]]}}", "TestTemplate", isSourceGen: true);
        BranchNode outer = Assert.IsType<BranchNode>(Assert.Single(nodes));
        BranchNode inner = Assert.IsType<BranchNode>(Assert.Single(outer.BranchEntries["a"]));
        VariableNode variable = Assert.IsType<VariableNode>(Assert.Single(inner.BranchEntries["x"]));

        Assert.Equal("upper", Assert.Single(variable.Filters).Name);
    }


    /* Surrounding text */

    [Fact]
    public void Parse_MultipleNewSyntaxBranches_WithTextBetween()
    {
        ITemplateNode[] nodes = LissicParser.Parse("{{ branch.a [x => \"X\"] }} middle {{ branch.b [y => part.p, _ => \"\"] }}", "TestTemplate");

        Assert.Equal(3, nodes.Length);
        Assert.Equal("X", SingleHtmlText(Assert.IsType<BranchNode>(nodes[0]).BranchEntries["x"]));
        Assert.Equal(" middle ", HtmlText(nodes[1]));
        Assert.Equal("p", Assert.IsType<PartNode>(Assert.Single(Assert.IsType<BranchNode>(nodes[2]).BranchEntries["y"])).Name);
    }

    [Fact]
    public void Parse_OldAndNewSyntaxBranches_InSameTemplate()
    {
        ITemplateNode[] nodes = LissicParser.Parse("{{ branch.a [x => \"X\"] }}{{ branch.b [y=>@Y;] }}", "TestTemplate");

        Assert.Equal(2, nodes.Length);
        Assert.Equal("X", SingleHtmlText(Assert.IsType<BranchNode>(nodes[0]).BranchEntries["x"]));
        Assert.Equal("Y", SingleHtmlText(Assert.IsType<BranchNode>(nodes[1]).BranchEntries["y"]));
    }


    /* Errors */

    [Theory]
    // Unclosed quotes
    [InlineData("{{branch.q[a => \"Red]}}")]
    [InlineData("{{branch.q[a => 'Red]}}")]
    [InlineData("{{branch.q[a => \"Red']}}")]
    [InlineData("{{branch.q[a => \"Red\"\"]}}")]
    // Missing delimiter or unexpected text after a literal
    [InlineData("{{branch.q[a => \"Red\" b => \"Blue\"]}}")]
    [InlineData("{{branch.q[a => \"Red\" x, b => \"Blue\"]}}")]
    // Empty keys or actions
    [InlineData("{{branch.q[ => \"Red\"]}}")]
    [InlineData("{{branch.q[a => , b => \"Blue\"]}}")]
    [InlineData("{{branch.q[a => ]}}")]
    [InlineData("{{branch.q[a => \"Red\",, b => \"Blue\"]}}")]
    [InlineData("{{branch.q[,]}}")]
    // Unclosed brackets, parentheses, or tag
    [InlineData("{{branch.q[a => \"Red\"}}")]
    [InlineData("{{branch.q[a => \"Red\",}}")]
    [InlineData("{{branch.q[a => \"Red\"]")]
    [InlineData("{{branch.q[a => (name | upper, b => \"Blue\"]}}")]
    [InlineData("{{branch.q[a => name | upper), b => \"Blue\"]}}")]
    [InlineData("{{branch.q[a => \"Red\"] unexpected}}")]
    // Parentheses in actions must surround the complete tag statement
    [InlineData("{{branch.q[a => (name) | upper, b => \"Blue\"]}}")]
    [InlineData("{{branch.q[a => (name | upper) | lower, b => \"Blue\"]}}")]
    [InlineData("{{branch.q[a => (name)?, b => \"Blue\"]}}")]
    // Malformed branches used directly as actions
    [InlineData("{{branch.q[a => branch.inner, b => \"Blue\"]}}")]
    [InlineData("{{branch.q[a => branch.inner [x => \"X\", b => \"Blue\"]}}")]
    [InlineData("{{branch.q[a => branch.inner [x => \"X\"] junk, b => \"Blue\"]}}")]
    [InlineData("{{branch.q[a => branch.inner [x => \"X\"] branch.other [y => \"Y\"]]}}")]
    [InlineData("{{branch.q[a => (branch.inner [x => \"X\"], b => \"Blue\"]}}")]
    [InlineData("{{branch.q[a => branch.inner [x => \"X\", x => \"Y\"]]}}")]
    [InlineData("{{branch.q[a => branch.inner [_ => \"X\", _ => \"Y\"]]}}")]
    [InlineData("{{branch.q[a => branch.inner [x => \"X\"; y => \"Y\"]]}}")]
    public void Parse_MalformedNewSyntax_Throws(string template)
    {
        Assert.Throws<TemplateParsingException>(() => LissicParser.Parse(template, "TestTemplate"));
    }

    [Theory]
    [InlineData("{{branch.q[a => \"A\", a => \"B\"]}}")]
    [InlineData("{{branch.q[a => \"A\", b => \"B\", a => part.x]}}")]
    [InlineData("{{branch.q[a=>@A;a=>@B;]}}")]
    [InlineData("{{branch.q[_=>@A;_=>@B;]}}")]
    public void Parse_DuplicateKeys_Throw(string template)
    {
        Assert.Throws<TemplateParsingException>(() => LissicParser.Parse(template, "TestTemplate"));
    }

    [Theory]
    [InlineData("{{branch.q[_ => \"A\", _ => \"B\"]}}")]
    [InlineData("{{branch.q[_ => \"A\", a => \"X\", _ => name]}}")]
    public void Parse_DuplicateDefaults_Throw(string template)
    {
        Assert.Throws<TemplateParsingException>(() => LissicParser.Parse(template, "TestTemplate"));
    }

    [Fact]
    public void Parse_DuplicateKeys_MessageNamesKeyAndBranch()
    {
        TemplateParsingException ex = Assert.Throws<TemplateParsingException>(
            () => LissicParser.Parse("{{branch.color[shade => \"A\", shade => \"B\"]}}", "TestTemplate"));

        Assert.Contains("shade", ex.Message);
        Assert.Contains("color", ex.Message);
    }

    [Theory]
    // ';' delimiter mixed with ',' delimiter
    [InlineData("{{branch.q[a => x; b => y,]}}")]
    [InlineData("{{branch.q[a => x, b => y;]}}")]
    // '@' literal mixed with quoted literal
    [InlineData("{{branch.q[a=>@A; b => \"B\"]}}")]
    [InlineData("{{branch.q[a => \"A\", b=>@B,]}}")]
    // quoted literals with ';' delimiter
    [InlineData("{{branch.q[a => \"A\"; b => \"B\";]}}")]
    [InlineData("{{branch.q[a => 'A';]}}")]
    // '@' literal with ',' delimiter
    [InlineData("{{branch.q[a => @A, b => @B,]}}")]
    // new syntax filters inside old syntax branch, and old filters inside new syntax branch
    [InlineData("{{branch.q[a => name | upper;]}}")]
    [InlineData("{{branch.q[a => name <upper>, b => \"B\"]}}")]
    // default option is not supported in old syntax, so '_' with ';' is a normal key, but mixing still fails
    [InlineData("{{branch.q[a=>@A; _ => \"D\"]}}")]
    public void Parse_MixedOldAndNewSyntaxInOneBranch_Throws(string template)
    {
        Assert.Throws<TemplateParsingException>(() => LissicParser.Parse(template, "TestTemplate"));
    }
}


public class ParseFiltersV020Tests
{
    private static VariableNode ParseSingleVariable(string template)
        => Assert.IsType<VariableNode>(Assert.Single(LissicParser.Parse(template, "TestTemplate")));


    /* Pipe syntax */

    [Theory]
    [InlineData("{{ name | upper }}")]
    [InlineData("{{name|upper}}")]
    [InlineData("{{   name   |   upper   }}")]
    [InlineData("{{ name | upper() }}")]
    [InlineData("{{ name\n|\nupper\n}}")]
    public void Parse_SingleFilterWithoutArgs(string template)
    {
        VariableNode variable = ParseSingleVariable(template);

        Assert.Equal("name", variable.Name);
        FilterCall filter = Assert.Single(variable.Filters);
        Assert.Equal("upper", filter.Name);
        Assert.Empty(filter.Args);
    }

    [Fact]
    public void Parse_FilterChain_KeepsOrderAndArgs()
    {
        VariableNode variable = ParseSingleVariable("{{ name | lower(\"x\") | pad(42, 'down') | upper() }}");

        Assert.Collection(variable.Filters,
            f => { Assert.Equal("lower", f.Name); Assert.Equal(["x"], f.Args); },
            f => { Assert.Equal("pad", f.Name); Assert.Equal(["42", "down"], f.Args); },
            f => { Assert.Equal("upper", f.Name); Assert.Empty(f.Args); });
    }

    [Fact]
    public void Parse_FilterArgsAcrossLines()
    {
        VariableNode variable = ParseSingleVariable("{{name|replace(\n    \"a\",\n    \"b\"\n)}}");

        Assert.Equal(["a", "b"], Assert.Single(variable.Filters).Args);
    }

    [Fact]
    public void Parse_OptionalVariableWithFilters()
    {
        VariableNode variable = ParseSingleVariable("{{ city? | trim }}");

        Assert.Equal("city", variable.Name);
        Assert.False(variable.Required);
        Assert.Equal("trim", Assert.Single(variable.Filters).Name);
    }

    [Fact]
    public void Parse_VariableWithoutFilters_HasNoFilters()
    {
        Assert.Empty(ParseSingleVariable("{{ name }}").Filters);
    }

    [Fact]
    public void Parse_SourceGenMode_ParsesPipeFilters()
    {
        ITemplateNode[] nodes = LissicParser.Parse("a {{ name? | upper(\"x\") }} b", "TestTemplate", isSourceGen: true);
        VariableNode variable = Assert.Single(nodes.OfType<VariableNode>());

        Assert.Equal("name", variable.Name);
        Assert.False(variable.Required);
        FilterCall filter = Assert.Single(variable.Filters);
        Assert.Equal("upper", filter.Name);
        Assert.Equal(["x"], filter.Args);
    }


    /* Whitespace before parentheses is now allowed (only new syntax) */

    [Theory]
    [InlineData("{{ name | append (\"x\") }}")]
    [InlineData("{{ name | append  (\"x\") }}")]
    [InlineData("{{ name | append\n(\"x\") }}")]
    public void Parse_WhitespaceBeforeArgumentParentheses_IsAllowed(string template)
    {
        FilterCall filter = Assert.Single(ParseSingleVariable(template).Filters);

        Assert.Equal("append", filter.Name);
        Assert.Equal(["x"], filter.Args);
    }


    /* Argument quoting */

    [Theory]
    [InlineData("{{ name | slice(5, 30) }}", new[] { "5", "30" })]
    [InlineData("{{ name | repeat(5) }}", new[] { "5" })]
    [InlineData("{{ name | f(-3) }}", new[] { "-3" })]
    [InlineData("{{ name | f(1.5) }}", new[] { "1.5" })]
    [InlineData("{{ name | f(-0.25) }}", new[] { "-0.25" })]
    [InlineData("{{ name | f(0) }}", new[] { "0" })]
    [InlineData("{{ name | f( 7 , \"x\" ) }}", new[] { "7", "x" })]
    [InlineData("{{ name | f(1-) }}", new[] { "1-" })]  // Some numbering systems allow trailing signs
    public void Parse_UnquotedNumberArgs_AreAccepted(string template, string[] expected)
    {
        Assert.Equal(expected, Assert.Single(ParseSingleVariable(template).Filters).Args);
    }

    [Theory]
    [InlineData("{{ name | pad(10, left) }}")]
    [InlineData("{{ name | append(abc) }}")]
    [InlineData("{{ name | append(the great) }}")]
    [InlineData("{{ name | append(1a) }}")]
    [InlineData("{{ name | append(a1) }}")]
    [InlineData("{{ name | f(--1) }}")]
    [InlineData("{{ name | f(1.2.3) }}")]
    [InlineData("{{ name | f(true) }}")]
    [InlineData("{{ name | f(\"x\", y) }}")]
    public void Parse_UnquotedStringArgs_ThrowInNewSyntax(string template)
    {
        Assert.Throws<TemplateParsingException>(() => LissicParser.Parse(template, "TestTemplate"));
    }

    [Fact]
    public void Parse_UnquotedStringArgs_StillAcceptedInDeprecatedSyntax()
    {
        VariableNode variable = ParseSingleVariable("{{ name <pad(10, left)> }}");

        Assert.Equal(["10", "left"], Assert.Single(variable.Filters).Args);
    }

    [Theory]
    [InlineData("{{ name | append(\"say \"\"hi\"\"\") }}", "say \"hi\"")]
    [InlineData("{{ name | append('it''s') }}", "it's")]
    [InlineData("{{ name | append(\"it's\") }}", "it's")]
    [InlineData("{{ name | append('say \"hi\"') }}", "say \"hi\"")]
    [InlineData("{{ name | append(\"\"\"\") }}", "\"")]
    [InlineData("{{ name | append(\"\") }}", "")]
    [InlineData("{{ name | append('') }}", "")]
    [InlineData("{{ name | append(\" the great\") }}", " the great")]
    [InlineData("{{ name | append(\"5\") }}", "5")]
    public void Parse_QuotedArgs_HandleEscapesAndWhitespace(string template, string expected)
    {
        Assert.Equal([expected], Assert.Single(ParseSingleVariable(template).Filters).Args);
    }

    [Fact]
    public void Parse_QuotedArgsMayContainDelimiters()
    {
        VariableNode variable = ParseSingleVariable("{{ name | join(\"a,b\", 'c|d', \")\", \"(\", \"<>\", \"]\") }}");

        Assert.Equal(["a,b", "c|d", ")", "(", "<>", "]"], Assert.Single(variable.Filters).Args);
    }


    /* Mixing and malformed filters */

    [Theory]
    [InlineData("{{ name <upper> | lower }}")]
    [InlineData("{{ name | upper <lower> }}")]
    [InlineData("{{ name | <upper> }}")]
    [InlineData("{{ name <upper | lower> | trim }}")]
    public void Parse_MixedFilterSyntax_Throws(string template)
    {
        Assert.Throws<TemplateParsingException>(() => LissicParser.Parse(template, "TestTemplate"));
    }

    [Theory]
    [InlineData("{{ name || upper }}")]
    [InlineData("{{ name | | upper }}")]
    [InlineData("{{ | upper }}")]
    [InlineData("{{ name | upper | }}")]
    [InlineData("{{ name | pad(\"1\" }}")]
    [InlineData("{{ name | pad(\"1\",) }}")]
    [InlineData("{{ name | pad(,\"1\") }}")]
    [InlineData("{{ name | pad(\"1) }}")]
    [InlineData("{{ name | pad(\"1\") x }}")]
    [InlineData("{{ name | pad(\"1\" \"2\") }}")]
    public void Parse_MalformedPipeFilters_Throw(string template)
    {
        Assert.Throws<TemplateParsingException>(() => LissicParser.Parse(template, "TestTemplate"));
    }
}


public class ParseParenthesesV020Tests
{
    [Theory]
    [InlineData("{{ (name) }}")]
    [InlineData("{{(name)}}")]
    [InlineData("{{ ( name ) }}")]
    public void Parse_ParenthesizedVariable(string template)
    {
        VariableNode variable = Assert.IsType<VariableNode>(Assert.Single(LissicParser.Parse(template, "TestTemplate")));

        Assert.Equal("name", variable.Name);
        Assert.True(variable.Required);
        Assert.Empty(variable.Filters);
    }

    [Theory]
    [InlineData("{{ (name | upper | append(\" the great\")) }}")]
    [InlineData("{{(name|upper|append(\" the great\"))}}")]
    [InlineData("{{ ( name | upper | append( \" the great\" ) ) }}")]
    [InlineData("{{ (\n  name\n  | upper\n  | append(\" the great\")\n) }}")]
    public void Parse_ParenthesizedFilteredVariable(string template)
    {
        VariableNode variable = Assert.IsType<VariableNode>(Assert.Single(LissicParser.Parse(template, "TestTemplate")));

        Assert.Equal("name", variable.Name);
        Assert.Collection(variable.Filters,
            f => { Assert.Equal("upper", f.Name); Assert.Empty(f.Args); },
            f => { Assert.Equal("append", f.Name); Assert.Equal([" the great"], f.Args); });
    }

    [Fact]
    public void Parse_ParenthesizedOptionalVariable()
    {
        VariableNode variable = Assert.IsType<VariableNode>(Assert.Single(LissicParser.Parse("{{ (name? | upper) }}", "TestTemplate")));

        Assert.Equal("name", variable.Name);
        Assert.False(variable.Required);
        Assert.Equal("upper", Assert.Single(variable.Filters).Name);
    }

    [Theory]
    [InlineData("{{ (part.header) }}", typeof(PartNode), true)]
    [InlineData("{{ (subTemplate.header) }}", typeof(PartNode), true)]
    [InlineData("{{ (part.header?) }}", typeof(PartNode), false)]
    [InlineData("{{ ( part.header ) }}", typeof(PartNode), true)]
    [InlineData("{{ (loop.header) }}", typeof(LoopNode), true)]
    [InlineData("{{ (forEach.header) }}", typeof(LoopNode), true)]
    [InlineData("{{ (loop.header?) }}", typeof(LoopNode), false)]
    public void Parse_ParenthesizedPartsAndLoops(string template, Type expected, bool required)
    {
        ITemplateNode node = Assert.Single(LissicParser.Parse(template, "TestTemplate"));

        Assert.IsType(expected, node);
        switch (node)
        {
            case PartNode part:
                Assert.Equal("header", part.Name);
                Assert.Equal(required, part.Required);
                break;
            case LoopNode loop:
                Assert.Equal("header", loop.Name);
                Assert.Equal(required, loop.Required);
                break;
        }
    }

    [Theory]
    [InlineData("{{ (branch.color [red => \"Red\", _ => \"Other\"]) }}")]
    [InlineData("{{(branch.color[red => \"Red\", _ => \"Other\"])}}")]
    [InlineData("{{ ( switch.color [ red => \"Red\", _ => \"Other\" ] ) }}")]
    public void Parse_ParenthesizedBranch(string template)
    {
        BranchNode branch = Assert.IsType<BranchNode>(Assert.Single(LissicParser.Parse(template, "TestTemplate")));

        Assert.Equal("color", branch.BranchName.ToString());
        Assert.Equal("Red", Encoding.UTF8.GetString(Assert.IsType<HtmlNode>(Assert.Single(branch.BranchEntries["red"])).Html.Span));
    }

    [Fact]
    public void Parse_ParenthesizedOptionalBranch()
    {
        BranchNode branch = Assert.IsType<BranchNode>(Assert.Single(LissicParser.Parse("{{ (branch.color? [red => \"Red\"]) }}", "TestTemplate")));

        Assert.False(branch.Required);
    }

    [Fact]
    public void Parse_ParenthesizedTagsWithSurroundingText()
    {
        ITemplateNode[] nodes = LissicParser.Parse("(a) {{ (name) }} (b)", "TestTemplate");

        Assert.Equal(3, nodes.Length);
        Assert.Equal("(a) ", Encoding.UTF8.GetString(Assert.IsType<HtmlNode>(nodes[0]).Html.Span));
        Assert.Equal("name", Assert.IsType<VariableNode>(nodes[1]).Name);
        Assert.Equal(" (b)", Encoding.UTF8.GetString(Assert.IsType<HtmlNode>(nodes[2]).Html.Span));
    }

    [Theory]
    [InlineData("{{ (name }}")]
    [InlineData("{{ name) }}")]
    [InlineData("{{ (name | upper }}")]
    [InlineData("{{ name | upper) }}")]
    [InlineData("{{ (name) trailing }}")]
    [InlineData("{{ (part.header }}")]
    [InlineData("{{ (loop.items }}")]
    [InlineData("{{ (branch.color [red => \"Red\"] }}")]
    [InlineData("{{ (part .header) }}")]
    [InlineData("{{ (part. header) }}")]
    // Parentheses must surround the complete tag statement
    [InlineData("{{ (name) | upper }}")]
    [InlineData("{{ (name)|upper }}")]
    [InlineData("{{ (name | upper) | lower }}")]
    [InlineData("{{ (name)? }}")]
    [InlineData("{{ (name)? | upper }}")]
    [InlineData("{{ (part.header)? }}")]
    [InlineData("{{ (part.header) | upper }}")]
    [InlineData("{{ (branch.color) [red => \"Red\"] }}")]
    [InlineData("{{ (branch.color [red => \"Red\"]) [blue => \"Blue\"] }}")]
    public void Parse_MalformedParentheses_Throw(string template)
    {
        Assert.Throws<TemplateParsingException>(() => LissicParser.Parse(template, "TestTemplate"));
    }
}
