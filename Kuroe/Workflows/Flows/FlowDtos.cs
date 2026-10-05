using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Flows;

/// <summary>流程文件的形状：文件里的字段名与类型在此固定，读入后装配成 <see cref="FlowFile"/>。
/// 属性必须可写，否则缺键的项拿不到声明的默认值。</summary>
internal sealed class FlowFileDto
{
    /// <summary>顶层节点库：节点定义，供流程引用。</summary>
    public List<NodeDto> Nodes { get; set; } = [];

    public List<FlowDto> Flows { get; set; } = [];
}

/// <summary>文件里的一条流程：命名的模型选择与装配树。</summary>
internal sealed class FlowDto
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
/// 写有 <c>Use</c> 即引用节点库定义；否则即节点本身的定义，有 <c>Nodes</c> 子数组为容器，否则为执行节点。</summary>
internal sealed class NodeDto
{
    /// <summary>节点名：库定义名或装配实例名。</summary>
    public string? Name { get; set; }

    /// <summary>引用的节点库定义名。</summary>
    public string? Use { get; set; }

    /// <summary>对执行单元的额外要求，与目标一起构成指令。</summary>
    public string? Prompt { get; set; }

    /// <summary>上下文取自哪些更早节点的产出，引用规则由 FlowRules 校验。</summary>
    public List<string>? From { get; set; }

    /// <summary>可选启动条件组：From 组必须先齐备，再满足任一组成员齐备才启动，组内成员并取。组间任一。</summary>
    public List<List<string>>? AnyOf { get; set; }

    /// <summary>输出校验：收口时校验模型产出，不通过则节点阻塞待返工。</summary>
    public ValidationDto? Validate { get; set; }

    /// <summary>同一执行路径的最高执行次数，未写时取默认值。</summary>
    public int? MaxRuns { get; set; }

    /// <summary>容器声明传入端口名，成员可用 @端口 引用。</summary>
    public List<string>? Inputs { get; set; }

    /// <summary>容器引用把端口绑定到当前作用域可达节点。</summary>
    public Dictionary<string, string>? In { get; set; }

    /// <summary>模型选择引用：装配层执行节点写流程模型配置名，节点组容器成员写模型槽位名。</summary>
    public string? Model { get; set; }

    /// <summary>引用节点组时的模型槽位绑定：槽位名到本流程模型配置名的映射。</summary>
    public Dictionary<string, string>? Models { get; set; }

    /// <summary>能力工具白名单，按工具路径前缀匹配，写上级路径即放行整棵子树。未写或空时不给出任何能力工具。</summary>
    public List<string>? Tools { get; set; }

    public NodeOutput? Output { get; set; }

    /// <summary>执行节点模式的名字：Single、PerItem。</summary>
    public string? Mode { get; set; }

    /// <summary>本执行节点只处理拆分中归属该分支的条目，未写时处理全部条目。</summary>
    public string? Branch { get; set; }

    public NodeGate? Gate { get; set; }

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

/// <summary>输出校验的 JSON 形状：谓词名与参数。</summary>
internal sealed class ValidationDto
{
    /// <summary>谓词名，读取时按枚举解析。</summary>
    public string? Predicate { get; set; }

    /// <summary>谓词参数：指定文本或正则，NonEmpty 不需要。</summary>
    public string? Argument { get; set; }
}
