# Template Tags

##### Table of Contents
- Tag Overview
  - Type and Name
  - Name Restrictions
- Variables
  - Variable Filters
- Parts (SubTemplate)
- Loops (ForEach)
- Branches (Switch)
  - Branch Options
  - Literal Text
- Optional Tags
- Whitespace and Case Sensitivity
  - Whitespace
  - Case Sensitivity
- Reserved Tag Names

---

### Tag Overview

Tags are used to insert text into templates.  The four types of tags are variable, part, loop, and branch.  All four tag types have their values assigned in the template's data transfer object (see DefiningTemplatesAndDTOs.md for additional information).

NOTE:  All template tags will be automatically added as properties to the template DTO by a source generator.  See DefiningTemplatesAndDTOs.md for additional information.

##### Type and Name

All tags open with "`{{`" and close with "`}}`".  Variable tags only need to contain the name of the tag.  All other tags start with the tag type, followed by a `.`, and then followed by the tag name (e.g. `{{ name }}` or `{{ type.name }}`).

##### Name Restrictions

Tag names must be a legal C# property name.  Tag names must be unique, including across different tag types, and must not use a reserved name (see below).  However, the same tag can be used multiple times.  For example, you cannot have `{{ foo }}` and `{{ part.foo }}` in the same template, but `{{ foo }}` can be used as many times as is necessary.

---

### Variables

Variables are used to directly insert text into a template.  Variable tags do not require a tag type prefix.  They only require the tag name (e.g. `{{ tagName }}`).

##### Variable Filters

Variables can also have filters to modify the text.  A filter must be listed after the tag name, inside a "`<`" and "`>`" (e.g. `{{ tagName <filterName> }}`).

Multiple filters can be applied by separating them with a "`|`" (e.g. `{{ tagName <filter1 | filter2> }}`).  Filters will be applied from left to right (i.e. `filter1` first then `filter2`, in this example)

Filters can also have arguments passed to them, inside parentheses (e.g. `{{ tagName < filterName("arg1", "arg2") > }}`).

See FiltersAndFormatters.md for a list of available built-in filters and instructions on how to create custom filters.

---

### Parts

Parts, also known as subTemplates, are used to insert another template into the current template.  Parts can be identified by either `part` or `subTemplate` type, followed by a `.` and the tag name (e.g. `{{ part.partName }}` or `{{ subTemplate.otherTemplate }}`).

The tag name does not have any restrictions other than the general tag name restrictions noted above.

---

### Loops

Loops, also known as forEach, are used to insert a `List` of rendered templates into the current template.  Loops can be identified by either `loop` or `forEach` type, followed by a `.` and the tag name (e.g. `{{ loop.loopName }}` or `{{ forEach.otherList }}`).

The loop's list of templates is assigned inside the parent template's data transfer object (see DefiningTemplatesAndDTOs.md).  Assign a `List<LissicTemplate>` to the parent object, and each template in the list will be rendered, in order.

Loops can either be all the same template type or a mix of different template types.  For example, a loop could be used strictly to insert rows into a table, but it could also contain a header row template every 10 rows.

---

### Branches

Branches, also known as switches, are used to insert different text based on external input.  Branches can be identified by either `branch` or `switch` type, followed by a `.` and the tag name.  The branch options then follow the branch name inside brackets (e.g. `{{ branch.branchName [...options go here...] }}` or `{{ switch.stateValue [...options go here...] }}`).

In the parent template, the branch is assigned a text key value.  The key value is then used to determine which branch should be taken.

```
   "branchName" is assigned either "key1" or "key2" in the parent template
             ↓
{{ branch.branchName [key1=>action1; key2=>action2] }}
                       ↑              ↑
    the action is selected based on the key passed to "branchName"
```

##### Branch Options

Branch options are a list of mappings between a key value and an action.  Actions can be any of the tag types, including other branches, or a literal text string.

Options are formatted as `key=>action;` (e.g. `{{ branch.isLoggedIn [True=>name; False=>part.logInLink;] }}`).   Any number of key/action pairs can be included in a branch (e.g. `{{ switch.fruitType [apple=>part.apple; banana=>part.banana; cantaloupe=>part.cantaloupe;...] }}`).

##### Literal Text

Branch actions can also insert literal text.  To insert literal text, the action should start with `@`.  All text following the `@` will be included in the text literal until the closing `;` is reached (e.g. `{{ branch.literalExample [taken=>@This branch is taken; notTaken=>@other text;] }}` will insert "This branch is taken" into the text).

A semicolon can be inserted into the text with a double semicolon escape sequence.  "`;;`" will be inserted as "`;`", and the text literal will continue afterward (e.g. `{{ branch.literalExample [taken=>@This branch;; is taken; notTaken=>@other text;] }}` will insert "This branch; is taken" into the text).

Text literals will also be parsed for other tags, including other branches.  The following branches are valid and all nested tags will be parsed:
```
{{ branch.nestedTags [
    nestedVariable=>@This will retrieve the {{ name }} value from the template;
    nestedPart => @This will retrieve the {{ part.insertMe }} template from the current template;
    nestedBranch => @Lets do this again {{ branch.subBranch [
        doNothing => @; 
        doSomething => @actually, let's put {{ name }} here;
    ]}}
]}}
```
NOTE: all tag names declared inside branches will be added to the list of properties in the template's data transfer object.

---

### Optional Tags

All tags are required by default, enforced by use of the `required` keyword in the data transfer object.  This means that a value ***must*** be assigned when the data transfer object is declared.  This behavior can be disabled individually on each tag or globally with the LissicIgnoreRequiredProperties option (see ConfigurationAndOptions.md for information on setting `LissicIgnoreRequiredProperties`).

To mark an individual tag as optional, add a `?` after the tag name (e.g. `{{ phoneNumber? }}`, `{{ loop.optionalTable? }}`, or `{{ branch.ausDetails? [inPerson=>address; online=>vtcLink;] }}`).  This will remove the `required` keyword in the data transfer object.

---

### Whitespace and Case Sensitivity

##### Whitespace

Lissic allows whitespace in most places inside tags, with two exceptions:
- There cannot be any space before or after the `.` that connects a tag type to a tag name
- There cannot be any space between a filter name and the parentheses that contain arguments

All of the following are valid tags:
```
{{ myVar }}
{{ myVar < filter > }}
{{ myVar < filter( "arg1" , "arg2" ) | filter2 > }}
{{myVar<filter(
    "arg1", 
    "arg2"
)>}}
{{ part.myPart }}
{{ loop.myLoop }}
{{ branch.myBranch [ key1 => action ; key2 => action2 ; key3 => @literal text; ] }}
{{branch.myBranch[
    key1 => action;
    key2 => action2;
    key3 => @literal text;
]}}
```
All of the following are ***not*** valid:
```
{{variable<filter ("arg")>}}
{{part .myPart}}
{{part. myPart}}
```

##### Case Sensitivity

The following are not case sensitive:
- Tag types, so `{{ type.name }}` is equivalent to `{{ Type.name }}` or `{{ TYPE.name }}`
- Filter names, so `{{ name <capitalize> }}` is equivalent to `{{ name <Capitalize> }}` or `{{ name <CapITaliZe> }}`.

The following are case sensitive:
- Tag names, so `{{ type.name }}` is ***not*** equivalent to `{{ type.Name }}`
- Branch keys, so `{{ branch.myBranch [key1=>data1;]}}` is ***not*** equivalent to `{{ branch.myBranch [Key1=>data1;]}}`

---

### Reserved Tag Names

The following names are used internally to Lissic.  Attempting to create a tag with one of these names will result in undefined behavior.

- Get_BranchKey
- Get_Part
- Get_VariableString
- Get_Loop
- Has_Value
- Inherited_Values
- Stream_Variable
- Template_Index
- Any name that starts with "\_args\_"

---
