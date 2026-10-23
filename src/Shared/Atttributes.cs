namespace Lissic;


/// <summary>
/// Defines the name used to apply an <see cref="ITemplateFilter"/> inline in a template.
/// </summary>
/// <param name="name"></param>
[AttributeUsage(AttributeTargets.Class)]
public class FilterNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
