using ErrorOr;
using Kuroe.Execution.Shared.Contract;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Ports;
using Kuroe.Execution.Shared.Request;

namespace Kuroe.Execution.Shared.Runtime;

/// <summary>一次执行的请求入口：运行器与工具经它读取请求、交回端口、上报无法完成与读取契约状态。</summary>
public interface IExecutionRequestEntry
{
    /// <summary>执行标识。</summary>
    ExecutionId Id { get; }

    /// <summary>本次执行的中性请求。</summary>
    ExecutionRequest Request { get; }

    /// <summary>本轮契约状态。</summary>
    ExecutionStatus ReadStatus();

    /// <summary>交回端口产出，成功时给出提交后的本轮契约状态。</summary>
    ErrorOr<ExecutionStatus> SubmitOutputs(IReadOnlyList<ExecutionOutputValue> values);

    /// <summary>上报本轮无法完成并给出原因，成功时给出上报后的本轮契约状态。</summary>
    ErrorOr<ExecutionStatus> ReportCannotComplete(string reason);
}