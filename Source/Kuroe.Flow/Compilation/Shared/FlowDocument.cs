using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Compilation.Shared;

/// <summary>流程文件的描述文档：顶层节点库定义与流程，保留文件里写下的形态，不构造模型。
/// 输出端口在所有节点上使用同一形状，节点类别只决定来源作用域。</summary>
public sealed record FlowDocument(
    IReadOnlyList<NodeDefinitionDocument> Nodes,
    IReadOnlyList<FlowDocumentFlow> Flows);

/// <summary>描述文档里的一条流程。HostStartInputs 是开始端口到宿主输入名的绑定，端口名可以自定义，输入名由宿主解释。</summary>
public sealed record FlowDocumentFlow(
    string Name,
    string? Description,
    IReadOnlyList<PortDocument> StartOutputs,
    IReadOnlyDictionary<string, string> HostStartInputs,
    NodeDocument? Root);

/// <summary>输出端口条目：所有节点共用。Type 省略时按 Text；Value 是固定值，Ref 是同一节点内部作用域中的来源端口，两者互斥。
/// Constant 标记常量端口，值由定义确定，只供 Static 读取，不进入 From。</summary>
public sealed record PortDocument(
    string Name,
    PortType? Type = null,
    string? Description = null,
    string? Value = null,
    string? Ref = null,
    bool IsConstant = false,
    bool? Trigger = null);

/// <summary>输入端口条目：执行节点与容器共用。Static 标记常量输入，值由 Static 绑定或默认值提供。
/// Role 是执行角色，Trigger 是动态绑定是否使用触发门控。</summary>
public sealed record InputPortDocument(
    string Name,
    PortType? Type = null,
    string? Description = null,
    string? Default = null,
    bool IsStatic = false,
    InputRole? Role = null,
    bool? Trigger = null);

/// <summary>输出校验条目。</summary>
public sealed record ValidationDocument(string Predicate, string? Argument);

/// <summary>上游连接：目标输入端口与来源端口引用。</summary>
public sealed record FromLinkDocument(string Input, string Ref);

/// <summary>一组启动条件。</summary>
public sealed record FromGroupDocument(IReadOnlyList<FromLinkDocument> Links);

/// <summary>节点描述：定义与引用共享的公共字段。</summary>
public abstract record NodeDocument
{
    /// <summary>上游启动组。</summary>
    public IReadOnlyList<FromGroupDocument> From { get; init; } = [];

    /// <summary>静态端口绑定：目标静态输入端口到来源引用文本。来源识别与 From、In 使用同一套作用域规则。</summary>
    public IReadOnlyDictionary<string, string>? Static { get; init; }

    /// <summary>输出校验覆盖。</summary>
    public ValidationDocument? Validate { get; init; }

    /// <summary>执行次数上限。</summary>
    public int? MaxExecutions { get; init; }
}

/// <summary>定义节点：节点库定义、流程内联定义与容器。所有定义共用统一输出端口表。</summary>
public sealed record NodeDefinitionDocument : NodeDocument
{
    /// <summary>节点名，定义在库里的名字。</summary>
    public required string Name { get; init; }

    /// <summary>节点类型名。</summary>
    public required string Kind { get; init; }

    /// <summary>输入端口声明。</summary>
    public IReadOnlyList<InputPortDocument> Inputs { get; init; } = [];

    /// <summary>输出端口声明，容器按 Ref 写成员来源。</summary>
    public IReadOnlyList<PortDocument> Outputs { get; init; } = [];

    /// <summary>定义条目写了输入端口绑定。库定义不接线，内联容器在流程作用域绑定自身声明的传入端口。</summary>
    public IReadOnlyDictionary<string, string>? In { get; init; }

    /// <summary>子节点，非空即容器。</summary>
    public IReadOnlyList<NodeDocument>? Nodes { get; init; }

    /// <summary>类型参数段。</summary>
    public IReadOnlyDictionary<string, FlowValue> Parameters { get; init; } = new Dictionary<string, FlowValue>();
}

/// <summary>引用节点：使用节点库定义，只写标签与使用处可覆盖的字段。</summary>
public sealed record NodeReferenceDocument : NodeDocument
{
    /// <summary>节点在图里的标识段。</summary>
    public required string Label { get; init; }

    /// <summary>引用的节点库定义名。</summary>
    public required string Use { get; init; }

    /// <summary>容器引用处的输入端口绑定。</summary>
    public IReadOnlyDictionary<string, string>? In { get; init; }
}