using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Execution.Registry;

/// <summary>执行器工厂注册表：按工厂类型精确匹配，没有注册时明确失败。</summary>
public sealed class ExecutionRunnerRegistry(
    IReadOnlyList<ExecutionRunnerRegistration>? registrations = null) : IExecutionRunnerRegistry
{
    private readonly IReadOnlyList<ExecutionRunnerRegistration> _registrations = registrations ?? [];

    /// <summary>按工厂类型查找执行器工厂，没有注册时为空。</summary>
    public IExecutionRunnerFactory? Find(Type runnerFactoryType) =>
        _registrations.FirstOrDefault(candidate => candidate.RunnerFactoryType == runnerFactoryType)?.Factory;

    /// <summary>为一次执行建立运行器。</summary>
    public IExecutionRunner Create(
        IExecutionTarget target,
        ExecutionId execution)
    {
        IExecutionRunnerFactory? factory = Find(target.RunnerFactoryType);

        return factory is null
            ? throw new InvalidOperationException(
                $"执行器工厂 {target.RunnerFactoryType.FullName ?? target.RunnerFactoryType.Name} 没有注册。")
            : factory.Create(target, execution);
    }
}