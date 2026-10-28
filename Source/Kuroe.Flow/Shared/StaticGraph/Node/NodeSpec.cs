using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Shared.StaticGraph.Node;

/// <summary>统一节点：定义、引用、执行节点、输入节点与容器都落在同一表示上。
/// 有 <see cref="Use"/> 是引用库定义，否则由 <see cref="Type"/> 决定类型；有 <see cref="Nodes"/> 是容器，有 <see cref="Spec"/> 是执行节点或输入节点。
/// 定义条目写节点名，标签取节点名文本；引用条目写标签，节点名留空。</summary>
public sealed partial record NodeSpec
{
    /// <summary>定义条目的构造：标签取节点名的文本，名字是定义在库里的名字，标签是它在图里的标识段。</summary>
    public static NodeSpec Define(NodeName name) => new()
    {
        Name = name,
        Label = new NodeLabel(name.Value),
    };

    /// <summary>引用条目的构造：标识是使用处写下的标签，定义身份由被引用的定义给出。</summary>
    public static NodeSpec Reference(NodeLabel label, NodeName use) => new()
    {
        Label = label,
        Use = use,
    };

    /// <summary>节点名：定义条目的名字，节点库的注册名，也是 Use 引用的名字。引用条目为空。</summary>
    public NodeName? Name { get; init; }

    /// <summary>节点标签：节点在图里的标识段，同一容器内唯一且恒非空。定义条目取节点名的文本，引用条目取使用处写下的标签。</summary>
    public required NodeLabel Label { get; init; }

    /// <summary>节点类型的描述实例，类型专属声明封在它里面。引用节点自身不写类型，由被引用的库定义决定，此处为空。</summary>
    public INodeType? Type { get; init; }

    /// <summary>节点声明的类型名，引用节点为空。</summary>
    public NodeKind? Kind => Type?.Kind;

    /// <summary>生效的节点类型名：展开后由定义的类型填入，只有展开前的引用节点为空。</summary>
    public NodeKind EffectiveKind => Type?.Kind
        ?? throw new InvalidOperationException($"节点 {Label.Value} 没有类型名，只有展开后的节点树可取生效类型。");

    /// <summary>引用的节点库定义名，非空即引用形态。</summary>
    public NodeName? Use { get; init; }

    /// <summary>输入端口声明：执行节点与容器共用，容器的传入端口由自身 In 绑定，执行节点由自身 From 绑定；
    /// 成员用 @端口 引用容器传入端口。派发执行节点的 Text 输入端口可声明默认值，
    /// 默认值在 From 组没有连接该端口时提供内容。</summary>
    public IReadOnlyList<InputPort> Inputs { get; init; } = [];

    /// <summary>容器输出端口：端口名到子树内成员引用的映射，值可写 成员标签@端口 精确取成员命名段。
    /// 属于容器定义与装配层内联容器，引用处不能修改；下游执行节点经 From 按端口消费。
    /// 文件里与 <see cref="Outputs"/> 同写在一个 Outputs 数组，条目形状由节点类型决定。容器没有整份产出，一切对外内容都经声明的命名端口。</summary>
    public IReadOnlyDictionary<PortName, ContainerOutput>? ContainerOutputs { get; init; }

    /// <summary>输入端口绑定：容器节点把声明的传入端口绑到当前作用域的来源，值可以是 来源@端口 或 @更外层端口。
    /// 引用容器绑定库定义声明的端口，内联容器绑定自身声明的端口；执行节点不写 In，接线一律经 <see cref="From"/>。</summary>
    public IReadOnlyDictionary<PortName, PortRef>? In { get; init; }

    /// <summary>上游启动组：组是一套完整的输入绑定方案，组内是输入端口到来源端口的连接，组间任一齐备即启动。
    /// 取用方式由输入端口的声明决定。库执行定义不写接线，由引用处注入；库容器成员与装配层节点自身声明。</summary>
    public IReadOnlyList<FromGroup> From { get; init; } = [];

    /// <summary>执行节点的输出端口表：名字与类型。类型在编译期落定产出形态与消费方式，引用处按名字消费。
    /// 执行逐次交回 Text 与 Signal 端口值，引用处不能修改声明。</summary>
    public IReadOnlyList<Port> Outputs { get; init; } = [];

    /// <summary>输出校验的覆盖声明：引用执行节点时可覆盖库定义的校验，展开后并入执行配置。</summary>
    public OutputValidation? Validate { get; init; }

    /// <summary>执行次数上限：执行节点为自身或引用覆盖，容器为组内执行节点的统一默认。未写时由展开解析。</summary>
    public int? MaxExecutions { get; init; }

    /// <summary>有序子节点，非空即容器。容器自身不执行，成员产出齐备时作汇合点。</summary>
    public IReadOnlyList<NodeSpec>? Nodes { get; init; }

    /// <summary>静态端口绑定：目标静态输入端口到来源引用。来源识别与 From、In 使用同一套作用域规则，静态来源必须是静态端口。
    /// 静态绑定不进入 From、版本、就绪与触发，运行期沿绑定链取值。</summary>
    public IReadOnlyDictionary<PortName, PortRef>? Static { get; init; }

    /// <summary>生效输入端口表：作者声明的输入加节点类型声明的静态输入。作者声明同名端口时以作者声明为准。</summary>
    public IReadOnlyList<InputPort> EffectiveInputs =>
        Type?.Shape.StaticInputs is { Count: > 0 } statics
            ? [.. Inputs, .. statics.Where(staticPort => Inputs.All(input => input.Name != staticPort.Name))]
            : Inputs;

    /// <summary>按名字取生效输入端口声明，未声明时为空。</summary>
    public InputPort? FindInput(PortName name) => EffectiveInputs.Find(name);

    /// <summary>执行配置，非空即执行节点或输入节点，只有派发执行的类型会用到其中的执行方式。</summary>
    public ExecutableSpec? Spec { get; init; }
}
