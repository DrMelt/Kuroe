using ErrorOr;

namespace Kuroe.Flow.Execution;

/// <summary>按执行种类选择恢复器。通配恢复器在没有同种类恢复器时使用。</summary>
public sealed class ExecutionRestorerRegistry(IReadOnlyList<IExecutionRestorer>? restorers = null)
{
    /// <summary>校验恢复负载并装配一次恢复。没有匹配的恢复器或负载不能恢复时返回错误。</summary>
    public ErrorOr<PreparedExecutionRestore> Prepare(ExecutionRestoreContext context)
    {
        if (Find(context.Request.Kind) is not { } restorer)
        {
            return Error.Failure("Execution.Restore", $"执行种类 {context.Request.Kind} 不支持恢复。");
        }

        ErrorOr<IExecutionRestoreData> data = restorer.Prepare(context);
        return data.IsError
            ? data.ErrorsOrEmptyList
            : new PreparedExecutionRestore(context, data.Value);
    }

    /// <summary>按恢复结果建立运行器。</summary>
    public IExecutionRunner CreateRestored(ExecutionId execution, PreparedExecutionRestore prepared)
    {
        IExecutionRestorer restorer = Find(prepared.Context.Request.Kind)
            ?? throw new InvalidOperationException($"执行种类 {prepared.Context.Request.Kind} 不支持恢复。");

        return restorer.CreateRestored(prepared.Context with { Execution = execution }, prepared.Data);
    }

    private IExecutionRestorer? Find(string kind) =>
        restorers?.FirstOrDefault(candidate => candidate.Kind == kind)
        ?? restorers?.FirstOrDefault(candidate => candidate.Kind == "*");
}