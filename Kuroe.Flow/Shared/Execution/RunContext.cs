using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Shared.Execution;

/// <summary>一次执行的上下文。启动的执行只用这里声明的内容，不继承任何未声明的历史。</summary>
public sealed record RunContext
{
    /// <summary>所属任务。</summary>
    public required TaskId Task { get; init; }

    /// <summary>所属执行节点在流程图里的序号。</summary>
    public required int NodeIndex { get; init; }

    /// <summary>所属执行节点的名称。</summary>
    public required NodeName NodeName { get; init; }

    /// <summary>本次执行要做的事。</summary>
    public required string Instruction { get; init; }

    /// <summary>宿主对该执行节点的声明，引擎透传，未声明时为空。</summary>
    public INodeDeclaration? Declaration { get; init; }

    /// <summary>所属条目序号，非按条目展开时为空。</summary>
    public int? ItemIndex { get; init; }

    /// <summary>本次执行被叫到的名字：执行节点名，展开条目时带上条目号。</summary>
    public string Label => ItemIndex is { } index ? $"{NodeName}·条目 {index + 1}" : NodeName.ToString();

    /// <summary>当前节点下本实例的执行次数，按节点与条目各自累计，从 1 起。</summary>
    public int ExecutionCount { get; init; } = 1;

    /// <summary>本轮可用的能力路径：节点声明的能力加引擎附上的契约能力。</summary>
    public IReadOnlyList<CapabilityPath> Tools { get; init; } = [];

    /// <summary>上游装配进来的已有内容，按序置于指令之前。</summary>
    public IReadOnlyList<ContextMessage> Seed { get; init; } = [];

    /// <summary>系统指令：节点声明的恒定块在前，引擎生成的本轮接口说明在后。</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>节点声明的 Text 输出端口，未声明时为空。交回契约据此给模型端口名与说明。</summary>
    public IReadOnlyList<Port> OutputPorts { get; init; } = [];
}
