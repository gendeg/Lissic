using System.Buffers;
using Lissic.Tools;

namespace Lissic.Filters.BuiltIn;


/// <summary>
/// <para>Makes input safe for HTML text by escaping '&amp;' and '&lt;' characters.</para>
/// <para>Safe between tags (e.g. "&lt;div>This_Text_Is_Safe&lt;/div>")</para>
/// <para>Not safe inside of tags or attributes (e.g. "&lt;div attr='Not_Safe'>")</para>
/// </summary>
[FilterName("htmlTextSafe")]
public class HtmlTextSafe() : IFilterFactory
{
    public static ITemplateFilter Create(ReadOnlySpan<string> args) => new HtmlTextSafe();
    public int Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output)
    {
        int index = 0;
        int bytesWritten = 0;

        int next = input.IndexOfAny(HtmlSafeSearch.Text);
        while(next != -1)
        {
            output.Write(input[index..next]);
            output.Write(HtmlSafeSearch.Escapes[input[next]].Span);
            bytesWritten += next + 6 - index;  // All return values are 6 bytes

            index = next + 1;  // All search values are 1 byte
            next = input[index..].IndexOfAny(HtmlSafeSearch.Text);
            next = next < 0 ? -1 : index + next;
        }
        output.Write(input[index..]);
        return bytesWritten + input.Length - index;
    }
}
