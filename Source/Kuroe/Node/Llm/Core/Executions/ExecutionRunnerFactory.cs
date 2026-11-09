using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Node.Llm.Sessions;

namespace Kuroe.Node.Llm.Executions;

/// <summary>为 Llm 执行节点建立单次执行的运行器，并校验与建立恢复执行。</summary>
public sealed class ExecutionRunnerFactory(
    SessionFactory sessions,
    IExecutionStatusReader status) : IExecutionRunnerFactory, INodeExecutionRestorer
{
    /// <summary>为一次普通执行建立运行器。</summary>
    public IExecutionRunner Create(ExecutableNode node, ExecutionId execution) =>
        new ExecutionRunner(sessions, status, execution);

    /// <summary>校验恢复数据并装配一次恢复。已上报无法完成的执行不允许恢复。</summary>
    public ErrorOr<PreparedRestore> Prepare(RestorePreparationContext context)
    {
        try
        {
            if (context.Contract.CannotCompleteReason is { } reason)
            {
                return Error.Failure("Llm.Restore", $"执行已上报无法完成：{reason}");
            }

            LlmExecutionState restored = LlmExecutionStateCodec.Decode(context.Payload);
            string? model = (context.Input.Declaration as KuroeNodeDeclaration)?.Model?.Value;
            if (restored.Model != model)
            {
                return Error.Failure(
                    "Llm.Restore",
                    $"恢复数据属于模型 {restored.Model}，当前执行使用 {model ?? "未声明模型"}。");
            }

            if (!LlmExecutionStateCodec.IsRestorable(restored))
            {
                return Error.Failure("Llm.Restore", "恢复数据中的会话历史不能继续。");
            }

            return new PreparedRestore(context.Input, restored);
        }
        catch (Exception ex)
        {
            return Error.Failure("Llm.Restore", $"恢复数据不可读：{ex.Message}");
        }
    }

    /// <summary>按恢复结果建立运行器。</summary>
    public IExecutionRunner CreateRestored(
        ExecutableNode node,
        ExecutionId execution,
        PreparedRestore restore) =>
        restore.Data is LlmExecutionState state
            ? new ExecutionRunner(sessions, status, execution, state)
            : throw new InvalidOperationException("Llm 恢复数据类型不匹配。");
}