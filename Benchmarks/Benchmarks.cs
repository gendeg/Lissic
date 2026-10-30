using BenchmarkDotNet.Attributes;
//using Microsoft.VSDiagnostics;


namespace Benchmarks;

[MemoryDiagnoser, ShortRunJob]
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
    }

    [Benchmark(Baseline = true)]
    public string Lissic_RenderToString()
    {
        return lissicBenchmarks.Render();
    }

    [Benchmark]
    public void Lissic_RenderToBuffer()
    {
        lissicBenchmarks.RenderToBuffer();
    }

    [Benchmark]
    public string DotLiquid_Render()
    {
        return dotLiquidBenchmarks.Render();
    }

    [Benchmark]
    public string Fluid_Render()
    {
        return fluidBenchmarks.Render();
    }

    [Benchmark]
    public async Task RazorSlices_RenderToBuffer()
    {
        await razorSlicesBenchmarks.RenderToResult();
    }

    [Benchmark]
    public string Scriban_Render()
    {
        return scribanBenchmarks.Render();
    }

    [Benchmark]
    public string Handlebars_Render()
    {
        return handlebarsBenchmarks.Render();
    }
}

