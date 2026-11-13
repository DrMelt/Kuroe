namespace Kuroe.Flow.Control.Shared;

/// <summary>没有额外运行期状态的流程实现使用的空绑定。</summary>
public sealed class NoFlowStateBinder : IFlowStateBinder
{
    /// <summary>共享实例。</summary>
    public static NoFlowStateBinder Instance { get; } = new();

    /// <inheritdoc/>
    public void Bind(ITaskFlowContext task)
    {
    }
}