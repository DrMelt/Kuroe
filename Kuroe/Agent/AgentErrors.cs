using ErrorOr;

namespace Kuroe.Agent;

/// <summary>Agent 域的错误构造。</summary>
public static class AgentErrors
{
    /// <summary>设置节反序列化失败。</summary>
    public static Error Bind(string message) => Error.Validation($"{AgentSettings.SectionName}.Bind", message);

    /// <summary>未选择模型，选择指引由调用方给出。</summary>
    public static Error ModelNotSelected() => Error.Validation(
        $"{AgentSettings.SectionName}.{nameof(AgentSettings.Model)}",
        "当前未选择模型。");
}