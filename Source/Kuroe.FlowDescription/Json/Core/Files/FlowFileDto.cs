namespace Kuroe.FlowDescription.Json.Files;

/// <summary>流程文件的形状：文件里的字段名与类型在此固定，读入后装配成流程模型。
/// 属性必须可写，否则缺键的项拿不到声明的默认值。</summary>
public sealed class FlowFileDto
{
    /// <summary>文件里的流程，按声明序。</summary>
    public List<FlowDto> Flows { get; set; } = [];
}