using System.Buffers;
using System.Globalization;
using System.Text;

namespace Lissic.Tests.TemplateValues;


public class VariableValueTests
{
    [Theory]
    [InlineData(typeof(bool), VariableValueType.Bool)]
    [InlineData(typeof(byte), VariableValueType.Byte)]
    [InlineData(typeof(sbyte), VariableValueType.SByte)]
    [InlineData(typeof(DateTimeOffset), VariableValueType.DateTimeOffset)]
    [InlineData(typeof(decimal), VariableValueType.Decimal)]
    [InlineData(typeof(double), VariableValueType.Double)]
    [InlineData(typeof(float), VariableValueType.Float)]
    [InlineData(typeof(Half), VariableValueType.Half)]
    [InlineData(typeof(int), VariableValueType.Int)]
    [InlineData(typeof(uint), VariableValueType.UInt)]
    [InlineData(typeof(Int128), VariableValueType.Int128)]
    [InlineData(typeof(UInt128), VariableValueType.UInt128)]
    [InlineData(typeof(long), VariableValueType.Long)]
    [InlineData(typeof(ulong), VariableValueType.ULong)]
    [InlineData(typeof(nint), VariableValueType.NInt)]
    [InlineData(typeof(nuint), VariableValueType.NUInt)]
    [InlineData(typeof(short), VariableValueType.Short)]
    [InlineData(typeof(ushort), VariableValueType.UShort)]
    public void VariableValue_CanIngestAndReturnUnmanagedValue(Type expectedType, VariableValueType expectedKind)
    {
        dynamic input = expectedKind switch
        {
            VariableValueType.Bool            => (bool)true,
            VariableValueType.Byte            => (byte)42,
            VariableValueType.SByte           => (sbyte)-42,
            VariableValueType.DateTimeOffset  => DateTimeOffset.UtcNow,
            VariableValueType.Decimal         => (decimal)3.14,
            VariableValueType.Double          => (double)3.14,
            VariableValueType.Float           => (float)3.14,
            VariableValueType.Half            => (Half)3.14,
            VariableValueType.Int             => (int)-42,
            VariableValueType.UInt            => (uint)42,
            VariableValueType.Int128          => (Int128)(-42),
            VariableValueType.UInt128         => (UInt128)42,
            VariableValueType.Long            => (long)-42,
            VariableValueType.ULong           => (ulong)42,
            VariableValueType.NInt            => (nint)(-42),
            VariableValueType.NUInt           => (nuint)42,
            VariableValueType.Short           => (short)-42,
            VariableValueType.UShort          => (ushort)42,
            _ => throw new ArgumentOutOfRangeException()
        };

        VariableValue value = input;
        Assert.Equal(expectedKind, value.Kind);

        object roundTripped = expectedKind switch
        {
            VariableValueType.Bool            => value.AsBool(),
            VariableValueType.Byte            => value.AsByte(),
            VariableValueType.SByte           => value.AsSByte(),
            VariableValueType.DateTimeOffset  => value.AsDateTimeOffset(),
            VariableValueType.Decimal         => value.AsDecimal(),
            VariableValueType.Double          => value.AsDouble(),
            VariableValueType.Float           => value.AsFloat(),
            VariableValueType.Half            => value.AsHalf(),
            VariableValueType.Int             => value.AsInt(),
            VariableValueType.UInt            => value.AsUInt(),
            VariableValueType.Int128          => value.AsInt128(),
            VariableValueType.UInt128         => value.AsUInt128(),
            VariableValueType.Long            => value.AsLong(),
            VariableValueType.ULong           => value.AsULong(),
            VariableValueType.NInt            => value.AsNInt(),
            VariableValueType.NUInt           => value.AsNUInt(),
            VariableValueType.Short           => value.AsShort(),
            VariableValueType.UShort          => value.AsUShort(),
            _ => throw new ArgumentOutOfRangeException()
        };

        Assert.Equal(expectedType, roundTripped.GetType());
        Assert.Equal(input, roundTripped);
    }

    [Theory]
    [InlineData(typeof(char))]
    [InlineData(typeof(string))]
    [InlineData(typeof(ReadOnlyMemory<byte>))]
    public void VariableValue_CanIngestAndReturnUtf8TextTypes(Type textType)
    {
        dynamic input = textType switch
        {
            Type t when t == typeof(char) => 'a',
            Type t when t == typeof(string) => "hello",
            Type t when t == typeof(ReadOnlyMemory<byte>) => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes("hello").AsMemory(),
            _ => throw new ArgumentOutOfRangeException()
        };
        VariableValue value = input;

        Assert.Equal(VariableValueType.Utf8Text, value.Kind);

        string roundTripped = Encoding.UTF8.GetString(value.AsUtf8Text());
        string expected = textType switch
        {
            Type t when t == typeof(char) => "a",
            Type t when t == typeof(string) => "hello",
            Type t when t == typeof(ReadOnlyMemory<byte>) => "hello",
            _ => throw new ArgumentOutOfRangeException()
        };

        Assert.Equal(expected, roundTripped);
    }

    [Theory]
    [InlineData(VariableValueType.Bool)]
    [InlineData(VariableValueType.Byte)]
    [InlineData(VariableValueType.SByte)]
    [InlineData(VariableValueType.DateTimeOffset)]
    [InlineData(VariableValueType.Decimal)]
    [InlineData(VariableValueType.Double)]
    [InlineData(VariableValueType.Float)]
    [InlineData(VariableValueType.Half)]
    [InlineData(VariableValueType.Int)]
    [InlineData(VariableValueType.UInt)]
    [InlineData(VariableValueType.Int128)]
    [InlineData(VariableValueType.UInt128)]
    [InlineData(VariableValueType.Long)]
    [InlineData(VariableValueType.ULong)]
    [InlineData(VariableValueType.NInt)]
    [InlineData(VariableValueType.NUInt)]
    [InlineData(VariableValueType.Short)]
    [InlineData(VariableValueType.UShort)]
    public void VariableValue_CanIngestAndReturnUnmanagedAsString(VariableValueType expectedKind)
    {
        dynamic input = expectedKind switch
        {
            VariableValueType.Bool            => (bool)true,
            VariableValueType.Byte            => (byte)42,
            VariableValueType.SByte           => (sbyte)-42,
            VariableValueType.DateTimeOffset  => DateTimeOffset.UtcNow,
            VariableValueType.Decimal         => (decimal)3.14,
            VariableValueType.Double          => (double)3.14,
            VariableValueType.Float           => (float)3.14,
            VariableValueType.Half            => (Half)3.14,
            VariableValueType.Int             => (int)-42,
            VariableValueType.UInt            => (uint)42,
            VariableValueType.Int128          => (Int128)(-42),
            VariableValueType.UInt128         => (UInt128)42,
            VariableValueType.Long            => (long)-42,
            VariableValueType.ULong           => (ulong)42,
            VariableValueType.NInt            => (nint)(-42),
            VariableValueType.NUInt           => (nuint)42,
            VariableValueType.Short           => (short)-42,
            VariableValueType.UShort          => (ushort)42,
            _ => throw new ArgumentOutOfRangeException()
        };

        VariableValue value = input;
        Assert.Equal(expectedKind, value.Kind);

        string expected = input switch
        {
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            _ => input.ToString(),
        };

        Assert.Equal(expected, value.ToString());
    }

    [Theory]
    [InlineData(typeof(char))]
    [InlineData(typeof(string))]
    [InlineData(typeof(ReadOnlyMemory<byte>))]
    public void VariableValue_CanIngestAndReturnUtf8TextAsString(Type textType)
    {
        dynamic input = textType switch
        {
            Type t when t == typeof(char) => 'a',
            Type t when t == typeof(string) => "hello",
            Type t when t == typeof(ReadOnlyMemory<byte>) => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes("hello").AsMemory(),
            _ => throw new ArgumentOutOfRangeException()
        };
        VariableValue value = input;

        Assert.Equal(VariableValueType.Utf8Text, value.Kind);

        string expected = textType switch
        {
            Type t when t == typeof(char) => "a",
            Type t when t == typeof(string) => "hello",
            Type t when t == typeof(ReadOnlyMemory<byte>) => "hello",
            _ => throw new ArgumentOutOfRangeException()
        };

        Assert.Equal(expected, value.ToString());
    }

    [Theory]
    [InlineData(VariableValueType.Bool)]
    [InlineData(VariableValueType.Byte)]
    [InlineData(VariableValueType.SByte)]
    [InlineData(VariableValueType.DateTimeOffset)]
    [InlineData(VariableValueType.Decimal)]
    [InlineData(VariableValueType.Double)]
    [InlineData(VariableValueType.Float)]
    [InlineData(VariableValueType.Half)]
    [InlineData(VariableValueType.Int)]
    [InlineData(VariableValueType.UInt)]
    [InlineData(VariableValueType.Int128)]
    [InlineData(VariableValueType.UInt128)]
    [InlineData(VariableValueType.Long)]
    [InlineData(VariableValueType.ULong)]
    [InlineData(VariableValueType.NInt)]
    [InlineData(VariableValueType.NUInt)]
    [InlineData(VariableValueType.Short)]
    [InlineData(VariableValueType.UShort)]
    public void VariableValue_CanIngestAndWriteUnmanagedTypes(VariableValueType expectedKind)
    {
        dynamic input = expectedKind switch
        {
            VariableValueType.Bool            => (bool)true,
            VariableValueType.Byte            => (byte)42,
            VariableValueType.SByte           => (sbyte)-42,
            VariableValueType.DateTimeOffset  => DateTimeOffset.UtcNow,
            VariableValueType.Decimal         => (decimal)3.14,
            VariableValueType.Double          => (double)3.14,
            VariableValueType.Float           => (float)3.14,
            VariableValueType.Half            => (Half)3.14,
            VariableValueType.Int             => (int)-42,
            VariableValueType.UInt            => (uint)42,
            VariableValueType.Int128          => (Int128)(-42),
            VariableValueType.UInt128         => (UInt128)42,
            VariableValueType.Long            => (long)-42,
            VariableValueType.ULong           => (ulong)42,
            VariableValueType.NInt            => (nint)(-42),
            VariableValueType.NUInt           => (nuint)42,
            VariableValueType.Short           => (short)-42,
            VariableValueType.UShort          => (ushort)42,
            _ => throw new ArgumentOutOfRangeException()
        };

        VariableValue value = input;
        Assert.Equal(expectedKind, value.Kind);

        ArrayBufferWriter<byte> writer = new();
        int bytesWritten = value.WriteTo(writer);

        string expected = input switch
        {
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            _ => input.ToString(),
        };

        Assert.Equal(expected, Encoding.UTF8.GetString(writer.WrittenSpan));
        Assert.Equal(writer.WrittenCount, bytesWritten);
    }

    [Fact]
    public void Default_IsEmpty_ToStringReturnsEmptyString()
    {
        Assert.Equal(string.Empty, default(VariableValue).ToString());
    }

    [Fact]
    public void Default_IsEmpty_WriteToWritesNothing()
    {
        ArrayBufferWriter<byte> writer = new();
        int bytesWritten = default(VariableValue).WriteTo(writer);

        Assert.Equal(0, writer.WrittenCount);
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void Default_IsEmpty_HasEmptyKind()
    {
        VariableValue value = default;

        Assert.True(value.IsEmpty);
        Assert.Equal(VariableValueType.Empty, value.Kind);
    }

    [Fact]
    public void ZeroLengthText_IsNotEmpty()
    {
        VariableValue value = "";

        Assert.False(value.IsEmpty);
        Assert.Equal(VariableValueType.Utf8Text, value.Kind);
    }

    [Fact]
    public void Utf8Text_WriteTo_PreservesBytesAndReturnsByteCount()
    {
        VariableValue value = "héllo 世界";
        ArrayBufferWriter<byte> writer = new();

        int bytesWritten = value.WriteTo(writer);

        byte[] expected = Encoding.UTF8.GetBytes("héllo 世界");
        Assert.Equal(expected, writer.WrittenSpan.ToArray());
        Assert.Equal(expected.Length, bytesWritten);
    }

    [Fact]
    public void Int128AndUInt128_WriteTo_FormatsBoundaryValues()
    {
        AssertWriteToMatchesToString(Int128.MinValue);
        AssertWriteToMatchesToString(UInt128.MaxValue);
    }

    private static void AssertWriteToMatchesToString(VariableValue value)
    {
        ArrayBufferWriter<byte> writer = new();

        int bytesWritten = value.WriteTo(writer);

        Assert.Equal(value.ToString(), Encoding.UTF8.GetString(writer.WrittenSpan));
        Assert.Equal(writer.WrittenCount, bytesWritten);
    }

    [Fact]
    public void TypedAccessors_ThrowOnKindMismatch()
    {
        Assert.Throws<KindMismatchException>(() => new VariableValue(1L).AsDouble());
        Assert.Throws<KindMismatchException>(() => new VariableValue("text").AsLong());
        Assert.Throws<KindMismatchException>(() => default(VariableValue).AsUtf8Text());
    }
}


public class BranchValueTests
{
    [Fact]
    public void Key_ReturnsConstructorValue()
    {
        BranchValue branch = new("abc");
        Assert.Equal("abc", branch.Key);
    }
}

public class LoopValueTests
{
    [Fact]
    public void Children_CountAndGetChild_UseConstructorChildren()
    {
        TestTemplate firstChild = new();
        TestTemplate secondChild = new();
        List<LissicTemplate> children = [firstChild, secondChild];
        LoopValue loop = children;

        Assert.Same(children, loop.Children);
        Assert.Equal(2, loop.Count);
        Assert.Same(firstChild, loop.GetChild(0));
        Assert.Same(secondChild, ((LoopValue)loop).GetChild(1));
    }

    private sealed class TestTemplate : LissicTemplate
    {
        public override int Template__Index => 0;

        public override string Get__BranchKey(ReadOnlySpan<char> keyName) => throw new NotImplementedException();
        public override LissicTemplate? Get__Part(ReadOnlySpan<char> name) => throw new NotImplementedException();
        public override string? Get__VariableString(ReadOnlySpan<char> name) => throw new NotImplementedException();
        public override LoopValue? Get__Loop(ReadOnlySpan<char> name) => throw new NotImplementedException();
        public override int Stream__Variable(ReadOnlySpan<char> name, IBufferWriter<byte> writer) => throw new NotImplementedException();
    }
}

public class FormattedVariableAttributeTests
{
    private readonly struct DummyFormatter : IVariableFormatter
    {
        public static int Format(in VariableValue value, IBufferWriter<byte> output, ReadOnlySpan<string> args) { return 0; }
    }

    [Fact]
    public void Constructor_StoresVariableNameAndFormatterType()
    {
        FormattedVariableAttribute<DummyFormatter> attr = new("phone");

        Assert.Equal("phone", attr.VariableName);
        Assert.Equal(typeof(DummyFormatter), attr.FormatterType);
    }
}
