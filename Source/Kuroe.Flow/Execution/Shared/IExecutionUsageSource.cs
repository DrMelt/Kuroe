namespace Kuroe.Flow.Execution;

/// <summary>执行器上报本次执行的模型请求用量。没有模型请求的执行器不实现。</summary>
public interface IExecutionUsageSource
{
    /// <summary>本次执行累计的用量，运行中给出当前累计，执行结束后给出最终值。</summary>
    ExecutionUsageSnapshot Usage { get; }
}