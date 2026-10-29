using System.Text;
using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Runtime.Contract;
using Kuroe.Presentation;

namespace Kuroe.Tool.Contract;

/// <summary>查看本轮执行契约状态的通道。载体按执行换一份，查看时才知道是哪个执行在报。</summary>
/// <remarks>按执行建立载体。</remarks>
public sealed class StatusTool(ExecutionStatusReader reader, ExecutionId execution) : ITool
{
    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; } = Declare(reader, execution);

    /// <summary>把状态定义绑上读取该执行契约状态的调用体。</summary>
    private static IReadOnlyList<ToolFunction> Declare(ExecutionStatusReader reader, ExecutionId execution) =>
    [
        ContractToolDefinitions.Status().Bind(_ => Describe(reader, execution)),
    ];

    /// <summary>本轮契约状态的模型可见文本，读不到时把错误交回模型。</summary>
    private static ErrorOr<string> Describe(ExecutionStatusReader reader, ExecutionId execution)
    {
        ErrorOr<ExecutionStatus> read = reader.Read(execution);
        if (read.IsError)
        {
            return read.ErrorsOrEmptyList;
        }

        ExecutionStatus status = read.Value;

        var text = new StringBuilder();
        text.AppendLine($"节点 {status.Node} · 第 {status.ExecutionCount} 轮 · 执行上限 {status.MaxExecutions}");
        text.AppendLine($"节点状态：{InfoLabels.Of(status.NodeState)}");
        text.AppendLine(ContractReceipts.Needs(status));

        if (status.CannotCompleteReason is { } reason)
        {
            text.AppendLine($"已上报无法完成：{reason}");
        }

        return text.ToString().TrimEnd();
    }
}
