namespace Kuroe.Agent.Turns;

/// <summary>一轮模型请求的观察者。库只在这里交出过程，不关心它被写到何处。</summary>
public interface ITurnSink
{
    /// <summary>模型输出的一段文本，按到达顺序给出。</summary>
    void OnText(string delta);

    /// <summary>一次工具调用及其结果。</summary>
    void OnToolCall(ToolCallRecord record);

    /// <summary>把过程写进 journal，另给一份观察者时依次通知。没有别的观察者时只写 journal。</summary>
    internal static ITurnSink For(TurnJournal journal, ITurnSink? observer) => observer is null
        ? new JournalSink(journal)
        : new ChainedSink(new JournalSink(journal), observer);
}

/// <summary>把过程写进过程记录。</summary>
internal sealed class JournalSink(TurnJournal journal) : ITurnSink
{
    public void OnText(string delta) => journal.AppendText(delta);

    public void OnToolCall(ToolCallRecord record) => journal.Append(new ToolCallEntry(record));
}

/// <summary>依次通知多个观察者。</summary>
internal sealed class ChainedSink(params ITurnSink[] inner) : ITurnSink
{
    public void OnText(string delta)
    {
        foreach (ITurnSink sink in inner)
        {
            sink.OnText(delta);
        }
    }

    public void OnToolCall(ToolCallRecord record)
    {
        foreach (ITurnSink sink in inner)
        {
            sink.OnToolCall(record);
        }
    }
}
