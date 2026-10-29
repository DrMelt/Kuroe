using ErrorOr;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Kuroe.Flow.Runtime.Nodes;

namespace Kuroe.Flow.Runtime.Contract;

/// <summary>模型侧交回结构化产出的入口：输出端口交端口名到端口值，Text 端口给内容，Signal 端口只给端口名，
/// Trigger 版端口再给是否触发下游的布尔值。提交者身份在此认定，结果以提交后的本轮契约状态交回，回执措辞由工具实现层给出。</summary>
public sealed class PortSubmitter(ExecutionIndex executions)
{
    /// <summary>需交回输出端口的执行节点交回端口产出，端口必须与需交回的端口一一对应。成功时给出提交后的本轮契约状态，错误以 ErrorOr 表达。</summary>
    public ErrorOr<ExecutionStatus> SubmitValues(ExecutionId id, IReadOnlyDictionary<string, PortValue> values)
    {
        ErrorOr<(Execution Execution, WorkTask Task)> owner = SubmitterContext.Resolve(executions, id, "提交端口产出");
        if (owner.IsError)
        {
            return owner.ErrorsOrEmptyList;
        }

        if (values.Count == 0)
        {
            return [ContractErrors.Ports("端口产出为空")];
        }

        Dictionary<PortName, PortValue> parsed = [];
        foreach ((string port, PortValue value) in values)
        {
            ErrorOr<PortName> name = PortName.Create(port);
            if (name.IsError)
            {
                return name.ErrorsOrEmptyList;
            }

            parsed[name.Value] = value;
        }

        (Execution execution, WorkTask task) = owner.Value;
        lock (task.Gate)
        {
            IReadOnlyList<Port> declared = task.Runtime.Executable(execution.Input.Node).Executable.RequiredPorts;
            if (declared.Count == 0)
            {
                return Error.Validation("Tool.Argument", "该节点没有需要交回的输出端口。");
            }

            RuntimeExecutable node = task.Runtime.Node(execution);
            if (node.SubmittedPorts(execution.Id) is not null)
            {
                return Error.Validation("Tool.Argument", "端口产出已提交，无需重复提交。");
            }

            HashSet<PortName> expected = [.. declared.Select(port => port.Name)];
            if (!expected.SetEquals(parsed.Keys))
            {
                return Error.Validation("Tool.Argument", $"端口必须与需交回的端口一一对应，应提交 {string.Join('、', expected)}。");
            }

            if (BlankTextPort(declared, parsed) is { } blank)
            {
                return [ContractErrors.Ports($"端口 {blank} 的产出为空")];
            }

            if (TriggerOnPlainPort(declared, parsed) is { } plain)
            {
                return [ContractErrors.Ports($"端口 {plain} 不是 Trigger 版，不能设置触发值")];
            }

            node.CollectPorts(execution.Id, parsed);

            return ExecutionStatusReader.Of(task, execution);
        }
    }

    /// <summary>交回空文本的 Text 语义端口名，按声明序取第一个；Signal 端口不承载内容，给出的取值不作产出。</summary>
    private static PortName? BlankTextPort(IReadOnlyList<Port> declared, IReadOnlyDictionary<PortName, PortValue> values) =>
        declared
            .Where(port => port.Type.Semantics == PortSemantics.Text
                && string.IsNullOrWhiteSpace(values.GetValueOrDefault(port.Name).Text))
            .Select(port => (PortName?)port.Name)
            .FirstOrDefault();

    /// <summary>普通端口给出不触发值时命中的端口名，按声明序取第一个。普通端口只按触发处理。</summary>
    private static PortName? TriggerOnPlainPort(IReadOnlyList<Port> declared, IReadOnlyDictionary<PortName, PortValue> values) =>
        declared
            .Where(port => !port.Type.IsTrigger && !values.GetValueOrDefault(port.Name).Fires)
            .Select(port => (PortName?)port.Name)
            .FirstOrDefault();
}
