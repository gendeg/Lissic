using System.Runtime.InteropServices;

namespace Lissic;


public interface ITemplateValue {}


public readonly struct BranchValue(string key) : ITemplateValue
{
    public string Key => key;

    public static implicit operator BranchValue(string value) => new(value);
}


public readonly struct LoopValue(List<LissicTemplate> children) : ITemplateValue
{
    public List<LissicTemplate> Children => children;
    public int Count => children?.Count ?? 0;
    public LissicTemplate GetChild(int index) => children[index];

    public static implicit operator LoopValue(List<LissicTemplate> value) => new(value);
}


public readonly struct PartValue(LissicTemplate? template) : ITemplateValue
{
    public LissicTemplate? Template => template;

    public static implicit operator PartValue(LissicTemplate? value) => new(value);
}


public readonly partial struct VariableValue : ITemplateValue
{
    private readonly ReadOnlyMemory<byte> rawTextValue;
    private readonly UnmanagedStore rawUnmanagedValue;
    private readonly VariableValueType type;

    public bool IsEmpty => type == VariableValueType.Empty;
    public VariableValueType Kind => type;

    [StructLayout(LayoutKind.Explicit)]
    private struct UnmanagedStore
    {
        [FieldOffset(0)] public bool Bool;
        [FieldOffset(0)] public byte Byte;
        [FieldOffset(0)] public sbyte SByte;
        [FieldOffset(0)] public DateTimeOffset DateTimeOffset;
        [FieldOffset(0)] public decimal Decimal;
        [FieldOffset(0)] public double Double;
        [FieldOffset(0)] public float Float;
        [FieldOffset(0)] public Half Half;
        [FieldOffset(0)] public int Int;
        [FieldOffset(0)] public uint UInt;
        [FieldOffset(0)] public Int128 Int128;
        [FieldOffset(0)] public UInt128 UInt128;
        [FieldOffset(0)] public long Long;
        [FieldOffset(0)] public ulong ULong;
        [FieldOffset(0)] public nint NInt;
        [FieldOffset(0)] public nuint NUInt;
        [FieldOffset(0)] public short Short;
        [FieldOffset(0)] public ushort UShort;
    }
}


public enum VariableValueType : byte
{
    Empty,
    Bool,
    Byte,
    SByte,
    DateTimeOffset,
    Decimal,
    Double,
    Float,
    Half,
    Int,
    UInt,
    Int128,
    UInt128,
    Long,
    ULong,
    NInt,
    NUInt,
    Short,
    UShort,
    Utf8Text
}
