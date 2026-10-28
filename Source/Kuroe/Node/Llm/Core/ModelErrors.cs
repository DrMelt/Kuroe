using ErrorOr;

namespace Kuroe.Node.Llm;

/// <summary>模型侧的错误构造。</summary>
public static class ModelErrors
{
    /// <summary>未选择模型，选择指引由调用方给出。</summary>
    public static Error ModelNotSelected() => Error.Validation("Runtime.Model", "当前未选择模型。");

    /// <summary>执行节点没有模型静态端口，无法确定使用的模型。</summary>
    public static Error ModelMissing(string node) =>
        Error.Validation("Node.Model", $"节点 {node} 没有静态端口 {LlmNodeTypes.ModelPort.Value}，无法确定使用的模型。");

    /// <summary>模型静态端口没有任务启动值。</summary>
    public static Error ModelNotProvided(string node) =>
        Error.Validation("Node.Model", $"节点 {node} 的静态端口 {LlmNodeTypes.ModelPort.Value} 没有任务启动值，无法确定使用的模型。");
}
