using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Node.Llm.Executions;
using Kuroe.Node.Llm.Sessions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;

namespace Kuroe.Node.Llm.Tests;

/// <summary>流式更新的过程记录：思考与回复按内容类型分别写入并播发，其余内容不进入过程记录。</summary>
public sealed class SessionTests
{
    [Fact]
    public void Reasoning_and_reply_contents_are_recorded_by_type()
    {
        ProcessJournal journal = new();
        List<ProcessContent> contents = [];
        journal.ContentAppended += contents.Add;
        AgentResponseUpdate update = new(
            ChatRole.Assistant,
            [
                new TextReasoningContent("先想"),
                new TextContent("再答"),
                new FunctionCallContent("call-1", "ReadFile"),
            ]);

        Session.RecordContent(update, journal);

        Assert.Equal(
            [
                new ProcessContent(ProcessContentKind.Reasoning, "先想"),
                new ProcessContent(ProcessContentKind.Reply, "再答"),
            ],
            contents);
        Assert.Equal(["思考", "回复"], journal.Entries.Select(entry => entry.Label));
        Assert.Equal(["先想", "再答"], journal.Entries.Select(entry => entry.Text));
    }
}
