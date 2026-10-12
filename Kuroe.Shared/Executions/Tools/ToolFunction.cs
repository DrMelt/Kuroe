using ErrorOr;

namespace Kuroe.Shared.Executions.Tools;

/// <summary>模型可调用的函数：定义与调用体。调用体把结果交回模型，失败时以 ErrorOr 表达。</summary>
public sealed class ToolFunction(ToolDefinition definition, Func<ToolArguments, ErrorOr<string>> invoke)
{
    /// <summary>函数的定义。</summary>
    public ToolDefinition Definition { get; } = definition;

    /// <summary>调用体调用后的结果，失败时携带错误。</summary>
    public Func<ToolArguments, ErrorOr<string>> Invoke { get; } = invoke;
}
