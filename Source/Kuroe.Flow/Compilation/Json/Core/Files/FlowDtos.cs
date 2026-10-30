using System.Text.Json;

namespace Kuroe.Flow.Json.Files;

/// <summary>流程文件的形状：文件里的字段名与类型在此固定，读入后装配成节点定义与流程模型。
/// 属性必须可写，否则缺键的项拿不到声明的默认值。</summary>
public sealed class FlowFileDto
{
    /// <summary>顶层节点库：节点定义，供流程引用。</summary>
    public List<NodeDto> Nodes { get; set; } = [];

    /// <summary>文件里的流程，按声明序。</summary>
    public List<FlowDto> Flows { get; set; } = [];
}

/// <summary>文件里的一条流程：开始入口的输出端口与装配树。</summary>
public sealed class FlowDto
{
    /// <summary>流程名。</summary>
    public string? Name { get; set; }

    /// <summary>流程说明。</summary>
    public string? Description { get; set; }

    /// <summary>开始入口的输出端口，条目形状与节点输出端口一致。</summary>
    public StartDto? Start { get; set; }

    /// <summary>开始端口到宿主输入名的绑定。</summary>
    public Dictionary<string, string>? HostStartInputs { get; set; }

    /// <summary>装配树的根节点，恰好一个。</summary>
    public List<NodeDto> Nodes { get; set; } = [];
}

/// <summary>文件里一条流程的开始入口：流程的唯一来源，输出端口即提交时给的初始输入。</summary>
public sealed class StartDto
{
    /// <summary>开始端口的条目，按声明序。</summary>
    public List<JsonElement>? Outputs { get; set; }
}
