namespace Kuroe.FlowDescription.Json.Files;

/// <summary>节点库文件的形状：文件里的字段名与类型在此固定，读入后装配成节点定义。
/// 属性必须可写，否则缺键的项拿不到声明的默认值。</summary>
public sealed class NodeFileDto
{
    /// <summary>节点库定义，按声明序。</summary>
    public List<NodeDto> Nodes { get; set; } = [];
}