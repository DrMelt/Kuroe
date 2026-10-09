using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Executions;

/// <summary>上下文中一条内容的出处，详情视图据此回跳上游。</summary>
public abstract record ContextSource
{
    /// <summary>一行可读出处文本。</summary>
    public abstract string Label { get; }

    /// <summary>可跳转到的 run，出处不是执行产出时为空。</summary>
    public virtual RunId? FromRun => null;
}

/// <summary>某个 run 在某执行节点上的产出。</summary>
public sealed record RunSource(RunId Run, NodeName NodeName) : ContextSource
{
    /// <inheritdoc/>
    public override string Label => $"{Run} · {NodeName} 产出";

    /// <inheritdoc/>
    public override RunId? FromRun => Run;
}

/// <summary>规划执行节点交回的某个条目。</summary>
public sealed record ItemSource(RunId Plan, int Index, string Title) : ContextSource
{
    /// <inheritdoc/>
    public override string Label => $"{Plan} · 规划条目 {Index + 1}：{Title}";

    /// <inheritdoc/>
    public override RunId? FromRun => Plan;
}

/// <summary>输入节点收到的一段用户回答。</summary>
public sealed record InputSource(NodeName NodeName) : ContextSource
{
    /// <inheritdoc/>
    public override string Label => $"节点「{NodeName}」的输入";
}

/// <summary>容器命名输出端口的产出，出处指向容器而不落到具体成员 run。</summary>
public sealed record ContainerPortSource(NodeName Container, PortName Port) : ContextSource
{
    /// <inheritdoc/>
    public override string Label => $"容器「{Container}」端口「{Port}」";
}

/// <summary>某个 run 的上下文帧，供统一结构传输后的出处标记与详情回跳。</summary>
public sealed record ContextFrameSource(RunId Run, NodeName NodeName, int? Item) : ContextSource
{
    /// <inheritdoc/>
    public override string Label => Item is { } index
        ? $"{Run} · {NodeName} 条目 {index + 1} 的上下文"
        : $"{Run} · {NodeName} 的上下文";

    /// <inheritdoc/>
    public override RunId? FromRun => Run;
}
