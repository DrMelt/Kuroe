using ErrorOr;
using Kuroe.Shared;
using Kuroe.Shared.Agent;

namespace Kuroe.Agent;

/// <summary>执行域的错误构造。</summary>
static class AgentErrors
{
    /// <summary>未选择模型，选择指引由调用方给出。</summary>
    public static Error ModelNotSelected() => Error.Validation("Agent.Model", "当前未选择模型。");

    public static Error RunNotFound(int value) => Error.NotFound(ErrorCodes.RunNotFound, $"没有 agent #{value}。");

    /// <summary>该 agent 还无可采纳的产出。</summary>
    public static Error NothingToAdopt(RunId run) =>
        Error.Validation("Run.Adopt", $"{run} 还没有可采纳的产出。");

    /// <summary>该 agent 已结束。</summary>
    public static Error RunSettled(RunId run, string action) =>
        Error.Validation("Run.Settled", $"{run} 已结束，不能{action}。");
}
