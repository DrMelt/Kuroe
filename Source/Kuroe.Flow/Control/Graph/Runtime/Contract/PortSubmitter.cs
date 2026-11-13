using ErrorOr;
using Kuroe.Flow.Control;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Control.Graph.Engine;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.RuntimeGraph.Ports;
using Kuroe.Flow.Node.StaticGraph.Ports;
using ExecutionStatus = Kuroe.Flow.Execution.ExecutionStatus;

namespace Kuroe.Flow.Control.Graph.Contract;

/// <summary>模型侧交回结构化产出的入口：输出端口交端口名到端口值，Text 端口给内容，Signal 端口只给端口名。
/// 提交者身份在此认定，结果以提交后的本轮契约状态交回，回执措辞由工具实现层给出。</summary>
public sealed class PortSubmitter(IFlowExecutionGateway executions) : IExecutionPortSubmitter
{
    /// <summary>需交回输出端口的执行节点交回端口产出，端口必须与需交回的端口一一对应。</summary>
    public ErrorOr<ExecutionStatus> Submit(
        ExecutionId id,
        IReadOnlyList<ExecutionOutputValue> values)
    {
        ErrorOr<(IExecution Execution, ITaskFlowContext Task)> owner = SubmitterContext.Resolve(
            executions,
            id,
            "提交端口产出");
        if (owner.IsError)
        {
            return owner.ErrorsOrEmptyList;
        }

        if (values.Count == 0)
        {
            return [ContractErrors.Ports("端口产出为空")];
        }

        Dictionary<PortName, PortValue> parsed = [];
        foreach (ExecutionOutputValue value in values)
        {
            ErrorOr<PortName> name = PortName.Create(value.Port.Value);
            if (name.IsError)
            {
                return name.ErrorsOrEmptyList;
            }

            parsed[name.Value] = new PortValue(value.Text, value.Fires);
        }

        (IExecution execution, ITaskFlowContext task) = owner.Value;
        lock (task.Gate)
        {
            GraphFlowControl control = task.Control as GraphFlowControl
                ?? throw new InvalidOperationException($"任务 {task.Id} 没有图流程控制。");

            return control.SubmitPorts(execution, parsed);
        }
    }
}