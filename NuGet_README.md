Lissic is a web and text templating engine for .NET.  It provides the following features:

- Ultra-fast: renders faster than other major .NET templating engines**
- Zero allocation by default when rendering to a buffer (e.g. `HttpResponse.BodyWriter`)**
- 100% AOT compatible without any restrictions (no use of `Reflection` or `dynamic`)
- Simple but powerful tagging system that keeps a clear division between layout and logic
- Compile-time safety with source-generated Data Transfer Objects (DTOs) that automatically adjust to your template text
- Define templates directly in your code with text or import them from template files
- Powerful transformation pipeline that allows using Formatters to modify how values are converted to text and Filters to modify text before insertion into the template
- Hot reload for file-based templates in debug mode
- Thorough AI-friendly documentation

For instructions on using Lissic, please see [the documentation](https://github.com/gendeg/Lissic/tree/main/docs).  If you are new to using Lissic, start with the [Getting Started guide](https://github.com/gendeg/Lissic/blob/main/docs/GettingStarted.md).

[Changelog](https://github.com/gendeg/Lissic/blob/main/ChangeLog.md)

** *See benchmarks at the [GitHub repository](https://github.com/gendeg/Lissic/)*
