# Configuration and Options

##### Table of Contents
- LissicOptions
- Project-Level Configuration

---

### LissicOptions

The following options exist in the `LissicOptions` class:

**Setting:** `EnableCycleDetection`
**Behavior:** Enables or disables cycle detection to prevent infinite loops.  When enabled, a `TemplateRenderException` will be thrown if a cycle is detected during template rendering.  A cycle is defined as a situation where a template instance indirectly references itself through another template.  References to other instances of the same template will not trigger a cycle.  This option causes a small amount of allocation during template rendering and should be disabled if zero allocation is desired.
**Default:** false

**Setting:** `OutputSizeThreshold`
**Behavior:** Rendering stops when the output size, in bytes, exceeds this threshold.  Set to 0 to disable the limit.
**Default:** 10MB

**Setting:** `RecursionDepthLimit`
**Behavior:** Specifies the maximum allowed recursion depth during template rendering.
**Default:** 64

**Setting:** `RegisterAsTransient`
**Behavior:** Determines whether `LissicProcessor` and `ILissicProcessor` should be registered as transient services in the dependency injection container.  When false, they will be registered as scoped services.
**Default:** false

**Setting:** `UnknownBranchKeyDebugMode`
**Behavior:** Sets the behavior for handling unknown branch keys.  This option will only take effect if built in Debug mode and has no effect in Release builds.  In Release builds, unknown keys will always be skipped silently.
**Default:** `UnknownBranchKeyDebugMode.Skip` (enum)

**Setting:** `UnknownTagDebugMode`
**Behavior:** Sets the behavior for handling unknown tags.  This option will only take effect if built in Debug mode and has no effect in Release builds.  In Release builds, unknown tags will always be skipped silently.
**Default:** `UnknownTagDebugMode.Skip` (enum)

---

### Project-Level Configuration

The following settings are set in the project's `.csproj` file:

**Setting:** `LissicIgnoreRequiredProperties`
**Behavior:** When set to `true`, all properties on template data transfer objects will be optional.  Otherwise, all properties will be `required` unless flagged with a `?` in the template.
**Default:** false
**Example:**
```xml
  <PropertyGroup>
    <LissicIgnoreRequiredProperties>true</LissicIgnoreRequiredProperties>
  </PropertyGroup>
```

**Setting:** `LissicTemplateRoot`
**Behavior:** Sets the directory where Lissic will look for templates.  This directory is relative to the project's root directory.  This is also the directory where the templates will be copied when the project is built.  This value must be a relative path.
**Default:** Templates
**Example:**
```xml
  <PropertyGroup>
    <LissicTemplateRoot>Themes/SunshineExpress/Templates</LissicTemplateRoot>
  </PropertyGroup>
```

**Setting:** `LissicTemplateSourceRoot`
**Behavior:** Sets the directory where Lissic will look for templates when executing a template hot reload in debug builds.  This value can be a relative path (relative to the project's root directory when compiled) or a full path.
**Default:** The full path to the directory identified by `LissicTemplateRoot`
**Example:**
```xml
  <PropertyGroup>
    <LissicTemplateSourceRoot>C:/WebProject/src/Templates</LissicTemplateSourceRoot>
  </PropertyGroup>
```

---