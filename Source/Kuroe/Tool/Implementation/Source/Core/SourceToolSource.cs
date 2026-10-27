namespace Kuroe.Tool.Source;

/// <summary>导出资料工具源，导出目录在装配期给出。载体的任一函数放行时创建。</summary>
/// <param name="sourceDirectory">随程序导出的源码与文档目录。</param>
public sealed class SourceToolSource(string sourceDirectory) : IToolSource
{
    private readonly IReadOnlyList<ToolDefinition> _definitions =
        [.. new SourceTool(sourceDirectory).Functions.Select(function => function.Definition)];

    /// <inheritdoc/>
    public IReadOnlyList<ToolDefinition> Definitions => _definitions;

    /// <inheritdoc/>
    public IReadOnlyList<ITool> Create(ToolContext context) =>
        context.AllowsAny(_definitions) ? [new SourceTool(sourceDirectory)] : [];
}