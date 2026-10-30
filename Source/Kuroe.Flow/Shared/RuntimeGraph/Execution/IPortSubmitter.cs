using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;

namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>模型侧交回结构化产出的入口。工具实现依赖此契约，具体提交由执行侧实现。</summary>
public interface IPortSubmitter
{
    /// <summary>交回命名端口产出，成功时给出提交后的本轮契约状态。</summary>
    ErrorOr<ExecutionStatus> SubmitValues(ExecutionId id, IReadOnlyDictionary<string, PortValue> values);
}