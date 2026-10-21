using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Compiler.Shared;

/// <summary>流程文件的描述文档：顶层节点库定义与流程，保留文件里写下的形态，不构造模型。</summary>
public sealed record FlowDocument(
    IReadOnlyList<NodeDefinitionDocument> Nodes,
    IReadOnlyList<FlowDocumentFlow> Flows);

/// <summary>描述文档里的一条流程。</summary>
public sealed record FlowDocumentFlow(
    string Name,
    string? Description,
    IReadOnlyList<ModelDocument> Models,
    IReadOnlyList<PortDocument> StartOutputs,
    NodeDocument? Root);

/// <summary>流程里命名的模型选择。</summary>
public sealed record ModelDocument(string Name, string? Model, bool Runtime = false);

/// <summary>输出端口条目：执行节点写 Name 与 Type，容器写 Name 与 Ref，开始端口可写 Value。</summary>
public sealed record PortDocument(
    string Name,
    PortType? Type = null,
    string? Description = null,
    string? Value = null,
    string? Ref = null);

/// <summary>输入端口条目：执行节点与容器共用。</summary>
public sealed record InputPortDocument(
    string Name,
    PortType? Type = null,
    string? Description = null,
    string? Default = null);

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

    /// <summary>输出校验覆盖。</summary>
    public ValidationDocument? Validate { get; init; }

    /// <summary>执行次数上限。</summary>
    public int? MaxExecutions { get; init; }
}

/// <summary>定义节点：节点库定义、流程内联定义与容器。</summary>
public sealed record NodeDefinitionDocument : NodeDocument
{
    /// <summary>节点名，定义在库里的名字。</summary>
    public required string Name { get; init; }

    /// <summary>节点类型名。</summary>
    public required string Kind { get; init; }

    /// <summary>输入端口声明。</summary>
    public IReadOnlyList<InputPortDocument> Inputs { get; init; } = [];

    /// <summary>输出端口声明，容器条目写 Ref。</summary>
    public IReadOnlyList<PortDocument> Outputs { get; init; } = [];

    /// <summary>容器定义的模型槽位声明。</summary>
    public IReadOnlyList<string>? ModelSlots { get; init; }

    /// <summary>执行节点的模型选择。</summary>
    public string? Model { get; init; }

    /// <summary>定义条目写了模型绑定对象。绑定只属于引用节点组，构造层据此报错。</summary>
    public bool HasModelBindings { get; init; }

    /// <summary>定义条目写了输入端口绑定。绑定只属于引用容器，构造层据此报错。</summary>
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

    /// <summary>执行节点的模型选择。</summary>
    public string? Model { get; init; }

    /// <summary>容器引用处的模型槽位绑定。</summary>
    public IReadOnlyDictionary<string, string>? Models { get; init; }
}