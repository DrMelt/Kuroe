using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Workflows.Graph;

/// <summary>一棵流程树展平后的一个执行节点及其解析结果。Kind 区分执行节点与输入节点，只有执行节点会派发执行。</summary>
public sealed record ExecutableNode : GraphNode
{
    /// <summary>节点类型：执行节点或输入节点。输入节点不启动 run。</summary>
    public required NodeKind Kind { get; init; }

    /// <summary>展开后的执行配置：能力声明与执行方式，编译期只把执行上限落定为生效值。</summary>
    public required ExecutableSpec Execution { get; init; }

    /// <summary>引用的模型选择解析结果：输入节点不启动 run，为空。</summary>
    public required ModelDefinition? Model { get; init; }

    /// <summary>上游依赖：来源执行节点序号、来源输出端口与目标输入端口，类型取自目标输入端口的声明。
    /// 依赖按启动组聚合，组是一套完整的输入绑定方案，组间任一齐备即启动，组内全部齐备。</summary>
    public required IReadOnlyList<Dependency> From { get; init; }

    /// <summary>执行节点的输出端口表：名字与类型，类型决定产出形态与消费方式。</summary>
    public required IReadOnlyList<Port> Outputs { get; init; }

    /// <summary>恒定系统指令块：作为系统指令置于请求最前，内容必须恒定以命中服务商前缀缓存。</summary>
    public required IReadOnlyList<string> SystemPrompt { get; init; }

    /// <summary>执行次数的生效上限，配置链未声明时由编译落定为默认值。</summary>
    public required int MaxRuns { get; init; }

    /// <summary>执行次数上限的默认值，配置链未声明时由编译落定。</summary>
    public const int DefaultMaxRuns = 100;

    /// <summary>是否有 Data 端口，未声明即无模型可交回的命名产出。</summary>
    public bool HasDataPorts => Outputs.Any(port => port.Type == PortType.Data);

    /// <summary>是否声明 Items 端口，声明即由模型交回条目拆分。</summary>
    public bool HasItemsPort => Outputs.Any(port => port.Type == PortType.Items);

    /// <summary>按名字取端口声明，未声明时为空。</summary>
    public Port? FindPort(PortName name) => Outputs.Find(name);
}
