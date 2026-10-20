using ErrorOr;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.Runtime.Submissions;
using Kuroe.Flow.RuntimeNodes;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>模型侧交回结构化产出的入口：输出端口交端口名到文本的命名段，Text 端口给内容，Signal 端口只给端口名。
/// 提交者身份在此认定，结果以提交后的本轮契约状态交回，回执措辞由工具实现层给出。</summary>
public sealed class PortSubmitter(TaskRegistry registry)
{
    /// <summary>需交回输出端口的执行节点交回端口产出，端口必须与声明一一对应。成功时给出提交后的本轮契约状态，错误以 ErrorOr 表达。</summary>
    public ErrorOr<ExecutionStatus> SubmitValues(ExecutionId id, string valuesJson)
    {
        ErrorOr<(Execution Execution, WorkTask Task)> owner = SubmitterContext.Resolve(registry, id, "提交端口产出");
        if (owner.IsError)
        {
            return owner.ErrorsOrEmptyList;
        }

        (Execution execution, WorkTask task) = owner.Value;
        ErrorOr<IReadOnlyDictionary<PortName, string>> parsed = PortValues.Parse(valuesJson);
        if (parsed.IsError)
        {
            return parsed.ErrorsOrEmptyList;
        }

        lock (task.Gate)
        {
            IReadOnlyList<Port> declared = task.Runtime.Executable(execution.Input.Node).Executable.RequiredPorts;
            if (declared.Count == 0)
            {
                return Error.Validation("Tool.Argument", "该节点没有需要交回的输出端口。");
            }

            RuntimeExecutable node = task.Runtime.Node(execution);
            if (node.Ports(execution.Id) is not null)
            {
                return Error.Validation("Tool.Argument", "端口产出已提交，无需重复提交。");
            }

            HashSet<PortName> expected = [.. declared.Select(port => port.Name)];
            if (!expected.SetEquals(parsed.Value.Keys))
            {
                return Error.Validation("Tool.Argument", $"端口必须与声明一一对应，应提交 {string.Join('、', expected)}。");
            }

            if (BlankTextPort(declared, parsed.Value) is { } blank)
            {
                return [TaskErrors.Ports($"端口 {blank} 的产出为空")];
            }

            node.CollectPorts(execution.Id, parsed.Value);

            return ExecutionStatusReader.Of(task, execution);
        }
    }

    /// <summary>交回空文本的 Text 端口名，按声明序取第一个；Signal 端口不承载内容，给出的取值不作产出。</summary>
    private static PortName? BlankTextPort(IReadOnlyList<Port> declared, IReadOnlyDictionary<PortName, string> values) =>
        declared
            .Where(port => port.Type == PortType.Text && string.IsNullOrWhiteSpace(values.GetValueOrDefault(port.Name)))
            .Select(port => (PortName?)port.Name)
            .FirstOrDefault();
}
