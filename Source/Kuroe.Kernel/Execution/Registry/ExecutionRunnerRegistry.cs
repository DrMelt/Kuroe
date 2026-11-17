using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Execution.Registry;

/// <summary>执行器工厂注册表：按运行器键索引工厂，没有注册或键重复时明确失败。</summary>
public sealed class ExecutionRunnerRegistry(
    IReadOnlyList<IExecutionRunnerFactory>? factories = null) : IExecutionRunnerRegistry
{
    private readonly IReadOnlyDictionary<ExecutionRunnerKey, IExecutionRunnerFactory> _factories =
        Index(factories ?? []);

    /// <summary>按运行器键查找执行器工厂，没有注册时为空。</summary>
    public IExecutionRunnerFactory? Find(ExecutionRunnerKey key) =>
        _factories.GetValueOrDefault(key);

    /// <summary>为一次执行建立运行器。</summary>
    public IExecutionRunner Create(
        IExecutionTarget target,
        ExecutionId execution)
    {
        IExecutionRunnerFactory factory = Find(target.RunnerKey)
            ?? throw new InvalidOperationException(
                $"执行器工厂 {target.RunnerKey} 没有注册。");

        return factory.Create(target, execution);
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