using System.Text;

namespace Lissic.Parsing;


internal static class LissicParser
{
    internal static ITemplateNode[] Parse(ReadOnlySpan<char> partText, ReadOnlySpan<char> templateName, bool isSourceGen = false, int parseIdx = 0)
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

            if (partText[pos..].TrimStart().StartsWith("branch.", StringComparison.OrdinalIgnoreCase)
                || partText[pos..].TrimStart().StartsWith("switch.", StringComparison.OrdinalIgnoreCase))
            {
                // Parse branch tag
                (BranchNode branchNode, int consumed) = ParseBranch(partText[pos..], templateName, isSourceGen, parseIdx + pos);
                nodes.Add(branchNode);
                pos += consumed;
            }
            else
            {
                // Parse regular tag (variable, loop, part)
                int endTag = partText[pos..].IndexOf("}}");
                if (endTag == -1)
                {
                    throw new TemplateParsingException($"Unclosed tag in template: {templateName.ToString()}, near position: {parseIdx + pos - 2}.");
                }
                ReadOnlySpan<char> tag = partText[pos..(pos + endTag)];
                nodes.Add(ParseTag(tag, templateName, parseIdx + pos));

                pos += endTag + 2; // Skip past the closing }}
            }
        }

        return [..nodes];
    }

    internal static (BranchNode, int) ParseBranch(ReadOnlySpan<char> branch, ReadOnlySpan<char> templateName, bool isSourceGen, int parseIdx)
    {
        BranchParseState s = new()
        {
            pos = branch.IndexOf('.') + 1,
            // NOTE: endCmdIdx is the end index relative to the original span
            // This allows pos, and pos offsets, to be directly compared to endCmdIdx without needing to adjust both as we go
            endCmdIdx = branch.IndexOf("}}")
        };
        if (s.endCmdIdx == -1)
            throw new TemplateParsingException($"Unclosed branch tag in template: {templateName.ToString()}, near position: {parseIdx - 2}.");

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

        return (new BranchNode(s.keyName, s.branchOptions, required), s.pos);
    }

    internal static string ParseBranchLiteral(ReadOnlySpan<char> options, ref BranchParseState s, char quoteType, ReadOnlySpan<char> templateName, int parseIdx)
    {
        StringBuilder literalText = new();

        int endText = options[s.pos..].IndexOf(quoteType);
        if (endText == -1)
            throw new TemplateParsingException($"Option for branch '{s.keyName}', in template: {templateName.ToString()}, near position: {parseIdx + s.pos}, is not closed properly.  Is there a closing quote mark?");
        
        // skip embedded tag(s) detected in literal text
        while ((s.pos + endText) > s.endCmdIdx)
        {
            int newClose = options[(s.pos + endText)..].IndexOf("}}");
            if (newClose == -1)
                throw new TemplateParsingException($"Unclosed branch tag in template: {templateName.ToString()}, near position: {parseIdx - 2}.");
            s.endCmdIdx = s.pos + endText + newClose;
        }

        // Handle escape sequence
        while(options[s.pos + endText + 1] == quoteType)
        {
            literalText.Append(options[s.pos..(s.pos + endText + 1)].ToString());
            s.pos += endText + 2;

            endText = options[s.pos..].IndexOf(quoteType);
            if (endText == -1)
                throw new TemplateParsingException($"Option for branch '{s.keyName}', in template: {templateName.ToString()}, near position: {parseIdx + s.pos}, is not closed properly.  Is there a closing quote mark?");
            
            // skip embedded tag(s) detected in literal text
            while ((s.pos + endText) > s.endCmdIdx)
            {
                int newClose = options[(s.pos + endText)..].IndexOf("}}");
                if (newClose == -1)
                    throw new TemplateParsingException($"Unclosed branch tag in template: {templateName.ToString()}, near position: {parseIdx - 2}.");
                s.endCmdIdx = s.pos + endText + newClose;
            }
        }

        literalText.Append(options[s.pos..(s.pos + endText)].ToString());
        s.pos += endText + 1;

        return literalText.ToString();
    }

    internal static string ParseBranchLiteralOld(ReadOnlySpan<char> options, ref BranchParseState s, ReadOnlySpan<char> templateName, int parseIdx)
    {
        StringBuilder literalText = new();

        int endText = options[s.pos..].IndexOf(';');
        if (endText == -1)
            throw new TemplateParsingException($"Option for branch '{s.keyName}', in template: {templateName.ToString()}, near position: {parseIdx + s.pos}, is not closed properly.  Is there a closing ';'?");

        // skip embedded tag(s) detected in literal text
        while ((s.pos + endText) > s.endCmdIdx)
        {
            int newClose = options[(s.pos + endText)..].IndexOf("}}");
            if (newClose == -1)
                throw new TemplateParsingException($"Unclosed branch tag in template: {templateName.ToString()}, near position: {parseIdx - 2}.");
            s.endCmdIdx = s.pos + endText + newClose;
        }
        
        // Handle ";;" escape sequence
        while(options[s.pos + endText + 1] == ';')
        {
            literalText.Append(options[s.pos..(s.pos + endText + 1)].ToString());
            s.pos += endText + 2;

            endText = options[s.pos..].IndexOf(';');
            if (endText == -1)
                throw new TemplateParsingException($"Option for branch '{s.keyName}', in template: {templateName.ToString()}, near position: {parseIdx + s.pos}, is not closed properly.  Is there a closing ';'?");

            // skip embedded tag(s) detected in literal text
            while ((s.pos + endText) > s.endCmdIdx)
            {
                int newClose = options[(s.pos + endText)..].IndexOf("}}");
                if (newClose == -1)
                    throw new TemplateParsingException($"Unclosed branch tag in template: {templateName.ToString()}, near position: {parseIdx - 2}.");
                s.endCmdIdx = s.pos + endText + newClose;
            }
        }
        literalText.Append(options[s.pos..(s.pos + endText)].ToString());
        s.pos += endText + 1;

        return literalText.ToString();
    }

    internal static bool ParseBranchOption(ReadOnlySpan<char> options, ref BranchParseState s, ReadOnlySpan<char> templateName, bool isSourceGen, int parseIdx)
    {
        int fatArrow = options[s.pos..].IndexOf("=>");

        // No more options in this branch
        if (fatArrow == -1 || (s.pos + fatArrow) > s.endCmdIdx)
        {
            int closeBracket = options[s.pos..].IndexOf(']');
            if (s.pos + closeBracket < s.endCmdIdx 
                && closeBracket != -1
                && options[(s.pos + closeBracket + 1)..s.endCmdIdx].Trim().IsEmpty)
            {
                s.pos = s.endCmdIdx + 2;
                return false;
            }
            throw new TemplateParsingException($"Options for branch: '{s.keyName}', in template: {templateName.ToString()}, near position: {parseIdx + s.pos}, are not closed properly.");
        }

        string keyValue = options[s.pos..(s.pos + fatArrow)].Trim().ToString();
        if (keyValue == "")
            throw new TemplateParsingException($"Branch option key is missing in template: {templateName.ToString()}, near position: {parseIdx + s.pos}.");

        s.pos += fatArrow + 2;
        while (char.IsWhiteSpace(options[s.pos])) s.pos++;

        // Literal text branch value
        if (options[s.pos] == '"' || options[s.pos] == '\'')
        {
            if (s.oldSyntax == true)
                throw new TemplateParsingException($"Mixed syntax versions in template: {templateName.ToString()}, near position: {parseIdx + s.pos}.");
            s.oldSyntax = false;

            s.pos++;
            int literalStartPos = parseIdx + s.pos;
            string literalText = ParseBranchLiteral(options, ref s, options[s.pos - 1], templateName, parseIdx);
            s.branchOptions[keyValue] = Parse(literalText, templateName, isSourceGen, literalStartPos);

            while (char.IsWhiteSpace(options[s.pos])) s.pos++;
            if (options[s.pos] == ',') s.pos++;
        }
        else if (options[s.pos] == '@')
        {
            if (s.oldSyntax == false)
                throw new TemplateParsingException($"Mixed syntax versions in template: {templateName.ToString()}, near position: {parseIdx + s.pos}.");
            s.oldSyntax = true;

            s.pos++;
            int literalStartPos = parseIdx + s.pos;
            string literalText = ParseBranchLiteralOld(options, ref s, templateName, parseIdx);
            s.branchOptions[keyValue] = Parse(literalText, templateName, isSourceGen, literalStartPos);
        }

        // Command branch value
        else
        {
            int semicolon = options[s.pos..].IndexOf(';');
            if (s.oldSyntax == null)
            {
                if (semicolon != -1) // TODO: skip characters inside literals
                    s.oldSyntax = true;
                else
                    s.oldSyntax = false;
            }

            if (s.oldSyntax == true)
            {
                int endIdx = semicolon;
                if (endIdx == -1 || (s.pos + endIdx) > s.endCmdIdx)
                    throw new TemplateParsingException($"Option for branch '{s.keyName}', in template: {templateName.ToString()}, near position: {parseIdx + s.pos}, is not closed properly.  Is there a closing ';'?");

                s.branchOptions[keyValue] = [ParseTag(options[s.pos..(s.pos + endIdx)], templateName, parseIdx + s.pos)];
                s.pos += endIdx + 1;
            }

            else
            {
                int endIdx = s.pos + 1;
                while (endIdx < options.Length && options[endIdx] != ',' && options[endIdx] != ']')
                {
                    if (options[endIdx] == '(')
                    {
                        while (options[endIdx] != ')')
                            endIdx++;
                    }
                    else
                        endIdx++;
                }

                if (endIdx > s.endCmdIdx)
                    throw new TemplateParsingException($"Option for branch '{s.keyName}', in template: {templateName.ToString()}, near position: {parseIdx + s.pos}, is not closed properly.");

                s.branchOptions[keyValue] = [ParseTag(options[s.pos..endIdx], templateName, parseIdx + s.pos)];
                s.pos = options[endIdx] == ']' ? endIdx : endIdx + 1;
            }
        }

        return true;
    }

    internal static FilterCall[] ParseFilters(ReadOnlySpan<char> text, ReadOnlySpan<char> variableName, ReadOnlySpan<char> templateName, int parseIdx)
    {
        TemplateParsingException Error(string problem, int at, string propName, string tmplName)
            => new($"Filter for variable '{propName}', in template: {tmplName}, near position: {parseIdx + at}, {problem}");

        if (text.Trim().Length == 0)
            return [];

        List<FilterCall> filters = [];
        int pos = 0;

        while (true)
        {
            int nameStart = pos;
            while (pos < text.Length && text[pos] != '(' && text[pos] != '|' && text[pos] != '<')
                pos++;
            
            string name = text[nameStart..pos].Trim().ToString();
            if (name.Length == 0)
                throw Error("is missing a name.", nameStart, variableName.ToString(), templateName.ToString());

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

    internal static FilterCall[] ParseFiltersOld(ReadOnlySpan<char> text, ReadOnlySpan<char> variableName, ReadOnlySpan<char> templateName, int parseIdx)
    {
        TemplateParsingException Error(string problem, int at, string propName, string tmplName)
            => new($"Filter for variable '{propName}', in template: {tmplName}, near position: {parseIdx + at}, {problem}");

        if (text.Trim().Length == 1 && text.Trim()[0] == '>')
            return [];

        List<FilterCall> filters = [];
        int pos = 0;

        while (true)
        {
            int nameStart = pos;
            while (pos < text.Length && text[pos] != '(' && text[pos] != '|' && text[pos] != '>')
                pos++;

            string name = text[nameStart..pos].Trim().ToString();
            if (pos >= text.Length)
                throw Error("is not closed properly.  Is there a closing '>'?", pos, variableName.ToString(), templateName.ToString());
            if (name.Length == 0)
                throw Error("is missing a name.", nameStart, variableName.ToString(), templateName.ToString());
            if (text[pos] == '(' && char.IsWhiteSpace(text[pos - 1]))
                throw Error("has unexpected whitespace before filter arguments.", pos - 1, variableName.ToString(), templateName.ToString());

            string[] args = [];
            if (text[pos] == '(')
            {
                pos++;
                args = ParseFilterArgsOld(text, ref pos, Error, variableName, templateName);
            }
            filters.Add(new FilterCall(name, args));

            while (pos < text.Length && char.IsWhiteSpace(text[pos]))
                pos++;
            if (pos >= text.Length)
                throw Error("is not closed properly.  Is there a closing '>'?", pos, variableName.ToString(), templateName.ToString());

            char nextChar = text[pos++];
            if (nextChar == '>')
                break;
            if (nextChar != '|')
                throw Error($"has unexpected '{nextChar}' after filter '{name}'.", pos - 1, variableName.ToString(), templateName.ToString());
        }

        if (!text[pos..].Trim().IsEmpty)
            throw Error("has unexpected text after the closing '>'.", pos, variableName.ToString(), templateName.ToString());

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
                while(text[pos + length + 2] == quote)
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

    private static string[] ParseFilterArgsOld(ReadOnlySpan<char> text, ref int pos, Func<string, int, string, string, TemplateParsingException> error, ReadOnlySpan<char> variableName, ReadOnlySpan<char> templateName)
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
                int close = text[(pos + 1)..].IndexOf(quote);
                if (close == -1)
                    throw error($"has an argument with an unclosed {quote} quote.", pos, variableName.ToString(), templateName.ToString());
                arg = text.Slice(pos + 1, close).ToString();
                pos += close + 2;
            }
            else
            {
                int start = pos;
                while (pos < text.Length && text[pos] != ',' && text[pos] != ')')
                    pos++;
                arg = text[start..pos].Trim().ToString();
                if (arg.Length == 0)
                    throw error("has an empty argument.", start, variableName.ToString(), templateName.ToString());
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
        int period = tag.IndexOf('.');
        int lessThan = tag.IndexOf('<');
        int pipe = tag.IndexOf('|');

        // Parse variable nodes
        if (period == -1 || (period > pipe && pipe != -1) || (period > lessThan && lessThan != -1))
        {
            if (lessThan != -1 && (pipe == -1 || lessThan < pipe) && (period == -1 || lessThan < period))
                return ParseVariableTagOld(tag, templateName, parseIdx, lessThan);
            
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

        return new VariableNode(variableName.ToString(), required, filters);
    }

    internal static VariableNode ParseVariableTagOld(ReadOnlySpan<char> tag, ReadOnlySpan<char> templateName, int parseIdx, int lessThan)
    {
        FilterCall[]? filters = null;
        bool required = true;

        ReadOnlySpan<char> variableName;
        if (lessThan == -1)
            variableName = tag.Trim();
        else
        {
            variableName = tag[..lessThan].Trim();
            filters = ParseFiltersOld(tag[(lessThan + 1)..], variableName, templateName, parseIdx + lessThan + 1);
        }

        if (!variableName.IsEmpty && variableName[^1] == '?')
        {
            variableName = variableName[..^1];
            required = false;
        }
        if (variableName.IsEmpty)
            throw new TemplateParsingException($"Variable name is missing in template: {templateName.ToString()}, near position: {parseIdx}.");

        return new VariableNode(variableName.ToString(), required, filters);
    }

    internal struct BranchParseState
    {
        public Dictionary<string, ITemplateNode[]> branchOptions;
        public int endCmdIdx;
        public string keyName;
        public bool? oldSyntax;
        public int pos;
    }
}


internal class TemplateParsingException : Exception
{
    public TemplateParsingException() : base() { }
    public TemplateParsingException(string message) : base(message) { }
    public TemplateParsingException(string message, Exception inner) : base(message, inner) { }
}
