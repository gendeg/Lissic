using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Lissic;

public readonly partial struct VariableValue : ITemplateValue
{
    public VariableValue(bool value)
    {
        type = VariableValueType.Bool;
        rawUnmanagedValue = new UnmanagedStore { Bool = value };
    }
    public static implicit operator VariableValue(bool value) => new(value);

    public VariableValue(byte value)
    {
        type = VariableValueType.Byte;
        rawUnmanagedValue = new UnmanagedStore { Byte = value };
    }
    public static implicit operator VariableValue(byte value) => new(value);

    public VariableValue(sbyte value)
    {
        type = VariableValueType.SByte;
        rawUnmanagedValue = new UnmanagedStore { SByte = value };
    }
    public static implicit operator VariableValue(sbyte value) => new(value);

    public VariableValue(DateTimeOffset value)
    {
        type = VariableValueType.DateTimeOffset;
        rawUnmanagedValue = new UnmanagedStore { DateTimeOffset = value };
    }
    public static implicit operator VariableValue(DateTimeOffset value) => new(value);

    public VariableValue(decimal value)
    {
        type = VariableValueType.Decimal;
        rawUnmanagedValue = new UnmanagedStore { Decimal = value };
    }
    public static implicit operator VariableValue(decimal value) => new(value);

    public VariableValue(double value)
    {
        type = VariableValueType.Double;
        rawUnmanagedValue = new UnmanagedStore { Double = value };
    }
    public static implicit operator VariableValue(double value) => new(value);

    public VariableValue(float value)
    {
        type = VariableValueType.Float;
        rawUnmanagedValue = new UnmanagedStore { Float = value };
    }
    public static implicit operator VariableValue(float value) => new(value);

    public VariableValue(Half value)
    {
        type = VariableValueType.Half;
        rawUnmanagedValue = new UnmanagedStore { Half = value };
    }
    public static implicit operator VariableValue(Half value) => new(value);

    public VariableValue(int value)
    {
        type = VariableValueType.Int;
        rawUnmanagedValue = new UnmanagedStore { Int = value };
    }
    public static implicit operator VariableValue(int value) => new(value);

    public VariableValue(uint value)
    {
        type = VariableValueType.UInt;
        rawUnmanagedValue = new UnmanagedStore { UInt = value };
    }
    public static implicit operator VariableValue(uint value) => new(value);

    public VariableValue(Int128 value)
    {
        type = VariableValueType.Int128;
        rawUnmanagedValue = new UnmanagedStore { Int128 = value };
    }
    public static implicit operator VariableValue(Int128 value) => new(value);

    public VariableValue(UInt128 value)
    {
        type = VariableValueType.UInt128;
        rawUnmanagedValue = new UnmanagedStore { UInt128 = value };
    }
    public static implicit operator VariableValue(UInt128 value) => new(value);

    public VariableValue(long value)
    {
        type = VariableValueType.Long;
        rawUnmanagedValue = new UnmanagedStore { Long = value };
    }
    public static implicit operator VariableValue(long value) => new(value);

    public VariableValue(ulong value)
    {
        type = VariableValueType.ULong;
        rawUnmanagedValue = new UnmanagedStore { ULong = value };
    }
    public static implicit operator VariableValue(ulong value) => new(value);

    public VariableValue(nint value)
    {
        type = VariableValueType.NInt;
        rawUnmanagedValue = new UnmanagedStore { NInt = value };
    }
    public static implicit operator VariableValue(nint value) => new(value);

    public VariableValue(nuint value)
    {
        type = VariableValueType.NUInt;
        rawUnmanagedValue = new UnmanagedStore { NUInt = value };
    }
    public static implicit operator VariableValue(nuint value) => new(value);

    public VariableValue(short value)
    {
        type = VariableValueType.Short;
        rawUnmanagedValue = new UnmanagedStore { Short = value };
    }
    public static implicit operator VariableValue(short value) => new(value);

    public VariableValue(ushort value)
    {
        type = VariableValueType.UShort;
        rawUnmanagedValue = new UnmanagedStore { UShort = value };
    }
    public static implicit operator VariableValue(ushort value) => new(value);

    public VariableValue(char value)
    {
        type = VariableValueType.Utf8Text;
        rawTextValue = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes([value]));
    }
    public static implicit operator VariableValue(char value) => new(value);

    public VariableValue(ReadOnlyMemory<byte> utf8Bytes)
    {
        type = VariableValueType.Utf8Text;
        rawTextValue = utf8Bytes;
    }
    public static implicit operator VariableValue(ReadOnlyMemory<byte> utf8Bytes) => new(utf8Bytes);

    public VariableValue(string value)
    {
        type = VariableValueType.Utf8Text;
        rawTextValue = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(value));
    }
    public static implicit operator VariableValue(string value) => new(value);

    public bool AsBool() => type == VariableValueType.Bool ? rawUnmanagedValue.Bool : throw ThrowKindMismatch(VariableValueType.Bool);
    public byte AsByte() => type == VariableValueType.Byte ? rawUnmanagedValue.Byte : throw ThrowKindMismatch(VariableValueType.Byte);
    public sbyte AsSByte() => type == VariableValueType.SByte ? rawUnmanagedValue.SByte : throw ThrowKindMismatch(VariableValueType.SByte);
    public DateTimeOffset AsDateTimeOffset() => type == VariableValueType.DateTimeOffset ? rawUnmanagedValue.DateTimeOffset : throw ThrowKindMismatch(VariableValueType.DateTimeOffset);
    public decimal AsDecimal() => type == VariableValueType.Decimal ? rawUnmanagedValue.Decimal : throw ThrowKindMismatch(VariableValueType.Decimal);
    public double AsDouble() => type == VariableValueType.Double ? rawUnmanagedValue.Double : throw ThrowKindMismatch(VariableValueType.Double);
    public float AsFloat() => type == VariableValueType.Float ? rawUnmanagedValue.Float : throw ThrowKindMismatch(VariableValueType.Float);
    public Half AsHalf() => type == VariableValueType.Half ? rawUnmanagedValue.Half : throw ThrowKindMismatch(VariableValueType.Half);
    public int AsInt() => type == VariableValueType.Int ? rawUnmanagedValue.Int : throw ThrowKindMismatch(VariableValueType.Int);
    public uint AsUInt() => type == VariableValueType.UInt ? rawUnmanagedValue.UInt : throw ThrowKindMismatch(VariableValueType.UInt);
    public Int128 AsInt128() => type == VariableValueType.Int128 ? rawUnmanagedValue.Int128 : throw ThrowKindMismatch(VariableValueType.Int128);
    public UInt128 AsUInt128() => type == VariableValueType.UInt128 ? rawUnmanagedValue.UInt128 : throw ThrowKindMismatch(VariableValueType.UInt128);
    public long AsLong() => type == VariableValueType.Long ? rawUnmanagedValue.Long : throw ThrowKindMismatch(VariableValueType.Long);
    public ulong AsULong() => type == VariableValueType.ULong ? rawUnmanagedValue.ULong : throw ThrowKindMismatch(VariableValueType.ULong);
    public nint AsNInt() => type == VariableValueType.NInt ? rawUnmanagedValue.NInt : throw ThrowKindMismatch(VariableValueType.NInt);
    public nuint AsNUInt() => type == VariableValueType.NUInt ? rawUnmanagedValue.NUInt : throw ThrowKindMismatch(VariableValueType.NUInt);
    public short AsShort() => type == VariableValueType.Short ? rawUnmanagedValue.Short : throw ThrowKindMismatch(VariableValueType.Short);
    public ushort AsUShort() => type == VariableValueType.UShort ? rawUnmanagedValue.UShort : throw ThrowKindMismatch(VariableValueType.UShort);
    public ReadOnlySpan<byte> AsUtf8Text() => type == VariableValueType.Utf8Text ? rawTextValue.Span : throw ThrowKindMismatch(VariableValueType.Utf8Text);

    public override string ToString()
    {
        return type switch
        {
            VariableValueType.Empty => string.Empty,
            VariableValueType.Bool => rawUnmanagedValue.Bool.ToString(),
            VariableValueType.Byte => rawUnmanagedValue.Byte.ToString(),
            VariableValueType.SByte => rawUnmanagedValue.SByte.ToString(),
            VariableValueType.DateTimeOffset => rawUnmanagedValue.DateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            VariableValueType.Decimal => rawUnmanagedValue.Decimal.ToString(),
            VariableValueType.Double => rawUnmanagedValue.Double.ToString(),
            VariableValueType.Float => rawUnmanagedValue.Float.ToString(),
            VariableValueType.Half => rawUnmanagedValue.Half.ToString(),
            VariableValueType.Int => rawUnmanagedValue.Int.ToString(),
            VariableValueType.UInt => rawUnmanagedValue.UInt.ToString(),
            VariableValueType.Int128 => rawUnmanagedValue.Int128.ToString(),
            VariableValueType.UInt128 => rawUnmanagedValue.UInt128.ToString(),
            VariableValueType.Long => rawUnmanagedValue.Long.ToString(),
            VariableValueType.ULong => rawUnmanagedValue.ULong.ToString(),
            VariableValueType.NInt => rawUnmanagedValue.NInt.ToString(),
            VariableValueType.NUInt => rawUnmanagedValue.NUInt.ToString(),
            VariableValueType.Short => rawUnmanagedValue.Short.ToString(),
            VariableValueType.UShort => rawUnmanagedValue.UShort.ToString(),
            VariableValueType.Utf8Text => Encoding.UTF8.GetString(rawTextValue.Span),
            _ => throw new InvalidOperationException($"Unknown value type: {type}.  This should not happen.  Unless you have modified the template system, please report this issue to the Lissic maintainers.")
        };
    }

    public int WriteTo(IBufferWriter<byte> writer)
    {
        switch (type)
        {
            case VariableValueType.Empty:
                return 0;

            case VariableValueType.Bool:
                Span<byte> span = writer.GetSpan(5);
                if (Utf8Formatter.TryFormat(rawUnmanagedValue.Bool, span, out int bytesWritten))
                    writer.Advance(bytesWritten);
                return bytesWritten;

            case VariableValueType.Byte:
                span = writer.GetSpan(4);
                if (Utf8Formatter.TryFormat(rawUnmanagedValue.Byte, span, out bytesWritten))
                    writer.Advance(bytesWritten);
                return bytesWritten;

            case VariableValueType.SByte:
                span = writer.GetSpan(4);
                if (Utf8Formatter.TryFormat(rawUnmanagedValue.SByte, span, out bytesWritten))
                    writer.Advance(bytesWritten);
                return bytesWritten;

            case VariableValueType.DateTimeOffset:
                span = writer.GetSpan(40);
                StandardFormat format = StandardFormat.Parse("O");
                if (Utf8Formatter.TryFormat(rawUnmanagedValue.DateTimeOffset, span, out bytesWritten, format))
                    writer.Advance(bytesWritten);
                return bytesWritten;

            case VariableValueType.Decimal:
                span = writer.GetSpan(32);
                if (Utf8Formatter.TryFormat(rawUnmanagedValue.Decimal, span, out bytesWritten))
                    writer.Advance(bytesWritten);
                return bytesWritten;

            case VariableValueType.Double:
                span = writer.GetSpan(32);
                if (Utf8Formatter.TryFormat(rawUnmanagedValue.Double, span, out bytesWritten))
                    writer.Advance(bytesWritten);
                return bytesWritten;

            case VariableValueType.Float:
                span = writer.GetSpan(32);
                if (Utf8Formatter.TryFormat(rawUnmanagedValue.Float, span, out bytesWritten))
                    writer.Advance(bytesWritten);
                return bytesWritten;

            case VariableValueType.Half:
                Span<char> charSpan = stackalloc char[32];
                if (rawUnmanagedValue.Half.TryFormat(charSpan, out int charsWritten))
                {
                    Span<byte> writeSpan = writer.GetSpan(32);
                    int written = Encoding.UTF8.GetBytes(charSpan[..charsWritten], writeSpan);
                    writer.Advance(written);
                }
                return charsWritten;

            case VariableValueType.Int:
                span = writer.GetSpan(11);
                if (Utf8Formatter.TryFormat(rawUnmanagedValue.Int, span, out bytesWritten))
                    writer.Advance(bytesWritten);
                return bytesWritten;

            case VariableValueType.UInt:
                span = writer.GetSpan(11);
                if (Utf8Formatter.TryFormat(rawUnmanagedValue.UInt, span, out bytesWritten))
                    writer.Advance(bytesWritten);
                return bytesWritten;

            case VariableValueType.Int128:
                charSpan = stackalloc char[40];
                if (rawUnmanagedValue.Int128.TryFormat(charSpan, out charsWritten))
                {
                    Span<byte> writeSpan = writer.GetSpan(40);
                    int written = Encoding.UTF8.GetBytes(charSpan[..charsWritten], writeSpan);
                    writer.Advance(written);
                }
                return charsWritten;

            case VariableValueType.UInt128:
                charSpan = stackalloc char[40];
                if (rawUnmanagedValue.UInt128.TryFormat(charSpan, out charsWritten))
                {
                    Span<byte> writeSpan = writer.GetSpan(40);
                    int written = Encoding.UTF8.GetBytes(charSpan[..charsWritten], writeSpan);
                    writer.Advance(written);
                }
                return charsWritten;

            case VariableValueType.Long:
                span = writer.GetSpan(20);
                if (Utf8Formatter.TryFormat(rawUnmanagedValue.Long, span, out bytesWritten))
                    writer.Advance(bytesWritten);
                return bytesWritten;

            case VariableValueType.ULong:
                span = writer.GetSpan(20);
                if (Utf8Formatter.TryFormat(rawUnmanagedValue.ULong, span, out bytesWritten))
                    writer.Advance(bytesWritten);
                return bytesWritten;

            case VariableValueType.NInt:
                span = writer.GetSpan(20);
                if (Utf8Formatter.TryFormat(rawUnmanagedValue.NInt, span, out bytesWritten))
                    writer.Advance(bytesWritten);
                return bytesWritten;

            case VariableValueType.NUInt:
                span = writer.GetSpan(20);
                if (Utf8Formatter.TryFormat(rawUnmanagedValue.NUInt, span, out bytesWritten))
                    writer.Advance(bytesWritten);
                return bytesWritten;

            case VariableValueType.Short:
                span = writer.GetSpan(6);
                if (Utf8Formatter.TryFormat(rawUnmanagedValue.Short, span, out bytesWritten))
                    writer.Advance(bytesWritten);
                return bytesWritten;

            case VariableValueType.UShort:
                span = writer.GetSpan(6);
                if (Utf8Formatter.TryFormat(rawUnmanagedValue.UShort, span, out bytesWritten))
                    writer.Advance(bytesWritten);
                return bytesWritten;

            case VariableValueType.Utf8Text:
                writer.Write(rawTextValue.Span);
                return rawTextValue.Length;
        }
        return 0;
    }

    public KindMismatchException ThrowKindMismatch(VariableValueType requested)
        => new($"Cannot read a {type} property value as {requested}.");
}


public class DateFormatter : IVariableFormatter
{
    const int MaxDateBufferSize = 1024;

    public static int Format(in VariableValue value, IBufferWriter<byte> output, ReadOnlySpan<string> args)
    {
        if (value.Kind != VariableValueType.DateTimeOffset)
        {
            UnknownTagDebug.Handle(output, $"VariableValue passed to DateFormatter is not a DateTimeOffset");
            return 0;
        }

        int size = 64;
        while (true)
        {
            Span<byte> span = output.GetSpan(size);
            if (value.AsDateTimeOffset().TryFormat(span, out int bytesWritten, args[0], CultureInfo.InvariantCulture))
            {
                output.Advance(bytesWritten);
                return bytesWritten;
            }

            size += 64;

            if (size >= MaxDateBufferSize)
                throw new InvalidOperationException(
                    $"Buffer size exceeded {MaxDateBufferSize} bytes while formatting DateTimeOffset.");
        }
    }
}

public class KindMismatchException : Exception
{
    public KindMismatchException() : base() { }
    public KindMismatchException(string message) : base(message) { }
    public KindMismatchException(string message, Exception inner) : base(message, inner) { }
}
