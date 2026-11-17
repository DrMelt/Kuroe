using Kuroe.Node.Llm;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Restore;

namespace Kuroe.Assistant.Dialogue.Flow;

/// <summary>对话执行的类型目标：Llm 执行器工厂类型、对话参数与上一轮会话快照。</summary>
internal sealed class DialogueFlowTarget(
    LlmParameters parameters,
    string model,
    ExecutionRestoreState? seed) : IExecutionTarget
{
    /// <inheritdoc/>
    public ExecutionTargetId Id => new(DialogueFlow.ReplyLabel);

    /// <inheritdoc/>
    public Type RunnerFactoryType => LlmNodeTypes.RunnerFactoryType;

    /// <inheritdoc/>
    public IExecutionParameters Parameters { get; } = parameters.ToExecutionParameters(model, seed);
}