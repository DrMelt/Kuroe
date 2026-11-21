using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kuroe.FlowDescription.Json.Files;

/// <summary>文件里的一个节点，省略的字段在装配时取安全默认值，合法性交校验。
/// 写有 <c>Use</c> 即引用节点库定义；否则即节点本身的定义。文件层只认结构字段，类型专属字段只能写在 <see cref="Parameters"/> 对象里。</summary>
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

    /// <summary>节点输出端口表：所有节点共用。条目写 Name 与可选 Type、Description、Value、Ref、Trigger；
    /// 容器按 Ref 写 成员@端口，执行节点按 Ref 写自身静态输入，常量输出可写 Value。</summary>
    public List<JsonElement>? Outputs { get; set; }

    /// <summary>上游 <see cref="NodeDto.From"/> 组：数组元素是一个组，组内是输入端口名到来源的映射，值写引用文本或带 Ref 与 Condition 的对象。
    /// 组是一套完整的输入绑定方案，组间任一齐备即启动。引用规则在流程编译阶段校验。</summary>
    public List<JsonElement>? From { get; set; }

    /// <summary>同一执行路径的最高执行次数，未写时取默认值。</summary>
    public int? MaxExecutions { get; set; }

    /// <summary>输入端口条目：执行节点与容器共用，写 Name 与可选 Type、Mode、Description、Default、Role。
    /// 值由节点自身的 <see cref="NodeDto.Bindings"/> 绑定提供，或由 <see cref="NodeDto.From"/> 提供。Mode 为 Constant 时是静态输入。</summary>
    public List<JsonElement>? Inputs { get; set; }

    /// <summary>产出模式：Repeated 逐次发布，Once 首次发布后冻结。</summary>
    public string? Emission { get; set; }

    /// <summary>静态输入绑定：目标输入端口到来源引用文本。来源写 @端口 或 来源@端口，识别规则与 <see cref="NodeDto.From"/> 一致。
    /// 动态输入写在 <see cref="NodeDto.From"/>，输入绑定的值不能写对象与 Condition。</summary>
    public Dictionary<string, JsonElement>? Bindings { get; set; }

    /// <summary>类型参数段：类型专属字段的唯一位置，取值与字段名由节点类型解释，文件层只要求它是对象。</summary>
    public JsonElement? Parameters { get; set; }

    /// <summary>子节点，非空即容器。</summary>
    public List<NodeDto>? Nodes { get; set; }

    /// <summary>节点对象上不属于结构字段的键，文件层据此报出类型专属字段写错了位置。</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Unrecognized { get; set; }
}
