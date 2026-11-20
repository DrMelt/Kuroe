using ErrorOr;
using Kuroe.Runner.Llm.Sessions;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.Restore;
using Kuroe.Kernel.Execution.Shared.Runtime;

namespace Kuroe.Runner.Llm.Executions;

/// <summary>为 Llm 执行节点建立单次执行的运行器，并校验与建立恢复执行。</summary>
public sealed class LlmExecutionRunnerFactory(
    SessionFactory sessions) : IExecutionRunnerFactory, IExecutionRestorer
{
    /// <summary>为一次普通执行建立运行器。</summary>
    public IExecutionRunner Create(
        IExecutionParameters parameters,
        IExecutionRequestEntry requests) =>
        parameters is LlmExecutionParameters llm
            ? new LlmExecutionRunner(sessions, requests, llm)
            : throw new InvalidOperationException("Llm 执行节点的参数类型不匹配。");

    /// <summary>校验恢复负载的格式与模型并装配一次恢复。历史可继续性在建立恢复会话时校验。已上报无法完成的执行不允许恢复。</summary>
    public ErrorOr<IExecutionRestoreData> Prepare(ExecutionRestoreContext context)
    {
        try
        {
            if (context.Contract.CannotCompleteReason is { } reason)
            {
                return Error.Failure("Llm.Restore", $"执行已上报无法完成：{reason}");
            }

            LlmAgentSessionPayload restored = AgentSessionCodec.Decode(context.Payload);
            string? model = (context.Invocation.Parameters as LlmExecutionParameters)?.Model;
            if (restored.Model != model)
            {
                return Error.Failure(
                    "Llm.Restore",
                    $"恢复数据属于模型 {restored.Model}，当前执行使用 {model ?? "未声明模型"}。");
            }

            return restored;
        }
        catch (Exception ex)
        {
            return Error.Failure("Llm.Restore", $"恢复数据不可读：{ex.Message}");
        }
    }

    /// <summary>按恢复结果建立运行器。</summary>
    public IExecutionRunner CreateRestored(
        ExecutionRestoreContext context,
        IExecutionRestoreData data,
        IExecutionRequestEntry requests) =>
        data is LlmAgentSessionPayload state && context.Invocation.Parameters is LlmExecutionParameters parameters
            ? new LlmExecutionRunner(sessions, requests, parameters, state)
            : throw new InvalidOperationException("Llm 恢复数据类型不匹配。");
}