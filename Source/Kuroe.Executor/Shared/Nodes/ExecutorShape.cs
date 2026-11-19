using Kuroe.Executor.Shared.Ports;

namespace Kuroe.Executor.Shared.Nodes;

/// <summary>执行器条目的静态形状：装配按它解析声明与校验端口，不依赖条目自身的行为。
/// 形状之外的差异由条目在校验与运行期实现里表达。</summary>
public sealed record ExecutorShape
{
    /// <summary>是否组织子节点，容器专用。组织的条目不派发执行。</summary>
    public bool Organizes { get; init; }

    /// <summary>是否派发执行。不派发的条目由上游版本或外部输入驱动产出。</summary>
    public bool DispatchesExecutions { get; init; }

    /// <summary>执行启动后需要等待外部输入。</summary>
    public bool AwaitsExternalInput { get; init; }

    /// <summary>等待的输入是否为批准。</summary>
    public bool AwaitsApproval { get; init; }

    /// <summary>输出端口声明方式。未写时由流程或执行器库声明。</summary>
    public ExecutorOutputDeclaration Outputs { get; init; } = ExecutorOutputDeclaration.Declared.Instance;

    /// <summary>不需环外来源即可在环内启动，作周期外源。</summary>
    public bool StartsUnaided { get; init; }

    /// <summary>条目执行契约声明的输入端口。流程未声明同名端口时作为默认声明。</summary>
    public IReadOnlyList<ExecutorInputPort> ContractInputs { get; init; } = [];

    /// <summary>是否接受 From 目标引入未声明的 Text 内容输入。</summary>
    public bool AcceptsInferredInputs { get; init; }

    /// <summary>允许的输入端口类型。</summary>
    public IReadOnlyList<PortType> InputTypes { get; init; } = [];

    /// <summary>声明的输出端口类型值域。空表示该条目不声明输出端口。</summary>
    public IReadOnlyList<PortType> OutputTypes { get; init; } = [];

    /// <summary>允许的输出端口类型：条目给出端口时取端口类型，其余取 OutputTypes。</summary>
    public IReadOnlyList<PortType> AllowedOutputTypes =>
        Outputs is ExecutorOutputDeclaration.Given given
            ? [.. given.Ports.Select(port => port.Type).Distinct()]
            : OutputTypes;
}