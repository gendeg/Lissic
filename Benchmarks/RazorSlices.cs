using System.IO.Pipelines;
using System.Text;
using RazorSlices;

namespace Benchmarks;

public class RazorSlicesBenchmarks : BaseBenchmarks
{
    PipeWriter buffer;
    MemoryStream ms;

    public RazorSlicesBenchmarks()
    {
        ms = new MemoryStream();
        buffer = PipeWriter.Create(ms);
    }

    private static async Task RenderHelper(RazorSlice slice, PipeWriter thisBuffer)
    {
        await slice.RenderAsync(thisBuffer);
        await thisBuffer.FlushAsync();
    }

    public override string Render()
    {
        MemoryStream thisMs = new();
        PipeWriter thisBuffer = PipeWriter.Create(thisMs);
        using RazorSlice slice = Slices.Product.Create(Products);
        RenderHelper(slice,thisBuffer).GetAwaiter().GetResult();
        thisMs.Position = 0;
        using StreamReader reader = new(thisMs, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public async Task RenderToResult()
    {
        ms.Position = 0;
        using RazorSlice slice = Slices.Product.Create(Products);
        await slice.RenderAsync(buffer);
    }
}
