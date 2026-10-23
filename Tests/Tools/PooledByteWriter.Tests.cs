using Lissic.Tools;
using System.Buffers;

namespace Lissic.Tests.PooledByteWriterTests;


public class PooledByteWriterTests
{
    private static byte[] GetWrittenBytes(PooledByteWriter writer)
        => writer.ToArray().ToArray();

    [Fact]
    public void Constructor_Default_StartsEmpty()
    {
        using PooledByteWriter writer = new();

        Assert.Equal(0, writer.Length);
        Assert.Empty(writer.ToArray());
    }

    [Fact]
    public void Constructor_CustomCapacity_StartsEmpty()
    {
        using PooledByteWriter writer = new(128);

        Assert.Equal(0, writer.Length);
        Assert.Empty(writer.ToArray());
    }

    [Fact]
    public void Write_AppendsBytes_UpdatesLength()
    {
        using PooledByteWriter writer = new();
        writer.Write("Hello"u8);

        Assert.Equal(5, writer.Length);
        Assert.Equal("Hello"u8.ToArray(), GetWrittenBytes(writer));
    }

    [Fact]
    public void Write_MultipleCalls_AppendsSequentially()
    {
        using PooledByteWriter writer = new();
        writer.Write("Hello, "u8);
        writer.Write("World!"u8);

        Assert.Equal("Hello, World!"u8.ToArray(), GetWrittenBytes(writer));
    }

    [Fact]
    public void Write_EmptySpan_DoesNotChangeLength()
    {
        using PooledByteWriter writer = new();
        writer.Write("abc"u8);
        writer.Write(ReadOnlySpan<byte>.Empty);

        Assert.Equal(3, writer.Length);
    }

    [Fact]
    public void Write_ExceedingInitialCapacity_GrowsBufferAndPreservesExistingData()
    {
        using PooledByteWriter writer = new(4);
        writer.Write("ab"u8);
        writer.Write("cdefghij"u8);

        Assert.Equal("abcdefghij"u8.ToArray(), GetWrittenBytes(writer));
    }

    [Fact]
    public void GetSpan_ReturnsSpanAtLeastRequestedSize()
    {
        using PooledByteWriter writer = new(4);

        Span<byte> span = writer.GetSpan(16);

        Assert.True(span.Length >= 16);
    }

    [Fact]
    public void GetSpan_WhenRequestExceedsCapacity_GrowsBufferWithoutLosingWrittenData()
    {
        using PooledByteWriter writer = new(4);
        writer.Write("ab"u8);

        Span<byte> span = writer.GetSpan(32);

        Assert.True(span.Length >= 32);
        Assert.Equal("ab"u8.ToArray(), GetWrittenBytes(writer));
    }

    [Fact]
    public void GetMemory_ReturnsMemoryAtLeastRequestedSize()
    {
        using PooledByteWriter writer = new(4);

        Memory<byte> memory = writer.GetMemory(16);

        Assert.True(memory.Length >= 16);
    }

    [Fact]
    public void Advance_IncreasesLength()
    {
        using PooledByteWriter writer = new();
        Span<byte> span = writer.GetSpan(4);
        "abcd"u8.CopyTo(span);

        writer.Advance(4);

        Assert.Equal(4, writer.Length);
        Assert.Equal("abcd"u8.ToArray(), GetWrittenBytes(writer));
    }

    [Fact]
    public void Advance_TypicalBufferWriterPattern_ProducesCorrectData()
    {
        using PooledByteWriter writer = new(4);

        Span<byte> first = writer.GetSpan(3);
        "foo"u8.CopyTo(first);
        writer.Advance(3);

        Span<byte> second = writer.GetSpan(3);
        "bar"u8.CopyTo(second);
        writer.Advance(3);

        Assert.Equal("foobar"u8.ToArray(), GetWrittenBytes(writer));
    }

    [Fact]
    public void Reset_ResetsLengthToZero()
    {
        using PooledByteWriter writer = new();
        writer.Write("abc"u8);

        writer.Reset();

        Assert.Equal(0, writer.Length);
    }

    [Fact]
    public void Reset_ThenWrite_StartsFromBeginning()
    {
        using PooledByteWriter writer = new();
        writer.Write("abcdef"u8);

        writer.Reset();
        writer.Write("xy"u8);

        Assert.Equal("xy"u8.ToArray(), GetWrittenBytes(writer));
    }

    [Fact]
    public void Export_ReturnsOnlyWrittenBytes()
    {
        using PooledByteWriter writer = new(4);
        writer.Write("hello world"u8);

        ReadOnlyMemory<byte> exported = writer.ToArray();

        Assert.Equal("hello world"u8.ToArray(), exported.ToArray());
    }

    [Fact]
    public void Export_Empty_ReturnsEmptyMemory()
    {
        using PooledByteWriter writer = new();

        Assert.Empty(writer.ToArray());
    }

    [Fact]
    public void Export_ReflectsSubsequentWrites()
    {
        using PooledByteWriter writer = new();
        writer.Write("abc"u8);
        ReadOnlyMemory<byte> firstExport = writer.ToArray();
        writer.Write("def"u8);

        Assert.Equal("abc"u8.ToArray(), firstExport.ToArray());
        Assert.Equal("abcdef"u8.ToArray(), writer.ToArray());
    }

    [Fact]
    public void WrittenSpan_ReturnsOnlyWrittenBytes()
    {
        using PooledByteWriter writer = new(4);
        writer.Write("hello world"u8);

        ReadOnlySpan<byte> writtenSpan = writer.WrittenSpan;

        Assert.Equal("hello world"u8.ToArray(), writtenSpan.ToArray());
    }

    [Fact]
    public void WrittenSpan_Empty_ReturnsEmptyMemory()
    {
        using PooledByteWriter writer = new();

        Assert.Equal(0, writer.WrittenSpan.Length);
    }

    [Fact]
    public void WrittenSpan_ReflectsSubsequentWrites()
    {
        using PooledByteWriter writer = new();
        writer.Write("abc"u8);
        ReadOnlySpan<byte> firstExport = writer.WrittenSpan;
        writer.Write("def"u8);

        Assert.Equal("abc"u8.ToArray(), firstExport.ToArray());
        Assert.Equal("abcdef"u8.ToArray(), writer.WrittenSpan.ToArray());
    }

    [Fact]
    public void WriteTo_CopiesWrittenBytesToTargetWriter()
    {
        using PooledByteWriter writer = new(4);
        writer.Write("hello world"u8);
        ArrayBufferWriter<byte> target = new();

        writer.WriteTo(target);

        Assert.Equal("hello world"u8.ToArray(), target.WrittenSpan.ToArray());
    }

    [Fact]
    public void WriteTo_Empty_WritesNothing()
    {
        using PooledByteWriter writer = new();
        ArrayBufferWriter<byte> target = new();

        writer.WriteTo(target);

        Assert.Equal(0, target.WrittenCount);
    }

    [Fact]
    public void Dispose_ReturnsBufferToPool_DoesNotThrow()
    {
        using PooledByteWriter writer = new();
        writer.Write("data"u8);

        Exception? ex = Record.Exception(writer.Dispose);

        Assert.Null(ex);
    }

    [Fact]
    public void GetString_DecodesWrittenUtf8()
    {
        using PooledByteWriter writer = new();
        writer.Write("Hello, 世界! 🙂🙂🙂🙂🙂"u8);

        Assert.Equal("Hello, 世界! 🙂🙂🙂🙂🙂", writer.GetString());
    }

    [Fact]
    public void GetString_Empty_ReturnsEmptyString()
    {
        using PooledByteWriter writer = new();

        Assert.Equal(string.Empty, writer.GetString());
    }

    [Fact]
    public void Capacity_IsAtLeastInitialCapacity()
    {
        using PooledByteWriter writer = new(100);

        Assert.True(writer.Capacity >= 100);
    }

    [Fact]
    public void Capacity_GrowsWhenWritingPastIt_AndSurvivesReset()
    {
        using PooledByteWriter writer = new(4);
        int initial = writer.Capacity;

        writer.Write(new byte[initial + 1]);
        int grown = writer.Capacity;
        writer.Reset();

        Assert.True(grown > initial);
        Assert.Equal(grown, writer.Capacity);
    }

    [Fact]
    public void Write_AfterGrowing_DoesNotDependOnClearedMemory()
    {
        using PooledByteWriter first = new(4);
        first.Write(new byte[64].Select(_ => (byte)0xFF).ToArray());
        first.Dispose();

        // The pool may hand back the dirty array; only the written range may be visible.
        using PooledByteWriter second = new(4);
        second.Write("ab"u8);

        Assert.Equal("ab"u8.ToArray(), GetWrittenBytes(second));
    }

    [Fact]
    public void ToArrayOutput_NotAffectedBySubsequentWritesAfterReset()
    {
        using PooledByteWriter writer = new();
        writer.Write("initial"u8);
        ReadOnlyMemory<byte> firstExport = writer.ToArray();
        Assert.Equal("initial"u8.ToArray(), firstExport.ToArray());

        writer.Reset();
        Assert.Empty(writer.ToArray());
        writer.Write("new stuff"u8);
        Assert.Equal("initial"u8.ToArray(), firstExport.ToArray());
        Assert.Equal("new stuff"u8.ToArray(), writer.ToArray());
        Assert.Equal("new stuff"u8.ToArray(), writer.WrittenSpan.ToArray());
    }
}
