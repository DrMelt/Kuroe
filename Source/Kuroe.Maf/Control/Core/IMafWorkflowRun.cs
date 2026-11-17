namespace Kuroe.Maf.Control;

/// <summary>一次 MAF 运行：事件流与释放。</summary>
internal interface IMafWorkflowRun : IAsyncDisposable
{
    /// <summary>消费运行事件，流结束表示流程走完。</summary>
    IAsyncEnumerable<MafWorkflowEvent> WatchAsync(CancellationToken cancellationToken);
}