using Kuroe.Execution.Shared;
using Kuroe.Node.Llm;

namespace Kuroe.Assistant.Dialogue.Flow;

/// <summary>对话执行的类型目标：Llm 种类、对话参数与上一轮会话快照。</summary>
internal sealed class DialogueFlowTarget(
    LlmParameters parameters,
    string model,
    ExecutionRestoreState? seed) : IExecutionTarget
{
    /// <inheritdoc/>
    public ExecutionTargetId Id => new(DialogueFlow.ReplyLabel);

    /// <inheritdoc/>
    public string Kind => LlmNodeTypes.Kind;

    /// <inheritdoc/>
    public bool UsesDefaultRunner => true;

    /// <inheritdoc/>
    public IExecutionParameters Parameters { get; } = new LlmExecutionParameters(parameters, model, LlmNodeTypes.Kind)
    {
        ConversationSeed = seed,
    };
}