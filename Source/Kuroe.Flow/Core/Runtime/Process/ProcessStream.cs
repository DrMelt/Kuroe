using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;

namespace Kuroe.Flow.Runtime.Process;

/// <summary>一次执行的过程播发：文本增量与工具调用记录转给附加的观察者，收口后关闭。</summary>
internal sealed class ProcessStream
{
    private readonly Lock _gate = new();
    private IProcessSink[] _observers = [];
    private bool _closed;

    /// <summary>附加观察者，关闭后附加不再有观察者。</summary>
    public void Observe(IProcessSink observer)
    {
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            _observers = [.. _observers, observer];
        }
    }

    /// <summary>关闭播发，之后不再有观察者。</summary>
    public void Close()
    {
        lock (_gate)
        {
            _closed = true;
            _observers = [];
        }
    }

    public void OnText(string delta)
    {
        foreach (IProcessSink observer in Current())
        {
            observer.OnText(delta);
        }
    }

    public void OnToolCall(ToolCallRecord record)
    {
        foreach (IProcessSink observer in Current())
        {
            observer.OnToolCall(record);
        }
    }

    /// <summary>取当前观察者，播发在锁外进行。</summary>
    private IProcessSink[] Current()
    {
        lock (_gate)
        {
            return _observers;
        }
    }
}