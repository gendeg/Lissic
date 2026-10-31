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
        catalogMs = new MemoryStream();
        catalogBuffer = PipeWriter.Create(catalogMs);
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

    public override string RenderCatalog()
    {
        MemoryStream thisMs = new();
        PipeWriter thisBuffer = PipeWriter.Create(thisMs);
        using RazorSlice slice = Slices.Catalog.Create(CatalogData);
        RenderHelper(slice, thisBuffer).GetAwaiter().GetResult();
        thisMs.Position = 0;
        using StreamReader reader = new(thisMs, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public async Task RenderCatalogToResult()
    {
        catalogMs.Position = 0;
        using RazorSlice slice = Slices.Catalog.Create(CatalogData);
        await slice.RenderAsync(catalogBuffer);
    }

    private readonly MemoryStream catalogMs;
    private readonly PipeWriter catalogBuffer;

    public async Task RenderToResult()
    {
        ms.Position = 0;
        using RazorSlice slice = Slices.Product.Create(Products);
        await slice.RenderAsync(buffer);
    }
}
