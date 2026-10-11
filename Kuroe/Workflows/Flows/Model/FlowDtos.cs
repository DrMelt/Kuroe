namespace Kuroe.Workflows.Flows.Model;

/// <summary>流程文件的形状：文件里的字段名与类型在此固定，读入后装配成节点定义与流程模型。
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
