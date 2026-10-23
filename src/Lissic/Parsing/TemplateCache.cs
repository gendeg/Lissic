using System.Diagnostics;

namespace Lissic.Parsing;


public class TemplateCache
{
    private ITemplateNode[][] cache = [];
    private bool initialized = false;

    internal ITemplateNode[] Get(int index) => cache[index];

    public async Task Initialize(ITemplateManifest manifest)
    {
        string templateDirectory = Path.Combine(AppContext.BaseDirectory, manifest.TemplateRoot);
        cache = new ITemplateNode[manifest.Count][];

        List<(int index, string Name, string Path, Task<string>)> readTasks = [];

        for (int i = 0; i < manifest.Count; i++)
        {
            TemplateEntry entry = manifest.Get(i);
            switch (entry.Kind)
            {
                case TemplateKind.File:
                    readTasks.Add((i, entry.Name, entry.Value, File.ReadAllTextAsync(Path.Combine(templateDirectory, entry.Value))));
                    break;
                case TemplateKind.Literal:
                    cache[i] = ParseTemplate(entry.Value, entry.Name);
                    break;
                default:
                    throw new InvalidOperationException($"Template kind {entry.Kind} is unknown.  This should not happen.  Unless you have modified the template system, please report this issue to the Lissic maintainers.");
            }
        }

        // Wait for path template read tasks to complete and parse them
        foreach (var (index, name, path, readTask) in readTasks)
        {
            string text = await readTask;
            cache[index] = ParseTemplate(text, $"{name} ({path})");
        }
        
        initialized = true;
    }

    // Not async because [Conditional("DEBUG")] requires returning void, acceptable in a debug-only context
    [Conditional("DEBUG")]
    public void ReloadPathTemplates(ITemplateManifest manifest, CancellationToken cancellationToken = default)
    {
        if (!initialized)
            throw new InvalidOperationException("Template cache must be initialized before templates can be reloaded.");

        List<(int index, string Name, string Path, Task<string>)> readTasks = [];

        for (int i = 0; i < manifest.Count; i++)
        {
            TemplateEntry entry = manifest.Get(i);
            if (entry.Kind == TemplateKind.File)
            {
                string path = Path.Combine(AppContext.BaseDirectory, manifest.TemplateSourceRoot, entry.Value);
                readTasks.Add((i, entry.Name, entry.Value, File.ReadAllTextAsync(path, cancellationToken)));
            }
        }
        
        // Wait for path template read tasks to complete and parse them
        foreach (var (index, name, path, readTask) in readTasks)
        {
            string text = readTask.GetAwaiter().GetResult();
            cache[index] = ParseTemplate(text, $"{name} ({path})");
        }
    }

    private static ITemplateNode[] ParseTemplate(string text, string templateName)
    {
        ITemplateNode[] nodes = LissicParser.Parse(text, templateName);
        ResolveFilters(nodes, templateName);
        return nodes;
    }

    // Replaces filtered VariableNode instances with FilteredVariableNode instances
    // Handled at runtime because the filter registry data isn't readily available to the parser at compile time
    private static void ResolveFilters(ITemplateNode[] nodes, string templateName)
    {
        for (int i = 0; i < nodes.Length; i++)
        {
            switch (nodes[i])
            {
                case VariableNode { Filters.Length: > 0 } variable:
                    nodes[i] = new FilteredVariableNode(variable, CreateFilters(variable, templateName));
                    break;
                case BranchNode branch:
                    foreach (ITemplateNode[] entry in branch.BranchEntries.Values)
                        ResolveFilters(entry, templateName);
                    break;
            }
        }
    }

    private static ITemplateFilter[] CreateFilters(VariableNode variable, string templateName)
    {
        FilterCall[] calls = variable.Filters;
        ITemplateFilter[] filters = new ITemplateFilter[calls.Length];

        for (int i = 0; i < calls.Length; i++)
        {
            try
            {
                filters[i] = FilterRegistry.TryCreate(calls[i].Name, calls[i].Args, out ITemplateFilter? filter)
                    ? filter
                    : new UnknownFilter(calls[i].Name);
            }
            catch (Exception ex)
            {
                throw new TemplateParsingException($"Filter '{calls[i].Name}' for variable '{variable.Name}', in template: {templateName}, could not be created: {ex.Message}");
            }
        }

        return filters;
    }
}


public interface ITemplateManifest
{
    public int Count { get; }
    public TemplateEntry Get(int index);
    public string TemplateRoot { get; }
    public string TemplateSourceRoot { get; }
}


public readonly struct TemplateEntry(TemplateKind kind, string value, string name)
{
    public TemplateKind Kind { get; } = kind;
    public string Value { get; } = value;
    public string Name { get; } = name;
}


public enum TemplateKind
{
    File,
    Literal
}
