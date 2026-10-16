namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>统一节点：定义、引用、执行节点、输入节点与容器都落在同一表示上。
/// 有 <see cref="Use"/> 是引用库定义，否则由 <see cref="Type"/> 决定类型；有 <see cref="Nodes"/> 是容器，有 <see cref="Execution"/> 是执行节点或输入节点。</summary>
public sealed record NodeSpec
{
    /// <summary>节点名：库定义名或流程/容器内的实例名。</summary>
    public required NodeName Name { get; init; }

    /// <summary>节点类型的描述实例，类型专属声明封在它里面。引用节点自身不写类型，由被引用的库定义决定，此处为空。</summary>
    public INodeType? Type { get; init; }

    /// <summary>节点声明的类型名，引用节点为空。</summary>
    public NodeKind? Kind => Type?.Kind;

    /// <summary>生效的节点类型名：展开后由定义的类型填入，只有展开前的引用节点为空。</summary>
    public NodeKind EffectiveKind => Type?.Kind
        ?? throw new InvalidOperationException($"节点 {Name.Value} 没有类型名，只有展开后的节点树可取生效类型。");

    /// <summary>引用的节点库定义名，非空即引用形态。</summary>
    public NodeName? Use { get; init; }

    /// <summary>输入端口声明：执行节点与容器共用，容器由引用处绑定，执行节点由自身 From 绑定；
    /// 成员用 @端口 引用容器传入端口。</summary>
    public IReadOnlyList<InputPort> Inputs { get; init; } = [];

    /// <summary>容器输出端口：端口名到子树内成员引用的映射，值可写 成员名@端口 精确取成员命名段。
    /// 属于容器定义与装配层内联容器，引用处不能修改；下游执行节点经 From 按端口消费。
    /// 文件里与 <see cref="Outputs"/> 同写在一个 Outputs 数组，条目形状由节点类型决定。容器没有整份产出，一切对外内容都经声明的命名端口。</summary>
    public IReadOnlyDictionary<PortName, ContainerOutput>? ContainerOutputs { get; init; }

    /// <summary>输入端口绑定：引用容器时是端口名到当前作用域可达节点引用的映射，值可以是 来源@端口 或 @更外层端口；
    /// 执行节点不写 In，接线一律经 <see cref="From"/>。</summary>
    public IReadOnlyDictionary<PortName, PortRef>? In { get; init; }

    /// <summary>上游启动组：组是一套完整的输入绑定方案，组内是输入端口到来源端口的连接，组间任一齐备即启动。
    /// 取用方式由输入端口的声明决定。库执行定义不写接线，由引用处注入；库容器成员与装配层节点自身声明。</summary>
    public IReadOnlyList<FromGroup> From { get; init; } = [];

    /// <summary>执行节点的输出端口表：名字与类型。类型在编译期落定产出形态与消费方式，引用处按名字消费。
    /// 模型逐 run 交回 Text 端口值，引用处不能修改声明。</summary>
    public IReadOnlyList<Port> Outputs { get; init; } = [];

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

    /// <summary>容器定义声明的模型槽位：成员用 <see cref="Model"/> 写槽位名，引用处用 <see cref="Models"/> 绑定到流程模型选择名，按声明序。只有节点库容器定义声明，装配层内联容器与引用节点为空。</summary>
    public IReadOnlyList<ModelRef>? ModelSlots { get; init; }

    /// <summary>执行配置，非空即执行节点或输入节点，只有派发 run 的类型会用到其中的执行方式。</summary>
    public ExecutableSpec? Execution { get; init; }
}
