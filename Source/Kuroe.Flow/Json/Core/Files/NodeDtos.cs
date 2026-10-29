using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kuroe.Flow.Json.Files;

/// <summary>文件里的一个节点，省略的字段在装配时取安全默认值，合法性交校验。
/// 写有 <c>Use</c> 即引用节点库定义；否则即节点本身的定义，结构字段在此固定，类型专属字段落在参数段。</summary>
public sealed class NodeDto
{
    /// <summary>节点名：定义条目的名字，节点库定义的名字或流程与容器内内联定义的名字。被使用的节点不写。</summary>
    public string? Name { get; set; }

    /// <summary>节点标签：被使用的节点在图里的标识段。定义条目不写。</summary>
    public string? Label { get; set; }

    /// <summary>节点类型名，取值由节点类型注册表决定。非引用节点必填，引用节点不写。</summary>
    public string? Kind { get; set; }

    /// <summary>引用的节点库定义名。</summary>
    public string? Use { get; set; }

    /// <summary>节点输出端口表：所有节点共用。条目写 Name 与可选 Type、Description、Value、Ref、Constant、Trigger；
    /// 容器按 Ref 写 成员@端口，执行节点按 Ref 写自身静态输入，常量输出可写 Value。</summary>
    public List<JsonElement>? Outputs { get; set; }

    /// <summary>上游启动组：数组元素是一个组，组内是输入端口名到来源端口引用的映射，值写 @端口 或 来源@端口。
    /// 组是一套完整的输入绑定方案，组间任一齐备即启动。引用规则由 FlowRules 校验。</summary>
    public List<JsonElement>? From { get; set; }

    /// <summary>输出校验：收口时校验模型产出，不通过则节点阻塞待返工。</summary>
    public ValidationDto? Validate { get; set; }

    /// <summary>同一执行路径的最高执行次数，未写时取默认值。</summary>
    public int? MaxExecutions { get; set; }

    /// <summary>输入端口条目：执行节点与容器共用，写 Name 与可选 Type、Description。
    /// 容器的传入端口由容器节点的 In 绑定，执行节点的由自身 From 绑定。</summary>
    public List<JsonElement>? Inputs { get; set; }

    /// <summary>容器节点的传入端口绑定：端口名到当前作用域来源的映射。</summary>
    public Dictionary<string, string>? In { get; set; }

    /// <summary>静态端口绑定：目标静态输入端口到来源引用文本。来源写 @端口 或 来源@端口，识别规则与 From、In 一致。</summary>
    public Dictionary<string, string>? Static { get; set; }

    /// <summary>节点类型的参数段：键为类型声明的字段名，未知字段与形状不符由文件层报出，字段的语义由类型报出。</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Parameters { get; set; }

    /// <summary>子节点，非空即容器。</summary>
    public List<NodeDto>? Nodes { get; set; }
}

/// <summary>端口的对象形状：输出端口与输入端口共用条目，输出写 Constant，输入写 Static。</summary>
public sealed class PortEntryDto
{
    /// <summary>端口名。</summary>
    public string? Name { get; set; }

    /// <summary>端口类型名，取值由端口类型注册表决定。未写时按 Text。</summary>
    public string? Type { get; set; }

    /// <summary>输出端口的来源引用，写 成员@端口、来源@端口 或 @本节点输入。</summary>
    public string? Ref { get; set; }

    /// <summary>端口承载内容的说明，未写时为空。</summary>
    public string? Description { get; set; }

    /// <summary>常量输出端口的固定值，未写时由 Ref 提供来源。</summary>
    public string? Value { get; set; }

    /// <summary>输入端口的默认值，来源组没有连接该端口时提供内容。</summary>
    public string? Default { get; set; }

    /// <summary>输出端口的常量标记。值由定义确定，只供 Static 读取。</summary>
    public bool? Constant { get; set; }

    /// <summary>输出端口的触发标记。动态产出按交回值决定是否触发下游。</summary>
    public bool? Trigger { get; set; }

    /// <summary>输入端口的静态标记。静态输入只从 Static 绑定或默认值取值。</summary>
    public bool? Static { get; set; }

    /// <summary>输入端口的执行角色，取值 Content、Configuration 或 Control。</summary>
    public string? Role { get; set; }
}

/// <summary>输出校验的 JSON 形状：谓词名与参数。</summary>
public sealed class ValidationDto
{
    /// <summary>谓词名，读取时按枚举解析。</summary>
    public string? Predicate { get; set; }

    /// <summary>谓词参数：指定文本或正则，NonEmpty 不需要。</summary>
    public string? Argument { get; set; }
}
