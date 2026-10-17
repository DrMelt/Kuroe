namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>节点类型的静态形状：引擎按它解析文件、校验声明与推进执行，不依赖类型自身的行为。
/// 形状之外的差异由类型定义自己在校验与运行期实现里表达。</summary>
public sealed record NodeTypeShape
{
    /// <summary>是否组织子节点，容器专用。组织的类型不派发 run。</summary>
    public bool Organizes { get; init; }

    /// <summary>是否派发 run。不派发的类型由上游版本或外部输入驱动产出。</summary>
    public bool DispatchesRuns { get; init; }

    /// <summary>对外端口不在文件里声明，由输入端口原样派生。只有不派发 run 的类型成立。</summary>
    public bool DerivesOutputsFromInputs { get; init; }

    /// <summary>不需环外来源即可在环内启动，作周期外源。</summary>
    public bool StartsUnaided { get; init; }

    /// <summary>允许的输入端口类型。</summary>
    public IReadOnlyList<PortType> InputTypes { get; init; } = [];

    /// <summary>允许的输出端口类型。空表示该类型不声明输出端口。</summary>
    public IReadOnlyList<PortType> OutputTypes { get; init; } = [];
}
