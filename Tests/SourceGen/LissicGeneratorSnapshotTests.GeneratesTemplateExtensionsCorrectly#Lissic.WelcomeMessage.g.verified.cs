//HintName: Lissic.WelcomeMessage.g.cs
#nullable enable
using Lissic;
using Lissic.Tools;
using System;
using System.Buffers;

namespace Tests.FakeSpace;

public partial class Templates { 
public partial class WelcomeMessage : LissicTemplate
{
    public required VariableValue Name { get; set; }
    public VariableValue City { get; set; }
    public required PartValue thingPart { get; set; }
    public required LoopValue itemsForLoop { get; set; }
    public required VariableValue testVal { get; set; }
    public required VariableValue testVal2 { get; set; }
    public required BranchValue farewellType { get; set; }
    private static readonly string[] __args_Name = ["10", "left"];
    private static readonly string[] __args_City = ["trim"];

    public override string Get__BranchKey(ReadOnlySpan<char> keyName) => keyName switch
    {
        "farewellType" => farewellType.Key,
        _ => ""
    };

    public override LoopValue? Get__Loop(ReadOnlySpan<char> name)
    {
        switch (name)
        {
            case "itemsForLoop": return itemsForLoop;
            default: return null;
        }
    }

    public override LissicTemplate? Get__Part(ReadOnlySpan<char> name) => name switch
    {
        "thingPart" => thingPart.Template,
        _ => null
    };

    public override string? Get__VariableString(ReadOnlySpan<char> name)
    {
        switch (name)
        {
            case "Name":
            {
                if (Name.IsEmpty) return string.Empty;
                using PooledByteWriter buffer = new();
                global::Tests.Formatters.PadFormatter.Format(Name, buffer, __args_Name);
                return buffer.GetString();
            }
            case "City":
            {
                if (City.IsEmpty) return string.Empty;
                using PooledByteWriter buffer = new();
                global::Tests.Formatters.LowerFormatter.Format(City, buffer, __args_City);
                return buffer.GetString();
            }
            case "testVal": return testVal.ToString();
            case "testVal2": return testVal2.ToString();
            default: return null;
        }
    }

    public override int Stream__Variable(ReadOnlySpan<char> variableName, IBufferWriter<byte> writer)
    {
        switch (variableName)
        {
            case "Name": if (Name.IsEmpty) return -1; return global::Tests.Formatters.PadFormatter.Format(Name, writer, __args_Name);
            case "City": if (City.IsEmpty) return -1; return global::Tests.Formatters.LowerFormatter.Format(City, writer, __args_City);
            case "testVal": if (testVal.IsEmpty) return -1; return testVal.WriteTo(writer);
            case "testVal2": if (testVal2.IsEmpty) return -1; return testVal2.WriteTo(writer);
            default: return -1;
        }
    }
}
}
