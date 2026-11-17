using Lissic.Parsing;

namespace Lissic.Tests.Parsing;

// Tests for LissicParser.FindTagEnd, which finds the closing '}}' of a tag, skipping anything
// inside quotes.  'start' is the first character after the opening '{{'.
//
// Return values:
//   >= 0  index of the first '}' of the closing '}}'
//   -1    no closing '}}' found (includes an unclosed quote)
//
// Most cases are written as (input, expectedPrefix): the closing '}}' is expected right after
// expectedPrefix, so the expected index is expectedPrefix.Length.  This avoids hand-counted indexes.
public class FindTagEndTests
{
    private static void AssertEndsAfter(string input, string expectedPrefix)
    {
        Assert.StartsWith(expectedPrefix, input);
        int end = LissicParser.FindTagEnd(input);
        Assert.Equal(expectedPrefix.Length, end);
        Assert.Equal("}}", input.Substring(end, 2));
    }


    /* Plain tags */

    [Theory]
    [InlineData(" name }}", " name ")]
    [InlineData("name}}", "name")]
    [InlineData("}}", "")]
    [InlineData(" part.p }}", " part.p ")]
    [InlineData(" name? | upper }}", " name? | upper ")]
    [InlineData(" (name | upper) }}", " (name | upper) ")]
    [InlineData(" name\r\n\t}}", " name\r\n\t")]
    public void FindsClosingBraces(string input, string expectedPrefix)
        => AssertEndsAfter(input, expectedPrefix);

    [Theory]
    [InlineData(" name }} text }}", " name ")]
    [InlineData(" name }}{{ other }}", " name ")]
    [InlineData(" name }} \"text }}\"", " name ")]
    public void StopsAtTheFirstClosingBraces(string input, string expectedPrefix)
        => AssertEndsAfter(input, expectedPrefix);

    [Theory]
    [InlineData(" a } b }}", " a } b ")]
    [InlineData(" name } }}", " name } ")]
    [InlineData(" name }\n}}", " name }\n")]
    [InlineData(" { name }}", " { name ")]
    public void SingleBraceIsNotAnEnd(string input, string expectedPrefix)
        => AssertEndsAfter(input, expectedPrefix);

    // "}}}" matches what IndexOf("}}") does today: the first two braces close the tag
    [Theory]
    [InlineData(" name }}}", " name ")]
    [InlineData(" name }}}}", " name ")]
    public void ExtraClosingBraces_FirstPairEndsTheTag(string input, string expectedPrefix)
        => AssertEndsAfter(input, expectedPrefix);


    /* Quotes */

    [Theory]
    [InlineData(" name | f(\"}}\") }}", " name | f(\"}}\") ")]
    [InlineData(" name | f('}}') }}", " name | f('}}') ")]
    [InlineData(" name | f(\"a }} b\") }}", " name | f(\"a }} b\") ")]
    [InlineData(" name | f(\"}}}}\") }}", " name | f(\"}}}}\") ")]
    [InlineData(" name | f(\"}}\", '}}') }}", " name | f(\"}}\", '}}') ")]
    [InlineData(" name | f(\"}}\") | g('}}') }}", " name | f(\"}}\") | g('}}') ")]
    [InlineData(" name | f(\"{{\") }}", " name | f(\"{{\") ")]
    [InlineData(" name | f(\"{{ x }}\") }}", " name | f(\"{{ x }}\") ")]
    [InlineData(" (name | f(\")}}\")) }}", " (name | f(\")}}\")) ")]
    public void ClosingBracesInsideQuotesAreSkipped(string input, string expectedPrefix)
        => AssertEndsAfter(input, expectedPrefix);

    [Theory]
    [InlineData(" name | f(\"it's }}\") }}", " name | f(\"it's }}\") ")]
    [InlineData(" name | f('say \"}}\"') }}", " name | f('say \"}}\"') ")]
    [InlineData(" name | f(\"'\", '}}') }}", " name | f(\"'\", '}}') ")]
    [InlineData(" name | f('\"', \"}}\") }}", " name | f('\"', \"}}\") ")]
    public void OtherQuoteTypeInsideQuotesIsLiteral(string input, string expectedPrefix)
        => AssertEndsAfter(input, expectedPrefix);

    [Theory]
    [InlineData(" name | f(\"\") }}", " name | f(\"\") ")]
    [InlineData(" name | f('') }}", " name | f('') ")]
    [InlineData(" name | f(\"\", \"}}\") }}", " name | f(\"\", \"}}\") ")]
    [InlineData(" name | f(\"\")}}", " name | f(\"\")")]
    public void EmptyQuotes(string input, string expectedPrefix)
        => AssertEndsAfter(input, expectedPrefix);

    [Theory]
    // Escape in the middle
    [InlineData(" name | f(\"a\"\"}}\"\"b\") }}", " name | f(\"a\"\"}}\"\"b\") ")]
    [InlineData(" name | f('it''s }}') }}", " name | f('it''s }}') ")]
    // Escape at the start
    [InlineData(" name | f(\"\"\"}}\") }}", " name | f(\"\"\"}}\") ")]
    // Escape right before the closing quote
    [InlineData(" name | f(\"}}\"\"\") }}", " name | f(\"}}\"\"\") ")]
    [InlineData(" name | f(\"say \"\"hi\"\"\") }}", " name | f(\"say \"\"hi\"\"\") ")]
    [InlineData(" name | f('it''') }}", " name | f('it''') ")]
    // Only an escaped quote
    [InlineData(" name | f(\"\"\"\") }}", " name | f(\"\"\"\") ")]
    [InlineData(" name | f('''') }}", " name | f('''') ")]
    // Several escapes in a row
    [InlineData(" name | f(\"\"\"\"\"\") }}", " name | f(\"\"\"\"\"\") ")]
    [InlineData(" name | f(\"a\"\"\"\"}}\") }}", " name | f(\"a\"\"\"\"}}\") ")]
    // Closing quote immediately followed by the closing braces
    [InlineData(" name | f(\"a\"\"\")}}", " name | f(\"a\"\"\")")]
    public void DoubledQuoteEscapesStayInsideQuotes(string input, string expectedPrefix)
        => AssertEndsAfter(input, expectedPrefix);


    /* Branches */

    [Theory]
    [InlineData(" branch.c [a => \"x\"] }}", " branch.c [a => \"x\"] ")]
    [InlineData(" branch.c [a => \"x }} y\"] }}", " branch.c [a => \"x }} y\"] ")]
    [InlineData(" branch.c [a => 'x }} y'] }}", " branch.c [a => 'x }} y'] ")]
    [InlineData(" branch.c [a => \"it's\", b => 'say \"hi\"'] }}", " branch.c [a => \"it's\", b => 'say \"hi\"'] ")]
    [InlineData(" branch.c [a => name | f(\"}}\"), b => y] }}", " branch.c [a => name | f(\"}}\"), b => y] ")]
    [InlineData(" (branch.c [a => \"}})\"]) }}", " (branch.c [a => \"}})\"]) ")]
    public void BranchLiteralsAndArgs(string input, string expectedPrefix)
        => AssertEndsAfter(input, expectedPrefix);

    // Tags nested in a quoted literal are inside the quotes, so the outer tag's '}}' is found directly
    [Theory]
    [InlineData(" branch.c [a => \"{{ name }}\"] }}", " branch.c [a => \"{{ name }}\"] ")]
    [InlineData(" branch.c [a => \"<b>{{ name }}</b>\", b => \"{{ x }}\"] }}", " branch.c [a => \"<b>{{ name }}</b>\", b => \"{{ x }}\"] ")]
    [InlineData(" branch.c [a => '{{ name | f(\"}}\") }}'] }}", " branch.c [a => '{{ name | f(\"}}\") }}'] ")]
    [InlineData(" branch.c [a => \"{{ branch.d [b => \"\"B\"\"] }}\"] }}", " branch.c [a => \"{{ branch.d [b => \"\"B\"\"] }}\"] ")]
    [InlineData(" branch.c [a => \"{{ name | f(\"\"}}\"\") }}\"] }}", " branch.c [a => \"{{ name | f(\"\"}}\"\") }}\"] ")]
    [InlineData(" branch.c [a => \"{{ branch.d [b => '{{ e }}'] }}\"] }}", " branch.c [a => \"{{ branch.d [b => '{{ e }}'] }}\"] ")]
    public void NestedTagsInsideQuotedLiterals(string input, string expectedPrefix)
        => AssertEndsAfter(input, expectedPrefix);


    /* Start position */

    [Theory]
    [InlineData("{{ a }} {{ b }}", 2, 3)]
    [InlineData("{{ a }} {{ b }}", 10, 3)]
    [InlineData("text {{ name | f(\"}}\") }} more", 7, 16)]
    [InlineData("{{ a }}", 5, 0)]
    public void StartsSearchingAtStart(string input, int start, int expected)
    {
        Assert.Equal(expected, LissicParser.FindTagEnd(input[start..]));
    }

    [Fact]
    public void ClosingBracesBeforeStart_AreIgnored()
    {
        Assert.Equal(-1, LissicParser.FindTagEnd("{{ a }} b"[7..]));
    }

    [Theory]
    [InlineData("}}", 2)]
    [InlineData("a }}", 4)]
    [InlineData("a }}", 3)]
    public void StartAtOrNearEnd_ReturnsNotFound(string input, int start)
    {
        Assert.Equal(-1, LissicParser.FindTagEnd(input[start..]));
    }


    /* Not found (-1) */

    [Theory]
    [InlineData("")]
    [InlineData(" name ")]
    [InlineData(" name }")]
    [InlineData(" name } }")]
    [InlineData("}")]
    [InlineData(" name | f(\"}}\") ")]
    [InlineData(" branch.c [a => \"{{ x }}\"] ")]
    public void NoClosingBraces_ReturnsNotFound(string input)
    {
        Assert.Equal(-1, LissicParser.FindTagEnd(input));
    }

    [Theory]
    [InlineData(" name | f(\"}} ")]
    [InlineData(" name | f('a }}")]
    [InlineData(" name | f(\"a\"\" }}")]
    [InlineData(" branch.c [a => \"x] }}")]
    [InlineData("\"")]
    [InlineData("'")]
    [InlineData("\"\"\"")]
    [InlineData("\"}}")]
    public void UnclosedQuote_ReturnsNotFound(string input)
    {
        Assert.Equal(-1, LissicParser.FindTagEnd(input));
    }
}
