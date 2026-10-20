using ErrorOr;
using Kuroe.Node.Llm.Tools;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Runtime.Tasks;

namespace Kuroe.Tool.Contract;

/// <summary>上报本轮无法完成的通道。载体按执行换一份，上报时才知道是哪个执行在报。</summary>
public sealed class CannotCompleteTool : IScopedTool
{
    private readonly CannotCompleteSubmitter _intake;

    /// <summary>容器装配用的载体，尚未绑定执行。</summary>
    public CannotCompleteTool(CannotCompleteSubmitter intake)
    {
        _intake = intake;
        Functions = Declare(intake, null);
    }

    private CannotCompleteTool(CannotCompleteSubmitter intake, ExecutionId execution)
    {
        _intake = intake;
        Functions = Declare(intake, execution);
    }

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>换一份绑定到该执行的载体。</summary>
    public ITool ForExecution(ExecutionId execution) => new CannotCompleteTool(_intake, execution);

    /// <summary>把契约定义绑上上报该执行无法完成的调用体。</summary>
    private static IReadOnlyList<ToolFunction> Declare(CannotCompleteSubmitter intake, ExecutionId? execution) =>
    [
        ContractToolDefinitions.CannotComplete().Bind(arguments => Report(intake, execution ?? default, arguments)),
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