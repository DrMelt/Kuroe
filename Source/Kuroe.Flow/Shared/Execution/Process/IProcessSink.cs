namespace Kuroe.Flow.Shared.Execution.Process;

/// <summary>一次执行过程的接收方：文本增量与工具调用记录按到达顺序送达。只做播发，不落记录。</summary>
public interface IProcessSink
{
    /// <summary>模型输出的一段文本，按到达顺序给出。</summary>
    void OnText(string delta);

    /// <summary>一次工具调用及其结果。</summary>
    void OnToolCall(ToolCallRecord record);
}
