using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Workflows.Graph;

/// <summary>一棵流程树展平后的一个执行节点及其解析结果。执行节点是唯一会派发执行的节点。</summary>
public sealed record ExecutableNode : GraphNode
{
    /// <summary>展开后的执行配置：执行契约与能力声明，编译期只把执行上限落定为生效值。</summary>
    public required ExecutableSpec Execution { get; init; }

    /// <summary>引用的模型选择解析结果：输入节点不启动 run，为空。</summary>
    public required ModelDefinition? Model { get; init; }

    /// <summary>上游依赖：来源执行节点序号与命名的输出端口，未带端口取整份产出。</summary>
    public required IReadOnlyList<Dependency> From { get; init; }

    /// <summary>可选启动组：组内按成员顺序的序号，组间顺序保持声明。</summary>
    public required IReadOnlyList<IReadOnlyList<int>> AnyOf { get; init; }

    /// <summary>命名输出端口表，空表即隐式单端口（整份产出）。</summary>
    public required IReadOnlyList<PortName> Outputs { get; init; }

    /// <summary>恒定系统指令块：作为系统指令置于请求最前，内容必须恒定以命中服务商前缀缓存。</summary>
    public required IReadOnlyList<string> SystemPrompt { get; init; }

    /// <summary>执行次数的生效上限，配置链未声明时由编译落定为默认值。</summary>
    public required int MaxRuns { get; init; }

    /// <summary>上下文输入端口的来源序号，未绑定时为空。</summary>
    public required int? ContextInput { get; init; }

    /// <summary>执行次数上限的默认值，配置链未声明时由编译落定。</summary>
    public const int DefaultMaxRuns = 100;

    /// <summary>隐式上下文输出端口的保留名：每个执行节点无需声明即具备，下游用 From 按「来源@ContextOutput」消费。</summary>
    public static PortName ContextOutputPort { get; } = new("ContextOutput");

    /// <summary>隐式上下文输入端口的保留名：每个执行节点具备，装配层用 In 的「ContextInput」键绑定来源，内容置于上下文开头。</summary>
    public static PortName ContextInputPort { get; } = new("ContextInput");

    /// <summary>拆分只有固定条目，模型不参与补充。</summary>
    public bool IsStaticSplit => Execution.IsStaticSplit;

    /// <summary>是否有命名输出端口，空表即隐式单端口（整份产出）。</summary>
    public bool HasOutputPorts => Outputs.Count > 0;
}
