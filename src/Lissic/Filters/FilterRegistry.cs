using Lissic.Parsing;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Lissic;


/// <summary>
/// Maps filter names to factories. The source generator registers every <see cref="FilterNameAttribute"/> type at module load.
/// </summary>
public static class FilterRegistry
{
    private static readonly ConcurrentDictionary<string, Func<string[], ITemplateFilter>> factories = new(StringComparer.OrdinalIgnoreCase);

    public static void Register<T>(string name) where T : IFilterFactory
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));
        else if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Name cannot be empty", nameof(name));
        else if (!LissicParser.IsValidName(name))
            throw new ArgumentException("Invalid name", nameof(name));
        factories[name] = static args => T.Create(args);
    }

    public static bool TryCreate(string name, string[] args, [NotNullWhen(true)] out ITemplateFilter? filter)
    {
        if (factories.TryGetValue(name, out Func<string[], ITemplateFilter>? createFilter))
        {
            filter = createFilter(args);
            return true;
        }

        filter = null;
        return false;
    }
}
