using ErrorOr;
using Kuroe.Kernel.Execution.Shared.Contract;
using Kuroe.Kernel.Execution.Shared.Runtime;

namespace Kuroe.Ability.Tool.Implementation.Contract;

/// <summary>上报本轮无法完成的通道。载体按执行换一份，上报时才知道是哪个执行在报。</summary>
/// <remarks>按执行建立载体。</remarks>
public sealed class CannotCompleteTool(IExecutionRequestEntry requests) : ITool
{
    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; } = Declare(requests);

    /// <summary>把契约定义绑上上报该执行无法完成的调用体。</summary>
    private static IReadOnlyList<ToolFunction> Declare(IExecutionRequestEntry requests) =>
    [
        ContractToolDefinitions.CannotComplete().Bind(arguments => Report(requests, arguments)),
    ];

    /// <summary>上报本轮无法完成并把上报后的本轮契约状态写成回执，错误原样交回模型。</summary>
    private static ErrorOr<string> Report(
        IExecutionRequestEntry requests,
        ToolArguments arguments)
    {
        string? reason = arguments.Text(new ToolName("reason"));
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("Tool.Argument", "上报无法完成时必须给出原因。");
        }

        ErrorOr<ExecutionStatus> outcome = requests.ReportCannotComplete(reason);
        if (outcome.IsError)
        {
            return outcome.ErrorsOrEmptyList;
        }

        return $"已记录本轮无法完成：{outcome.Value.CannotCompleteReason}。{ContractReceipts.Needs(outcome.Value)}";
    }
}