using ErrorOr;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Restore;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Execution.Registry;

/// <summary>执行装配：按执行运行器键索引工厂，组合执行目标与工厂，并准备恢复。</summary>
public sealed class ExecutionInvocationFactory(
    IReadOnlyList<IExecutionRunnerFactory>? factories = null) : IExecutionInvocationFactory
{
    private readonly IReadOnlyDictionary<ExecutionRunnerKey, IExecutionRunnerFactory> _factories =
        Index(factories ?? []);

    /// <summary>按运行器键查找执行器工厂，没有注册时为空。</summary>
    public IExecutionRunnerFactory? Find(ExecutionRunnerKey key) =>
        _factories.GetValueOrDefault(key);

    /// <inheritdoc/>
    public ErrorOr<ExecutionInvocation> Create(ExecutionTarget target) =>
        Find(target.RunnerKey) is { } factory
            ? new ExecutionInvocation(target, factory)
            : Error.Failure("Execution.Assembly", $"执行器工厂 {target.RunnerKey} 没有注册。");

    /// <inheritdoc/>
    public ErrorOr<PreparedExecutionRestore> PrepareRestore(ExecutionRestoreContext context)
    {
        if (context.Target.Id != context.Request.Target)
        {
            return Error.Failure(
                "Execution.Assembly",
                $"执行目标 {context.Target.Id} 与请求目标 {context.Request.Target} 不一致。");
        }

        if (Find(context.Target.RunnerKey) is not { } factory)
        {
            return Error.Failure(
                "Execution.Assembly",
                $"执行器工厂 {context.Target.RunnerKey} 没有注册。");
        }

        if (factory is not IExecutionRestorer restorer)
        {
            return Error.Failure(
                "Execution.Assembly",
                $"执行器工厂 {context.Target.RunnerKey} 不支持恢复。");
        }

        ErrorOr<IExecutionRestoreData> data = restorer.Prepare(context);

        return data.IsError
            ? data.ErrorsOrEmptyList
            : new PreparedExecutionRestore(
                new ExecutionInvocation(context.Target, factory),
                context,
                data.Value);
    }

    private static Dictionary<ExecutionRunnerKey, IExecutionRunnerFactory> Index(
        IReadOnlyList<IExecutionRunnerFactory> factories)
    {
        IGrouping<ExecutionRunnerKey, IExecutionRunnerFactory>? duplicate =
            factories.GroupBy(factory => factory.Key).FirstOrDefault(group => group.Skip(1).Any());

        if (duplicate is not null)
        {
            throw new InvalidOperationException($"执行器工厂键 {duplicate.Key} 重复注册。");
        }

        return factories.ToDictionary(factory => factory.Key, factory => factory);
    }
}