//HintName: Lissic.Filters.g.cs
#nullable enable
namespace Lissic;

internal static class Lissic_FilterRegistration
{
    internal static void Register()
    {
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.AppendFilter>("append");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.CapitalizeFilter>("capitalize");
        global::Lissic.FilterRegistry.Register<global::Tests.Filters.LowerFilter>("lower");
        global::Lissic.FilterRegistry.Register<global::Tests.Filters.PadFilter>("pad");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.PrependFilter>("prepend");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.RemoveFilter>("remove");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.RemoveFirstFilter>("removefirst");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.RemoveLastFilter>("removelast");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.RepeatFilter>("repeat");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.ReplaceFilter>("replace");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.ReplaceFirstFilter>("replacefirst");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.ReplaceLastFilter>("replacelast");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.SliceFilter>("slice");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.TrimFilter>("trim");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.TrimEndFilter>("trimend");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.TrimStartFilter>("trimstart");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.TruncateFilter>("truncate");
        global::Lissic.FilterRegistry.Register<global::Tests.Filters.UpperFilter>("upper");
    }
}