# Getting Started

##### Table of Contents
- Installation
- Introduction to Using Lissic
  - Creating a Template
  - Passing Values to Lissic Templates
  - Rendering Lissic Templates
  - Lissic Tag Types
- WebApplicationBuilder and Dependency Injection

---

### Installation

Lissic is available on [NuGet](https://www.nuget.org/packages/Lissic) and requires .NET 8.0 or newer.
```bash
dotnet add package Lissic
```

---

### Introduction to Using Lissic

##### Creating a Template

When using Lissic, the first thing that needs to be done is defining a template.  Templates can be declared by applying the `TemplateText` attribute to an empty partial class.  Example:
```csharp
using Lissic;

[TemplateText("Hello {{ name }}!")]
public partial class SimpleTemplate {}
```

Templates can also be declared by referencing a file with the `TemplatePath` attribute.  By default, Lissic will look for templates in a Templates directory under the project root directory (see ConfigurationAndOptions.md for information on changing the default).  Example:
```csharp
using Lissic;

// Will look for a template at {AppContext.BaseDirectory}/Templates/examples/hello.html
[TemplatePath("/examples/hello.html")]
public partial class SimpleTemplate {}
```

##### Passing Values to Lissic Templates

When a template is declared, Lissic automatically converts the class into a data transfer object (DTO) for the template.  Every tag in the template will result in a public property being generated for the tag.  Example:
```csharp
using Lissic;

[TemplateText("Hello {{ name }}!")]
public partial class SimpleTemplate {}

SimpleTemplate myTemplate = new() { name = "John" };
```

Template variables can accept most built-in types, as well as DateTimeOffset objects.  For a full list of accepted types, see DefiningTemplatesAndDTOs.md.  Example:
```csharp
using Lissic;

[TemplateText("Hello {{ name }}!")]
public partial class SimpleTemplate {}

// All of the following are valid, even if they are weird values to assign to "name"

SimpleTemplate myDateTemplate = new() { name = DateTimeOffset.UtcNow };

SimpleTemplate myIntTemplate = new() { name = 10 };

SimpleTemplate myFloatTemplate = new() { name = 3.14 };
```

##### Rendering Lissic Templates

To render a template, use the `LissicProcessor` class.  `LissicProcessor` has two rendering methods:
- `void RenderToBuffer(LissicTemplate template, IBufferWriter<byte> writer)`
- `string RenderToString(LissicTemplate template, int sizeHint = 128)`

`RenderToBuffer` writes the template, in UTF-8 text, to the `IBufferWriter<byte>` passed into the method.  `RenderToString` renders the template to an internal buffer and then returns it as a string.  SizeHint is the initial size of the internal buffer.

In most cases, `LissicProcessor` should be accessed as an `ILissicProcessor` object retrieved from a dependency injection engine.  See RenderingTemplates.md for instruction on manually setting up a `LissicProcessor`.
```csharp
using Lissic;

[TemplateText("Hello {{ name }}!")]
public partial class SimpleTemplate {}

SimpleTemplate myTemplate = new() { name = "John" };

// Writes "Hello John!" to the HTTP response body
lissicProcessor.RenderToBuffer(myTemplate, HttpResponse.BodyWriter);

// Sets myString to "Hello John!"
string myString = lissicProcessor.RenderToString(myTemplate);
```

##### Lissic Tag Types

In the above example, `{{ name }}` is a variable tag.  There are four types of tags in Lissic.  These tags are fully explained in TemplateTags.md.
- *Variable*: Inserts a value into the template.  Variable tags only need the name of the variable, e.g. `{{ myVariableName }}`.  Variable tags can also have filters applied to them by adding filter names after the variable name inside angle brackets `< >`.  Arguments can be passed inside parentheses `( )`, and multiple filters can be separated by a pipe `|` (e.g. `{{ myVariableName <filter | filterWithArgs('arg1', 'arg2')> }}`).
- *Part* or *SubTemplate*: Inserts another template into the current template.  Part tags must begin with "part." or "subTemplate.", followed by the tag name (e.g. `{{ part.otherTemplate }}` or `{{ subTemplate.otherTemplate }}`).
- *Loop* or *ForEach*: Inserts a list of templates into the current template.  Loop tags must begin with "loop." or "forEach.", followed by the tag name (e.g. `{{ loop.otherTemplateList }}` or `{{ forEach.otherTemplateList }}`).
- *Branch* or *Switch*: Enables branching logic in templates.  Branch tags must begin with "branch." or "switch.", followed by the tag name.  The name must be followed by options enclosed in brackets `[ ]`.  Each option uses the following format: "keyValue => branchResult;", where branchResult can be any of the four tag types, or a literal text value preceded by `@` (e.g. `{{ branch.myBranch [key1 => name; key2 => part.otherPart;] }}`, `{{ switch.loggedIn [True => part.loggedInMessage; False => @Please <a href='/login'>log in</a>.; ] }}`).

The following example shows all four tag types in action:
```csharp
using Lissic;

[TemplateText(@"Hello {{ name }}!

Our next Templating Mania meeting will be on {{ date }} at {{ time <append('pm')|upper> }}.  Our meeting has the following agenda:
{{ loop.meetingTopics }}
{{ branch.memberStatus [
    Gold => part.goldMessage;
    Standard => part.standardMessage;
    None => @We look forward to seeing you there!  Please consider signing up to be a member today at {{ signUpLink }}!;
] }}

Best Wishes,

{{ signature }}")]
public partial class NotSoSimpleTemplate {}

// AgendaItem is another template, defined elsewhere
List<LissicTemplate> agenda = [];
forEach (var d in databaseData)
{
    AgendaItem item = new() { topic = d.topic };
    agenda.Add(item);
}

NotSoSimpleTemplate message = new()
{
    name = "John Doe",
    date = "Wednesday, January 15th",
    time = "4:30",
    meetingTopics = agenda,
    memberStatus = "None",
    signUpLink = "<a href='www.templatemania.com/signup'>templatemania.com</a>",
    signature = "George Washington\nFounder, Templating Mania"
};

LissicProcessor processor = new();
string processedMessage = processor.RenderToString(message);

/* processedMessage now equals:
Hello John Doe!

Our next Templating Mania meeting will be on Wednesday, January 15th at 4:30PM.  Our meeting has the following agenda:
 - February template activities
 - Election for new club Vice President
 - Budget

We look forward to seeing you there!  Please consider signing up to be a member today at <a href='www.templatemania.com/signup'>templatemania.com</a>

Best Wishes,

Julius Caesar
Founder, Templating Mania
*/
```

---

### WebApplicationBuilder and Dependency Injection

Lissic can be automatically added to `WebApplicationBuilder`, or any other dependency injection system that uses `IServiceCollection`, using the `AddLissicAsync` method.  `AddLissicAsync` will lock in the Lissic options settings and register both `LissicProcessor` and `ILissicProcessor` in the dependency injection engine as scoped dependencies (configurable to transient in options).

Options can be passed to `AddLissicAsync` either directly as a `LissicOptions` object or as an `Action<LissicOptions>`.

Examples:
```csharp
using Lissic;

WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args });

await builder.Services.AddLissicAsync(options =>
{
    options.EnableCycleDetection = false;
});

WebApplication app = builder.Build();
```

OR

```csharp
using Lissic;

ServiceCollection services = new();

LissicOptions options = new()
{
    EnableCycleDetection = true
};

await services.AddLissicAsync(options);
```

---
