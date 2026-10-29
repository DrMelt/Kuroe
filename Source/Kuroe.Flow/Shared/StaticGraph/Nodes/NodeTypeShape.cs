using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Shared.StaticGraph.Nodes;

/// <summary>节点类型的静态形状：引擎按它解析文件、校验声明与推进执行，不依赖类型自身的行为。
/// 形状之外的差异由类型定义自己在校验与运行期实现里表达。</summary>
public sealed record NodeTypeShape
{
    /// <summary>是否组织子节点，容器专用。组织的类型不派发执行。</summary>
    public bool Organizes { get; init; }

    /// <summary>是否派发执行。不派发的类型由上游版本或外部输入驱动产出。</summary>
    public bool DispatchesExecutions { get; init; }

    /// <summary>等待的输入是否为批准。等待型节点里 Input 为回答，Approval 为批准。</summary>
    public bool AwaitsApproval { get; init; }

    /// <summary>对外端口不在文件里声明，由输入端口原样派生。只有不派发执行的类型成立。</summary>
    public bool DerivesOutputsFromInputs { get; init; }

    /// <summary>不需环外来源即可在环内启动，作周期外源。</summary>
    public bool StartsUnaided { get; init; }

    /// <summary>节点执行契约声明的输入端口。流程未声明同名端口时作为默认声明，声明同名端口时以流程声明覆盖。
    /// 契约端口的角色与常量要求由节点类型规则继续校验，不把执行契约固定成图侧唯一配置。</summary>
    public IReadOnlyList<InputPort> ContractInputs { get; init; } = [];

    /// <summary>是否接受 From 目标引入未声明的 Text 内容输入。只对端口集合开放的节点类型成立。</summary>
    public bool AcceptsInferredInputs { get; init; }

    /// <summary>允许的输入端口类型。</summary>
    public IReadOnlyList<PortType> InputTypes { get; init; } = [];

    /// <summary>允许的输出端口类型。空表示该类型不声明输出端口。</summary>
    public IReadOnlyList<PortType> OutputTypes { get; init; } = [];
}
