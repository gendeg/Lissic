using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
//using Microsoft.VSDiagnostics;


namespace Benchmarks;

[GcServer(true)]
[GcConcurrent(false)]
[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
//[CPUUsageDiagnoser]
public class Benchmarks
{
    private readonly LissicBenchmarks lissicBenchmarks = new();
    private readonly DotLiquidBenchmarks dotLiquidBenchmarks = new();
    private readonly FluidBenchmarks fluidBenchmarks = new();
    private readonly HandlebarsBenchmarks handlebarsBenchmarks = new();
    private readonly RazorSlicesBenchmarks razorSlicesBenchmarks = new();
    private readonly ScribanBenchmarks scribanBenchmarks = new();

    public Benchmarks()
    {
        string lissic = lissicBenchmarks.Render();
        string dotLiquid = dotLiquidBenchmarks.Render();
        string fluid = fluidBenchmarks.Render();
        string handlebars = handlebarsBenchmarks.Render();
        string scriban = scribanBenchmarks.Render();
        string razor = razorSlicesBenchmarks.Render();

        if (!BaseBenchmarks.CheckBenchmark(lissic)) throw new Exception($"Lissic failed: {lissic[..100]}");
        if (!BaseBenchmarks.CheckBenchmark(dotLiquid)) throw new Exception($"DotLiquid failed: {dotLiquid[..100]}");
        if (!BaseBenchmarks.CheckBenchmark(fluid)) throw new Exception($"Fluid failed: {fluid[..100]}");
        if (!BaseBenchmarks.CheckBenchmark(handlebars)) throw new Exception($"Handlebars failed: {handlebars[..100]}");
        if (!BaseBenchmarks.CheckBenchmark(scriban)) throw new Exception($"Scriban failed: {scriban[..100]}");
        if (!BaseBenchmarks.CheckBenchmark(razor)) throw new Exception($"Razor failed: {razor[..100]}");

        string lissicC = lissicBenchmarks.RenderCatalog();
        string dotLiquidC = dotLiquidBenchmarks.RenderCatalog();
        string fluidC = fluidBenchmarks.RenderCatalog();
        string handlebarsC = handlebarsBenchmarks.RenderCatalog();
        string scribanC = scribanBenchmarks.RenderCatalog();
        string razorC = razorSlicesBenchmarks.RenderCatalog();

        if (!ComplexBenchmarkData.CheckBenchmark(lissicC)) throw new Exception($"Lissic catalog failed: {lissicC[..100]}");
        if (!ComplexBenchmarkData.CheckBenchmark(dotLiquidC)) throw new Exception($"DotLiquid catalog failed: {dotLiquidC[..100]}");
        if (!ComplexBenchmarkData.CheckBenchmark(fluidC)) throw new Exception($"Fluid catalog failed: {fluidC[..100]}");
        if (!ComplexBenchmarkData.CheckBenchmark(handlebarsC)) throw new Exception($"Handlebars catalog failed: {handlebarsC[..100]}");
        if (!ComplexBenchmarkData.CheckBenchmark(scribanC)) throw new Exception($"Scriban catalog failed: {scribanC[..100]}");
        if (!ComplexBenchmarkData.CheckBenchmark(razorC)) throw new Exception($"Razor catalog failed: {razorC[..100]}");
    }

    // Uncomment to get consistent memory use reporting at the expense of completely unreliable run times and no GC data
    /*
    [IterationCleanup]
    public void Cleanup()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
    */

    [Benchmark(Baseline = true), BenchmarkCategory("Small")]
    public string Lissic_RenderToString()
    {
        return lissicBenchmarks.Render();
    }

    [Benchmark, BenchmarkCategory("Small")]
    public void Lissic_RenderToBuffer()
    {
        lissicBenchmarks.RenderToBuffer();
    }

    [Benchmark, BenchmarkCategory("Small")]
    public string DotLiquid_Render()
    {
        return dotLiquidBenchmarks.Render();
    }

    [Benchmark, BenchmarkCategory("Small")]
    public string Fluid_Render()
    {
        return fluidBenchmarks.Render();
    }

    [Benchmark, BenchmarkCategory("Small")]
    public async Task RazorSlices_RenderToBuffer()
    {
        await razorSlicesBenchmarks.RenderToResult();
    }

    [Benchmark, BenchmarkCategory("Small")]
    public string Scriban_Render()
    {
        return scribanBenchmarks.Render();
    }

    [Benchmark, BenchmarkCategory("Small")]
    public string Handlebars_Render()
    {
        return handlebarsBenchmarks.Render();
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Large")]
    public string Lissic_Catalog_RenderToString()
    {
        return lissicBenchmarks.RenderCatalog();
    }

    [Benchmark, BenchmarkCategory("Large")]
    public void Lissic_Catalog_RenderToBuffer()
    {
        lissicBenchmarks.RenderCatalogToBuffer();
    }

    [Benchmark, BenchmarkCategory("Large")]
    public string DotLiquid_Catalog_Render()
    {
        return dotLiquidBenchmarks.RenderCatalog();
    }

    [Benchmark, BenchmarkCategory("Large")]
    public string Fluid_Catalog_Render()
    {
        return fluidBenchmarks.RenderCatalog();
    }

    [Benchmark, BenchmarkCategory("Large")]
    public async Task RazorSlices_Catalog_RenderToBuffer()
    {
        await razorSlicesBenchmarks.RenderCatalogToResult();
    }

    [Benchmark, BenchmarkCategory("Large")]
    public string Scriban_Catalog_Render()
    {
        return scribanBenchmarks.RenderCatalog();
    }

    [Benchmark, BenchmarkCategory("Large")]
    public string Handlebars_Catalog_Render()
    {
        return handlebarsBenchmarks.RenderCatalog();
    }
}
