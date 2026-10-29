using ErrorOr;
using Kuroe.Node.Llm.Tools;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Runtime.Contract;

namespace Kuroe.Tool.Contract;

/// <summary>上报本轮无法完成的通道。载体按执行换一份，上报时才知道是哪个执行在报。</summary>
/// <remarks>按执行建立载体。</remarks>
public sealed class CannotCompleteTool(CannotCompleteSubmitter intake, ExecutionId execution) : ITool
{
    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; } = Declare(intake, execution);

    /// <summary>把契约定义绑上上报该执行无法完成的调用体。</summary>
    private static IReadOnlyList<ToolFunction> Declare(CannotCompleteSubmitter intake, ExecutionId execution) =>
    [
        ContractToolDefinitions.CannotComplete().Bind(arguments => Report(intake, execution, arguments)),
    ];

    /// <summary>上报本轮无法完成并把上报后的本轮契约状态写成回执，错误原样交回模型。</summary>
    private static ErrorOr<string> Report(CannotCompleteSubmitter intake, ExecutionId execution, ToolArguments arguments)
    {
        ErrorOr<ExecutionStatus> outcome = intake.Report(execution, arguments.Text(new ToolName("reason")));
        if (outcome.IsError)
        {
            return outcome.ErrorsOrEmptyList;
        }

        return $"已记录本轮无法完成：{outcome.Value.CannotCompleteReason}。{ContractReceipts.Needs(outcome.Value)}";
    }
}