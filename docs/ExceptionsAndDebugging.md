# Exceptions and Debugging

- Hot Reload (Debug Only)
- Diagnostics
- Exceptions
  - KindMismatchException
  - TemplateParsingException
  - TemplateRenderException

---

### Hot Reload (Debug Only)

The `TemplateCache` class has a `ReloadPathTemplates` method to reload file-based templates during runtime.  This method is only available in debug builds and takes one optional argument for a `CancellationToken`.

`ReloadPathTemplates` should only be used in development environments due to the following limitations:
- The `TemplateCache` must be initialized before the reload can be executed (this is done automatically during startup when registered with an `IServiceCollection`).
- Reloads are not thread safe and rendering templates while the reload is processing can lead to unpredictable results.
- Reloads are executed synchronously (due to restrictions on debug-only methods), but the `CancellationToken` will be passed to the file IO.
- The data transfer objects are defined at compile time and will not be updated for any tags that are added, removed, or modified.

---

### Diagnostics

##### LISSIC001 - DuplicateFormatter

*Error* - Indicates that a template variable has two formatters applied to it.  Each variable can only have a single formatter.

##### LISSIC002 - FilterIsNotAFactory

*Error* - A class has the `FilterName` attribute, but does not implement `IFilterFactory`.

##### LISSIC003 - DuplicateFilterName

*Warning* - Two custom filters have the same name.  Custom filters are allowed to have the same name as built-in filters, and the custom filters will take precedence.

##### LISSIC004 - FilterIsNotAccessible

*Error* - A filter is declared that is either not accessable (e.g. private), or is contained inside another type that is not accessible.

##### LISSIC005 - InvalidTemplate

*Error* - The template has a formatting error that prevents it from being parsed.

##### LISSIC006 - UnknownFilter

*Warning* - An unknown or unregistered filter was called by a template.

##### LISSIC007 - MissingTemplateFile

*Error* - A `TemplatePath` value cannot be resolved to a file.

##### LISSIC008 - EmptyTemplateFile

*Warning* - The file identified by a `TemplatePath` attribute is empty.

---

### Exceptions

##### KindMismatchException

`KindMismatchException` is caused by using a `VariableValue` value access method that does not match `VariableValue.Kind`.

This should only be possible inside formatters, so custom formatters should preemptively check `VariableValue.Kind` and explicitly handle deviations from the expected kind.

##### TemplateParsingException

`TemplateParsingException` is caused by improper formatting in template text.  This exception will come with a message that describes the error and where it occurred.

##### TemplateRenderException

`TemplateRenderException` notes a critical problem during template rendering.  This exception will only be thrown under the following circumstances:
- `UnknownTagDebugMode` is set to `ThrowException`, and an empty tag value is encountered
- `UnknownBranchKeyDebugMode` is set to `ThrowException`, and an empty key is encountered
- `EnableCycleDetection` is enabled and a cycle is detected
- `RecursionDepthLimit` is exceeded
- `OutputSizeThreshold` is exceeded

---
