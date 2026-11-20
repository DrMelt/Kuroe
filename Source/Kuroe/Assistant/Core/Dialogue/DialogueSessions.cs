using ErrorOr;
using Kuroe.Assistant.Dialogue.Flow;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Kernel.Control.Shared;
using Kuroe.Runner;
using Kuroe.Kernel.Tasks.Presentation.Shared;
using Kuroe.Kernel.Tasks.Runtime;
using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Kernel.Execution.Shared.State;

namespace Kuroe.Assistant.Dialogue;

/// <summary>内置助手的会话目录：建立、选择、改名与删除会话，并给出会话摘要。</summary>
internal sealed class DialogueSessions(
    TaskActions actions,
    TaskRegistry registry,
    ModelService models,
    SettingsProvider settings,
    FlowProvider flow,
    DialogueTurns turns)
{
    private const string NewTitle = "新对话";

    private readonly Lock _gate = new();
    private TaskId? _active;

    /// <summary>当前会话标识，没有当前会话时为空。</summary>
    internal TaskId? ActiveId
    {
        get
        {
            lock (_gate)
            {
                return _active;
            }
        }
    }

    /// <summary>当前会话摘要，没有当前会话时为空。</summary>
    internal DialogueSessionInfo? Active =>
        ActiveId is { } id && Info(id) is { IsError: false } info ? info.Value : null;

    /// <summary>全部会话，按最近活动排序。</summary>
    internal IReadOnlyList<DialogueSessionInfo> List()
    {
        List<DialogueSessionInfo> sessions = [];
        foreach (TaskSnapshot snapshot in registry.Snapshots())
        {
            if (Info(snapshot.Id) is { IsError: false } info)
            {
                sessions.Add(info.Value);
            }
        }

        return
        [
            .. sessions
                .OrderByDescending(session => session.LastActivityAt)
                .ThenBy(session => session.CreatedAt),
        ];
    }

    /// <summary>新建会话。模型与语言取当前全局配置，标题与模式可由调用方给出。</summary>
    internal ErrorOr<DialogueSessionInfo> Create(string? title = null, DialogueMode? mode = null)
    {
        if (models.Current is not { } model)
        {
            return ModelErrors.ModelNotSelected();
        }

        DialogueMode selected = mode ?? DialogueMode.Default;
        StartInputs inputs = new(
        [
            new StartInput(DialogueFlow.StartPort, "对话"),
            new StartInput(DialogueFlow.ModePort, selected.Key),
            new StartInput(DialogueFlow.ModelPort, model.Value),
            new StartInput(DialogueFlow.LanguagePort, settings.AssistantLanguage),
        ]);

        ErrorOr<TaskSnapshot> created = actions.Submit(
            flow,
            DialogueFlow.Name,
            inputs,
            string.IsNullOrWhiteSpace(title) ? NewTitle : title);
        if (created.IsError)
        {
            return created.ErrorsOrEmptyList;
        }

        lock (_gate)
        {
            _active = created.Value.Id;
        }

        return Info(created.Value.Id);
    }

    /// <summary>选择当前会话。</summary>
    internal ErrorOr<Success> Select(TaskId id)
    {
        ErrorOr<WorkTask> found = registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        lock (_gate)
        {
            _active = id;
        }

        return Result.Success;
    }

    /// <summary>选择最近活动的会话，没有会话时保持当前为空。</summary>
    internal void SelectLatest()
    {
        IReadOnlyList<DialogueSessionInfo> sessions = List();
        if (sessions.Count > 0)
        {
            Select(sessions[0].Id);
        }
    }

    /// <summary>切换指定会话的模式。下一轮执行按新模式建立工具面，不重建任务。</summary>
    internal ErrorOr<Success> SelectMode(TaskId id, DialogueMode mode)
    {
        ErrorOr<WorkTask> found = registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        WorkTask task = found.Value;
        task.UnderLock(() =>
        {
            DialogueFlowState.Of(task).SelectMode(mode);

            return true;
        });
        ((ITaskFlowContext)task).NotifyChanged();

        return Result.Success;
    }

    /// <summary>改会话标题。</summary>
    internal ErrorOr<Success> Rename(TaskId id, string title) => actions.Rename(id, title);

    /// <summary>标题仍是新建标题时，用首条输入作为会话标题。</summary>
    internal void NameFromInput(WorkTask task, string input)
    {
        if (task.Title == NewTitle)
        {
            actions.Rename(task.Id, TaskTitle.Shorten(input));
        }
    }

    /// <summary>取会话任务。</summary>
    internal ErrorOr<WorkTask> Find(TaskId id) => registry.Find(id);

    /// <summary>删除会话。运行中的任务先停止，等执行收口后移除任务、存档与租约，再选择最近的会话。</summary>
    internal async Task<ErrorOr<Success>> DeleteAsync(TaskId id, CancellationToken cancellationToken)
    {
        ErrorOr<WorkTask> found = registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        WorkTask task = found.Value;
        if (turns.IsBusy(id))
        {
            ErrorOr<Success> stopped = actions.Terminate(id);
            if (stopped.IsError)
            {
                return stopped;
            }

            if (!await turns.WaitIdleAsync(task, cancellationToken).ConfigureAwait(false))
            {
                return [DialogueErrors.DeleteBusy(id)];
            }
        }
        else if (!task.State.IsSettled())
        {
            ErrorOr<Success> stopped = actions.Terminate(id);
            if (stopped.IsError)
            {
                return stopped;
            }
        }

        ErrorOr<Success> dropped = actions.Drop(id);
        if (dropped.IsError)
        {
            if (registry.Find(id).IsError)
            {
                ReleaseActive(id);
            }

            return dropped;
        }

        ReleaseActive(id);

        return Result.Success;
    }

    /// <summary>取会话摘要。</summary>
    internal ErrorOr<DialogueSessionInfo> Info(TaskId id)
    {
        ErrorOr<WorkTask> found = registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        return Info(found.Value);
    }

    private DialogueSessionInfo Info(WorkTask task)
    {
        bool running = turns.IsRunning(task.Id);

        return task.UnderLock(() =>
        {
            DialogueFlowState state = DialogueFlowState.Of(task);

            return new DialogueSessionInfo(
                task.Id,
                task.Title,
                state.Model,
                state.Language,
                state.Mode,
                task.State,
                running || task.Executions.Any(execution => execution.IsLive),
                task.Executions.Count,
                task.CreatedAt,
                task.LastActivityAt);
        });
    }

    /// <summary>当前会话被删除后让出，并把最近的会话设为当前。</summary>
    private void ReleaseActive(TaskId id)
    {
        lock (_gate)
        {
            if (_active != id)
            {
                return;
            }

            _active = null;
        }

        SelectLatest();
    }
}