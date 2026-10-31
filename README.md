
           LLLL        IIIIIIIII    SSSSSS      SSSSSS     IIIIIIIII    CCCCCC
          LLLL        IIIIIIIII   SSSSSSSSS   SSSSSSSSS   IIIIIIIII   CCCCCCCCC
         LLLL           III      SSS         SSS            III      CCC    CC
        LLLL           III       SSSSS       SSSSS         III      CCC
       LLLL           III         SSSSS       SSSSS       III      CCC
      LLLL           III            SSS         SSS      III      CCC    CC
     LLLLLLLLL   IIIIIIIII   SSSSSSSSS   SSSSSSSSS   IIIIIIIII   CCCCCCCCC
    LLLLLLLLL   IIIIIIIII     SSSSSS      SSSSSS    IIIIIIIII     CCCCCC

### Overview

Lissic is a web and text templating engine for .NET.  It provides the following features:

- Ultra-fast: renders faster than other major .NET templating engines
- Zero allocation by default when rendering to a buffer (e.g. `HttpResponse.BodyWriter`)
- 100% AOT compatible without any restrictions (no use of `Reflection` or `dynamic`)
- Simple but powerful tagging system that keeps a clear division between layout and logic
- Compile-time safety with source-generated Data Transfer Objects (DTOs) that automatically adjust to your template text
- Define templates directly in your code with text or import them from template files
- Powerful transformation pipeline that allows using Formatters to modify how values are converted to text and Filters to modify text before insertion into the template
- Hot reload for file-based templates in debug mode
- Thorough AI-friendly documentation*


Lissic is available on [NuGet](https://www.nuget.org/packages/Lissic) and requires .NET 8.0 or newer.
```bash
dotnet add package Lissic
```

For instructions on using Lissic, please see [the documentation](https://github.com/gendeg/Lissic/tree/main/docs).  If you are new to using Lissic, start with the [Getting Started guide](https://github.com/gendeg/Lissic/blob/main/docs/GettingStarted.md).

<sub>**Verified with a simple task management ASP.NET app created by QWEN3.8-27B using only the Lissic documentation as a reference*</sub>

### Simple to Use

Just define your templates and let Lissic prepare the rest:

```csharp
[TemplateText("Hello {{ name <upper> }}!")]
public partial class LissicExample {}

// Automatically transforms the partial class into a DTO, with a property for each tag
LissicExample myTemplate = new() { name = "John" };

// LissicProcessor retrieved from Dependency Injection (see Getting Started)
lissicProcessor.RenderToBuffer(myTemplate, HttpResponse.BodyWriter);

// Writes "Hello JOHN!" to the HTTP response body
```

<sup>*Usage Note: `LissicProcessor` should be used as scoped or transient; they are cheap to create and dispose of, but are not thread safe*</sup>

### Benchmarks

##### Small Benchmark

The benchmarks below use Fluid's standard benchmarking job, which creates an HTML list with 100 formatted rows.

| Method                     | Mean         | Error      | Ratio | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|----------------------------|-------------:|-----------:|------:|---------:|--------:|-----------:|------------:|
| **Lissic_RenderToString**  | **9.635 us** |  0.1307 us |  1.00 |   0.5035 |       - |    18072 B |        1.00 |
| **Lissic_RenderToBuffer**  | **8.822 us** |  0.0068 us |  0.92 |        - |       - |    **0 B** |    **0.00** |
| DotLiquid_Render           |   187.731 us |  1.3993 us | 19.49 |  16.6016 |  0.4883 |   717184 B |       39.68 |
| Fluid_Render               |    16.998 us |  0.1865 us |  1.76 |   1.0681 |       - |    38264 B |        2.12 |
| Handlebars_Render          |    23.916 us |  0.3485 us |  2.48 |   3.1738 |       - |   123184 B |        6.82 |
| RazorSlices_RenderToBuffer |    10.546 us |  0.0930 us |  1.09 |   0.2747 |       - |    10592 B |        0.59 |
| Scriban_Render             |    73.278 us |  0.5067 us |  7.61 |   3.4180 |       - |   154040 B |        8.52 |

##### Large Benchmark

Renders a "Catalog" page with 96 produsts in 8 categories.  Each product has 4 tags, 3 variants in a table, and 3 reviews.  The benchmark features nested loops, boolean decisions, and several common filters.

| Method                     | Mean         | Error      | Ratio | Gen0     | Gen1    | Gen2    | Allocated  | Alloc Ratio |
|----------------------------|-------------:|-----------:|------:|---------:|--------:|--------:|-----------:|------------:|
| Lissic_RenderToString      |   225.724 us |  1.3055 us |  1.00 |  12.4512 | 12.4512 | 12.4512 |   368720 B |        1.00 |
| **Lissic_RenderToBuffer**  |**154.339 us**|  0.2282 us |  0.68 |        - |       - |       - |    **0 B** |    **0.00** |
| DotLiquid_Render           | 3,019.152 us | 23.5912 us | 13.38 | 222.6563 | 70.3125 | 11.7188 | 10730544 B |       29.10 |
| Fluid_Render               |   540.491 us |  1.6578 us |  2.39 |  13.6719 | 13.6719 | 13.6719 |   971080 B |        2.63 |
| Handlebars_Render          |   586.686 us |  7.3129 us |  2.60 |  24.4141 | 11.7188 | 11.7188 |  1355712 B |        3.68 |
| RazorSlices_RenderToBuffer |   191.728 us |  1.4913 us |  0.85 |   5.3711 |  0.2441 |  0.2441 |   199176 B |        0.54 |
| Scriban_Render             | 1,302.858 us | 13.3290 us |  5.77 |  23.4375 | 23.4375 | 23.4375 |  1776696 B |        4.82 |

<sup>*All benchmarks were captured with BenchmarkDotNet, on a 9800X3D with 32GB of RAM, using the latest versions of each library (as of 30 Oct 2026, including pre-release verion for Fluid)*</sup>

---

### Acknowledgements

Huge thank you to Andrew Lock and his [Creating a Source Generator](https://andrewlock.net/series/creating-a-source-generator/) series!

Benchmarks derived from the suite created by Sébastien Ros and [Fluid](https://github.com/sebastienros/fluid).

---

### Feature Wishlist

None of the following is guaranteed, and items may be added or removed without any notice.  However, this is what the current wishlist looks like:
- Add support for literal text in loops
- Add logging hooks
- Support external data sources (e.g. JSON files/objects)
