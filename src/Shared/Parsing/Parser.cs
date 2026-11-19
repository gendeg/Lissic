using System.Globalization;
#if !IN_SOURCE_GENERATOR
using System.Text;
#endif

namespace Lissic.Parsing;


internal static class LissicParser
{
    #if !IN_SOURCE_GENERATOR
    private static HtmlNode CreateHtmlNode(ReadOnlySpan<char> html)
    {
        byte[] bytes = new byte[Encoding.UTF8.GetByteCount(html)];
        Encoding.UTF8.GetBytes(html, bytes);
        return new HtmlNode(bytes);
    }
    #endif

    internal static ITemplateNode[] Parse(ReadOnlySpan<char> templateText, string templateName, int parseIdx = 0)
    {
        List<ITemplateNode> nodes = [];
        int pos = 0;

        while (pos < templateText.Length)
        {
            int startTag = templateText[pos..].IndexOf("{{");

            if (startTag == -1)
            {
                // No more tags found, add html and end parsing
                #if !IN_SOURCE_GENERATOR
                    nodes.Add(CreateHtmlNode(templateText[pos..]));
                #endif
                break;
            }

            if (startTag > 0)
            {
                // Add html before tag
                #if !IN_SOURCE_GENERATOR
                    nodes.Add(CreateHtmlNode(templateText[pos..(pos + startTag)]));
                #endif
                pos += startTag;
            }

            // Process tag content
            pos += 2;
            int endTag = FindTagEnd(templateText, pos);
            if (endTag == -1)
                throw new TemplateParsingException($"Unclosed tag in template: {templateName}, near position: {parseIdx + pos - 2}.");

            nodes.Add(ParseTag(templateText[pos..endTag], templateName, parseIdx + pos));

            pos = endTag + 2;
        }

        return [.. nodes];
    }

    internal static BranchNode ParseBranch(Tag tag, ReadOnlySpan<char> content, string templateName, int parseIdx)
    {
        string branchName = tag.Name.ToString();
        int pos = tag.AuxStart;
        if (pos >= content.Length || content[pos] != '[')
            throw new TemplateParsingException($"Expected '[' for branch: {branchName} in template: {templateName}, near position: {parseIdx + pos}.");
        pos++;

        Dictionary<string, ITemplateNode[]> options = new(2);
        while (true)
        {
            int optionEnd = FindOptionEnd(content, pos);
            if (optionEnd == -2)
                throw new TemplateParsingException($"Option for branch '{branchName}' in template: {templateName}, near position: {parseIdx + pos}, contains an extra ')'.");
            if (optionEnd == -1)
                throw new TemplateParsingException($"Option for branch '{branchName}' in template: {templateName}, near position: {parseIdx + pos}, is not closed properly.");

            bool isLast = content[optionEnd] == ']';
            while (pos < content.Length && char.IsWhiteSpace(content[pos]))
                pos++;

            ReadOnlySpan<char> option = content[pos..optionEnd].TrimEnd();
            if (option.Length == 0 && !isLast)
                throw new TemplateParsingException($"Empty option for branch '{branchName}' in template: {templateName}, near position: {parseIdx + pos}.");
            if (option.Length == 0 && isLast)
            {
                pos = optionEnd + 1;
                break;
            }

            ParseBranchOption(options, option, branchName, templateName, parseIdx + pos);

            pos = optionEnd + 1;
            if (isLast)
                break;
        }

        while (pos < content.Length && char.IsWhiteSpace(content[pos]))
            pos++;
        if (pos < content.Length)
            throw new TemplateParsingException($"Unexpected text after branch options in branch: '{branchName}', in template: {templateName}, near position: {parseIdx + pos}.");

        return new BranchNode(branchName, options, tag.Required, options.ContainsKey("_"));
    }

    internal static void ParseBranchOption(Dictionary<string, ITemplateNode[]> options, ReadOnlySpan<char> option, string branchName, string templateName, int parseIdx)
    {
        int fatArrow = option.IndexOf("=>");
        if (fatArrow == -1)
            throw new TemplateParsingException($"Option '{option.Trim().ToString()}' for branch '{branchName}', in template: {templateName}, near position: {parseIdx}, is missing '=>'.");

        string key = option[..fatArrow].Trim().ToString();
        if (key.Length == 0)
            throw new TemplateParsingException($"Branch option key is missing in template: {templateName}, near position: {parseIdx}.");
        if (options.ContainsKey(key))
            throw new TemplateParsingException($"Duplicate branch key '{key}', for branch: '{branchName}', in template: {templateName}, near position: {parseIdx}.");

        int pos = fatArrow + 2;
        while (pos < option.Length && char.IsWhiteSpace(option[pos]))
            pos++;

        ReadOnlySpan<char> value = option[pos..].TrimEnd();
        if (value.IsEmpty)
            throw new TemplateParsingException($"Missing value for branch option '{key}', for branch '{branchName}', in template: {templateName}, near position: {parseIdx + pos}");

        if (value[0] == '(' && value[^1] == ')')
        {
            value = value[1..^1].Trim();
            if (value.IsEmpty)
                throw new TemplateParsingException($"Missing value for branch option '{key}', for branch '{branchName}', in template: {templateName}, near position: {parseIdx + pos}");
        }
        else if (value[0] == '(' )
            throw new TemplateParsingException($"Mismatched parentheses for branch option '{key}', for branch '{branchName}', in template: {templateName}, near position: {parseIdx + pos}");

        char quote = value[0];
        if (quote == '"' || quote == '\'')
        {
            // Is string literal branch
            if (SkipQuoted(value, 0) != value.Length)
                throw new TemplateParsingException($"Invalid escape sequence or missing delimiter in branch option '{key}', for branch '{branchName}', in template: {templateName}, near position: {parseIdx + pos}");
            value = value[1..^1];

            options[key] = value.IndexOf(quote) == -1
                ? Parse(value, templateName, parseIdx + pos + 1)
                : Parse(CleanEscapes(value, quote), templateName, parseIdx + pos + 1);
        }
        else
            options[key] = [ParseTag(value, templateName, parseIdx + pos)];
    }

    internal static string[] ParseFilterArgs(ReadOnlySpan<char> content, ref int pos, string varName, string templateName, int parseIdx)
    {
        List<string> args = [];

        while (true)
        {
            while (pos < content.Length && char.IsWhiteSpace(content[pos]))
                pos++;
            if (pos >= content.Length)
                throw FilterError("has an argument that is not closed properly.  Is there a closing ')'?", varName, templateName, parseIdx + pos);

            if (content[pos] == ')')
            {
                if (args.Count > 0)
                    throw FilterError("has a trailing ',' with no argument after it.", varName, templateName, parseIdx + pos);
                pos++;
                return [];
            }

            string arg;
            char quote = content[pos];
            if (quote == '"' || quote == '\'')
            {
                int end = SkipQuoted(content, pos);
                if (end == -1)
                    throw FilterError("has an argument with no closing quote.", varName, templateName, parseIdx + pos);
                arg = CleanEscapes(content[(pos + 1)..(end - 1)], quote);
                pos = end;
            }
            else
            {
                int start = pos;
                while (pos < content.Length && content[pos] != ',' && content[pos] != ')')
                    pos++;
                arg = content[start..pos].Trim().ToString();
                if (arg.Length == 0)
                    throw FilterError("has an empty argument.", varName, templateName, parseIdx + start);
                if (!long.TryParse(arg, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _)
                    && !decimal.TryParse(arg, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                    throw FilterError($"has an apparent string argument, '{arg}', without quotes.", varName, templateName, parseIdx + start);
            }
            args.Add(arg);

            while (pos < content.Length && char.IsWhiteSpace(content[pos]))
                pos++;
            if (pos >= content.Length)
                throw FilterError("has arguments that are not closed properly.", varName, templateName, parseIdx + pos);

            char next = content[pos++];
            if (next == ')')
                return [.. args];
            if (next != ',')
                throw FilterError($"has unexpected '{next}' after an argument.", varName, templateName, parseIdx + pos - 1);
        }
    }

    internal static FilterCall[] ParseFilters(ReadOnlySpan<char> content, string varName, string templateName, int parseIdx)
    {
        List<FilterCall> filters = [];
        int pos = 0;

        while (true)
        {
            int nameStart = pos;
            while (pos < content.Length && content[pos] != '(' && content[pos] != '|')
                pos++;

            string filterName = content[nameStart..pos].Trim().ToString();
            if (filterName.Length == 0)
                throw FilterError("is missing a name.", varName, templateName, parseIdx + nameStart);
            if (!IsValidName(filterName))
                throw FilterError($"has an invalid name: '{filterName}'.", varName, templateName, parseIdx + nameStart);
            
            string[] args = [];
            if (pos < content.Length && content[pos] == '(')
            {
                pos++;
                args = ParseFilterArgs(content, ref pos, varName, templateName, parseIdx);
            }
            filters.Add(new FilterCall(filterName, args));

            while (pos < content.Length && char.IsWhiteSpace(content[pos]))
                pos++;
            if (pos == content.Length)
                return [.. filters];

            char nextChar = content[pos++];
            if (nextChar != '|')
                throw FilterError($"has unexpected '{nextChar}' after filter '{filterName}'.", varName, templateName, parseIdx + pos - 1);
        }
    }

    internal static ITemplateNode ParseTag(ReadOnlySpan<char> content, string templateName, int parseIdx)
    {
        ReadOnlySpan<char> tagContent = content.Trim();

        if (tagContent.Length == 0)
            throw new TemplateParsingException($"Empty tag in template: {templateName}, near position: {parseIdx}.");

        if (tagContent[0] == '(' && tagContent[^1] == ')')
            tagContent = tagContent[1..^1].Trim();
        else if (tagContent[0] == '(' )
            throw new TemplateParsingException($"Tag has incomplete parentheses in template: {templateName}, near position: {parseIdx}.");

        Tag tag = new(tagContent);
        if (!IsValidName(tag.Name))
            throw new TemplateParsingException($"Invalid tag name: '{tag.Name.ToString()}' in template: {templateName}, near position: {parseIdx}.");

        return tag.Type switch
        {
            TagType.Branch => ParseBranch(tag, tagContent, templateName, parseIdx),
            TagType.Loop => ParseLoop(tag),
            TagType.Part => ParsePart(tag),
            TagType.Variable => ParseVariable(tag, tagContent, templateName, parseIdx),
            _ => throw new TemplateParsingException($"Unknown tag type in template: {templateName}, near position: {parseIdx}."),
        };
    }

    internal static LoopNode ParseLoop(Tag tag)
    {
        return new LoopNode(tag.Name.ToString(), tag.Required);
    }

    internal static PartNode ParsePart(Tag tag)
    {
        return new PartNode(tag.Name.ToString(), tag.Required);
    }

    internal static VariableNode ParseVariable(Tag tag, ReadOnlySpan<char> content, string templateName, int parseIdx)
    {
        string varName = tag.Name.ToString();

        if (tag.AuxStart == content.Length)
            // No filters
            return new VariableNode(varName, tag.Required);

        if (content[tag.AuxStart] != '|')
            throw new TemplateParsingException($"Expected '|' for filters on variable: {varName} in template: {templateName}, near position: {parseIdx + tag.AuxStart}.");
        
        int pos = tag.AuxStart + 1;
        while (pos < content.Length && char.IsWhiteSpace(content[pos]))
            pos++;

        FilterCall[] filters = ParseFilters(content[pos..], varName, templateName, parseIdx + pos);

        return new VariableNode(varName, tag.Required, filters);
    }


    // Support Methods

    internal static string CleanEscapes(ReadOnlySpan<char> text, char quote)
    {
        if (text.IndexOf(quote) == -1)
            return text.ToString();
        else
            return text.ToString().Replace(quote == '"' ? "\"\"" : "''", quote == '"' ? "\"" : "'");
    }

    private static TemplateParsingException FilterError(string problem, string varName, string templateName, int position)
        => new($"Filter for variable '{varName}', in template: {templateName}, near position: {position}, {problem}");

    internal static int FindTagEnd(ReadOnlySpan<char> text, int start)
    {
        int idx = start;
        while (true)
        {
            int next = text[idx..].IndexOfAny('}', '"', '\'');
            if (next == -1)
                return -1;

            idx += next;
            if (text[idx] != '}')
                idx = SkipQuoted(text, idx);
            else if (idx + 1 < text.Length && text[idx + 1] == '}')
                return idx;
            else
                idx++;
        }
    }

    internal static int FindOptionEnd(ReadOnlySpan<char> options, int start)
    {
        int parens = 0;
        int brackets = 0;
        int idx = start;
        while (idx < options.Length)
        {
            switch (options[idx])
            {
                case ',' or ']' when parens == 0 && brackets == 0:
                    return idx;
                case '"' or '\'':
                    idx = SkipQuoted(options, idx);
                    continue;
                case '(':
                    parens++;
                    break;
                case ')':
                    if (--parens < 0)
                        return -2;
                    break;
                case '[' when parens == 0:
                    brackets++;
                    break;
                case ']' when parens == 0:
                    brackets--;
                    break;
            }
            idx++;
        }

        return -1;
    }

    internal static bool IsValidName(ReadOnlySpan<char> name)
    {
        if (name.Length == 0)
            return false;
        if (!char.IsLetter(name[0]) && name[0] != '_')
            return false;
        for (int i = 1; i < name.Length; i++)
            if (!char.IsLetterOrDigit(name[i]) && name[i] != '_')
                return false;

        return true;
    }

    private static int SkipQuoted(ReadOnlySpan<char> text, int idx)
    {
        char quote = text[idx];
        while (true)
        {
            int close = text[(idx + 1)..].IndexOf(quote);
            if (close == -1)
                return text.Length;

            idx += close + 2; // Just past the closing quote
            if (idx >= text.Length || text[idx] != quote)
                return idx;
        }
    }
}


internal enum TagType : byte
{
    None,
    Branch,
    Loop,
    Part,
    Variable
}


ref struct Tag
{
    public ushort AuxStart;
    public ReadOnlySpan<char> Name;
    public bool Required;
    public TagType Type;

    public Tag(ReadOnlySpan<char> text)
    {
        int pos = 0;

        if (text.StartsWith("branch.", StringComparison.OrdinalIgnoreCase))
        {
            Type = TagType.Branch;
            pos = 7;
        }
        else if (text.StartsWith("loop.", StringComparison.OrdinalIgnoreCase))
        {
            Type = TagType.Loop;
            pos = 5;
        }
        else if (text.StartsWith("part.", StringComparison.OrdinalIgnoreCase))
        {
            Type = TagType.Part;
            pos = 5;
        }
        else if (text.StartsWith("switch.", StringComparison.OrdinalIgnoreCase))
        {
            Type = TagType.Branch;
            pos = 7;
        }
        else if (text.StartsWith("foreach.", StringComparison.OrdinalIgnoreCase))
        {
            Type = TagType.Loop;
            pos = 8;
        }
        else if (text.StartsWith("subtemplate.", StringComparison.OrdinalIgnoreCase))
        {
            Type = TagType.Part;
            pos = 12;
        }
        else
            Type = TagType.Variable;

        switch (Type)
        {
            case TagType.Part:
            case TagType.Loop:
                AuxStart = 0;
                Name = text[pos..].TrimEnd();
                break;
            case TagType.Variable:
                while (pos < text.Length && !char.IsWhiteSpace(text[pos]) && text[pos] != '|')
                    pos++;
                Name = text[..pos];
                while (pos < text.Length && char.IsWhiteSpace(text[pos]))
                    pos++;
                AuxStart = (ushort)pos;
                break;
            case TagType.Branch:
                int nameStart = pos;
                while (pos < text.Length && !char.IsWhiteSpace(text[pos]) && text[pos] != '[')
                    pos++;
                Name = text[nameStart..pos];
                while (pos < text.Length && char.IsWhiteSpace(text[pos]))
                    pos++;
                AuxStart = (ushort)pos;
                break;
        }

        if (Name.Length > 0 && Name[^1] == '?')
        {
            Required = false;
            Name = Name[..^1];
        }
        else
            Required = true;
    }
}


internal class TemplateParsingException : Exception
{
    public TemplateParsingException() : base() { }
    public TemplateParsingException(string message) : base(message) { }
    public TemplateParsingException(string message, Exception inner) : base(message, inner) { }
}
