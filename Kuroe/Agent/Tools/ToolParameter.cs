namespace Kuroe.Agent.Tools;

/// <summary>函数的一个参数。真假参数在交给模型的 schema 里是布尔，其余是字符串。</summary>
/// <param name="Name">参数名，调用体按它取实参。</param>
/// <param name="Description">给模型的说明。</param>
/// <param name="Flag">该参数是布尔值，缺省为字符串。</param>
/// <param name="Required">模型是否必须给出，缺省为否。</param>
public sealed record ToolParameter(string Name, string Description, bool Flag = false, bool Required = false);
