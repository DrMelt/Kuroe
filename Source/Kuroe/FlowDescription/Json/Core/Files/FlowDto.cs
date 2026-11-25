using System.Text.Json;

namespace Kuroe.FlowDescription.Json.Files;

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