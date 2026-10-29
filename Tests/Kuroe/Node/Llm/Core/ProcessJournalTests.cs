using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Node.Llm.Executions;
using Kuroe.Tool;
using Xunit;

namespace Kuroe.Node.Llm.Tests;

/// <summary>执行过程记录的具体格式：文本合并、条目上限与过程播发。</summary>
public sealed class ProcessJournalTests
{
    [Fact]
    public void Text_deltas_merge_and_broadcast_each_delta()
    {
        List<string> texts = [];
        List<JournalEntry> entries = [];
        ProcessJournal journal = new();
        journal.TextAppended += texts.Add;
        journal.EntryAppended += entries.Add;

        journal.AppendText("甲");
        journal.AppendText("乙");

        Assert.Equal(["甲", "乙"], texts);
        Assert.Empty(entries);
        JournalEntry entry = Assert.Single(journal.Entries);
        Assert.Equal("回复", entry.Label);
        Assert.Equal(JournalLevel.Information, entry.Level);
        Assert.Equal("甲乙", entry.Text);
    }

    [Fact]
    public void Text_segment_splits_at_the_limit()
    {
        ProcessJournal journal = new();

        journal.AppendText(new string('长', 4096));
        journal.AppendText("尾");

        JournalEntry[] entries = [.. journal.Entries];
        Assert.Equal(2, entries.Length);
        Assert.Equal(4096, entries[0].Text.Length);
        Assert.Equal("尾", entries[1].Text);
    }

    [Fact]
    public void Tool_call_is_recorded_and_broadcast()
    {
        List<JournalEntry> entries = [];
        List<string> progress = [];
        ProcessJournal journal = new();
        journal.EntryAppended += entries.Add;
        journal.ProgressReported += progress.Add;
        ToolCallRecord call = new("GetLocalTime", "{}", "2026-10-18 19:10:48", false);

        journal.AppendToolCall(call);

        JournalEntry entry = Assert.Single(journal.Entries);
        Assert.Equal("工具", entry.Label);
        Assert.Equal(JournalLevel.Information, entry.Level);
        Assert.Equal("GetLocalTime {}\n结果 2026-10-18 19:10:48", entry.Text);
        Assert.Equal([entry], entries);
        Assert.Equal(["调用工具 GetLocalTime"], progress);
    }

    [Fact]
    public void Failed_tool_call_is_recorded_as_warning()
    {
        ProcessJournal journal = new();
        ToolCallRecord call = new("file/ReadFile", "{}", "不存在", true);

        journal.AppendToolCall(call);

        JournalEntry entry = Assert.Single(journal.Entries);
        Assert.Equal(JournalLevel.Warning, entry.Level);
        Assert.Equal("file/ReadFile {}\n失败 不存在", entry.Text);
    }

    [Fact]
    public void Prompt_and_discard_entries_are_recorded_only()
    {
        List<string> texts = [];
        List<JournalEntry> entries = [];
        List<string> progress = [];
        ProcessJournal journal = new();
        journal.TextAppended += texts.Add;
        journal.EntryAppended += entries.Add;
        journal.ProgressReported += progress.Add;

        journal.AppendPrompt("问题");
        journal.AppendDiscarded();

        Assert.Equal(2, journal.Entries.Count);
        JournalEntry prompt = journal.Entries[0];
        Assert.Equal("输入", prompt.Label);
        Assert.Equal(JournalLevel.Information, prompt.Level);
        Assert.Equal("问题", prompt.Text);
        JournalEntry discarded = journal.Entries[1];
        Assert.Equal("丢弃", discarded.Label);
        Assert.Equal(JournalLevel.Warning, discarded.Level);
        Assert.Empty(texts);
        Assert.Empty(entries);
        Assert.Empty(progress);
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
            journal.AppendPrompt(index.ToString());
        }

        Assert.Equal(400, journal.Entries.Count);
        Assert.Equal(1, journal.DroppedEntries);
        Assert.Equal("1", journal.Entries[0].Text);
        Assert.Equal("400", journal.Entries[^1].Text);
    }
}