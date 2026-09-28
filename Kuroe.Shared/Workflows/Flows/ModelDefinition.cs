namespace Kuroe.Shared.Workflows.Flows;

/// <summary>流程里命名的模型选择，执行节点按名引用。</summary>
public sealed record ModelDefinition
{
    /// <summary>配置名，流程内唯一。</summary>
    public required ModelRef Name { get; init; }

    /// <summary>使用的模型名，未写时该模型选择无法用于执行。</summary>
    public string? Model { get; init; }
}