using Kuroe.Execution.Shared;

namespace Kuroe.Execution;

/// <summary>按执行种类选择运行器：同种类注册优先，其次通配注册。</summary>
public sealed class ExecutionRunnerRegistry(
    IReadOnlyList<IExecutionRunnerFactory>? runners = null)
{
    /// <summary>为一次执行建立运行器。</summary>
    public IExecutionRunner Create(
        IExecutionTarget target,
        ExecutionId execution)
    {
        IExecutionRunnerFactory? factory = runners?.FirstOrDefault(candidate => candidate.Kind == target.Kind)
            ?? runners?.FirstOrDefault(candidate => candidate.Kind == "*");

        return factory is null
            ? throw new InvalidOperationException($"执行种类 {target.Kind} 没有可用的执行器。")
            : factory.Create(target, execution);
    }
}