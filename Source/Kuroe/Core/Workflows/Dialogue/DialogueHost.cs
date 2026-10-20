using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Node.Llm;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.InputChannel;

namespace Kuroe.Workflows.Dialogue;

/// <summary>常驻对话任务的管理层宿主：持有对话任务，普通输入路由为回答并等本轮收口，无模型时报模型未选择。</summary>
public sealed class DialogueHost(
    TaskService tasks,
    DialogueRound rounds,
    ModelService models,
    SettingsProvider settings,
    TaskRegistry registry,
    IInputChannel inputs)
{
    private readonly Lock _gate = new();
    private TaskId? _dialogue;

    /// <summary>一轮对话。无模型时返回模型未选择，不触碰任务。过程增量写给 observer。</summary>
    public async Task<ErrorOr<Success>> ReplyAsync(
        string input,
        IProcessSink? observer,
        CancellationToken cancellationToken)
    {
        if (models.Current is null)
        {
            return ModelErrors.ModelNotSelected();
        }

        TaskId id = EnsureDialogue();

        return await rounds.AskAsync(id, null, input, observer, cancellationToken);
    }

    /// <summary>重置对话：停止并丢弃当前对话任务，下一次输入重建。</summary>
    public void Reset()
    {
        TaskId? old;
        lock (_gate)
        {
            old = _dialogue;
            _dialogue = null;
        }

        if (old is { } id && registry.Find(id).IsSuccess)
        {
            tasks.StopTask(id);
        }
    }

    /// <summary>当前对话任务，不存在、已被清理或已收口时新建。助手语言在创建时读取，配置变化后用 /reset 重建任务生效。</summary>
    private TaskId EnsureDialogue()
    {
        lock (_gate)
        {
            if (_dialogue is { } current && IsUsable(current))
            {
                return current;
            }
        }

        ErrorOr<TaskSnapshot> created = tasks.SubmitFlow(
            "对话", Kuroe.Workflows.Flows.DialogueFlow.Build(inputs, settings.Current.Assistant.Language), "对话");
        if (created.IsError)
        {
            throw new InvalidOperationException(string.Join("；", created.ErrorsOrEmptyList.Select(error => error.Description)));
        }

        TaskId id = created.Value.Id;
        lock (_gate)
        {
            _dialogue = id;
        }

        return id;
    }

    /// <summary>当前任务是否还承担着对话：丢失、取消或收口后不再使用。</summary>
    private bool IsUsable(TaskId id)
    {
        ErrorOr<WorkTask> found = registry.Find(id);
        if (found.IsError)
        {
            return false;
        }

        lock (found.Value.Gate)
        {
            return found.Value.State is TaskState.Running;
        }
    }
}
