using ErrorOr;
using Kuroe.Flow.Execution.Runs;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.Execution.Submissions;
using Kuroe.Flow.RuntimeNodes;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.ExecutableNode;

namespace Kuroe.Flow.Execution.Tasks;

/// <summary>模型侧交回结构化产出的入口：输出端口交命名段。
/// 提交者身份在此认定，结果以文本交回模型，让它在同一轮里改正。</summary>
public sealed class PortSubmitter(TaskRegistry registry)
{
    /// <summary>声明输出端口的执行节点交回命名端口产出，端口必须与声明一一对应。错误以 ErrorOr 表达。</summary>
    public ErrorOr<string> SubmitValues(RunId id, string valuesJson)
    {
        ErrorOr<(Run Run, WorkTask Task)> owner = SubmitterContext.Resolve(registry, id, "提交端口产出");
        if (owner.IsError)
        {
            return owner.ErrorsOrEmptyList;
        }

        (Run run, WorkTask task) = owner.Value;
        ErrorOr<IReadOnlyDictionary<PortName, string>> parsed = PortValues.Parse(valuesJson);
        if (parsed.IsError)
        {
            return parsed.ErrorsOrEmptyList;
        }

        lock (task.Gate)
        {
            ExecutableNode executable = task.Runtime.Executable(run.Context.NodeIndex).Executable;
            if (!executable.HasTextPorts)
            {
                return Error.Validation("Tool.Argument", "该节点没有声明 Text 输出端口。");
            }

            RuntimeInstance instance = task.Runtime.Instance(run);
            if (instance.Ports(run.Id) is not null)
            {
                return Error.Validation("Tool.Argument", "端口产出已提交，无需重复提交。");
            }

            HashSet<PortName> declared = [.. executable.Outputs.Where(port => port.Type == PortType.Text).Select(port => port.Name)];
            if (!declared.SetEquals(parsed.Value.Keys))
            {
                return Error.Validation("Tool.Argument", $"端口必须与声明一一对应，应提交 {string.Join('、', declared)}。");
            }

            instance.CollectPorts(run.Id, parsed.Value);

            return $"已记录 {declared.Count} 个端口的产出。{ContractText.Outstanding(task, run)}";
        }
    }
}
