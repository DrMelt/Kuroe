using Kuroe.Flow.Shared.Model;

namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>一段内容的出处：内容从哪个节点的哪个端口来，详情视图据此回跳上游。</summary>
public abstract record ContextSource
{
    /// <summary>一行可读出处文本。</summary>
    public abstract string Label { get; }

    /// <summary>可跳转到的执行，出处不是执行产出时为空。</summary>
    public virtual ExecutionId? FromExecution => null;
}

/// <summary>某个执行在某执行节点某个输出端口上的产出。</summary>
public sealed record ExecutionSource(ExecutionId Execution, NodePath Node, PortName Port) : ContextSource
{
    /// <inheritdoc/>
    public override string Label => $"{Execution} · {Node} 产出";

    /// <inheritdoc/>
    public override ExecutionId? FromExecution => Execution;
}

/// <summary>输入节点在某个输出端口上收到的用户回答。</summary>
public sealed record InputSource(NodePath Node, PortName Port) : ContextSource
{
    /// <inheritdoc/>
    public override string Label => $"节点「{Node}」的输入";
}

/// <summary>流程起点的初始输入。</summary>
public sealed record StartSource(PortName Port) : ContextSource
{
    /// <inheritdoc/>
    public override string Label => $"流程起点端口「{Port}」";
}

/// <summary>容器命名输出端口的产出，出处指向容器而不落到具体成员执行。</summary>
public sealed record ContainerPortSource(NodePath Container, PortName Port) : ContextSource
{
    /// <inheritdoc/>
    public override string Label => $"容器「{Container}」端口「{Port}」";
}

/// <summary>执行节点输入端口声明的默认值。</summary>
public sealed record DefaultSource(NodePath Node, PortName Port) : ContextSource
{
    /// <inheritdoc/>
    public override string Label => $"节点「{Node}」的端口「{Port}」默认值";
}

/// <summary>不派发执行的节点自身在某个输出端口上的产出，如文件读取节点读到的内容。</summary>
public sealed record NodeSource(NodePath Node, PortName Port) : ContextSource
{
    /// <inheritdoc/>
    public override string Label => $"节点「{Node}」端口「{Port}」";
}


