namespace Kuroe.Shared.Agent.Turns;

/// <summary>一轮模型请求的观察者。库只在这里交出过程，不关心它被写到何处。</summary>
public interface ITurnSink
{
    /// <summary>模型输出的一段文本，按到达顺序给出。</summary>
    void OnText(string delta);

    /// <summary>一次工具调用及其结果。</summary>
    void OnToolCall(ToolCallRecord record);
}
