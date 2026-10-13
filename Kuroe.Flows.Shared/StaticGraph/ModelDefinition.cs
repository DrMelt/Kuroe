namespace Kuroe.Flows.Shared.StaticGraph;

/// <summary>流程里命名的模型选择，执行节点按名引用。模型名与运行时选择的语义由宿主解释。</summary>
public sealed record ModelDefinition
{
    /// <summary>模型选择名，流程内唯一。</summary>
    public required ModelRef Name { get; init; }

    /// <summary>使用的模型名，未写时该模型选择无法用于执行。</summary>
    public string? Model { get; init; }

    /// <summary>运行时模型：执行时取宿主当前选中的模型。与 Model 互斥。</summary>
    public bool Runtime { get; init; }
}
