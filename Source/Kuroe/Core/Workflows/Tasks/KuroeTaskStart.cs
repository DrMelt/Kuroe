using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Node.Llm;
using Kuroe.Shared;

namespace Kuroe.Workflows.Tasks;

/// <summary>Kuroe 的宿主开始输入：把表项里的宿主输入名解析为工作区、任务目录与当前模型，
/// 并为用户任务准备任务目录。表项的键是开始端口名，由宿主的开始输入表给出。</summary>
/// <param name="workspaceDirectory">工作区根目录的绝对路径。</param>
/// <param name="taskDirectory">任务目录的根，任务域没有任务目录时为空。</param>
/// <param name="models">当前模型选择，模型宿主输入按它给值。</param>
public sealed class KuroeTaskStart(
    string workspaceDirectory,
    TaskDirectory? taskDirectory,
    ModelService models) : IHostTaskStart
{
    /// <summary>准备任务目录并给出每个绑定端口的值。缺少模型选择与当前任务域不支持的任务目录在准备前拒绝。</summary>
    public ErrorOr<IPortTexts> Prepare(
        TaskId id,
        IReadOnlyDictionary<PortName, string> bindings)
    {
        ModelName? model = models.Current;
        foreach (string input in bindings.Values)
        {
            if (input == StartSources.Task && taskDirectory is null)
            {
                return [Error.Validation(ErrorCodes.TaskStartInput, "当前任务域没有任务目录，不能提供 TASK_WORK_PATH。")];
            }

            if (input == StartSources.Model && model is null)
            {
                return [ModelErrors.ModelNotSelected()];
            }

            if (input is not (StartSources.Workspace or StartSources.Task or StartSources.Model))
            {
                return [Error.Validation(ErrorCodes.TaskStartInput, $"未知的宿主开始输入 {input}。")];
            }
        }

        string? taskPath = null;
        if (taskDirectory is { } directory)
        {
            ErrorOr<string> prepared = directory.Prepare(id);
            if (prepared.IsError)
            {
                return prepared.ErrorsOrEmptyList;
            }

            taskPath = prepared.Value;
        }

        Dictionary<PortName, string> values = [];
        foreach ((PortName port, string input) in bindings)
        {
            string? value = input switch
            {
                StartSources.Workspace => workspaceDirectory,
                StartSources.Task => taskPath,
                StartSources.Model => model?.Value,
                _ => null,
            };

            if (value is null)
            {
                return [Error.Validation(ErrorCodes.TaskStartInput, $"宿主开始输入 {input} 没有可用的值。")];
            }

            values[port] = value;
        }

        return new PortTexts(values);
    }
}