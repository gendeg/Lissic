# Defining Templates and DTOs

- Defining Templates
  - Text Templates
  - Path Templates
  - Requirements (partial, partial hierarchy)
- Initiating a Data Transfer Object (DTO)
  - Required vs Optional values
- Variable Values
  - Formatters
- Part Values
- Loop Values
- Branch Values

---

### Defining Templates

Templates in Lissic can be defined by declaring the template text directly in code or declaring a path to a file with the template's text.

Templates are declared by applying an `attribute` to a `class`.  The template class must be `partial`.  If the class is nested inside other classes, all parent classes must also be `partial`.  The class does not need to have any members (e.g. properties, fields, methods).

Templates will be rendered by outputting all of text exactly as it appears in the template, except for text contained inside tags.  Tags are identified by double curly brackets to both open and close the tag (e.g. `{{ myTag }}`).  For more information on the available template tags, see TemplateTags.md.

Lissic handles all template text internally in UTF-8 and is compatible with any characters that are available in both .NET UTF-8 and .NET UTF-16 implementations.

##### Text Templates

Text templates are declared with the `TemplateText` attribute.  The `TemplateText` attribute takes a single string argument that contains the entire text of the template.  Example:

```csharp
using Lissic;

[TemplateText("<li>{{ product }}: ${{ price }}</li>")]
public partial class ProductListItem {}

ProductListItem myTemplate = new()
{
    product = "Antimatter",
    price = 31.42
};

// Will render as "<li>Antimatter: $31.42</li>"
```

##### Path Templates

Path templates are declared with the `TemplatePath` attribute.  The `TemplatePath` attribute takes a single string argument that contains the path to the template file.  By default, Lissic looks for template files starting in a Templates directory at the root of the current project.  This can be modified by setting a `LissicTemplateRoot` property in your project's .csproj file.  See ConfigurationAndOptions.md for more information.  Example:

```html
<!-- this is myTemplate.html -->
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>{{ title }}</title>
</head>
<body>
    <p>Hello World, this is {{ myName }}!</p> 
</body>
</html>
```

```csharp
using Lissic;

// Will look for the file in {AppContext.BaseDirectory}/Templates/pages/myTemplate.html
[TemplatePath("/pages/myTemplate.html")]
public partial class MyPage {}

MyPage myPage = new()
{
    title = "My Page Title",
    myName = "Julius Caesar"
};

/* Will render as:
<!-- this is myTemplate.html -->
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>My Page Title</title>
</head>
<body>
    <p>Hello World, this is Julius Caesar!</p> 
</body>
</html>
*/
```

---

### Initiating a Data Transfer Object (DTO)

All template classes are automatically turned into DTOs for that class, via a source generator.  If you change the tags in a template, the source generator will automatically change the associated DTO properties.  New templates and recent changes may require a build for your IDE to be aware of the source generated properties.

To create a new DTO for a template, create a new instance of the template class.  All required values must be assigned when the template instance is created.  Values are required by default.  See TemplateTags.md for information on making tags, and their associated DTO properties, optional.

All tags in the template will result in an associated public property being created for the template DTO, using the name of the tag.  For example, a `{{ part.Header }}` tag will result in a `public PartValue Header { get; set; }` property being created for the DTO.  The tag names are case sensitive, so `{{ part.heADeR }}` will result in a `public PartValue heADeR { get; set; }` property.

There are four `Type`s used for the properties that correspond to the four template tag types.  These are `BranchValue`, `LoopValue`, `PartValue`, and `VariableValue`. They are further described below.

---

### Variable Values

Variable properties will have a `VariableValue` type.  This is a specialized type that allows for any of the following types to be assigned:

`bool`, `byte`, `sbyte`, `char`, `DateTimeOffset`, `decimal`, `double`, `float`, `Half`, `int`, `uint`, `Int128`, `UInt128`, `long`, `ulong`, `nint`, `nuint`, `ReadOnlySpan<byte>` (UTF-8 text), `short`, `ushort`, `string`

Each of those types has a default rendering.  `char`, `ReadOnlySpan<byte>`, and `string` will be rendered exactly as they are entered.  All of the numeric values will be rendered with text equivalent to their .NET string representation.  `DateTimeOffset` will be rendered according to .NET `"O"` formatting (equivalent to `"yyyy-MM-ddTHH:mm:ss.fffffffzzz"` as of this document's writing).

The default rendering behavior can be changed by using a formatter.

Example:
```csharp
using Lissic;

[TemplateText("The value of {{ thing }} is {{ modifier }}{{ value }}.")]
public partial class TheDate {}

TheDate today = new()
{
    thing = "Pi",
    modifier = "approximately ",
    value = 3.14
};

// Will render similar to "The value of Pi is approximately 3.14."
```

NOTE: `ReadOnlySpan<byte>` must contain UTF-8 text.  Behavior for assigning a `ReadOnlySpan<byte>` that does not contain UTF-8 text is undefined.

##### Formatters

Formatters are used to change the way a `VariableValue` is rendered.  Formatters are applied as the value is retrieved from the DTO and take effect before any filters are applied.

To use a formatter, apply the `FormattedVariable<T>(string variableName, string[]? args = null)` attribute to the template class.  When applying a `FormattedVariable<T>` attribute, `T` is the class that will be used for the transformation.  `T` must implement `IVariableFormatter`.  The first argument in the attribute is the name of the variable to be modified (e.g. if modifying `{{ todayDate }}`, the first argument should be "todayDate").  The second argument is an optional `string[]` to be passed to the formatter.

The only built-in formatter is `DateFormatter`.  `DateFormatter` requires one additional string argument to pass [a .NET date and time format string](https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings).  Example:

```csharp
using Lissic;

[FormattedVariable<DateFormatter>("date", "yyyy-MM-dd")]
[TemplateText("Today is {{ date }}.")]
public partial class TheDate {}

TheDate today = new() { date = DateTimeOffset.UtcNow };

// Will render similar to "Today is 2013-12-25."
```

---

### Part Values

Part properties can have any template class assigned (i.e. a class with the `TemplateText` or `TemplatePath` attribute applied).  The assigned template will be rendered in place of the part tag.

Example:
```csharp
using Lissic;

[TemplateText("Welcome to {{ city }}!")]
public partial class Welcome {}

Welcome welcome = new() { city = "Atlantis" };

[TemplateText("I have something I want to say...\n{{part.theMessage}}")]
public partial class Message {}

Message message = new() { theMessage = welcome };

/* Will render:
I have something I want to say...
Welcome to Atlantis!
*/
```

---

### Loop Values

Loop values can have a `List<LissicTemplate>` of template classes assigned (i.e. classes with the `TemplateText` or `TemplatePath` attribute applied).  The templates in the list will be rendered, in order, in place of the loop tag.  The list can contain different types of templates.

Example:
```csharp
using Lissic;

[TemplateText("Welcome to {{ city }}!\n")]
public partial class Welcome {}

Welcome welcome = new() { city = "Metropolis" };
Welcome welcome2 = new() { city = "The Emerald City" };

[TemplateText("Wait... those cities don't exist...")]
public partial class Realization {}

Realization real = new();


[TemplateText("I have several things I want to say...\n{{part.theMessages}}")]
public partial class Messages {}

List<LissicTemplate> msgList = [welcome, welcome2, real];

Messages messages = new() { theMessages = msgList };

/* Will render:
I have several things I want to say...
Welcome to Metropolis!
Welcome to The Emerald City!
Wait... those cities don't exist...
*/
```

---

### Branch Values

Branch values store a `string` key that will determine which path the branch takes.  If the key matches one of the keys in the template, that branch will be taken.

If the key isn't found in a release build, the branch will be silently skipped.  If the key isn't found in a debug build, the action specified by the `UnknownBranchKeyDebugMode` option will be taken (default is silently skip).

NOTE: A null or empty branch key behaves according to `UnknownTagDebugMode`.  `UnknownBranchKeyDebugMode` is used when the key exists, but is not found in the template's options.

Example:
```csharp
using Lissic;

[TemplateText("This morning I will {{ branch.isWeekend [true=>@sleep in!; false=>@go to work.;] }}")]
public partial class MorningActivity {}

MorningActivity activity = new() { isWeekend = "true" };

// Will render "This morning I will sleep in!"
```
---
