using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Workflows.Graph;

/// <summary>一棵流程树展平后的一个执行节点及其解析结果。执行节点是唯一会派发执行的节点。</summary>
public sealed record ExecutableNode : GraphNode
{
    /// <summary>展开后的执行配置：执行契约与能力声明，编译期只把执行上限落定为生效值。</summary>
    public required ExecutableSpec Execution { get; init; }

    /// <summary>引用的模型选择解析结果：输入节点不启动 run，为空。</summary>
    public required ModelDefinition? Model { get; init; }

    /// <summary>上游依赖：来源执行节点序号与输出端口。端口来自来源节点的输出端口表。
    /// <see cref="Dependency.Or"/> 归入可选启动组，组内成员产出一并取用、任一组全齐备即启动；
    /// <see cref="Dependency.Signal"/> 只作触发信号，来源产出不进目标上下文；
    /// <see cref="Dependency.Context"/> 的来源产出置于目标上下文最前。</summary>
    public required IReadOnlyList<Dependency> From { get; init; }

    /// <summary>命名输出端口表。声明后模型必须经契约工具交回全部端口值，下游由此消费。</summary>
    public required IReadOnlyList<PortName> Outputs { get; init; }

    /// <summary>恒定系统指令块：作为系统指令置于请求最前，内容必须恒定以命中服务商前缀缓存。</summary>
    public required IReadOnlyList<string> SystemPrompt { get; init; }

    /// <summary>执行次数的生效上限，配置链未声明时由编译落定为默认值。</summary>
    public required int MaxRuns { get; init; }

    /// <summary>执行次数上限的默认值，配置链未声明时由编译落定。</summary>
    public const int DefaultMaxRuns = 100;

    /// <summary>拆分只有固定条目，模型不参与补充。</summary>
    public bool IsStaticSplit => Execution.IsStaticSplit;

    /// <summary>是否有命名输出端口，未声明即无模型可交回的命名产出。</summary>
    public bool HasOutputPorts => Outputs.Count > 0;
}
