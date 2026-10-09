using ErrorOr;
using Kuroe.Shared;
using Kuroe.Shared.Executions;

namespace Kuroe.Executions;

/// <summary>执行域的错误构造。</summary>
static class RunErrors
{
    /// <summary>未选择模型，选择指引由调用方给出。</summary>
    public static Error ModelNotSelected() => Error.Validation("Runtime.Model", "当前未选择模型。");

    /// <summary>引用的模型选择未声明使用的模型名。</summary>
    public static Error ModelNotConfigured(string model) =>
        Error.Validation("Node.Model", $"模型选择 {model} 未写 Model，无法确定使用的模型。");

    /// <summary>执行节点没有模型选择，无法确定使用的模型。</summary>
    public static Error ModelMissing(string node) =>
        Error.Validation("Node.Model", $"节点 {node} 没有模型选择，无法确定使用的模型。");

    public static Error RunNotFound(int value) => Error.NotFound(ErrorCodes.RunNotFound, $"没有 run #{value}。");

    /// <summary>该 run 已结束。</summary>
    public static Error RunSettled(RunId run, string action) =>
        Error.Validation("Run.Settled", $"{run} 已结束，不能{action}。");
}
