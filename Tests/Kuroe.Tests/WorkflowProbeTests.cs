using Microsoft.Agents.AI.Workflows;
using Xunit;

namespace Kuroe.Tests;

/// <summary>框架 Workflows 的执行契约探针：图构建、自定义 executor 链、条件路由与暂停恢复，
/// 是编排层迁移依赖的机制前提。</summary>
public sealed class WorkflowProbeTests
{
    [Fact]
    public async Task Custom_executor_chain_runs_to_completion()
    {
        ProbeStart start = new();
        ProbeSink sink = new("sink");
        Workflow workflow = new WorkflowBuilder(start)
            .AddEdge(start, sink)
            .WithOutputFrom(sink)
            .Build();

        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, "你好");

        List<WorkflowEvent> events = [];
        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            events.Add(evt);
        }

        Assert.Contains(events, evt => evt is WorkflowOutputEvent output && "你好".Equals(output.Data?.ToString()));
    }

    [Fact]
    public async Task Conditional_edge_routes_by_payload()
    {
        ProbeStart start = new();
        ProbeSink yes = new("yes");
        ProbeSink no = new("no");
        Workflow workflow = new WorkflowBuilder(start)
            .AddEdge<string>(start, yes, message => message == "你好")
            .AddEdge<string>(start, no, message => message != "你好")
            .WithOutputFrom(yes, no)
            .Build();

        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, "ok");

        List<string> outputs = [];
        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            if (evt is WorkflowOutputEvent output)
            {
                outputs.Add(output.Data?.ToString() ?? string.Empty);
            }
        }

        Assert.Contains("你好", outputs);
    }

    [Fact]
    public async Task Request_halt_pauses_run_until_new_input()
    {
        ProbeStart start = new();
        ProbeHalt halt = new();
        ProbeSink sink = new("sink");
        Workflow workflow = new WorkflowBuilder(start)
            .AddEdge(start, halt)
            .AddEdge(halt, sink)
            .WithOutputFrom(sink)
            .Build();

        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, "start");

        // 第一次事件流在请求暂停处结束
        var first = new List<WorkflowEvent>();
        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            first.Add(evt);
        }

        Assert.DoesNotContain(first, evt => evt is WorkflowOutputEvent);

        // 恢复：向入口再发一条消息，run 继续推进到 sink
        await run.TrySendMessageAsync("continue");

        var second = new List<WorkflowEvent>();
        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            second.Add(evt);
        }

        Assert.Contains(second, evt => evt is WorkflowOutputEvent output && "done".Equals(output.Data?.ToString()));
    }
}

[SendsMessage(typeof(string))]
internal sealed partial class ProbeStart : Executor
{
    public ProbeStart() : base("start")
    {
    }

    [MessageHandler]
    public async ValueTask HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (message == "start")
        {
            await context.SendMessageAsync("go", null, cancellationToken);
            return;
        }

        if (message == "continue")
        {
            await context.SendMessageAsync("done", null, cancellationToken);
            return;
        }

        if (message == "ok")
        {
            await context.SendMessageAsync("你好", null, cancellationToken);
            return;
        }

        await context.SendMessageAsync(message, null, cancellationToken);
    }
}

[SendsMessage(typeof(string))]
internal sealed partial class ProbeHalt : Executor
{
    public ProbeHalt() : base("halt")
    {
    }

    [MessageHandler]
    public async ValueTask HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (message == "go")
        {
            await context.RequestHaltAsync();
            return;
        }

        await context.SendMessageAsync("done", null, cancellationToken);
    }
}

[YieldsOutput(typeof(string))]
internal sealed partial class ProbeSink : Executor
{
    public ProbeSink(string id) : base(id)
    {
    }

    [MessageHandler]
    public async ValueTask HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        await context.YieldOutputAsync(message, cancellationToken);
    }
}