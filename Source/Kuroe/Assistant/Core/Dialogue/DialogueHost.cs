using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Assistant.Flows;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Node.Llm;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Compilation.Shared;

namespace Kuroe.Assistant.Dialogue;

/// <summary>常驻对话任务的管理层宿主：持有对话任务，普通输入路由为回答并等本轮收口，无模型时报模型未选择。
/// 模型在任务启动时写入流程，模型变化后下一次输入重建任务。</summary>
public sealed class DialogueHost(
    TaskActions actions,
    DialogueRound rounds,
    ModelService models,
    SettingsProvider settings,
    TaskRegistry registry,
    IFlowDescriptionCompiler compiler)
{
    private readonly Lock _gate = new();
    private TaskId? _dialogue;
    private ModelName? _model;

    /// <summary>一轮对话。无模型时返回模型未选择，不触碰任务。过程增量与过程条目经回调送出。</summary>
    public async Task<ErrorOr<Success>> ReplyAsync(
        string input,
        Action<string>? onText = null,
        Action<JournalEntry>? onEntry = null)
    {
        if (models.Current is not { } model)
        {
            return ModelErrors.ModelNotSelected();
        }

        TaskId id = EnsureDialogue(model);

        return await rounds.AskAsync(id, null, input, onText, onEntry);
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

    /// <summary>当前对话任务，不存在、已被清理、已收口或模型已变化时新建。助手语言在创建时读取，配置变化后重置对话重建任务生效。</summary>
    private TaskId EnsureDialogue(ModelName model)
    {
        TaskId? replaced = null;
        lock (_gate)
        {
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

        // 被替换的旧任务先停止，转为已取消后随 ClearSettled 清出任务域
        if (replaced is { } staleTask)
        {
            actions.StopTask(staleTask);
        }

        registry.ClearSettled();

        var document = DialogueFlow.Build(settings.AssistantLanguage);
        ErrorOr<CompiledDocument> compiled = compiler.Compile(document, []);
        if (compiled.IsError)
        {
            throw new InvalidOperationException(string.Join("；", compiled.ErrorsOrEmptyList.Select(error => error.Description)));
        }

        if (compiled.Value.Flows.Count != 1)
        {
            throw new InvalidOperationException("提交的文档必须恰好包含一条流程。");
        }

        ErrorOr<TaskSnapshot> created = actions.SubmitCompiled(
            StartInputs.Single(DialogueFlow.StartPort, "对话"), compiled.Value.Flows[0], "对话");
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
