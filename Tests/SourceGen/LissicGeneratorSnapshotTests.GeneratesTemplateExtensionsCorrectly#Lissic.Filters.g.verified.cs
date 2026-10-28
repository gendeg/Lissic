//HintName: Lissic.Filters.g.cs
#nullable enable
namespace Lissic;

internal static class Lissic_FilterRegistration
{
    internal static void Register()
    {
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.AppendFilter>("append");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.CapitalizeFilter>("capitalize");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.HtmlAttributeSafe>("htmlAttrSafe");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.HtmlTextSafe>("htmlTextSafe");
        global::Lissic.FilterRegistry.Register<global::Tests.Filters.LowerFilter>("lower");
        global::Lissic.FilterRegistry.Register<global::Tests.Filters.PadFilter>("pad");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.PrependFilter>("prepend");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.RemoveFilter>("remove");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.RemoveFirstFilter>("removeFirst");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.RemoveLastFilter>("removeLast");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.RepeatFilter>("repeat");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.ReplaceFilter>("replace");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.ReplaceFirstFilter>("replaceFirst");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.ReplaceLastFilter>("replaceLast");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.SliceFilter>("slice");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.TrimFilter>("trim");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.TrimEndFilter>("trimEnd");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.TrimStartFilter>("trimStart");
        global::Lissic.FilterRegistry.Register<global::Lissic.Filters.BuiltIn.TruncateFilter>("truncate");
        global::Lissic.FilterRegistry.Register<global::Tests.Filters.UpperFilter>("upper");
    }
}