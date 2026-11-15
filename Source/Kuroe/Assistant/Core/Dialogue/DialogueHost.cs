using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Assistant.Flows;
using Kuroe.Catalogs;
using Kuroe.Control.Shared;
using Kuroe.Tasks.Presentation.Shared;
using Kuroe.Tasks.Shared;
using Kuroe.Node.Llm;
using Kuroe.Tasks.Runtime;

namespace Kuroe.Assistant.Dialogue;

/// <summary>常驻对话任务的管理层宿主：持有对话任务，普通输入路由为回答并等本轮收口，无模型时报模型未选择。
/// 模型在任务启动时写入流程，模型变化后下一次输入重建任务。</summary>
public sealed class DialogueHost(
    TaskActions actions,
    FlowProvider flow,
    DialogueRound rounds,
    ModelService models,
    TaskRegistry registry)
{
    private readonly Lock _gate = new();
    private TaskId? _dialogue;
    private ModelName? _model;
    private DialogueMode _mode = DialogueMode.Default;

    /// <summary>当前选择的模式。模式变化后下一轮执行按新模式建立工具面。</summary>
    public DialogueMode Mode
    {
        get
        {
            lock (_gate)
            {
                return _mode;
            }
        }
    }

    /// <summary>选择模式。已有对话任务时更新流程状态，不重建任务、不清空历史。</summary>
    public void SelectMode(DialogueMode mode)
    {
        TaskId? current;
        lock (_gate)
        {
            _mode = mode;
            current = _dialogue;
        }

        if (current is { } id && registry.Find(id) is { IsError: false } found)
        {
            WorkTask task = found.Value;
            task.UnderLock(() =>
            {
                DialogueFlowAccess.State(task).SelectMode(mode);
                return true;
            });
            ((ITaskFlowContext)task).NotifyChanged();
        }
    }

    /// <summary>一轮对话。无模型时返回模型未选择，不触碰任务。过程事件经 observer 送出。</summary>
    public async Task<ErrorOr<Success>> ReplyAsync(
        string input,
        ProcessObserver? observer = null)
    {
        if (models.Current is not { } model)
        {
            return ModelErrors.ModelNotSelected();
        }

        TaskId id = EnsureDialogue(model);

        return await rounds.AskAsync(id, null, input, observer);
    }

    /// <summary>重置对话：停止并丢弃当前对话任务，下一次输入重建。</summary>
    public void Reset()
    {
        TaskId? old;
        lock (_gate)
        {
            old = _dialogue;
            _dialogue = null;
            _model = null;
        }

        if (old is { } id)
        {
            actions.StopTask(id);
        }

        registry.ClearSettled();
    }

    /// <summary>当前对话任务，不存在、已被清理、已收口或模型已变化时新建。助手语言与模式在创建时读取，配置变化后重置对话重建任务生效。</summary>
    private TaskId EnsureDialogue(ModelName model)
    {
        TaskId? replaced = null;
        DialogueMode mode;
        lock (_gate)
        {
            mode = _mode;
            if (_dialogue is { } current)
            {
                if (_model == model && IsUsable(current))
                {
                    return current;
                }

                replaced = current;
                _dialogue = null;
                _model = null;
            }
        }

        // 被替换的旧任务先终止，随 ClearSettled 清出任务域
        if (replaced is { } staleTask)
        {
            actions.StopTask(staleTask);
        }

        registry.ClearSettled();

        StartInputs inputs = new(
        [
            new StartInput(DialogueFlow.StartPort, "对话"),
            new StartInput(DialogueFlow.ModePort, mode.Key),
        ]);
        ErrorOr<TaskSnapshot> created = actions.Submit(flow, DialogueFlow.Name, inputs, "对话");
        if (created.IsError)
        {
            throw new InvalidOperationException(string.Join("；", created.ErrorsOrEmptyList.Select(error => error.Description)));
        }

        TaskId id = created.Value.Id;
        lock (_gate)
        {
            _dialogue = id;
            _model = model;
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

        return found.Value.UnderLock(() => found.Value.State is TaskState.Running);
    }
}
