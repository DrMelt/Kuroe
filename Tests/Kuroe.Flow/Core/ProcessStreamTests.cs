using Kuroe.Flow.Runtime.Process;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;
using Xunit;

namespace Kuroe.Flow.Tests;

/// <summary>过程播发：文本增量与工具调用记录转给附加的观察者，收口后关闭。</summary>
public sealed class ProcessStreamTests
{
    [Fact]
    public void Carries_text_and_tool_calls_to_every_observer()
    {
        ProcessStream stream = new();
        RecordingSink first = new();
        RecordingSink second = new();
        stream.Observe(first);
        stream.Observe(second);

        stream.OnText("增量");
        stream.OnToolCall(new ToolCallRecord("GetLocalTime", "{}", "2026-10-18 19:10:48", false));

        Assert.Equal(["增量"], first.Texts);
        Assert.Equal(["GetLocalTime"], first.Calls);
        Assert.Equal(["增量"], second.Texts);
        Assert.Equal(["GetLocalTime"], second.Calls);
    }

    [Fact]
    public void Closed_stream_carries_nothing()
    {
        ProcessStream stream = new();
        RecordingSink sink = new();
        stream.Observe(sink);

        stream.Close();
        stream.OnText("增量");
        stream.OnToolCall(new ToolCallRecord("GetLocalTime", "{}", "2026-10-18 19:10:48", false));

        Assert.Empty(sink.Texts);
        Assert.Empty(sink.Calls);
    }

    [Fact]
    public void Observe_after_close_registers_no_observer()
    {
        ProcessStream stream = new();
        RecordingSink sink = new();

        stream.Close();
        stream.Observe(sink);
        stream.OnText("增量");

        Assert.Empty(sink.Texts);
    }

    /// <summary>记下收到的过程，供断言。</summary>
    private sealed class RecordingSink : IProcessSink
    {
        public List<string> Texts { get; } = [];

        public List<string> Calls { get; } = [];

        public void OnText(string delta) => Texts.Add(delta);

        public void OnToolCall(ToolCallRecord record) => Calls.Add(record.Name);
    }
}