using ErrorOr;

namespace Kuroe.Shared.Executions.Tools;

/// <summary>模型可调用的一个函数：名字、说明、参数与调用体。调用体把结果交回模型，失败时以 ErrorOr 表达。</summary>
/// <remarks>参数的 JSON Schema 由宿主按参数拼装。</remarks>
public sealed class ToolFunction(
    ToolName name,
    string description,
    IReadOnlyList<ToolParameter> parameters,
    Func<ToolArguments, ErrorOr<string>> invoke,
    ToolPath? path = null)
{
    /// <summary>函数名，宿主按它列出可用性。</summary>
    public ToolName Name { get; } = name;

    /// <summary>给模型的说明。</summary>
    public string Description { get; } = description;

    /// <summary>参数声明，宿主按它拼装 JSON Schema。</summary>
    public IReadOnlyList<ToolParameter> Parameters { get; } = parameters;

    /// <summary>调用体调用后的结果，失败时携带错误。</summary>
    public Func<ToolArguments, ErrorOr<string>> Invoke { get; } = invoke;

    /// <summary>函数在工具层级里的路径，白名单按它匹配；未写时即函数名。</summary>
    public ToolPath Path { get; } = path is { Value.Length: > 0 } declared ? declared : new ToolPath(name.Value);
}
