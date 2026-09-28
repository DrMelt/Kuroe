using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Flows;

/// <summary>流程文件的形状：文件里的字段名与类型在此固定，读入后装配成 <see cref="Workflow"/>。
/// 属性必须可写，否则缺键的项拿不到声明的默认值。</summary>
internal sealed class FlowFileDto
{
    public List<WorkflowDto> Flows { get; set; } = [];
}

/// <summary>文件里的一条流程：命名的模型选择与节点树。</summary>
internal sealed class WorkflowDto
{
    public string? Name { get; set; }

    public string? Description { get; set; }

    public List<ModelDto> Models { get; set; } = [];

    public List<NodeDto> Nodes { get; set; } = [];
}

/// <summary>文件里的一个模型选择定义。</summary>
internal sealed class ModelDto
{
    public string? Name { get; set; }

    public string? Model { get; set; }
}

/// <summary>文件里的一个节点，省略的字段在装配时取安全默认值，合法性交校验。
/// 有子节点即容器，否则为执行节点。</summary>
internal sealed class NodeDto
{
    public string? Name { get; set; }

    public string? Prompt { get; set; }

    public List<string>? From { get; set; }

    /// <summary>引用的模型选择配置名。</summary>
    public string? Model { get; set; }

    /// <summary>能力工具白名单，按函数名匹配。未写或空时不给出任何能力工具。</summary>
    public List<string>? Tools { get; set; }

    public NodeOutput? Output { get; set; }

    /// <summary>执行节点模式的名字：Single、PerItem。</summary>
    public string? Mode { get; set; }

    /// <summary>本执行节点只处理拆分中归属该分支的条目，未写时处理全部条目。</summary>
    public string? Branch { get; set; }

    public NodeGate? Gate { get; set; }

    public RejectAction? OnReject { get; set; }

    public int? MaxAttempts { get; set; }

    /// <summary>拆分源的固定配置，只能写在规划执行节点上。</summary>
    public SplitDto? Split { get; set; }

    public List<NodeDto>? Nodes { get; set; }
}

/// <summary>拆分源的固定配置：静态条目、模型补充上限与统一验收文本。</summary>
internal sealed class SplitDto
{
    public List<SplitItemDto>? Items { get; set; }

    /// <summary>模型最多补充的条数，0 或未写表示不补充。</summary>
    public int? ExtrasMax { get; set; }

    /// <summary>统一验收文本，覆盖模型补充条目的验收标准。</summary>
    public string? Acceptance { get; set; }
}

/// <summary>拆分里一条定死的条目。</summary>
internal sealed class SplitItemDto
{
    public string? Title { get; set; }

    public string? Instruction { get; set; }

    public string? Acceptance { get; set; }

    public string? Branch { get; set; }
}