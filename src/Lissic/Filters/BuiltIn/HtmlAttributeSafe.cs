using System.Buffers;
using Lissic.Tools;

namespace Lissic.Filters.BuiltIn;


/// <summary>
/// <para>Makes input safe for HTML attributes by escaping &amp;, &lt;, >, ", ', /, and ` characters.</para>
/// <para>Safe between tags (e.g. "&lt;div>This_Text_Is_Safe&lt;/div>") and in attributes (e.g. "&lt;div attr='Safe'>")</para>
/// <para>Not safe anywhere else (e.g. in scripts, inside tags while outside attributes)</para>
/// </summary>
[FilterName("htmlAttrSafe")]
public class HtmlAttributeSafe() : IFilterFactory
{
    public static ITemplateFilter Create(ReadOnlySpan<string> args) => new HtmlAttributeSafe();
    public int Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output)
    {
        int index = 0;
        int bytesWritten = 0;

        int next = input.IndexOfAny(HtmlSafeSearch.Attribute);
        while(next != -1)
        {
            output.Write(input[index..next]);
            output.Write(HtmlSafeSearch.Escapes[input[next]].Span);
            bytesWritten += next + 6 - index;  // All return values are 6 bytes

            index = next + 1;  // All search values are 1 byte
            next = input[index..].IndexOfAny(HtmlSafeSearch.Attribute);
            next = next < 0 ? -1 : index + next;
        }
        output.Write(input[index..]);
        return bytesWritten + input.Length - index;
    }
}
