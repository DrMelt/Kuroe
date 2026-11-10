using System.Text.Json;

namespace Kuroe.Flow.Json.Files;

/// <summary>流程文件的形状：文件里的字段名与类型在此固定，读入后装配成流程模型。
/// 属性必须可写，否则缺键的项拿不到声明的默认值。</summary>
public sealed class FlowFileDto
{
    /// <summary>文件里的流程，按声明序。</summary>
    public List<FlowDto> Flows { get; set; } = [];
}

/// <summary>文件里的一条流程：流程级开始端口与装配树。</summary>
public sealed class FlowDto
{
    /// <summary>流程名。</summary>
    public string? Name { get; set; }

    /// <summary>流程说明。</summary>
    public string? Description { get; set; }

    /// <summary>流程级开始端口，条目形状与节点输出端口一致。</summary>
    public List<JsonElement>? Ports { get; set; }

    /// <summary>装配树的根节点，唯一。</summary>
    public NodeDto? Root { get; set; }
}
