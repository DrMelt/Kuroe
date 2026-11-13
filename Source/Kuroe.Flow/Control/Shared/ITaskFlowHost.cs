namespace Kuroe.Flow.Control.Shared;

/// <summary>流程运行期驱动任务所需的持有面：除控制实例外提供控制工厂与接上控制的入口。</summary>
public interface ITaskFlowHost : ITaskFlowContext
{
    /// <summary>任务的流程控制工厂。</summary>
    IFlowControlFactory Controls { get; }

    /// <summary>接上控制实例。启动与恢复时由运行期调用。</summary>
    void AttachControl(IFlowControl control);
}