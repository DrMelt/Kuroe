using Kuroe.Host.Ability.Tool;
using Xunit;

namespace Kuroe.Host.Ability.Runner.Llm.Tests.Executions;

/// <summary>执行过程记录的具体格式：内容合并、条目保留与过程播发。</summary>
public sealed class ProcessJournalTests
{
    [Fact(DisplayName = "回复内容增量合并并播发每个增量")]
    public void Reply_content_deltas_merge_and_broadcast_each_delta()
    {
        List<ProcessContent> contents = [];
        List<JournalEntry> entries = [];
        ProcessJournal journal = new(TimeProvider.System);
        journal.ContentAppended += contents.Add;
        journal.EntryAppended += entries.Add;

        journal.AppendContent(ProcessContentKind.Reply, "甲");
        journal.AppendContent(ProcessContentKind.Reply, "乙");

        Assert.Equal(
            [new ProcessContent(ProcessContentKind.Reply, "甲"), new ProcessContent(ProcessContentKind.Reply, "乙")],
            contents);
        Assert.Empty(entries);
        JournalEntry entry = Assert.Single(journal.Entries);
        Assert.Equal("回复", entry.Label);
        Assert.Equal(JournalLevel.Information, entry.Level);
        Assert.Equal("甲乙", entry.Text);
    }

    [Fact(DisplayName = "文本片段在上限处切分")]
    public void Text_segment_splits_at_the_limit()
    {
        ProcessJournal journal = new(TimeProvider.System);

        journal.AppendContent(ProcessContentKind.Reply, new string('长', 4096));
        journal.AppendContent(ProcessContentKind.Reply, "尾");

        JournalEntry[] entries = [.. journal.Entries];
        Assert.Equal(2, entries.Length);
        Assert.Equal(4096, entries[0].Text.Length);
        Assert.Equal("尾", entries[1].Text);
    }

    [Fact(DisplayName = "推理内容与回复内容分别合并")]
    public void Reasoning_content_merges_separately_from_reply_content()
    {
        List<ProcessContent> contents = [];
        ProcessJournal journal = new(TimeProvider.System);
        journal.ContentAppended += contents.Add;

        journal.AppendContent(ProcessContentKind.Reasoning, string.Empty);
        journal.AppendContent(ProcessContentKind.Reasoning, "想");
        journal.AppendContent(ProcessContentKind.Reasoning, "下去");
        journal.AppendContent(ProcessContentKind.Reply, "答复");
        journal.AppendContent(ProcessContentKind.Reasoning, "再想");

        Assert.Equal(
            [
                new ProcessContent(ProcessContentKind.Reasoning, "想"),
                new ProcessContent(ProcessContentKind.Reasoning, "下去"),
                new ProcessContent(ProcessContentKind.Reply, "答复"),
                new ProcessContent(ProcessContentKind.Reasoning, "再想"),
            ],
            contents);
        Assert.Equal(["思考", "回复", "思考"], journal.Entries.Select(entry => entry.Label));
        Assert.Equal(["想下去", "答复", "再想"], journal.Entries.Select(entry => entry.Text));
    }

    [Fact(DisplayName = "工具调用被记录并播发但没有进度")]
    public void Tool_call_is_recorded_and_broadcast_without_progress()
    {
        List<JournalEntry> entries = [];
        List<string> progress = [];
        ProcessJournal journal = new(TimeProvider.System);
        journal.EntryAppended += entries.Add;
        journal.ProgressReported += progress.Add;
        ToolCallRecord call = new("GetLocalTime", "{}", "2026-10-18 19:10:48", false);

        journal.AppendToolCall(call);

        JournalEntry entry = Assert.Single(journal.Entries);
        Assert.Equal("工具", entry.Label);
        Assert.Equal(JournalLevel.Information, entry.Level);
        Assert.Equal("GetLocalTime {}\n2026-10-18 19:10:48", entry.Text);
        Assert.Equal([entry], entries);
        Assert.Empty(progress);
    }

    [Fact(DisplayName = "进度只在变化时播发")]
    public void Progress_is_broadcast_only_when_it_changes()
    {
        List<string> progress = [];
        ProcessJournal journal = new(TimeProvider.System);
        journal.ProgressReported += progress.Add;

        journal.ReportProgress("调用工具 GetLocalTime");
        journal.ReportProgress("调用工具 GetLocalTime");
        journal.ReportProgress(string.Empty);
        journal.ReportProgress(string.Empty);

        Assert.Equal(["调用工具 GetLocalTime", string.Empty], progress);
    }

    [Fact(DisplayName = "回复与推理内容报告进度")]
    public void Reply_and_reasoning_content_report_progress()
    {
        List<string> progress = [];
        ProcessJournal journal = new(TimeProvider.System);
        journal.ProgressReported += progress.Add;

        journal.AppendContent(ProcessContentKind.Reply, "甲");
        journal.AppendContent(ProcessContentKind.Reply, "乙");
        journal.AppendContent(ProcessContentKind.Reasoning, "想");
        journal.AppendContent(ProcessContentKind.Reply, "丙");

        Assert.Equal(["输出", "思考", "输出"], progress);
    }

    [Fact(DisplayName = "失败工具调用记录为警告")]
    public void Failed_tool_call_is_recorded_as_warning()
    {
        ProcessJournal journal = new(TimeProvider.System);
        ToolCallRecord call = new("file/ReadFile", "{}", "不存在", true);

        journal.AppendToolCall(call);

        JournalEntry entry = Assert.Single(journal.Entries);
        Assert.Equal(JournalLevel.Warning, entry.Level);
        Assert.Equal("file/ReadFile {}\n不存在", entry.Text);
    }

    [Fact(DisplayName = "提示与丢弃条目只记录不播发")]
    public void Prompt_and_discard_entries_are_recorded_only()
    {
        List<ProcessContent> contents = [];
        List<JournalEntry> entries = [];
        List<string> progress = [];
        ProcessJournal journal = new(TimeProvider.System);
        journal.ContentAppended += contents.Add;
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
        Assert.Empty(contents);
        Assert.Empty(entries);
        Assert.Empty(progress);
    }

    [Fact(DisplayName = "提示清空进度")]
    public void Prompt_clears_progress()
    {
        List<string> progress = [];
        ProcessJournal journal = new(TimeProvider.System);
        journal.ProgressReported += progress.Add;

        journal.AppendContent(ProcessContentKind.Reply, "答复");
        journal.AppendPrompt("问题");

        Assert.Equal(["输出", string.Empty], progress);
    }

    [Fact(DisplayName = "空内容增量被忽略")]
    public void Empty_content_delta_is_ignored()
    {
        int broadcasts = 0;
        ProcessJournal journal = new(TimeProvider.System);
        journal.ContentAppended += _ => broadcasts++;

        journal.AppendContent(ProcessContentKind.Reply, string.Empty);

        Assert.Empty(journal.Entries);
        Assert.Equal(0, broadcasts);
    }

    [Fact(DisplayName = "进程记录条目无数量上限保留")]
    public void Entries_are_retained_without_a_count_limit()
    {
        ProcessJournal journal = new(TimeProvider.System);

        for (int index = 0; index <= 400; index++)
        {
            journal.AppendPrompt(index.ToString());
        }

        Assert.Equal(401, journal.Entries.Count);
        Assert.Equal("0", journal.Entries[0].Text);
        Assert.Equal("400", journal.Entries[^1].Text);
    }
}