using System.Buffers;
using System.Collections.Frozen;

namespace Lissic.Tools;

public static class HtmlSafeSearch
{
    public static readonly SearchValues<byte> Text = 
        SearchValues.Create([0x26, 0x3C]);
        //                     &     <

    public static readonly SearchValues<byte> Attribute = 
        SearchValues.Create([0x26, 0x3C, 0x3E, 0x22, 0x27, 0x2F, 0x60]);
        //                     &     <     >     "     '     /     `


    // NOTE: Current implementation assumes all search values are 1 byte and return values are 6 bytes
    public static readonly FrozenDictionary<byte, ReadOnlyMemory<byte>> Escapes = 
        new Dictionary<byte, ReadOnlyMemory<byte>> {
            {0x26, "&#x26;"u8.ToArray()},  // &
            {0x3C, "&#x3C;"u8.ToArray()},  // <
            {0x3E, "&#x3E;"u8.ToArray()},  // >
            {0x22, "&#x22;"u8.ToArray()},  // "
            {0x27, "&#x27;"u8.ToArray()},  // '
            {0x2F, "&#x2F;"u8.ToArray()},  // /
            {0x60, "&#x60;"u8.ToArray()}   // `
        }.ToFrozenDictionary();
}
