using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;
using Kuroe.Node.Llm.Executions;
using Xunit;

namespace Kuroe.Node.Llm.Tests;

/// <summary>执行过程记录的具体格式：文本合并、条目上限与过程播发。</summary>
public sealed class ProcessJournalTests
{
    [Fact]
    public void Text_deltas_merge_and_broadcast_each_delta()
    {
        List<string> texts = [];
        ProcessJournal journal = new();
        journal.TextAppended += texts.Add;

        journal.AppendText("甲");
        journal.AppendText("乙");

        Assert.Equal(["甲", "乙"], texts);
        TextEntry entry = Assert.IsType<TextEntry>(Assert.Single(journal.Entries));
        Assert.Equal("甲乙", entry.Text);
    }

    [Fact]
    public void Text_segment_splits_at_the_limit()
    {
        ProcessJournal journal = new();

        journal.AppendText(new string('长', 4096));
        journal.AppendText("尾");

        TextEntry[] entries = [.. journal.Entries.Cast<TextEntry>()];
        Assert.Equal(2, entries.Length);
        Assert.Equal(4096, entries[0].Text.Length);
        Assert.Equal("尾", entries[1].Text);
    }

    [Fact]
    public void Tool_call_is_recorded_and_broadcast()
    {
        List<ToolCallRecord> calls = [];
        ProcessJournal journal = new();
        journal.ToolCallRecorded += calls.Add;
        ToolCallRecord call = new("GetLocalTime", "{}", "2026-10-18 19:10:48", false);

        journal.Append(new ToolCallEntry(call));

        Assert.Equal(call, Assert.IsType<ToolCallEntry>(Assert.Single(journal.Entries)).Call);
        Assert.Equal([call], calls);
    }

    [Fact]
    public void Non_broadcast_entries_are_recorded_only()
    {
        List<string> texts = [];
        List<ToolCallRecord> calls = [];
        ProcessJournal journal = new();
        journal.TextAppended += texts.Add;
        journal.ToolCallRecorded += calls.Add;

        journal.Append(new PromptEntry("问题"));
        journal.Append(new DiscardedEntry());
        journal.Append(new ErrorEntry("错误"));
        journal.Append(new InfoEntry("信息"));

        Assert.Equal(4, journal.Entries.Count);
        Assert.Empty(texts);
        Assert.Empty(calls);
    }

    [Fact]
    public void Empty_text_delta_is_ignored()
    {
        int broadcasts = 0;
        ProcessJournal journal = new();
        journal.TextAppended += _ => broadcasts++;

        journal.AppendText(string.Empty);

        Assert.Empty(journal.Entries);
        Assert.Equal(0, broadcasts);
    }

    [Fact]
    public void Entries_beyond_the_limit_drop_the_oldest()
    {
        ProcessJournal journal = new();

        for (int index = 0; index <= 400; index++)
        {
            journal.Append(new InfoEntry(index.ToString()));
        }

        Assert.Equal(400, journal.Entries.Count);
        Assert.Equal(1, journal.DroppedEntries);
        Assert.Equal("1", Assert.IsType<InfoEntry>(journal.Entries[0]).Text);
        Assert.Equal("400", Assert.IsType<InfoEntry>(journal.Entries[^1]).Text);
    }
}