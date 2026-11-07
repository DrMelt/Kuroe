using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Xunit;

namespace Kuroe.Flow.Shared.Tests.RuntimeGraph.Execution;

/// <summary>执行过程记录：内容合并、进度与条目保留。</summary>
public sealed class ExecutionJournalTests
{
    [Fact]
    public void Content_deltas_merge_into_one_entry_and_report_progress()
    {
        ExecutionJournal journal = new();
        List<ProcessContent> contents = [];
        List<string> progress = [];
        journal.ContentAppended += contents.Add;
        journal.ProgressReported += progress.Add;

        journal.AppendContent(ProcessContentKind.Reply, "甲");
        journal.AppendContent(ProcessContentKind.Reply, "乙");

        JournalEntry entry = Assert.Single(journal.Entries);
        Assert.Equal("输出", entry.Label);
        Assert.Equal("甲乙", entry.Text);
        Assert.Equal(["输出"], progress);
        Assert.Equal(
            [new ProcessContent(ProcessContentKind.Reply, "甲"), new ProcessContent(ProcessContentKind.Reply, "乙")],
            contents);
    }

    [Fact]
    public void Entries_are_retained_without_a_count_limit()
    {
        ExecutionJournal journal = new();

        for (int index = 0; index <= 400; index++)
        {
            journal.Append("条目", index.ToString());
        }

        Assert.Equal(401, journal.Entries.Count);
        Assert.Equal("0", journal.Entries[0].Text);
        Assert.Equal("400", journal.Entries[^1].Text);
    }
}