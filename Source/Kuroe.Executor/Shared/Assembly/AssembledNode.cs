using Kuroe.Executor.Shared.Nodes;
using Kuroe.Tasks.Shared;

namespace Kuroe.Executor.Shared.Assembly;

/// <summary>装配后的一个节点：已解析的路径、执行器条目、端口与接线。容器条目组织子节点，其余条目派发执行。
/// 接线全部是已解析路径，标签引用在装配期解析完毕。</summary>
public sealed record AssembledNode
{
    /// <summary>节点路径。</summary>
    public required AssembledPath Path { get; init; }

    /// <summary>节点标签，同一容器内唯一。</summary>
    public required string Label { get; init; }

    /// <summary>执行器条目实例，容器条目对外只组织子节点。</summary>
    public required IExecutor Executor { get; init; }

    /// <summary>种类名。</summary>
    public ExecutorKind Kind => Executor.Kind;

    /// <summary>生效输入端口表：声明输入加 From 目标引入的内容端口。</summary>
    public IReadOnlyList<ExecutorInputPort> Inputs { get; init; } = [];

    /// <summary>输出端口表。</summary>
    public IReadOnlyList<ExecutorOutputPort> Outputs { get; init; } = [];

    /// <summary>汇入本节点的绑定，按装配顺序。</summary>
    public IReadOnlyList<AssembledInputBinding> InputBindings { get; init; } = [];

    /// <summary>输出端口到内部来源端口的映射，未声明来源时为空。</summary>
    public IReadOnlyDictionary<PortName, AssembledOutputBinding>? OutputBindings { get; init; }

    /// <summary>产出模式的生效声明，执行条目使用。</summary>
    public ExecutorEmission? Emission { get; init; }

    /// <summary>执行或发布次数的生效上限。</summary>
    public int? MaxExecutions { get; init; }

    /// <summary>有序子节点，非空即容器。</summary>
    public IReadOnlyList<AssembledNode> Children { get; init; } = [];

    /// <summary>节点是否组织子节点。</summary>
    public bool IsContainer => Children.Count > 0;
}