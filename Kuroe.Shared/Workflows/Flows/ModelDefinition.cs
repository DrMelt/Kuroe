namespace Kuroe.Shared.Workflows.Flows;

/// <summary>流程里命名的模型选择，执行节点按名引用。</summary>
public sealed record ModelDefinition
{
    /// <summary>配置名，流程内唯一。</summary>
    public required string Name { get; init; }

    /// <summary>使用的模型，未写时用提交任务时选中的模型。</summary>
    public string? Model { get; init; }
}