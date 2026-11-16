namespace Kuroe.Process;

/// <summary>进程无法启动。消息可直接交给工具调用方或节点失败说明。</summary>
public sealed class ProcessStartException : Exception
{
    /// <summary>用说明建立异常。</summary>
    public ProcessStartException(string message)
        : base(message)
    {
    }

    /// <summary>用说明与原始异常建立异常。</summary>
    public ProcessStartException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}