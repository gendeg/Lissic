using System.Text;

namespace Lissic.Parsing;


internal static class LissicParser
{
    internal static bool FindEndParen(ReadOnlySpan<char> text, ref int idx)
    {
        while (idx > 0 && char.IsWhiteSpace(text[--idx])) {}

        if (text[idx] != ')')
            return false;
        
        return true;
    }

    internal static int FindOptionEnd(ReadOnlySpan<char> options, int start)
    {
        int idx = start;
        int depth = 0;

        while (idx < options.Length)
        {
            char c = options[idx];

            switch (c)
            {
                case ',':
                case ']':
                    if (depth == 0) // Found the end of the option
                        return idx;
                    idx++;
                    break;
                case '"':
                case '\'':
                    while (idx < options.Length)
                    {
                        idx++;
                        if (idx < options.Length && options[idx] == c)
                        {
                            idx++;
                            if (idx < options.Length && options[idx] == c)
                                continue;
                            else
                                break;
                        }
                    }
                    break;
                case '(':
                    depth++;
                    idx++;
                    break;
                case ')':
                    if (depth <= 0)
                        return -2;
                    depth--;
                    idx++;
                    break;
                default:
                    idx++;
                    break;
            }
        }

        return -1;
    }

    internal static int FindTagEnd(ReadOnlySpan<char> text)
    {
        int idx = 0;

        while (idx < text.Length)
        {
            char c = text[idx];

            switch (c)
            {
                case '}':
                    if (idx + 1 < text.Length && text[idx + 1] == '}')
                        return idx;
                    idx++;
                    break;
                case '"':
                case '\'':
                    while (idx < text.Length)
                    {
                        idx++;
                        if (idx < text.Length && text[idx] == c)
                        {
                            idx++;
                            if (idx < text.Length && text[idx] == c)
                                continue;
                            else
                                break;
                        }
                    }
                    break;
                default:
                    idx++;
                    break;
            }
        }

        return -1;
    }

    internal static ITemplateNode[] Parse(
        ReadOnlySpan<char> partText, 
        ReadOnlySpan<char> templateName, 
        bool isSourceGen = false,
        int parseIdx = 0)
    {
        List<ITemplateNode> nodes = [];
        int pos = 0;

        while (pos < partText.Length)
        {
            int startTag = partText[pos..].IndexOf("{{");

            if (startTag == -1)
            {
                // No more tags found, add html and end parsing
                if (!isSourceGen)
                    nodes.Add(new HtmlNode(Encoding.UTF8.GetBytes(partText[pos..].ToArray())));
                break;
            }

            if (startTag > 0)
            {
                if (!isSourceGen)
                    nodes.Add(new HtmlNode(Encoding.UTF8.GetBytes(partText[pos..(pos + startTag)].ToArray())));
                pos += startTag;
            }

            pos += 2; // Skip past the opening {{

            while (pos < partText.Length && char.IsWhiteSpace(partText[pos]))
                pos++;
            
            if (pos >= partText.Length)
                throw new TemplateParsingException($"Unclosed tag in template: {templateName.ToString()}, near position: {parseIdx + pos - 2}.");

            bool enclosedInParens = false;
            if (partText[pos] == '(')
            {
                enclosedInParens = true;
                pos++;
            }

            if (partText[pos..].TrimStart().StartsWith("branch.", StringComparison.OrdinalIgnoreCase)
                || partText[pos..].TrimStart().StartsWith("switch.", StringComparison.OrdinalIgnoreCase))
            {
                // Parse branch tag
                (BranchNode branchNode, int consumed) = ParseBranch(partText[pos..], templateName, isSourceGen, parseIdx + pos, enclosedInParens);
                nodes.Add(branchNode);
                pos += consumed;
            }
            else
            {
                // Parse regular tag (variable, loop, part)
                int endTag = FindTagEnd(partText[pos..]);
                if (endTag == -1)
                    throw new TemplateParsingException($"Unclosed tag in template: {templateName.ToString()}, near position: {parseIdx + pos - 2}.");

                int length = endTag;
                if (enclosedInParens && !FindEndParen(partText[pos..], ref length))
                    throw new TemplateParsingException($"Expected ')' in template: {templateName.ToString()}, near position: {parseIdx + pos}.");

                ReadOnlySpan<char> tag = partText[pos..(pos + length)];
                nodes.Add(ParseTag(tag, templateName, parseIdx + pos));

                pos += endTag + 2; // Skip past the closing }}
            }
        }

        return [..nodes];
    }

    internal static (BranchNode, int) ParseBranch(
        ReadOnlySpan<char> branch, 
        ReadOnlySpan<char> templateName, 
        bool isSourceGen, 
        int parseIdx, 
        bool enclosedInParens)
    {
        BranchParseState s = new()
        {
            pos = branch.IndexOf('.') + 1,
            // NOTE: endCmdIdx is the end index relative to the original span
            // This allows pos, and pos offsets, to be directly compared to endCmdIdx without needing to adjust both as we go
            endTagIdx = FindTagEnd(branch)
        };
        s.endCmdIdx = s.endTagIdx;
        if (s.endTagIdx == -1)
            throw new TemplateParsingException($"Unclosed branch tag in template: {templateName.ToString()}, near position: {parseIdx - 2}.");

        if (enclosedInParens && !FindEndParen(branch, ref s.endCmdIdx))
            throw new TemplateParsingException($"Expected ')' in template: {templateName.ToString()}, near position: {parseIdx + s.pos}.");

        // Find the opening bracket for the branch options
        int openBracket = branch[s.pos..].IndexOf('[');
        if (openBracket == -1 || (s.pos + openBracket) > s.endCmdIdx)
            throw new TemplateParsingException($"Branch '{
                (s.endCmdIdx <= 20 ? branch[s.pos..s.endCmdIdx].ToString() : branch[s.pos..(s.pos + 20)].ToString() + "...")
                }', template: {templateName.ToString()} does not have options defined.");
        
        // Extract the branch name
        if (branch[s.pos..(s.pos + openBracket)] != branch[s.pos..(s.pos + openBracket)].TrimStart())
            throw new TemplateParsingException($"Branch in template: {templateName.ToString()}, near position: {parseIdx - 2}, has whitespace before the branch name.");
        
        ReadOnlySpan<char> keyNameSpan = branch[s.pos..(s.pos + openBracket)].TrimEnd();
        bool required = true;
        if (!keyNameSpan.IsEmpty && keyNameSpan[^1] == '?')
        {
            required = false;
            keyNameSpan = keyNameSpan[..^1];
        }
        if (keyNameSpan.IsEmpty)
            throw new TemplateParsingException($"Branch name is missing in template: {templateName.ToString()}, near position: {parseIdx - 2}.");
        s.keyName = keyNameSpan.ToString();

        s.pos += openBracket + 1;

        // Build the branch options dictionary
        s.branchOptions = new(2);
        while (ParseBranchOption(branch, ref s, templateName, isSourceGen, parseIdx)) {}

        return (new BranchNode(s.keyName, s.branchOptions, required, s.branchOptions.ContainsKey("_")), s.pos);
    }

    internal static string ParseBranchLiteral(ReadOnlySpan<char> options, ref BranchParseState s, char quoteType, ReadOnlySpan<char> templateName, int parseIdx)
    {
        StringBuilder literalText = new();

        int endText = options[s.pos..].IndexOf(quoteType);
        if (endText == -1)
            throw new TemplateParsingException($"Option for branch '{s.keyName}', in template: {templateName.ToString()}, near position: {parseIdx + s.pos}, is not closed properly.  Is there a closing quote mark?");

        // Handle escape sequence
        while(options[s.pos + endText + 1] == quoteType)
        {
            literalText.Append(options[s.pos..(s.pos + endText + 1)].ToString());
            s.pos += endText + 2;

            endText = options[s.pos..].IndexOf(quoteType);
            if (endText == -1)
                throw new TemplateParsingException($"Option for branch '{s.keyName}', in template: {templateName.ToString()}, near position: {parseIdx + s.pos}, is not closed properly.  Is there a closing quote mark?");
        }

        literalText.Append(options[s.pos..(s.pos + endText)].ToString());
        s.pos += endText + 1;

        return literalText.ToString();
    }

    internal static bool ParseBranchOption(
        ReadOnlySpan<char> options, 
        ref BranchParseState s, 
        ReadOnlySpan<char> templateName, 
        bool isSourceGen, 
        int parseIdx)
    {
        int optionEnd = FindOptionEnd(options, s.pos);
        if (optionEnd == -2)
            throw new TemplateParsingException($"Option for branch '{s.keyName}' in template: {templateName.ToString()}, near position: {parseIdx + s.pos}, contains an extra ')'.");
        if (optionEnd == -1)
            throw new TemplateParsingException($"Option for branch '{s.keyName}' in template: {templateName.ToString()}, near position: {parseIdx + s.pos}, is not closed properly.");

        int fatArrow = options[s.pos..].IndexOf("=>");
        int comma = options[s.pos..].IndexOf(',');
        if (comma != -1 && comma < fatArrow && optionEnd > fatArrow && (s.pos + fatArrow) < s.endCmdIdx)
            throw new TemplateParsingException($"Unexpected ',' before '=>' in branch option for branch '{s.keyName}', in template: {templateName.ToString()}, near position: {parseIdx + s.pos}.");

        // No more options in this branch
        if (fatArrow == -1 || (s.pos + fatArrow) > s.endCmdIdx)
        {
            int closeBracket = options[s.pos..].IndexOf(']');
            if (s.pos + closeBracket < s.endCmdIdx 
                && closeBracket != -1
                && options[(s.pos + closeBracket + 1)..s.endCmdIdx].Trim().IsEmpty)
            {
                s.pos = s.endTagIdx + 2;
                return false;
            }
            throw new TemplateParsingException($"Options for branch: '{s.keyName}', in template: {templateName.ToString()}, near position: {parseIdx + s.pos}, are not closed properly.");
        }

        string keyValue = options[s.pos..(s.pos + fatArrow)].Trim().ToString();
        if (keyValue == "")
            throw new TemplateParsingException($"Branch option key is missing in template: {templateName.ToString()}, near position: {parseIdx + s.pos}.");
        if (s.branchOptions.ContainsKey(keyValue))
            throw new TemplateParsingException($"Duplicate branch key '{keyValue}', for branch: '{s.keyName}', in template: {templateName.ToString()}, near position: {parseIdx + s.pos}.");

        s.pos += fatArrow + 2;
        while (char.IsWhiteSpace(options[s.pos])) s.pos++;
        if (options[s.pos] == ',' || options[s.pos] == ']')
            throw new TemplateParsingException($"Missing value for branch option '{keyValue}', for branch '{s.keyName}', in template: {templateName.ToString()}, near position: {parseIdx + s.pos}");

        bool parenLiteral = false;
        if (options[s.pos] == '(')
        {
            int next = s.pos + 1;
            while (char.IsWhiteSpace(options[next])) next++;
            if (options[next] == '"' || options[next] == '\'')
            {
                parenLiteral = true;
                s.pos = next;
            }
        }

        // Literal text branch value
        if (options[s.pos] == '"' || options[s.pos] == '\'')
        {
            s.pos++;
            int literalStartPos = parseIdx + s.pos;
            string literalText = ParseBranchLiteral(options, ref s, options[s.pos - 1], templateName, parseIdx);
            s.branchOptions[keyValue] = Parse(literalText, templateName, isSourceGen, literalStartPos);

            if (parenLiteral)
            {
                while (char.IsWhiteSpace(options[s.pos])) s.pos++;
                if (options[s.pos] != ')')
                    throw new TemplateParsingException($"Expected ')' after literal text in branch '{s.keyName}', in template: {templateName.ToString()}, near position: {parseIdx + s.pos}.");
                s.pos++;
            }

            while (char.IsWhiteSpace(options[s.pos])) s.pos++;
            if (options[s.pos] == ',')
                s.pos++;
            else if (options[s.pos] != ']')
                throw new TemplateParsingException($"Expected ',' or ']' at end of branch option in branch '{s.keyName}', in template: {templateName.ToString()}, near position: {parseIdx + s.pos}.");
        }

        // Command branch value
        else
        {
            if (optionEnd > s.endCmdIdx)
                throw new TemplateParsingException($"Option for branch '{s.keyName}', in template: {templateName.ToString()}, near position: {parseIdx + s.pos}, is not closed properly.");

            s.branchOptions[keyValue] = [ParseTag(options[s.pos..optionEnd], templateName, parseIdx + s.pos)];
            s.pos = options[optionEnd] == ']' ? optionEnd : optionEnd + 1;
        }

        return true;
    }

    internal static FilterCall[] ParseFilters(ReadOnlySpan<char> text, ReadOnlySpan<char> variableName, ReadOnlySpan<char> templateName, int parseIdx)
    {
        TemplateParsingException Error(string problem, int at, string propName, string tmplName)
            => new($"Filter for variable '{propName}', in template: {tmplName}, near position: {parseIdx + at}, {problem}");

        List<FilterCall> filters = [];
        int pos = 0;

        while (true)
        {
            int nameStart = pos;
            while (pos < text.Length && text[pos] != '(' && text[pos] != '|')
                pos++;
            
            string name = text[nameStart..pos].Trim().ToString();
            if (name.Length == 0)
                throw Error("is missing a name.", nameStart, variableName.ToString(), templateName.ToString());
            if (name[^1] == ')')
                throw Error("has an unexpected ')'.", nameStart, variableName.ToString(), templateName.ToString());

            string[] args = [];
            if (pos < text.Length && text[pos] == '(')
            {
                pos++;
                args = ParseFilterArgs(text, ref pos, Error, variableName, templateName);
            }
            filters.Add(new FilterCall(name, args));

            while (pos < text.Length && char.IsWhiteSpace(text[pos]))
                pos++;

            if (pos == text.Length)
                break;

            char nextChar = text[pos++];
            if (nextChar != '|')
                throw Error($"has unexpected '{nextChar}' after filter '{name}'.", pos - 1, variableName.ToString(), templateName.ToString());
        }

        return [.. filters];
    }

    private static string[] ParseFilterArgs(ReadOnlySpan<char> text, ref int pos, Func<string, int, string, string, TemplateParsingException> error, ReadOnlySpan<char> variableName, ReadOnlySpan<char> templateName)
    {
        List<string> args = [];

        while (true)
        {
            while (pos < text.Length && char.IsWhiteSpace(text[pos]))
                pos++;
            if (pos >= text.Length)
                throw error("has arguments that are not closed properly.  Is there a closing ')'?", pos, variableName.ToString(), templateName.ToString());

            if (text[pos] == ')')
            {
                if (args.Count > 0)
                    throw error("has a trailing ',' with no argument after it.", pos, variableName.ToString(), templateName.ToString());
                pos++;
                return [];
            }

            string arg;
            char quote = text[pos];
            if (quote == '"' || quote == '\'')
            {
                StringBuilder quotedArg = new();
                int length = text[(pos + 1)..].IndexOf(quote);
                if (length == -1)
                    throw error($"has an argument with an unclosed {quote} quote.", pos, variableName.ToString(), templateName.ToString());

                // Handle escape sequence
                while(pos + length + 2 < text.Length && text[pos + length + 2] == quote)
                {
                    quotedArg.Append(text.Slice(pos + 1, length + 1).ToString());
                    pos += length + 2;

                    length = text[(pos + 1)..].IndexOf(quote);
                    if (length == -1)
                        throw error($"has an argument with an unclosed {quote} quote.", pos, variableName.ToString(), templateName.ToString());
                }

                quotedArg.Append(text.Slice(pos + 1, length).ToString());
                arg = quotedArg.ToString();
                pos += length + 2;
            }
            else
            {
                int start = pos;
                while (pos < text.Length && text[pos] != ',' && text[pos] != ')')
                    pos++;
                arg = text[start..pos].Trim().ToString();
                if (arg.Length == 0)
                    throw error("has an empty argument.", start, variableName.ToString(), templateName.ToString());
                if (!long.TryParse(arg, out _) && !decimal.TryParse(arg, out _))
                    throw error("has an apparent string argument without quotes.", start, variableName.ToString(), templateName.ToString());
            }
            args.Add(arg);

            while (pos < text.Length && char.IsWhiteSpace(text[pos]))
                pos++;
            if (pos >= text.Length)
                throw error("has arguments that are not closed properly.  Is there a closing ')'?", pos, variableName.ToString(), templateName.ToString());

            char next = text[pos++];
            if (next == ')')
                return [.. args];
            if (next != ',')
                throw error($"has unexpected '{next}' after an argument.", pos - 1, variableName.ToString(), templateName.ToString());
        }
    }

    internal static ITemplateNode ParseTag(ReadOnlySpan<char> tag, ReadOnlySpan<char> templateName, int parseIdx)
    {
        tag = tag.Trim();
        if (tag.IsEmpty)
            throw new TemplateParsingException($"Tag is empty in template: {templateName.ToString()}, near position: {parseIdx}.");

        if (tag[0] == '(' && tag[^1] == ')')
            return ParseTag(tag[1..^1], templateName, parseIdx);
        if (tag[0] == '(')
            throw new TemplateParsingException($"Unmatched parentheses in tag: '{tag.ToString()}', in template: {templateName.ToString()}, near position: {parseIdx}");

        int period = tag.IndexOf('.');
        int pipe = tag.IndexOf('|');

        // Parse variable nodes
        if (period == -1 || (period > pipe && pipe != -1))
        {            
            return ParseVariableTag(tag, templateName, parseIdx, pipe);
        }
        
        // Parse part and loop nodes
        bool required = true;
        if (tag[..period] != tag[..period].TrimEnd() || tag[(period + 1)..] != tag[(period + 1)..].TrimStart())
            throw new TemplateParsingException($"Tag: '{tag.ToString()}', in template: {templateName.ToString()}, near position: {parseIdx}, has invalid whitespace.");

        string tagCommand = tag[..period].TrimStart().ToString().ToLower();
        ReadOnlySpan<char> tagValue = tag[(period + 1)..].TrimEnd();
        if (!tagValue.IsEmpty && tagValue[^1] == '?')
        {
            tagValue = tagValue[..^1];
            required = false;
        }
        if (tagValue.IsEmpty)
            throw new TemplateParsingException($"Tag value is missing for tag command: '{tagCommand}', in template: {templateName.ToString()}, near position: {parseIdx}.");
        
        if (pipe != -1)
            throw new TemplateParsingException($"Filters not allowed for tags of type {tagCommand}; in template: {templateName.ToString()}, near position: {parseIdx}");

        return tagCommand switch
        {
            "part" or "subtemplate" => new PartNode(tagValue.ToString(), required),
            "loop" or "foreach" => new LoopNode(tagValue.ToString(), required),
            _ => throw new TemplateParsingException($"Unknown tag command: '{tagCommand}', in template: {templateName.ToString()}, near position: {parseIdx}"),
        };
    }

    internal static VariableNode ParseVariableTag(ReadOnlySpan<char> tag, ReadOnlySpan<char> templateName, int parseIdx, int pipe)
    {
        FilterCall[]? filters = null;
        bool required = true;

        ReadOnlySpan<char> variableName;
        if (pipe == -1)
            variableName = tag.Trim();
        else
        {
            variableName = tag[..pipe].Trim();
            filters = ParseFilters(tag[(pipe + 1)..], variableName, templateName, parseIdx + pipe + 1);
        }

        if (!variableName.IsEmpty && variableName[^1] == '?')
        {
            variableName = variableName[..^1];
            required = false;
        }

        if (variableName.IsEmpty)
            throw new TemplateParsingException($"Variable name is missing in template: {templateName.ToString()}, near position: {parseIdx}.");
        if (variableName[^1] == ')')
            throw new TemplateParsingException($"Variable name has an unexpected ')', in template: {templateName.ToString()}, near position: {parseIdx}.");

        return new VariableNode(variableName.ToString(), required, filters);
    }

    internal struct BranchParseState
    {
        public Dictionary<string, ITemplateNode[]> branchOptions;
        public int endCmdIdx;
        public int endTagIdx;
        public string keyName;
        public int pos;
    }
}


internal class TemplateParsingException : Exception
{
    public TemplateParsingException() : base() { }
    public TemplateParsingException(string message) : base(message) { }
    public TemplateParsingException(string message, Exception inner) : base(message, inner) { }
}
