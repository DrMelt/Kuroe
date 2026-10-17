using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Turns;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Shared.Workflows;
using Kuroe.Flow.Execution.Tasks;
using Kuroe.Flow.InputChannel;

namespace Kuroe.Workflows.Execution;

/// <summary>常驻对话任务的管理层宿主：持有对话任务，普通输入路由为回答并等待回复，无模型时返回固定引导。</summary>
public sealed class DialogueHost(TaskService tasks, DialogueRound rounds, ModelService models, TaskRegistry registry, IInputChannel inputs)
{
    private readonly Lock _gate = new();
    private TaskId? _dialogue;

    /// <summary>无模型时对所有输入的固定引导。</summary>
    public const string NoModelGuidance = "还没有可用的模型，先 /model 查看并选择。";

    /// <summary>一轮对话。无模型时返回固定引导，不触碰任务。过程增量写给 observer。</summary>
    public async Task<ErrorOr<DialogueReply>> ReplyAsync(
        string input,
        ITurnSink? observer,
        CancellationToken cancellationToken)
    {
        if (models.Current is null)
        {
            return new DialogueReply(NoModelGuidance);
        }

        TaskId id = EnsureDialogue();

        ErrorOr<string> reply = await rounds.AskAsync(id, null, input, observer, cancellationToken);

        return reply.IsError ? reply.ErrorsOrEmptyList : new DialogueReply(reply.Value);
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

    /// <summary>当前对话任务，不存在、已被清理或已收口时新建。</summary>
    private TaskId EnsureDialogue()
    {
        lock (_gate)
        {
            if (_dialogue is { } current && IsUsable(current))
            {
                return current;
            }
        }

        ErrorOr<TaskSnapshot> created = tasks.SubmitFlow("对话", Kuroe.Workflows.Flows.DialogueFlow.Build(inputs), "对话");
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
