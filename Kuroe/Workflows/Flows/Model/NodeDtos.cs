using System.Text.Json;
using System.Text.Json.Serialization;
using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Flows.Model;

/// <summary>文件里的一个节点，省略的字段在装配时取安全默认值，合法性交校验。
/// 写有 <c>Use</c> 即引用节点库定义；否则即节点本身的定义，有 <c>Nodes</c> 子数组为容器，否则为执行节点。</summary>
internal sealed class NodeDto
{
    /// <summary>节点名：库定义名或装配实例名。</summary>
    public string? Name { get; set; }

    /// <summary>节点类型：Container、Executable 或 Input。非引用节点必填，引用节点不写。</summary>
    public NodeKind? Kind { get; set; }

    /// <summary>引用的节点库定义名。</summary>
    public string? Use { get; set; }

    /// <summary>对执行单元的额外要求，与目标一起构成指令。</summary>
    public string? Prompt { get; set; }

    /// <summary>输入节点的提示文本。</summary>
    public string? Question { get; set; }

    /// <summary>节点输出端口表：条目形状由节点类型决定。执行节点与输入节点写 Name 与 Kind，Kind 必填；
    /// 容器写 Name 与 Ref，Ref 写 成员@端口。</summary>
    public List<JsonElement>? Outputs { get; set; }

    /// <summary>恒定系统指令块：作为系统指令置于请求最前，内容必须恒定以命中服务商前缀缓存。</summary>
    public List<string>? SystemPrompt { get; set; }

    /// <summary>上游启动组：数组元素是一个组，组内是输入端口名到来源端口引用的映射，值写 @端口 或 来源@端口。
    /// 组是一套完整的输入绑定方案，组间任一齐备即启动。引用规则由 FlowRules 校验。</summary>
    public List<JsonElement>? From { get; set; }

    /// <summary>输出校验：收口时校验模型产出，不通过则节点阻塞待返工。</summary>
    public ValidationDto? Validate { get; set; }

    /// <summary>同一执行路径的最高执行次数，未写时取默认值。</summary>
    public int? MaxRuns { get; set; }

    /// <summary>输入端口条目：执行节点与容器共用，写 Name 与可选 Type、Description。
    /// 容器的输入端口由引用处 In 绑定，执行节点的由自身 From 绑定。</summary>
    public List<JsonElement>? Inputs { get; set; }

    /// <summary>容器引用把端口绑定到当前作用域可达节点。</summary>
    public Dictionary<string, string>? In { get; set; }

    /// <summary>模型选择引用：装配层执行节点写流程模型选择名，节点组容器成员写模型槽位名。</summary>
    public string? Model { get; set; }

    /// <summary>模型槽位：容器定义写槽位名数组声明，引用节点组写槽位名到本流程模型选择名的对象绑定。</summary>
    public JsonElement? Models { get; set; }

    /// <summary>能力工具白名单，按工具路径前缀匹配，写上级路径即放行整棵子树。未写或空时不给出任何能力工具。</summary>
    public List<string>? Tools { get; set; }

    /// <summary>执行节点模式的名字：Single、PerItem。</summary>
    public string? Mode { get; set; }

    /// <summary>本执行节点只处理拆分中归属该分支的条目，未写时处理全部条目。</summary>
    public string? Branch { get; set; }

    public NodeGate? Gate { get; set; }

    public List<NodeDto>? Nodes { get; set; }
}

/// <summary>端口的对象形状：输出端口写 Name 与 Type，容器输出端口写 Name 与 Ref，输入端口写 Name 与可选 Type，键名与读侧一致。</summary>
internal sealed class PortEntryDto
{
    public string? Name { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PortType? Type { get; set; }

    /// <summary>容器输出端口的转发目标，写 成员@端口。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Ref { get; set; }

    /// <summary>端口承载内容的说明，未写时为空。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    /// <summary>开始端口的固定值，带值时固定输出该值，未写时承接提交的任务目标。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Value { get; set; }
}

/// <summary>输出校验的 JSON 形状：谓词名与参数。</summary>
internal sealed class ValidationDto
{
    /// <summary>谓词名，读取时按枚举解析。</summary>
    public string? Predicate { get; set; }

    /// <summary>谓词参数：指定文本或正则，NonEmpty 不需要。</summary>
    public string? Argument { get; set; }
}
