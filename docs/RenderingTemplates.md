# Rendering Templates

##### Table of Contents
- LissicProcessor
  - Using LissicProcessor
  - Setup
  - Limitations

---

### LissicProcessor

Lissic templates are rendered using the `LissicProcessor` class.  `LissicProcessor` is capable of rendering a template as UTF-8 output to any stream compatible with `IBufferWriter<byte>`, such as `HttpResponse.BodyWriter`.  Alternatively, `LissicProcessor` can return a rendered template as a string.

Rendering a template to a stream is allocation free (unless the `EnableCycleDetection` option is enabled).

`LissicProcessor` implements the `ILissicProcessor` interface to allow easy test mocking.

*`LissicProcessor` is **not** thread safe* but is cheap to spin up and dispose of.  `LissicProcessor` is registered as a scoped dependency by default (configurable to transient in options).

##### Using LissicProcessor

`LissicProcessor` and `ILissicProcessor` have the following two methods:
- `void RenderToBuffer(LissicTemplate template, IBufferWriter<byte> writer)`
- `string RenderToString(LissicTemplate template, int sizeHint = 128)`

`RenderToBuffer` will directly write the rendered text, in UTF-8 format, to an `IBufferWriter<byte>`.
`RenderToString` will write the rendered text to an internal buffer and then return the contents of that buffer as a string.  The `sizeHint` argument sets the initial size of the buffer.

```csharp
using Lissic;

[TemplateText("Hello {{ name }}!")]
public partial class SimpleTemplate {}

public class ProcessorDemo(LissicProcessor processor)
{
    public void Execute(IBufferWriter<byte> writer)
    {
        SimpleTemplate myTemplate = new() { name = "John" };

        // Writes "Hello John!" to the HTTP response body
        lissicProcessor.RenderToBuffer(myTemplate, writer);

        // Sets myString to "Hello John!"
        string myString = lissicProcessor.RenderToString(myTemplate);
    }
}

SimpleTemplate myTemplate = new() { name = "John" };

// Writes "Hello John!" to the HTTP response body
lissicProcessor.RenderToBuffer(myTemplate, HttpResponse.BodyWriter);

// Sets myString to "Hello John!"
string myString = lissicProcessor.RenderToString(myTemplate);
```

##### Setup

In most cases, `ILissicProcessor` should be registered with and retrieved from a dependency injection engine (using the `AddLissicAsync` method on an `IServiceCollection`).  See GettingStarted.md for additional instructions on registering Lissic with any `IServiceCollection`, including a `WebApplicationBuilder`.

To manually initiate `LissicProcessor`, do the following, in order:
1. Set Lissic options using the static method `LissicConfigurator.SetOptions(options)`, where `options` is an instance of `LissicOptions`.  This must only be done once (see note 1 below).
2. Register filters by calling `Lissic_FilterRegistration.Register()`.
3. Initiate a new instance of `TemplateCache` and run `TemplateCache.Initialize()`.  This should only be done once (see note 2 below).
4. Initiate a new instance of `LissicProcessor` and pass the `TemplateCache` as a constructor argument.

NOTE 1: `LissicConfigurator.SetOptions()` can only be run once.  Lissic settings will then be locked for the rest of the application's runtime.  Any further attempts to set new options will result in an `InvalidOperationException`.  Initializing `TemplateCache` without running `LissicConfigurator.SetOptions()` will lock the settings to default values.

NOTE 2: Initializing a `TemplateCache` instance parses all templates, which would be an expensive action to take each time Lissic is used.  The `TemplateCache` should be initialized once and then passed to all `LissicProcessor` instances.

---
