using System.Buffers;

namespace Lissic.Tools;


public class PooledByteWriter : IDisposable, IBufferWriter<byte>
{
    private byte[] buffer;
    public int Length;
    public int Capacity => buffer.Length;
    public ReadOnlySpan<byte> WrittenSpan => buffer.AsSpan(0, Length);

    public PooledByteWriter(int initialCapacity = 64)
    {
        buffer = ArrayPool<byte>.Shared.Rent(initialCapacity);
        Length = 0;
    }

    public void Advance(int count)
    {
        if (Length + count > buffer.Length)
            Grow(Length + count);
        Length += count;
    }

    public string GetString() => System.Text.Encoding.UTF8.GetString(WrittenSpan);

    public Memory<byte> GetMemory(int size = 1)
    {
        if (size <= 0)
            size = 1;
        if (Length + size > buffer.Length)
            Grow(Length + size);
        return buffer.AsMemory(Length);
    }

    public Span<byte> GetSpan(int size = 1)
    {
        if (size <= 0)
            size = 1;
        if (Length + size > buffer.Length)
            Grow(Length + size);
        return buffer.AsSpan(Length);
    }

    private void Grow(int minCapacity)
    {
        int newSize = Math.Max(buffer.Length * 2, minCapacity);
        byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
        Array.Copy(buffer, 0, newBuffer, 0, Length);
        ArrayPool<byte>.Shared.Return(buffer);
        buffer = newBuffer;
    }

    public void Reset()
    {
        Length = 0;
    }

    public byte[] ToArray()
    {
        return buffer.AsMemory(0, Length).ToArray();
    }

    public void Write(ReadOnlySpan<byte> text)
    {
        if (Length + text.Length > buffer.Length)
            Grow(Length + text.Length);
        
        text.CopyTo(buffer.AsSpan(Length));
        Length += text.Length;
    }

    public void WriteTo(IBufferWriter<byte> writer)
    {
        writer.Write(buffer.AsSpan(0, Length));
    }

    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(buffer);
    }
}
