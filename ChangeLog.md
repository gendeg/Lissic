#### Version 0.1.2 (2026-Oct-25)

- Add documentation comments to template classes that list all generated properties
- Add support for assigning byte[] values to VariableValue; will be interpreted as a UTF-8 string
- Fix an issue where part and loop tags inside branch tags will corrupt subsequent branch keys
- Remove use of double underscores in generated class members
- LISSIC008 diagnostic (empty file) will no longer trigger at the same time as LISSIC007 (missing file)


#### Version 0.1.1 (2026-Oct-23)

- Fix IServiceCollection extensions in .NET 8 and make sure they are awaitable in all versions
- Update PackageTests to automatically build and import the latest code


#### Version 0.1.0 (2026-Oct-22)

- Initial Release Features
  - LissicProcessor
  - TemplateCache
  - Source Generation
  - Formatters and Filters