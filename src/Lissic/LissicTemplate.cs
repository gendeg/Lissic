using System.Buffers;

namespace Lissic;


public abstract partial class LissicTemplate
{
    public abstract int Template_Index { get; }

    /// <summary>Gets the key for a branch, by name.</summary>
    public abstract string Get_BranchKey(ReadOnlySpan<char> keyName);

    /// <summary>Gets an attached part by name.</summary>
    public abstract LissicTemplate? Get_Part(ReadOnlySpan<char> name);

    /// <summary>Gets the variable's value as a string, by name.</summary>
    public abstract string? Get_VariableString(ReadOnlySpan<char> name);

    /// <summary>Gets the loop value with attached children, by name.</summary>
    public abstract LoopValue? Get_Loop(ReadOnlySpan<char> name);

    /// <summary>Writes the variable's value; returns false when the variable is unset or unknown.</summary>
    public abstract int Stream_Variable(ReadOnlySpan<char> name, IBufferWriter<byte> writer);
}
