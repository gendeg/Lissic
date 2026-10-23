using System.Buffers;
using System.Diagnostics;
using System.Text;

namespace Lissic;


public static class LissicConfig
{
    public static readonly UnknownBranchKeyDebugMode UnknownBranchKeyDebugMode;
    public static readonly UnknownTagDebugMode UnknownTagDebugMode;
    public static readonly bool EnableCycleDetection;
    public static readonly ulong OutputSizeThreshold;
    public static readonly uint RecursionDepthLimit;

    static LissicConfig()
    {
        LissicOptions options = LissicConfigurator.InitializeAndGetOptions();

        UnknownBranchKeyDebugMode = options.UnknownBranchKeyDebugMode;
        UnknownTagDebugMode = options.UnknownTagDebugMode;
        EnableCycleDetection = options.EnableCycleDetection;
        OutputSizeThreshold = options.OutputSizeThreshold;
        RecursionDepthLimit = options.RecursionDepthLimit;
    }
}


public static class LissicConfigurator
{
    private static bool _initialized;
    private static readonly object _lock = new();
    private static LissicOptions? _options;

    public static bool IsInitialized => _initialized;


    public static void SetOptions(LissicOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        lock (_lock)
        {
            if (_initialized)
                throw new InvalidOperationException("Configuration has already been initialized and can no longer be changed.");

            _options = options;
            _initialized = true;
        }
    }

    internal static LissicOptions InitializeAndGetOptions()
    {
        lock (_lock)
        {
            _initialized = true;
            return _options ??= new LissicOptions();
        }
    }
}


public class LissicOptions
{
    /// <summary>
    /// <para>Enables or disables cycle detection to prevent infinite loops.
    /// When enabled, a TemplateRenderException will be thrown if a cycle is detected during template rendering.
    /// A cycle is defined as a situation where a template indirectly references itself.
    /// References to other instances of the same template will not trigger a cycle.</para>
    /// <para>This option causes a small amount of allocation during template rendering and should be disabled if zero allocation is desired.</para>
    /// Default is true.
    /// </summary>
    public bool EnableCycleDetection { get; set; } = false;
    /// <summary>
    /// <para>Rendering stops when the output size, in bytes, exceeds this threshold.  Set to 0 to disable the limit.</para>
    /// Default is 10 MB.
    /// </summary>
    public ulong OutputSizeThreshold { get; set; } = 10 * 1024 * 1024; // 10 MB
    /// <summary>
    /// <para>Specifies the maximum allowed recursion depth during template rendering.</para>
    /// Default is 64.
    /// </summary>
    public uint RecursionDepthLimit { get; set; } = 64;
    /// <summary>
    /// <para>Determines whether LissicProcessor and ILissicProcessor should be registered as a transient services in the dependency injection container.
    /// When false, they will be registered as scoped services.</para>
    /// Default is false.
    /// </summary>
    public bool RegisterAsTransient { get; set; } = false;
    /// <summary>
    /// <para>Sets the behavior for handling unknown branch keys.
    /// This option will only take effect if built in Debug builds and has no effect in Release builds.
    /// In Release builds, unknown tags will always be skipped silently.</para>
    /// Default is Skip.
    /// </summary>
    public UnknownBranchKeyDebugMode UnknownBranchKeyDebugMode { get; set; } = UnknownBranchKeyDebugMode.Skip;
    /// <summary>
    /// <para>Sets the behavior for handling unknown tags.
    /// This option will only take effect if built in Debug builds and has no effect in Release builds.
    /// In Release builds, unknown tags will always be skipped silently.</para>
    /// Default is Skip.
    /// </summary>
    public UnknownTagDebugMode UnknownTagDebugMode { get; set; } = UnknownTagDebugMode.Skip;
}


public enum UnknownBranchKeyDebugMode : byte
{
    /// <summary>
    /// Skip unknown tags without any additional action.
    /// </summary>
    Skip,
    /// <summary>
    /// <para>Insert the following error message into the output text when an unknown tag is encountered.</para>
    /// "&lt;!-- ERROR: Error Message --&gt;"
    /// </summary>
    InsertErrorText,
    /// <summary>
    /// Throw a TemplateRenderException exception when an unknown tag is encountered.
    /// </summary>
    ThrowException
}


public enum UnknownTagDebugMode : byte
{
    /// <summary>
    /// Skip unknown tags without any additional action.
    /// </summary>
    Skip,
    /// <summary>
    /// <para>Insert the following error message into the output text when an unknown tag is encountered.</para>
    /// "&lt;!-- ERROR: Error Message --&gt;"
    /// </summary>
    InsertErrorText,
    /// <summary>
    /// Throw a TemplateRenderException exception when an unknown tag is encountered.
    /// </summary>
    ThrowException
}


public static class UnknownTagDebug
{
    [Conditional("DEBUG")]
    public static void Handle(IBufferWriter<byte> output, string message)
    {
        switch (LissicConfig.UnknownTagDebugMode)
        {
            case UnknownTagDebugMode.InsertErrorText:
                output.Write(Encoding.UTF8.GetBytes($"<!-- ERROR: {message} -->"));
                return;
            case UnknownTagDebugMode.ThrowException:
                throw new TemplateRenderException(message);
        }
        return;
    }

    [Conditional("DEBUG")]
    public static void HandleKey(IBufferWriter<byte> output, string message)
    {
        switch (LissicConfig.UnknownBranchKeyDebugMode)
        {
            case UnknownBranchKeyDebugMode.InsertErrorText:
                output.Write(Encoding.UTF8.GetBytes($"<!-- ERROR: {message} -->"));
                return;
            case UnknownBranchKeyDebugMode.ThrowException:
                throw new TemplateRenderException(message);
        }
        return;
    }
}
