using Kuroe.Executor.Shared.Nodes;
using Kuroe.Tasks.Shared;

namespace Kuroe.Executor.Shared.Assembly;

/// <summary>执行器库条目：文档节点与代码内置定义的统一形状。有引用名即引用条目，否则由执行器条目决定种类；
/// 有子节点即容器，可执行条目派发执行。定义写名字，引用写标签与引用名。</summary>
public sealed record NodeDefinition
{
    /// <summary>定义条目的名字，引用条目的引用名指向它。</summary>
    public string? Name { get; init; }

    /// <summary>节点标签：同一容器内唯一的标识段，恒非空。</summary>
    public required string Label { get; init; }

    /// <summary>执行器条目实例，引用条目为空，由被引用的定义决定。</summary>
    public IExecutor? Executor { get; init; }

    /// <summary>引用的定义名，非空即引用形态。</summary>
    public string? Use { get; init; }

    /// <summary>输入端口声明。</summary>
    public IReadOnlyList<ExecutorInputPort> Inputs { get; init; } = [];

    /// <summary>静态输入绑定：输入端口到来源端口引用。</summary>
    public IReadOnlyDictionary<PortName, DefinitionReference>? Bindings { get; init; }

    /// <summary>上游 From 组。</summary>
    public IReadOnlyList<DefinitionFromGroup> From { get; init; } = [];

    /// <summary>输出端口表。</summary>
    public IReadOnlyList<ExecutorOutputPort> Outputs { get; init; } = [];

    /// <summary>产出模式的覆盖声明。</summary>
    public ExecutorEmission? Emission { get; init; }

    /// <summary>执行或发布次数上限。</summary>
    public int? MaxExecutions { get; init; }

    /// <summary>有序子节点，非空即容器。</summary>
    public IReadOnlyList<NodeDefinition>? Nodes { get; init; }

    /// <summary>是否组织子节点。</summary>
    public bool IsContainer => Nodes is { Count: > 0 };
}