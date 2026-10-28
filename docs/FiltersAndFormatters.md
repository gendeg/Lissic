# Filters and Formatters

##### Table of Contents
- Using Filters
- Built-In Filters
  - Append
  - Capitalize
  - Lower
  - Prepend
  - Remove
  - RemoveFirst
  - RemoveLast
  - Repeat
  - Replace
  - ReplaceFirst
  - ReplaceLast
  - Slice
  - Trim
  - TrimEnd
  - TrimStart
  - Truncate
  - Upper
- Using Formatters
- Built-In Formatters
  - DateFormatter
- Custom Filters
- Custom Formatters

---

### Using Filters

Filters enable modifications to variable text before they are inserted into the rendered template text.  Multiple filters can be applied to each variable.

To apply a filter, the filter name should be added to the variable tag inside angle brackets `< >` (e.g. `{{ variableName <filterName> }}`).  Filter names are not case sensitive.  All of the following are equivalent: `{{ var <filter> }}`, `{{ var <Filter> }}`, and `{{ var <FiLtER> }}`.

Arguments can be passed to filters using parentheses `( )` (e.g. `{{ variable <filter("filterArg")> }}`).  Multiple arguments can be passed by separating arguments with a comma `,` (e.g. `{{ variable <filter("filterArg", "anotherArg")> }}`).  Arguments can be enclosed in single quotes `'`, double quotes `"`, or no quotes.  All of the following are equivalent: `{{ var <filter("arg")> }}`, `{{ var <filter('arg')> }}`, and `{{ var <filter(arg)> }}`.  There must not be any whitespace between the end of the filter name and the first parenthesis.

Multiple filters can be applied to the same variable by separating the filters inside the angle brackets `< >` with a pipe `|` (e.g. `{{ variable <filter1 | filter2> }}`)  Filters will be applied from left to right.  In the prior example, `filter1` will be applied first, and `filter2` will be applied to the result of `filter1`.

---

### Built-In Filters

**Filter Name:** ***Append***
**Modification:** Adds text to the end of the variable
**Arguments:** text to append
**Use:** `{{ var <append("</p>")> }}`

**Filter Name:** ***Capitalize***
**Modification:** Capitalize the first letter in the variable text.  If the first letter is already capitalized, this filter does nothing.
**Arguments:** *none*
**Use:** `{{ var <capitalize> }}`

**Filter Name:** ***HtmlTextSafe***
**Modification:** Escapes `&` and `<` for safe HTML text (e.g. `<div>This_Text_Is_Safe</div>`)
**Arguments:** *none*
**Use:** `{{ var <htmlTextSafe> }}`

**Filter Name:** ***HtmlAttrSafe***
**Modification:** Escapes `&`, `<`, `>`, `"`, `'`, `/`, and `` ` `` for safety in HTML text and HTML attributes (e.g. `<div>This_Text_Is_Safe</div>` and `<div attr='Safe'>`)
**Arguments:** *none*
**Use:** `{{ var <htmlAttrSafe> }}`

**Filter Name:** ***Lower***
**Modification:** Converts all letters in the text to lower case
**Arguments:** *none*
**Use:** `{{ var <lower> }}`

**Filter Name:** ***Prepend***
**Modification:** Adds text to the beginning of the variable
**Arguments:** text to prepend
**Use:** `{{ var <prepend("<p>")> }}`

**Filter Name:** ***Remove***
**Modification:** Removes all instances of the given text
**Arguments:** The text to find and remove
**Use:** `{{ var <remove("<script")> }}`

**Filter Name:** ***RemoveFirst***
**Modification:** Removes the first instance of the given text
**Arguments:** The text to find and remove
**Use:** `{{ var <removeFirst("https://")> }}`

**Filter Name:** ***RemoveLast***
**Modification:** Removes the last instance of the given text
**Arguments:** The text to find and remove
**Use:** `{{ var <removeLast("https://")> }}`

**Filter Name:** ***Repeat***
**Modification:** Repeats the variable text the given number of times
**Arguments:** The number of times to repeat the text
**Use:** `{{ var <repeat(5)> }}`

**Filter Name:** ***Replace***
**Modification:** Replaces all instances of a given text with other text
**Arguments:** The text to find and the text to replace it with
**Use:** `{{ var <replace("badWord", "-removed-")> }}`

**Filter Name:** ***ReplaceFirst***
**Modification:** Replaces the first instance of a given text with other text
**Arguments:** The text to find and the text to replace it with
**Use:** `{{ var <replaceFirst("Friendly Initiative Regarding Substantial Templates", "Long Acronym Soup Thing (FIRST)")> }}`

**Filter Name:** ***ReplaceLast***
**Modification:** Replaces the last instance of a given text with other text
**Arguments:** The text to find and the text to replace it with
**Use:** `{{ var <replaceLast("Long Acronym Soup Thing", "Long Acronym Soup Thing (LAST)")> }}`

**Filter Name:** ***Slice***
**Modification:** Removes all text before a given starting character index and after a given length of characters
**Arguments:** The starting index and the number of characters to retain, in that order
**Use:** `{{ var <slice(5, 30)> }}`

**Filter Name:** ***Trim***
**Modification:** Removes all whitespace characters from the beginning and end of the variable text
**Arguments:** *none*
**Use:** `{{ var <trim> }}`

**Filter Name:** ***TrimEnd***
**Modification:** Removes all whitespace characters from the end of the variable text
**Arguments:** *none*
**Use:** `{{ var <trimEnd> }}`

**Filter Name:** ***TrimStart***
**Modification:** Removes all whitespace characters from the beginning of the variable text
**Arguments:** *none*
**Use:** `{{ var <trimStart> }}`

**Filter Name:** ***Truncate***
**Modification:** Removes all text after a given number of characters
**Arguments:** maximum number of characters
**Use:** `{{ var <truncate(20)> }}`

**Filter Name:** ***Upper***
**Modification:** Converts all letters in the text to upper case
**Arguments:** *none*
**Use:** `{{ var <upper> }}`

---

### Using Formatters

Formatters are used to modify how `VariableValue`s are converted into text.  Formatters can also be used to provide a constant transformation to any `VariableValue`s that already contain text.  Formatters are applied before any filters, and only one formatter can be applied to any given property.

Formatters are applied with use of the `FormattedVariable<T>` attribute, where `T` is a class that implements `IVariableFormatter`.  `FormattedVariable<T>` must be applied to the template class and takes two arguments: the name of the variable to be modified and optional formatter specific arguments, as `string[]`.

Example:
```csharp
using Lissic;

[FormattedVariable<MyFormatter>("count", ["salesFormat"])]
[TemplateText("{{ product }} sold {{ count }} items this quarter.")]
public partial class SalesLineItem {}

// All instances of the template will automatically show the sales count in "sales format"
```

---

### Built-In Formatters

**Formatter Name:** ***DateFormatter***
**Modification:** Converts a `DateTimeOffset` into formatted text
**Arguments:** The name of the variable to be transformed and [a .NET date and time format string](https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings).
**Example:**
```csharp
using Lissic;

[FormattedVariable<DateFormatter>("date", "yyyy-MM-dd")]
[TemplateText("Today is {{ date }}.")]
public partial class TheDate {}

TheDate today = new() { date = DateTimeOffset.UtcNow };

// Will render similar to "Today is 2013-12-25."
```

---

### Custom Filters

To create a custom filter, create a class that implements `IFilterFactory` and apply the `FilterName` attribute.

`IFilterFactory` requires two methods:
- `int Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output)` - This method receives the current variable text as `input` and writes the transformed text to `output`.  It returns the number of bytes written.  This is the method that is called each time the filter is applied.
- `static abstract ITemplateFilter Create(ReadOnlySpan<string> args)` - This method receives the arguments passed to the filter and returns an initialized instance of the filter class.  This method is run at startup, and the instance is stored for future use.

The `FilterName` attribute must be applied to the class.  This attribute takes a single argument that defines the name of the filter.  For example, a filter class with the attribute `[FilterName("myFilter")]` would be accessed with `{{ variable <myFilter> }}`.  Custom filters can override a built-in filter by declaring the same name, but no two custom filters can have the same name.

The example below is the append filter.  The `Create` method passes the first argument to a new instance of `AppendFilter`, which stores it as UTF-8 text, and then returns that instance to be cached for future use.  The `Apply` method writes the input to the `IBufferWriter<byte>` and then writes the stored text in order to append it to the back of the variable in the rendered template.

```csharp
using Lissic;

[FilterName("append")]
public class AppendFilter(string text) : IFilterFactory
{
    private readonly ReadOnlyMemory<byte> text = Encoding.UTF8.GetBytes(text);

    public static ITemplateFilter Create(ReadOnlySpan<string> args) => new AppendFilter(args[0]);
    public int Apply(ReadOnlySpan<byte> input, IBufferWriter<byte> output)
    {
        output.Write(input);
        output.Write(text.Span);
        return input.Length + text.Length;
    }
}
```

---

### Custom Formatters

To create a custom formatter, create a class that implements `IVariableFormatter`.  This is the only requirement to start using the class as `T` in `FormattedVariable<T>`.

`IVariableFormatter` requires one method:
- `static int Format(in VariableValue value, IBufferWriter<byte> output, ReadOnlySpan<string> args)` - This method receives the value as a `VariableValue` and any formatter specific `args`, and writes the formatted text in UTF-8 text to `output`.  It returns the number of bytes written to `output`.

Formatters should preemptively validate that `VariableValue.Kind` matches the expected `VariableValueType` before processing the value.  Requesting the wrong type from a `VariableValue` will result in a `KindMismatchException`.

The example below is a date and time formatter.  The `Format` method extracts the `DateTimeOffset` from the `VariableValue` using `AsDateTimeOffset()`.  It then uses the first argument string as a formatting string for `DateTimeOffset.TryFormat()` to attempt writing the output to the `IBufferWriter<byte>`.  The argument strings will be the `string[]` passed to the `FormattedVariable<T>` attribute.
```csharp
using Lissic;

public class DateFormatter : IVariableFormatter
{
    const int MaxDateBufferSize = 1024;

    public static int Format(in VariableValue value, IBufferWriter<byte> output, ReadOnlySpan<string> args)
    {
        if (value.Kind != VariableValueType.DateTimeOffset)
            return 0;

        int size = 64;
        while (true)
        {
            Span<byte> span = output.GetSpan(size);
            if (value.AsDateTimeOffset().TryFormat(span, out int bytesWritten, args[0], CultureInfo.InvariantCulture))
            {
                output.Advance(bytesWritten);
                return bytesWritten;
            }

            size += 64;

            if (size >= MaxDateBufferSize)
                throw new InvalidOperationException(
                    $"Buffer size exceeded {MaxDateBufferSize} bytes while formatting DateTimeOffset.");
        }
    }
}
```

---