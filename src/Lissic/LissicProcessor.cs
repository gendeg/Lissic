using Lissic.Parsing;
using Lissic.Tools;
using System.Buffers;
using System.Text;

namespace Lissic;


public interface ILissicProcessor
{
    public void RenderToBuffer(LissicTemplate template, IBufferWriter<byte> writer);
    public string RenderToString(LissicTemplate template, int sizeHint = 128);
}

public class LissicProcessor(TemplateCache partCache): ILissicProcessor, IDisposable
{
    private PooledByteWriter? scratchA;
    private PooledByteWriter? scratchB;

    public void RenderToBuffer(LissicTemplate template, IBufferWriter<byte> writer)
    {
        RenderState state = new() { BytesWritten = 0, RecursionDepth = 0 };
        if (LissicConfig.EnableCycleDetection)
            state.ProcessedTemplates = new HashSet<LissicTemplate>(ReferenceEqualityComparer.Instance);

        RenderToBuffer(template, writer, ref state);
    }

    private void RenderToBuffer(LissicTemplate template, IBufferWriter<byte> writer, ref RenderState state)
    {
        if (LissicConfig.EnableCycleDetection && !state.ProcessedTemplates.Add(template))
            throw new TemplateRenderException($"Cycle detected: Template of type {template.GetType().Name} is already being processed.");

        if (state.RecursionDepth++ >= LissicConfig.RecursionDepthLimit)
            throw new TemplateRenderException($"Maximum template recursion depth of {LissicConfig.RecursionDepthLimit} exceeded.");

        try
        {
            ITemplateNode[] nodes = partCache.Get(template.Template__Index);
            foreach (ITemplateNode node in nodes)
            {
                ProcessNode(node, template, writer, ref state);
                if (LissicConfig.OutputSizeThreshold > 0 && state.BytesWritten > LissicConfig.OutputSizeThreshold)
                    throw new TemplateRenderException($"Output size exceeded the threshold of {LissicConfig.OutputSizeThreshold} bytes.");
            }
        }
        finally
        {
            if (LissicConfig.EnableCycleDetection)
                state.ProcessedTemplates.Remove(template);
            state.RecursionDepth--;
        }
    }

    public string RenderToString(LissicTemplate template, int sizeHint = 128)
    {
        using PooledByteWriter buffer = new(sizeHint);
        RenderToBuffer(template, buffer);
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private void ProcessBranch(BranchNode branchNode, LissicTemplate template, IBufferWriter<byte> writer, ref RenderState state)
    {
        string key = template.Get__BranchKey(branchNode.BranchName);

        if (string.IsNullOrEmpty(key))
        {
            UnknownTagDebug.Handle(writer,
                $"Branch key for '{branchNode.BranchName}' is empty.  No branch key was provided or the value is empty.");
        }

        if (string.IsNullOrEmpty(key)) return;

        if (branchNode.BranchEntries.TryGetValue(key, out ITemplateNode[]? nodes))
        {
            foreach (ITemplateNode node in nodes)
            {
                ProcessNode(node, template, writer, ref state);
                if (LissicConfig.OutputSizeThreshold > 0 && state.BytesWritten > LissicConfig.OutputSizeThreshold)
                    throw new TemplateRenderException($"Output size exceeded the threshold of {LissicConfig.OutputSizeThreshold} bytes.");
            }
        }
        else
        {
            UnknownTagDebug.HandleKey(writer, $"No option found for branch key '{key}' in branch '{branchNode.BranchName}'");
        }
    }

    private void ProcessNode<T>(T node, LissicTemplate template, IBufferWriter<byte> writer, ref RenderState state) where T : ITemplateNode
    {
        switch (node)
        {
            case BranchNode branchNode:
                ProcessBranch(branchNode, template, writer, ref state);
                break;
            case HtmlNode htmlNode:
                writer.Write(htmlNode.Html.Span);
                state.BytesWritten += (uint)htmlNode.Html.Length;
                break;
            case PartNode partNode:
                ProcessPart(partNode, template, writer, ref state);
                break;
            case VariableNode variableNode:
                ProcessVariable(variableNode, template, writer, ref state);
                break;
            case FilteredVariableNode filteredNode:
                ProcessFilteredVariable(filteredNode, template, writer, ref state);
                break;
            case LoopNode loopNode:
                ProcessLoop(loopNode, template, writer, ref state);
                break;
        }
    }

    private void ProcessPart(PartNode partNode, LissicTemplate template, IBufferWriter<byte> writer, ref RenderState state)
    {
        LissicTemplate? part = template.Get__Part(partNode.Name);
        if (part != null)
        {
            RenderToBuffer(part, writer, ref state);
        }

        else
        {
            UnknownTagDebug.Handle(writer, $"Part '{partNode.Name}' not found");
        }
    }

    private static void ProcessVariable(VariableNode variableNode, LissicTemplate template, IBufferWriter<byte> writer, ref RenderState state)
    {
        int bytesWritten = template.Stream__Variable(variableNode.Name, writer);
        if (bytesWritten == -1 && variableNode.Required)
            UnknownTagDebug.Handle(writer, $"Missing value for variable '{variableNode.Name}'");
        else if (bytesWritten > 0)
            state.BytesWritten += (uint)bytesWritten;
    }

    private void ProcessFilteredVariable(FilteredVariableNode node, LissicTemplate template, IBufferWriter<byte> writer, ref RenderState state)
    {
        scratchA ??= new PooledByteWriter(256);
        scratchB ??= new PooledByteWriter(256);
        ITemplateFilter[] filters = node.Filters;

        PooledByteWriter input = scratchA;
        input.Reset();
        if (template.Stream__Variable(node.Name, input) == -1)
        {
            if (node.Required)
                UnknownTagDebug.Handle(writer, $"Missing value for variable '{node.Name}'");
            return;
        }

        // Each filter reads the buffer the previous one wrote; the last writes to the caller's writer.
        for (int i = 0; i < filters.Length - 1; i++)
        {
            PooledByteWriter output = ReferenceEquals(input, scratchA) ? scratchB : scratchA;
            output.Reset();
            filters[i].Apply(input.WrittenSpan, output);
            input = output;
        }
        filters[^1].Apply(input.WrittenSpan, writer);
        state.BytesWritten += (uint)input.WrittenSpan.Length;
    }

    private void ProcessLoop(LoopNode loopNode, LissicTemplate template, IBufferWriter<byte> writer, ref RenderState state)
    {
        LoopValue? loop = template.Get__Loop(loopNode.Name);

        if (loop is null)
        {
            UnknownTagDebug.Handle(writer, $"Loop '{loopNode.Name}' not found");
        }

        if (loop is null) return;

        for (int i = 0; i < loop.Value.Count; i++)
        {
            RenderToBuffer(loop.Value.GetChild(i), writer, ref state);
        }
    }


    public void Dispose()
    {
        scratchA?.Dispose();
        scratchB?.Dispose();
    }


    private ref struct RenderState
    {
        public ulong BytesWritten;
        public int RecursionDepth;
        public HashSet<LissicTemplate> ProcessedTemplates;
    }
}


public class TemplateRenderException : Exception
{
    public TemplateRenderException() : base() { }
    public TemplateRenderException(string message) : base(message) { }
    public TemplateRenderException(string message, Exception inner) : base(message, inner) { }
}
