using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Flow.Hosting;

/// <summary>图流程内置节点使用的执行运行器工厂。</summary>
public sealed record GraphExecutionRunners(
    IExecutionRunnerFactory Command,
    IExecutionRunnerFactory FileRead,
    IExecutionRunnerFactory FileWrite,
    IExecutionRunnerFactory Input,
    IExecutionRunnerFactory Approval);