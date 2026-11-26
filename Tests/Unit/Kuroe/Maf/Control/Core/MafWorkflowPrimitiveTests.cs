using Microsoft.Agents.AI.Workflows;
using Xunit;

namespace Kuroe.Maf.Control.Tests;

/// <summary>MAF 原语验证：开始扇出、汇合聚合、状态可见、环上限与屏障限制。</summary>
public sealed class MafWorkflowPrimitiveTests
{
    [Fact(DisplayName = "开始执行器向两个入口扇出")]
    public async Task Start_executor_fans_out_to_two_entries()
    {
        FanOutStart start = new("start");
        Recorder left = new("left");
        Recorder right = new("right");
        Workflow workflow = new WorkflowBuilder(start)
            .AddEdge(start, left)
            .AddEdge(start, right)
            .Build();

        await using Run run = await RunAsync(workflow, "go");

        Assert.Equal(RunStatus.Idle, await run.GetStatusAsync(TestContext.Current.CancellationToken));
        Assert.Equal("go", Assert.Single(left.Messages).Text);
        Assert.Equal("go", Assert.Single(right.Messages).Text);
    }

    [Fact(DisplayName = "汇合执行器等待全部组来源后只完成一次")]
    public async Task Join_executor_waits_for_all_group_sources()
    {
        FanOutStart start = new("start");
        Relay left = new("left");
        Relay right = new("right");
        GroupJoin join = new("join");
        Workflow workflow = new WorkflowBuilder(start)
            .AddEdge(start, left)
            .AddEdge(start, right)
            .AddEdge(left, join)
            .AddEdge(right, join)
            .Build();

        await using Run run = await RunAsync(workflow, "go");

        Assert.Equal(RunStatus.Idle, await run.GetStatusAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, join.HandlerCount);
        Assert.Equal(1, join.CompletedCount);
        Assert.Equal(2, join.Messages.Count);
    }

    [Fact(DisplayName = "一个执行器写入的状态在下一超步可见")]
    public async Task State_written_by_one_executor_is_visible_to_the_next()
    {
        FanOutStart start = new("start");
        StateWriter writer = new("writer");
        StateReader reader = new("reader");
        Workflow workflow = new WorkflowBuilder(start)
            .AddEdge(start, writer)
            .AddEdge(writer, reader)
            .Build();

        await using Run run = await RunAsync(workflow, "value");

        Assert.Equal(RunStatus.Idle, await run.GetStatusAsync(TestContext.Current.CancellationToken));
        Assert.Equal("value", reader.Value);
    }

    [Fact(DisplayName = "回边推进在达到执行上限后停止")]
    public async Task Cycle_stops_at_the_execution_cap()
    {
        const int cap = 3;
        CycleStart start = new("start");
        CycleA a = new("a", cap);
        CycleB b = new("b");
        Workflow workflow = new WorkflowBuilder(start)
            .AddEdge(start, a)
            .AddEdge(a, b)
            .AddEdge(b, a)
            .Build();

        await using Run run = await RunAsync(workflow, "go");

        Assert.Equal(RunStatus.Idle, await run.GetStatusAsync(TestContext.Current.CancellationToken));
        Assert.Equal(cap, a.Executions);
        Assert.Equal(cap - 1, b.Executions);
    }

    [Fact(DisplayName = "来源不发送消息时屏障不释放")]
    public async Task Barrier_does_not_release_when_one_source_is_silent()
    {
        FanOutStart start = new("start");
        BarrierSource source = new("source");
        SilentSource silent = new("silent");
        BarrierSink sink = new("sink");
        Workflow workflow = new WorkflowBuilder(start)
            .AddEdge(start, source)
            .AddEdge(start, silent)
            .AddFanInBarrierEdge([source, silent], sink)
            .Build();

        await DrainAsync(workflow, "go");

        Assert.Empty(sink.Messages);
    }

    private static async Task<Run> RunAsync(Workflow workflow, string input)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));

        return await InProcessExecution
            .RunAsync(workflow, input, cancellationToken: timeout.Token)
            .ConfigureAwait(false);
    }

    private static async Task DrainAsync(Workflow workflow, string input)
    {
        await using StreamingRun run = await InProcessExecution
            .RunStreamingAsync(workflow, input)
            .ConfigureAwait(false);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));

        try
        {
            await foreach (WorkflowEvent update in run.WatchStreamAsync(timeout.Token).ConfigureAwait(false))
            {
                _ = update;
            }
        }
        catch (OperationCanceledException)
        {
            // 屏障保留未释放消息时运行不会自动结束，取消事件流即可完成验证。
        }
    }

    private sealed record PrimitiveMessage(string Origin, string Text);

    private sealed class FanOutStart(string id) : Executor<string>(id)
    {
        [SendsMessage(typeof(PrimitiveMessage))]
        public override ValueTask HandleAsync(
            string message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default) =>
            context.SendMessageAsync(new PrimitiveMessage(Id, message), cancellationToken: cancellationToken);
    }

    private abstract class RecordingExecutor(string id) : Executor<PrimitiveMessage>(id)
    {
        private readonly Lock _gate = new();
        private readonly List<PrimitiveMessage> _messages = [];

        internal IReadOnlyList<PrimitiveMessage> Messages
        {
            get
            {
                lock (_gate)
                {
                    return [.. _messages];
                }
            }
        }

        protected void Record(PrimitiveMessage message)
        {
            lock (_gate)
            {
                _messages.Add(message);
            }
        }
    }

    private sealed class Recorder(string id) : RecordingExecutor(id)
    {
        public override ValueTask HandleAsync(
            PrimitiveMessage message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            Record(message);

            return default;
        }
    }

    private sealed class Relay(string id) : Executor<PrimitiveMessage>(id)
    {
        [SendsMessage(typeof(PrimitiveMessage))]
        public override ValueTask HandleAsync(
            PrimitiveMessage message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default) =>
            context.SendMessageAsync(new PrimitiveMessage(Id, message.Text), cancellationToken: cancellationToken);
    }

    private sealed class GroupJoin(string id) : RecordingExecutor(id)
    {
        private const string StateScope = "join";
        private int _handlers;
        private int _completed;

        internal int HandlerCount => Volatile.Read(ref _handlers);

        internal int CompletedCount => Volatile.Read(ref _completed);

        public override async ValueTask HandleAsync(
            PrimitiveMessage message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            Record(message);
            Interlocked.Increment(ref _handlers);

            string key = message.Origin;
            await context.QueueStateUpdateAsync(key, true, StateScope, cancellationToken).ConfigureAwait(false);
        }

        protected override async ValueTask OnMessageDeliveryFinishedAsync(
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            bool left = await context.ReadStateAsync<bool>("left", StateScope, cancellationToken).ConfigureAwait(false);
            bool right = await context.ReadStateAsync<bool>("right", StateScope, cancellationToken).ConfigureAwait(false);
            bool done = await context.ReadStateAsync<bool>("done", StateScope, cancellationToken).ConfigureAwait(false);
            if (left && right && !done)
            {
                await context.QueueStateUpdateAsync("done", true, StateScope, cancellationToken).ConfigureAwait(false);
                Interlocked.Increment(ref _completed);
            }
        }
    }

    private sealed class StateWriter(string id) : Executor<PrimitiveMessage>(id)
    {
        private const string StateScope = "shared";

        [SendsMessage(typeof(PrimitiveMessage))]
        public override async ValueTask HandleAsync(
            PrimitiveMessage message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            await context.QueueStateUpdateAsync("value", message.Text, StateScope, cancellationToken).ConfigureAwait(false);
            await context.SendMessageAsync(new PrimitiveMessage(Id, message.Text), cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class StateReader(string id) : Executor<PrimitiveMessage>(id)
    {
        private const string StateScope = "shared";

        internal string? Value { get; private set; }

        public override async ValueTask HandleAsync(
            PrimitiveMessage message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            Value = await context.ReadStateAsync<string>("value", StateScope, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class CycleStart(string id) : Executor<string>(id)
    {
        [SendsMessage(typeof(PrimitiveMessage))]
        public override ValueTask HandleAsync(
            string message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default) =>
            context.SendMessageAsync(new PrimitiveMessage(Id, message), cancellationToken: cancellationToken);
    }

    private sealed class CycleA(string id, int cap) : Executor<PrimitiveMessage>(id)
    {
        private const string StateScope = "cycle";
        private int _executions;

        internal int Executions => Volatile.Read(ref _executions);

        [SendsMessage(typeof(PrimitiveMessage))]
        public override async ValueTask HandleAsync(
            PrimitiveMessage message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            int count = await context.ReadOrInitStateAsync("count", () => 0, StateScope, cancellationToken).ConfigureAwait(false);
            count++;
            await context.QueueStateUpdateAsync("count", count, StateScope, cancellationToken).ConfigureAwait(false);
            Interlocked.Exchange(ref _executions, count);

            if (count < cap)
            {
                await context.SendMessageAsync(
                    new PrimitiveMessage(Id, message.Text),
                    "b",
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private sealed class CycleB(string id) : Executor<PrimitiveMessage>(id)
    {
        private int _executions;

        internal int Executions => Volatile.Read(ref _executions);

        [SendsMessage(typeof(PrimitiveMessage))]
        public override async ValueTask HandleAsync(
            PrimitiveMessage message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _executions);
            await context.SendMessageAsync(
                new PrimitiveMessage(Id, message.Text),
                "a",
                cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class BarrierSource(string id) : Executor<PrimitiveMessage>(id)
    {
        [SendsMessage(typeof(PrimitiveMessage))]
        public override ValueTask HandleAsync(
            PrimitiveMessage message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default) =>
            context.SendMessageAsync(new PrimitiveMessage(Id, message.Text), cancellationToken: cancellationToken);
    }

    private sealed class SilentSource(string id) : Executor<PrimitiveMessage>(id)
    {
        public override ValueTask HandleAsync(
            PrimitiveMessage message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default) => default;
    }

    private sealed class BarrierSink(string id) : RecordingExecutor(id)
    {
        public override ValueTask HandleAsync(
            PrimitiveMessage message,
            IWorkflowContext context,
            CancellationToken cancellationToken = default)
        {
            Record(message);

            return default;
        }
    }
}