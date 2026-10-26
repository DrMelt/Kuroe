using ErrorOr;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Shared;

namespace Kuroe.Workflows.Tasks;

/// <summary>Kuroe 的宿主开始输入：把流程 HostStartInputs 的输入名解析为工作区与任务目录，并为用户任务准备任务目录。</summary>
/// <param name="workspaceDirectory">工作区根目录的绝对路径。</param>
/// <param name="taskDirectory">任务目录的根，任务域没有任务目录时为空。</param>
public sealed class KuroeTaskStart(string workspaceDirectory, TaskDirectory? taskDirectory) : IHostTaskStart
{
    /// <summary>校验绑定后准备任务目录并给出每个绑定端口的值。未知输入名与当前任务域不支持的任务目录在准备前拒绝。</summary>
    public ErrorOr<HostStartValues> Prepare(
        TaskId id,
        IReadOnlyDictionary<PortName, string> bindings,
        FlowName flow,
        string title)
    {
        foreach (string input in bindings.Values)
        {
            if (input is not (StartSources.Workspace or StartSources.Task))
            {
                return [Error.Validation(ErrorCodes.TaskStartInput, $"未知的宿主开始输入 {input}。")];
            }

            if (input == StartSources.Task && taskDirectory is null)
            {
                return [Error.Validation(ErrorCodes.TaskStartInput, "当前任务域没有任务目录，不能提供 TASK_WORK_PATH。")];
            }
        }

        string? taskPath = null;
        if (taskDirectory is { } directory)
        {
            ErrorOr<string> prepared = directory.Prepare(id, flow, title);
            if (prepared.IsError)
            {
                return prepared.ErrorsOrEmptyList;
            }

            taskPath = prepared.Value;
        }

        Dictionary<PortName, string> values = [];
        foreach ((PortName port, string input) in bindings)
        {
            switch (input)
            {
                case StartSources.Workspace:
                    values[port] = workspaceDirectory;
                    break;

                case StartSources.Task:
                    values[port] = taskPath!;
                    break;
            }
        }

        return new HostStartValues(values);
    }
}