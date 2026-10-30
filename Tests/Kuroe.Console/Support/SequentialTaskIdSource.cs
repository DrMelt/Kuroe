using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Console.Tests.Support;

/// <summary>按提交顺序给出固定键，测试可直接以固定值引用任务。</summary>
internal sealed class SequentialTaskIdSource : ITaskIdSource
{
    private int _next;

    public TaskId Next() => new(Interlocked.Increment(ref _next));
}
