using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Execution.Registry;

/// <summary>执行器工厂的注册项，按工厂类型选择。</summary>
public sealed record ExecutionRunnerRegistration(
    Type RunnerFactoryType,
    IExecutionRunnerFactory Factory);