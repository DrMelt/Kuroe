namespace Kuroe.Tool;

/// <summary>按成对条目提供函数定义与载体的工具源。只有落在本轮可用工具面内的条目才创建载体。</summary>
/// <param name="entries">本源的条目，按声明序。</param>
public abstract class ToolSource(IReadOnlyList<ToolEntry> entries) : IToolSource
{
    private readonly IReadOnlyList<ToolEntry> _entries = entries;

    /// <inheritdoc/>
    public IReadOnlyList<ToolDefinition> Definitions { get; } =
        [.. entries.Select(entry => entry.Definition)];

    /// <inheritdoc/>
    public IReadOnlyList<ITool> Create(ToolContext context) =>
        [.. _entries
            .Where(entry => context.Allows(entry.Definition.Path))
            .Select(entry => entry.Create(context.For(entry.Definition.Path.Value)))];
}