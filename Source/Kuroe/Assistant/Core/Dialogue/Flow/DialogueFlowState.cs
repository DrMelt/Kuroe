using Kuroe.CodeFlow.Records.Shared;
using Kuroe.Kernel.Control.Shared;
using Kuroe.Node.Llm;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Assistant.Dialogue.Flow;

/// <summary>对话流程状态：开始输入、工作区、模型、语言与模式。</summary>
internal sealed class DialogueFlowState(
    StartInputs inputs,
    string workspace,
    string model,
    string language,
    DialogueMode mode) : IFlowState
{
    /// <inheritdoc/>
    public StartInputs Inputs { get; } = inputs;

    /// <summary>工作区根目录。</summary>
    internal string Workspace { get; } = workspace;

    /// <summary>执行使用的模型名。</summary>
    internal string Model { get; } = model;

    /// <summary>回复语言。</summary>
    internal string Language { get; } = language;

    /// <summary>当前模式。</summary>
    internal DialogueMode Mode { get; private set; } = mode;

    /// <summary>开始端口文本。</summary>
    internal FlowTexts StartTexts { get; } = new(
        inputs.Values.ToDictionary(input => input.Port, input => input.Text));

    /// <summary>回话执行参数。</summary>
    internal LlmParameters Parameters => DialogueFlow.Parameters(Mode, Language);

    /// <summary>按任务取对话流程状态。</summary>
    internal static DialogueFlowState Of(ITaskFlowView task) =>
        task.FlowState as DialogueFlowState
        ?? throw new InvalidOperationException("任务不是对话流程。");

    /// <summary>从存档状态重建流程状态，调用方给出缺失项的取值。</summary>
    internal static DialogueFlowState From(
        CodeFlowStateRecord saved,
        string language,
        string model,
        string workspace) =>
        new(saved.Inputs, workspace, model, language, DialogueMode.Parse(saved.Values.GetValueOrDefault(DialogueFlow.ModeKey)));

    /// <summary>切换模式，下一轮执行按新模式建立工具面。</summary>
    internal void SelectMode(DialogueMode mode) => Mode = mode;

    /// <summary>流程状态记录：开始状态、开始端口文本与存档值。</summary>
    internal CodeFlowStateRecord ToRecord() => new(Inputs, StartTexts, Values());

    /// <summary>存档里的状态值。</summary>
    private Dictionary<string, string> Values() =>
        new()
        {
            [DialogueFlow.WorkspaceKey] = Workspace,
            [DialogueFlow.ModelKey] = Model,
            [DialogueFlow.LanguageKey] = Language,
            [DialogueFlow.ModeKey] = Mode.Key,
        };
}