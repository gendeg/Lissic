
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

- Ultra-fast: renders near twice as fast as the fastest major .NET templating engine
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

<sub>**Verified with a simple single-file ASP.NET app created by QWEN3.8-27B, using only the Lissic documentation as a reference*</sub>

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

The benchmarks below use Fluid's standard benchmarking job, which creates an HTML list with 100 formatted rows.

| Method                       | Mean       | Error     | Ratio  | Gen0    | Gen1   | Allocated | Alloc Ratio |
|----------------------------- |-----------:|----------:|-------:|--------:|-------:|----------:|------------:|
| **Lissic_Render_ToString**   |**8.814 us**| 1.8091 us |**0.57**|  0.3510 |      - |   18072 B |    **0.49** |
| **Lissic_Render_ToBuffer**   |**8.482 us**| 0.2396 us |**0.54**|       - |      - |   **0 B** |    **0.00** |
| Fluid_Render                 |  15.593 us | 1.7042 us |   1.00 |  0.7324 |      - |   37128 B |        1.00 |
| Fluid_SourceGenerated_Render |  24.045 us | 1.2778 us |   1.54 |  0.9766 |      - |   49128 B |        1.32 |
| Scriban_Render               |  70.985 us | 6.6909 us |   4.55 |  2.8076 | 0.3662 |  144025 B |        3.88 |
| DotLiquid_Render             | 173.521 us | 8.7739 us |  11.13 | 13.4277 | 2.4414 |  674928 B |       18.18 |
| Handlebars_Render            |  21.944 us | 1.1924 us |   1.41 |  2.4109 |      - |  122176 B |        3.29 |

<sup>*These benchmarks were captured with BenchmarkDotNet, on a 9800X3D with 32GB of RAM, using the latest versions of each library (as of 20 Oct 2026).*</sup>

---

### Acknowledgements

Huge thank you to Andrew Lock and his [Creating a Source Generator](https://andrewlock.net/series/creating-a-source-generator/) series!

Benchmarks based on the suite created by Sébastien Ros and [Fluid](https://github.com/sebastienros/fluid).

---

### Feature Wishlist

None of the following is guaranteed, and items may be added or removed without any notice.  However, this is what the current wishlist looks like:
- Add support for literal text in loops
- Add logging hooks
- Support external data sources (e.g. JSON files/objects)
