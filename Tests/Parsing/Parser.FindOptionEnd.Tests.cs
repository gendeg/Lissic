using Lissic.Parsing;

namespace Lissic.Tests.Parsing;

// Tests for LissicParser.FindOptionEnd, which finds the delimiter (',', ']' or ';') that ends a
// branch option, skipping anything inside quotes or parentheses.
//
// Return values:
//   >= 0  index of the delimiter
//   -1    no delimiter found (includes unclosed quotes and unclosed '(')
//   -2    ')' without a matching '('
//
// Most cases are written as (input, expectedPrefix): the delimiter is expected right after
// expectedPrefix, so the expected index is expectedPrefix.Length.  This avoids hand-counted indexes.
public class FindOptionEndTests
{
    private static void AssertEndsAfter(string input, string expectedPrefix)
    {
        Assert.StartsWith(expectedPrefix, input);
        int end = LissicParser.FindOptionEnd(input, 0);
        Assert.Equal(expectedPrefix.Length, end);
        Assert.Contains(input[end], ",];");
    }


    /* Plain delimiters */

    [Theory]
    [InlineData("x, b => y]", "x")]
    [InlineData("x]", "x")]
    [InlineData("x; b=>y;]", "x")]
    [InlineData(" x , b => y]", " x ")]
    [InlineData("part.p, b => y]", "part.p")]
    [InlineData("loop.items?]", "loop.items?")]
    [InlineData(", b => y]", "")]
    [InlineData("]", "")]
    [InlineData(";", "")]
    public static void FindsFirstDelimiter(string input, string expectedPrefix)
        => AssertEndsAfter(input, expectedPrefix);

    [Theory]
    [InlineData("x; y, z]", "x")]
    [InlineData("x, y; z]", "x")]
    [InlineData("x] }} a; b, c", "x")]
    public static void StopsAtTheEarliestOfSeveralDelimiters(string input, string expectedPrefix)
        => AssertEndsAfter(input, expectedPrefix);

    [Theory]
    [InlineData("name\n, b => y]", "name\n")]
    [InlineData("name\r\n\t]", "name\r\n\t")]
    public static void WhitespaceIsNotADelimiter(string input, string expectedPrefix)
        => AssertEndsAfter(input, expectedPrefix);


    /* Quotes */

    [Theory]
    [InlineData("name | f(\"a,b\"), c => d]", "name | f(\"a,b\")")]
    [InlineData("name | f(\"a]b\")]", "name | f(\"a]b\")")]
    [InlineData("name | f(\"a;b\");", "name | f(\"a;b\")")]
    [InlineData("name | f('a,b'), c => d]", "name | f('a,b')")]
    [InlineData("name | f('a]b')]", "name | f('a]b')")]
    [InlineData("name | f('a;b');", "name | f('a;b')")]
    [InlineData("name | f(\";\")]", "name | f(\";\")")]
    [InlineData("name | f(\",\", \"]\", \";\")]", "name | f(\",\", \"]\", \";\")")]
    public static void DelimitersInsideQuotesAreSkipped(string input, string expectedPrefix)
        => AssertEndsAfter(input, expectedPrefix);

    [Theory]
    [InlineData("name | f(\")\"), b => y]", "name | f(\")\")")]
    [InlineData("name | f(\"(\"), b => y]", "name | f(\"(\")")]
    [InlineData("name | f('),('), b => y]", "name | f('),(')")]
    [InlineData("(name | f(\"),(\")), b => y]", "(name | f(\"),(\"))")]
    public static void ParenthesesInsideQuotesAreSkipped(string input, string expectedPrefix)
        => AssertEndsAfter(input, expectedPrefix);

    [Theory]
    [InlineData("name | f(\"it's, here\"), b => y]", "name | f(\"it's, here\")")]
    [InlineData("name | f('say \"a, b\"'), b => y]", "name | f('say \"a, b\"')")]
    [InlineData("name | f(\"'\", ','), b => y]", "name | f(\"'\", ',')")]
    public static void OtherQuoteTypeInsideQuotesIsLiteral(string input, string expectedPrefix)
        => AssertEndsAfter(input, expectedPrefix);

    [Theory]
    [InlineData("name | f(\"\"), b => y]", "name | f(\"\")")]
    [InlineData("name | f(''), b => y]", "name | f('')")]
    [InlineData("name | f(\"\", \",\"), b => y]", "name | f(\"\", \",\")")]
    [InlineData("name | f('', ','), b => y]", "name | f('', ',')")]
    [InlineData("name | f(\"\";\"\"), b => y]", "name | f(\"\";\"\")")]
    public static void EmptyQuotes(string input, string expectedPrefix)
        => AssertEndsAfter(input, expectedPrefix);

    [Theory]
    // Escape in the middle of the quoted text
    [InlineData("name | f(\"a\"\"b, c\"), b => y]", "name | f(\"a\"\"b, c\")")]
    [InlineData("name | f('it''s, here'), b => y]", "name | f('it''s, here')")]
    [InlineData("name | f(\"a\"\",\"), b => y]", "name | f(\"a\"\",\")")]
    // Escape at the start of the quoted text
    [InlineData("name | f(\"\"\"a, b\"), b => y]", "name | f(\"\"\"a, b\")")]
    [InlineData("name | f('''a, b'), b => y]", "name | f('''a, b')")]
    // Escape right before the closing quote
    [InlineData("name | f(\"say \"\"hi\"\"\"), b => y]", "name | f(\"say \"\"hi\"\"\")")]
    [InlineData("name | f('it'''), b => y]", "name | f('it''')")]
    // Quoted text that is only an escaped quote
    [InlineData("name | f(\"\"\"\"), b => y]", "name | f(\"\"\"\")")]
    [InlineData("name | f(''''), b => y]", "name | f('''')")]
    // Several escapes in a row
    [InlineData("name | f(\"\"\"\"\"\"), b => y]", "name | f(\"\"\"\"\"\")")]
    [InlineData("name | f(\"a\"\"\"\"b\"), b => y]", "name | f(\"a\"\"\"\"b\")")]
    // Escaped quote followed by a delimiter that is still inside the quotes
    [InlineData("name | f(\"a\"\";\"\"b\"), b => y]", "name | f(\"a\"\";\"\"b\")")]
    public static void DoubledQuoteEscapesStayInsideQuotes(string input, string expectedPrefix)
        => AssertEndsAfter(input, expectedPrefix);

    [Theory]
    [InlineData("x \"a, b\", c => d]", "x \"a, b\"")]
    [InlineData("\"a, b\", c => d]", "\"a, b\"")]
    [InlineData("'a; b'; c=>d;", "'a; b'")]
    public static void QuotesOutsideParenthesesAreSkipped(string input, string expectedPrefix)
        => AssertEndsAfter(input, expectedPrefix);


    /* Parentheses */

    [Theory]
    [InlineData("(name | upper), b => y]", "(name | upper)")]
    [InlineData("(part.p)]", "(part.p)")]
    [InlineData("name | slice(5, 30), b => y]", "name | slice(5, 30)")]
    [InlineData("name | f(a; b);", "name | f(a; b)")]
    [InlineData("name | f(])]", "name | f(])")]
    [InlineData("name | f( ), b => y]", "name | f( )")]
    public static void DelimitersInsideParenthesesAreSkipped(string input, string expectedPrefix)
        => AssertEndsAfter(input, expectedPrefix);

    [Theory]
    [InlineData("name | f(g(1, 2), 3), b => y]", "name | f(g(1, 2), 3)")]
    [InlineData("((name | f(1, 2))), b => y]", "((name | f(1, 2)))")]
    [InlineData("(name | f(1) | g(2, 3)), b => y]", "(name | f(1) | g(2, 3))")]
    [InlineData("name | f(1), b => g(2, 3)]", "name | f(1)")]
    public static void NestedAndSequentialParentheses(string input, string expectedPrefix)
        => AssertEndsAfter(input, expectedPrefix);


    /* Start position */

    [Theory]
    [InlineData("a => x, b => y]", 5, 6)]
    [InlineData("a => x, b => y]", 13, 14)]
    [InlineData("a => x, b => y]", 6, 6)]
    [InlineData("a => x, b => y]", 14, 14)]
    [InlineData("a => f(\",\"), b => y]", 5, 11)]
    public void StartsSearchingAtStart(string input, int start, int expected)
    {
        Assert.Equal(expected, LissicParser.FindOptionEnd(input, start));
    }

    [Fact]
    public void StartAtEnd_ReturnsNotFound()
    {
        Assert.Equal(-1, LissicParser.FindOptionEnd("x, y", 4));
    }


    /* Not found (-1) */

    [Theory]
    [InlineData("")]
    [InlineData("x")]
    [InlineData("   ")]
    [InlineData("name | f(1, 2)")]
    [InlineData("name | f(\"a, b\")")]
    [InlineData("\"a, b; c]\"")]
    public void NoDelimiter_ReturnsNotFound(string input)
    {
        Assert.Equal(-1, LissicParser.FindOptionEnd(input, 0));
    }

    [Theory]
    [InlineData("name | f(\"a, b]")]
    [InlineData("name | f('a, b]")]
    [InlineData("name | f(\"a\"\", b]")]
    [InlineData("\"")]
    [InlineData("'")]
    [InlineData("\"\"\"")]
    public void UnclosedQuote_ReturnsNotFound(string input)
    {
        Assert.Equal(-1, LissicParser.FindOptionEnd(input, 0));
    }

    [Theory]
    [InlineData("name | f(a, b]")]
    [InlineData("name | f(g(a), b]")]
    [InlineData("(name | upper, b => y]")]
    [InlineData("(")]
    public void UnclosedParenthesis_ReturnsNotFound(string input)
    {
        Assert.Equal(-1, LissicParser.FindOptionEnd(input, 0));
    }


    /* Unmatched ')' (-2) */

    [Theory]
    [InlineData(")")]
    [InlineData("x), b => y]")]
    [InlineData("name | f(a)), b => y]")]
    [InlineData("(name)), b => y]")]
    public void UnmatchedCloseParenthesis_ReturnsError(string input)
    {
        Assert.Equal(-2, LissicParser.FindOptionEnd(input, 0));
    }

    [Theory]
    [InlineData("x, y)")]
    [InlineData("x] }} (a) b)")]
    public void CloseParenthesisAfterTheDelimiter_IsIgnored(string input)
    {
        Assert.Equal(1, LissicParser.FindOptionEnd(input, 0));
    }
}


// The same edge cases checked through LissicParser.Parse, so they stay covered if FindOptionEnd is
// renamed, inlined, or its return codes change.
public class BranchOptionEndV020Tests
{
    private static BranchNode ParseSingleBranch(string template)
        => Assert.IsType<BranchNode>(Assert.Single(LissicParser.Parse(template, "TestTemplate")));

    private static string[] SingleFilterArgs(BranchNode branch, string key)
        => Assert.Single(Assert.IsType<VariableNode>(Assert.Single(branch.BranchEntries[key])).Filters).Args;

    [Theory]
    [InlineData("{{ branch.c [a => name | f(\"say \"\"hi\"\"\"), _ => \"\"] }}", "say \"hi\"")]
    [InlineData("{{ branch.c [a => name | f(\"\"\"\"), _ => \"\"] }}", "\"")]
    [InlineData("{{ branch.c [a => name | f(''''), _ => ''] }}", "'")]
    [InlineData("{{ branch.c [a => name | f(\"a\"\", b\"), _ => \"\"] }}", "a\", b")]
    [InlineData("{{ branch.c [a => name | f(\"a\"\"; b\"), _ => \"\"] }}", "a\"; b")]
    public void Parse_EscapedQuotesInFilterArgument_EndOptionCorrectly(string template, string expectedArg)
    {
        BranchNode branch = ParseSingleBranch(template);

        Assert.Equal(2, branch.BranchEntries.Count);
        Assert.Equal([expectedArg], SingleFilterArgs(branch, "a"));
    }

    [Theory]
    [InlineData("{{ branch.c [a => name | f(\"x, y\", ')'), b => z] }}", "x, y", ")")]
    [InlineData("{{ branch.c [a => name | f(\"(\", \"]\"), b => z] }}", "(", "]")]
    [InlineData("{{ branch.c [a => name | f(\")\", \",\"), b => z] }}", ")", ",")]
    [InlineData("{{ branch.c [a => name | f(\")\", \"]\"), b => z] }}", ")", "]")]
    public void Parse_DelimitersAndParenthesesInFilterArguments(string template, string arg1, string arg2)
    {
        BranchNode branch = ParseSingleBranch(template);

        Assert.Equal(2, branch.BranchEntries.Count);
        Assert.Equal([arg1, arg2], SingleFilterArgs(branch, "a"));
        Assert.Equal("z", Assert.IsType<VariableNode>(Assert.Single(branch.BranchEntries["b"])).Name);
    }

    [Theory]
    [InlineData("{{ branch.c [a => x), b => y] }}")]
    [InlineData("{{ branch.c [a => name | f(1)), b => y] }}")]
    [InlineData("{{ branch.c [a => name | f(1, b => y] }}")]
    [InlineData("{{ branch.c [a => name | f(\"1, b => y] }}")]
    [InlineData("{{ branch.c [a => (name | upper, b => y] }}")]
    public void Parse_UnbalancedOption_Throws(string template)
    {
        Assert.Throws<TemplateParsingException>(() => LissicParser.Parse(template, "TestTemplate"));
    }
}
