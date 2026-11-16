using Kuroe.Execution.Shared;

namespace Kuroe.Execution;

/// <summary>执行器工厂的注册项，按工厂类型选择。</summary>
public sealed record ExecutionRunnerRegistration(
    Type RunnerFactoryType,
    IExecutionRunnerFactory Factory);

/// <summary>按执行器工厂类型选择运行器。没有注册时明确失败。</summary>
public sealed class ExecutionRunnerRegistry(
    IReadOnlyList<ExecutionRunnerRegistration>? registrations = null)
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