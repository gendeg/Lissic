using System.Buffers;

namespace Lissic;


/// <summary>
/// Specifies a formatter to be applied to a variable used in a template.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="variableName"></param>
/// <param name="args"></param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class FormattedVariableAttribute<T>(string variableName, string[]? args = null) : Attribute where T : IVariableFormatter
{
    public string[]? Args { get; } = args;
    public Type FormatterType { get; } = typeof(T);
    public string VariableName { get; } = variableName;
}


/// <summary>
/// <para>Defines the interface for a formatter that is automatically applied to a variable in a template.</para>
/// <para>Implementers of this interface define a static Format method that takes a VariableValue to format,
/// an output buffer to write to, and optional arguments.  Format returns the number of bytes written.</para>
/// </summary>
public interface IVariableFormatter
{
    static abstract int Format(in VariableValue value, IBufferWriter<byte> output, ReadOnlySpan<string> args);
}


[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class LoopLiteralNameAttribute(int index, string name) : Attribute
{
    public int Index { get; } = index;
    public string Name { get; } = name;
}


/// <summary>
/// Defines a class as a template, using the template in the file at the specified path.
/// </summary>
/// <param name="name"></param>
/// <param name="path"></param>
[AttributeUsage(AttributeTargets.Class)]
public class TemplatePathAttribute(string path) : Attribute
{
    public string Path { get; } = path;
}


/// <summary>
/// Defines a class as a template, using the template text provided directly in the attribute.
/// </summary>
/// <param name="name"></param>
/// <param name="text"></param>
[AttributeUsage(AttributeTargets.Class)]
public class TemplateTextAttribute(string text) : Attribute
{
    public string Text { get; } = text;
}
