using System.Text;
using ErrorOr;
using Kuroe.Kernel.Execution.Shared.Contract;
using Kuroe.Kernel.Execution.Shared.Runtime;
using Kuroe.Kernel.Execution.Shared.State;

namespace Kuroe.Tool.Implementation.Contract;

/// <summary>查看本轮执行契约状态的通道。载体按执行换一份，查看时才知道是哪个执行在报。</summary>
/// <remarks>按执行建立载体。</remarks>
public sealed class StatusTool(
    IExecutionRequestEntry requests,
    int attempt,
    int maxAttempts) : ITool
{
    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; } = Declare(requests, attempt, maxAttempts);

    /// <summary>把状态定义绑上读取该执行契约状态的调用体。</summary>
    private static IReadOnlyList<ToolFunction> Declare(
        IExecutionRequestEntry requests,
        int attempt,
        int maxAttempts) =>
    [
        ContractToolDefinitions.Status().Bind(_ => Describe(requests, attempt, maxAttempts)),
    ];

    /// <summary>本轮契约状态的模型可见文本。</summary>
    private static ErrorOr<string> Describe(
        IExecutionRequestEntry requests,
        int attempt,
        int maxAttempts)
    {
        ExecutionStatus status = requests.ReadStatus();
        string limit = maxAttempts == int.MaxValue ? "不限" : maxAttempts.ToString();

        var text = new StringBuilder();
        text.AppendLine($"步骤 {status.Step} · 第 {attempt} 轮 · 执行上限 {limit}");
        text.AppendLine($"执行状态：{status.State.Label()}");
        text.AppendLine(ContractReceipts.Needs(status));

        if (status.CannotCompleteReason is { } reason)
        {
            text.AppendLine($"已上报无法完成：{reason}");
        }

        return text.ToString().TrimEnd();
    }
}
