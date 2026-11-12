using Kuroe.Flow.Control;

namespace Kuroe.Assistant.Dialogue;

/// <summary>对话流程状态的读取与存档值转换。</summary>
internal static class DialogueFlowAccess
{
    private const string WorkspaceKey = "workspace";
    private const string ModelKey = "model";
    private const string LanguageKey = "language";

    internal static DialogueFlowState State(ITaskFlowContext task) =>
        task.FlowState as DialogueFlowState
        ?? throw new InvalidOperationException("任务不是对话流程。");

    internal static IReadOnlyDictionary<string, string> Values(string language) =>
        new Dictionary<string, string> { [LanguageKey] = language };

    internal static IReadOnlyDictionary<string, string> Values(DialogueFlowState state) =>
        new Dictionary<string, string>
        {
            [WorkspaceKey] = state.Workspace,
            [ModelKey] = state.Model,
            [LanguageKey] = state.Language,
        };

    internal static string Language(IReadOnlyDictionary<string, string> values, string fallback) =>
        values.GetValueOrDefault(LanguageKey) ?? fallback;

    internal static string Model(IReadOnlyDictionary<string, string> values, string fallback) =>
        values.GetValueOrDefault(ModelKey) ?? fallback;

    internal static string Workspace(IReadOnlyDictionary<string, string> values, string fallback) =>
        values.GetValueOrDefault(WorkspaceKey) ?? fallback;
}