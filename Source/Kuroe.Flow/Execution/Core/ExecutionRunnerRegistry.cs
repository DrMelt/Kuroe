namespace Kuroe.Flow.Execution;

/// <summary>按执行种类选择运行器。节点类型自带运行器时优先，其次同种类注册，最后通配注册。</summary>
public sealed class ExecutionRunnerRegistry(
    IReadOnlyList<IExecutionRunnerFactory>? runners = null,
    IExecutionRunnerFactory? fallback = null)
{
    /// <summary>为一次执行建立运行器。</summary>
    public IExecutionRunner Create(
        IExecutionRunnerFactory? own,
        string kind,
        bool hasModelPort,
        IExecutionTarget target,
        ExecutionId execution)
    {
        IExecutionRunnerFactory? factory = own
            ?? (hasModelPort ? fallback : null)
            ?? runners?.FirstOrDefault(candidate => candidate.Kind == kind)
            ?? runners?.FirstOrDefault(candidate => candidate.Kind == "*");

        return factory is null
            ? throw new InvalidOperationException($"执行种类 {kind} 没有可用的执行器。")
            : factory.Create(target, execution);
    }
}