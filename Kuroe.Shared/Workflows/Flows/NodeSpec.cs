namespace Kuroe.Shared.Workflows.Flows;

/// <summary>统一节点：定义、引用、执行节点与容器都落在同一表示上。
/// 有 <see cref="Use"/> 是引用库定义，否则是节点本身。有 <see cref="Nodes"/> 是容器，有 <see cref="Execution"/> 是执行节点。</summary>
public sealed record NodeSpec
{
    /// <summary>节点名：库定义名或流程/容器内的实例名。</summary>
    public required NodeName Name { get; init; }

    /// <summary>引用的节点库定义名，非空即引用形态。</summary>
    public NodeName? Use { get; init; }

    /// <summary>节点产出后是否停在待批准：执行节点产出后，容器成员产出齐备后。</summary>
    public NodeGate Gate { get; init; } = NodeGate.Auto;

    /// <summary>容器声明传入端口名，成员可用 @端口 引用。</summary>
    public IReadOnlyList<PortName> Inputs { get; init; } = [];

    /// <summary>容器输出端口：端口名到子树内成员引用的映射，值可写 成员名@端口 精确取成员命名段。
    /// 只属于节点库容器定义，引用处不能修改；下游执行节点经 From 按端口消费。
    /// 容器没有整份产出，一切对外内容都经声明的命名端口。</summary>
    public IReadOnlyDictionary<PortName, PortRef>? Out { get; init; }

    /// <summary>输入端口绑定：引用容器时是端口名到当前作用域可达节点引用的映射，值可以是 来源@端口 或 @更外层端口；
    /// 执行节点不写 In，接线一律经 <see cref="From"/>。</summary>
    public IReadOnlyDictionary<PortName, PortRef>? In { get; init; }

    /// <summary>上游接线条目：条目必写 来源@端口 引用来源的输出端口，或 @端口 引用容器传入端口。
    /// <see cref="SourceRef.Or"/> 把条目归入可选启动组，<see cref="SourceRef.Signal"/> 让来源产出不进上下文只作触发信号，
    /// <see cref="SourceRef.Context"/> 把来源产出置于目标上下文最前。
    /// 执行节点定义与节点组成员上禁止声明，引用处注入。</summary>
    public IReadOnlyList<SourceRef> From { get; init; } = [];

    /// <summary>执行节点的命名输出端口表，模型逐 run 交回端口值。端口只随定义，引用处不能修改。</summary>
    public IReadOnlyList<PortName> Outputs { get; init; } = [];

    /// <summary>恒定系统指令块：作为系统指令置于请求最前，内容必须恒定以命中服务商前缀缓存。仅执行节点声明。</summary>
    public IReadOnlyList<string> SystemPrompt { get; init; } = [];

    /// <summary>输出校验的覆盖声明：引用执行节点时可覆盖库定义的校验，展开后并入执行配置。</summary>
    public OutputValidation? Validate { get; init; }

    /// <summary>执行次数上限：执行节点为自身或引用覆盖，容器为组内执行节点的统一默认。未写时由展开解析。</summary>
    public int? MaxRuns { get; init; }

    /// <summary>有序子节点，非空即容器。容器自身不执行，成员产出齐备时作汇合点。</summary>
    public IReadOnlyList<NodeSpec>? Nodes { get; init; }

    /// <summary>模型选择引用：装配层执行节点写本流程的模型选择名，节点组容器成员写模型槽位名，由引用处的模型绑定解析。</summary>
    public ModelRef? Model { get; init; }

    /// <summary>引用节点组时的模型槽位绑定：槽位名到本流程模型选择名的映射，未绑定的槽位在展开时报错。</summary>
    public IReadOnlyDictionary<ModelRef, ModelRef>? Models { get; init; }

    /// <summary>执行配置，非空即执行节点。</summary>
    public ExecutableSpec? Execution { get; init; }
}
