namespace Kuroe.Flow.Presentation;

/// <summary>展示用步骤：路径、标签、种类、状态、输入、输出与执行列表。</summary>
public sealed record FlowStepView(
    string Path,
    string Label,
    string Kind,
    FlowNodeState State,
    IReadOnlyList<FlowPortView> Inputs,
    IReadOnlyList<FlowPortView> Outputs,
    IReadOnlyList<FlowExecutionView> Executions,
    int MaxExecutions)
{
    /// <summary>未配置上限时的默认执行上限。</summary>
    public const int DefaultMaxExecutions = 100;
    /// <summary>所属分组的路径，根级步骤为空。</summary>
    public string? ParentPath { get; init; }

    /// <summary>该步骤已启动的执行数。</summary>
    public int ExecutionCount { get; init; }

    /// <summary>当前阻塞是否可恢复。</summary>
    public bool CanRestore { get; init; }

    /// <summary>当前阻塞的原因，未阻塞时为空。</summary>
    public string? Block { get; init; }

    /// <summary>模板中的节点类型名，任务运行期视图为空。</summary>
    public string? NodeType { get; init; }

    /// <summary>模板中的模型名，未绑定时为空。</summary>
    public string? Model { get; init; }

    /// <summary>模板中的节点类型属性。</summary>
    public IReadOnlyList<FlowAttributeView> Attributes { get; init; } = [];

    /// <summary>模板中的上游绑定，按启动组给出展示文本。</summary>
    public IReadOnlyList<string> Upstream { get; init; } = [];
}