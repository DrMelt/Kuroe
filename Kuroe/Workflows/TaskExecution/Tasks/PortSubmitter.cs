using ErrorOr;
using Kuroe.Executions.Runs;
using Kuroe.Executions.Turns;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Graph;
using ExecutableNode = Kuroe.Shared.Workflows.Graph.ExecutableNode;

namespace Kuroe.Workflows.TaskExecution.Tasks;

/// <summary>模型侧交回结构化产出的入口：输出端口交命名段。
/// 提交者身份在此认定，结果以文本交回模型，让它在同一轮里改正。</summary>
public sealed class PortSubmitter(TaskRegistry registry)
{
    /// <summary>声明输出端口的执行节点交回命名端口产出，端口必须与声明一一对应。错误以 ErrorOr 表达。</summary>
    public ErrorOr<string> SubmitValues(TurnScope? scope, string valuesJson)
    {
        if (Owner(scope) is not { } run)
        {
            return ToolErrors.Argument("只有进行中的文本 run 能提交端口产出。");
        }

        if (run.Context.Output != NodeOutput.Text)
        {
            return ToolErrors.Argument("端口产出只属于文本执行。");
        }

        ErrorOr<WorkTask> found = registry.Find(run.Context.Task);
        if (found.IsError)
        {
            return ToolErrors.Internal("该 run 没有归属任务。");
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
            if (!executable.HasOutputPorts)
            {
                return ToolErrors.Argument("该节点没有声明输出端口。");
            }

            if (task.PortValuesFor(run.Context.NodeIndex, run.Context.ItemIndex) is not null)
            {
                return ToolErrors.Argument("端口产出已提交，无需重复提交。");
            }

            HashSet<PortName> declared = [.. executable.Outputs];
            if (!declared.SetEquals(parsed.Value.Keys))
            {
                return ToolErrors.Argument($"端口必须与声明一一对应，应提交 {string.Join('、', declared)}。");
            }

            task.SetPortValues(run.Context.NodeIndex, run.Context.ItemIndex, parsed.Value);

            return $"已记录 {declared.Count} 个端口的产出。";
        }
    }

    /// <summary>提交者必须绑定了执行回合，且是该契约仍在跑的 run。</summary>
    private Run? Owner(TurnScope? scope) =>
        scope is { } s
        && registry.FindRun(s.Run) is { IsError: false } found
        && found.Value.IsLive
            ? found.Value
            : null;
}
