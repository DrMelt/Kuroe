using Kuroe.Shared.Agent;
using Kuroe.Shared.Agent.Turns;

namespace Kuroe.Agent.Turns;

/// <summary>把过程写进记录，另给一份观察者时依次通知。没有别的观察者时只写记录。</summary>
internal static class TurnSinks
{
    public static ITurnSink For(TurnJournal journal, ITurnSink? observer) => observer is null
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