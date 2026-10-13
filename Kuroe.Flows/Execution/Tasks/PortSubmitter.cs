using ErrorOr;
using Kuroe.Flows.Execution.Runs;
using Kuroe.Flows.Shared.Execution;
using Kuroe.Flows.Shared.StaticGraph;
using Kuroe.Flows.Execution.Model;
using Kuroe.Flows.RuntimeNodes;
using ExecutableNode = Kuroe.Flows.Shared.StaticGraph.ExecutableNode;

namespace Kuroe.Flows.Execution.Tasks;

/// <summary>模型侧交回结构化产出的入口：输出端口交命名段。
/// 提交者身份在此认定，结果以文本交回模型，让它在同一轮里改正。</summary>
public sealed class PortSubmitter(TaskRegistry registry)
{
    /// <summary>声明输出端口的执行节点交回命名端口产出，端口必须与声明一一对应。错误以 ErrorOr 表达。</summary>
    public ErrorOr<string> SubmitValues(RunId id, string valuesJson)
    {
        if (Owner(id) is not { } run)
        {
            return Error.Validation("Tool.Argument", "只有进行中的 run 能提交端口产出。");
        }

        ErrorOr<WorkTask> found = registry.Find(run.Context.Task);
        if (found.IsError)
        {
            return Error.Unexpected("Tool.Internal", "该 run 没有归属任务。");
        }

        WorkTask task = found.Value;
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

    /// <summary>提交者身份必须是仍在跑的 run。</summary>
    private Run? Owner(RunId id) =>
        registry.FindRun(id) is { IsError: false } found && found.Value.IsLive
            ? found.Value
            : null;
}
