namespace Kuroe.Shared.Agent.Tools;

/// <summary>模型可调用的一个函数：名字、说明、参数与调用体。调用体把一段文本交回模型，拒绝也写成文本。</summary>
/// <remarks>参数的 JSON Schema 由宿主按参数拼装。</remarks>
public sealed class ToolFunction(
    string name,
    string description,
    IReadOnlyList<ToolParameter> parameters,
    Func<ToolArguments, string> invoke)
{

    /// <summary>函数名，宿主按它列出可用性。</summary>
    public string Name { get; } = name;

    /// <summary>给模型的说明。</summary>
    public string Description { get; } = description;

    /// <summary>参数声明，宿主按它拼装 JSON Schema。</summary>
    public IReadOnlyList<ToolParameter> Parameters { get; } = parameters;

    /// <summary>调用体，实参由库解析后传入。</summary>
    public Func<ToolArguments, string> Invoke { get; } = invoke;
}
