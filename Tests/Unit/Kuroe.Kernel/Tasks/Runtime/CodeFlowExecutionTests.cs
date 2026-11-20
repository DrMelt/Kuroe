using ErrorOr;
using Kuroe.Kernel.Control.Shared;
using Kuroe.Kernel.Execution;
using Kuroe.CodeFlow.Records.Shared;
using Kuroe.Kernel.Tasks.Persistence.Serialization.Shared;
using Kuroe.Kernel.Presentation.Shared;
using Kuroe.Kernel.Control.Runtime;
using Kuroe.Kernel.Tasks.Presentation.Shared;
using Kuroe.Kernel.Tasks.Records.Shared;
using Kuroe.Kernel.Tasks.Shared;
using Xunit;
using Kuroe.Kernel.Execution.Shared.Contract;
using Kuroe.Kernel.Execution.Shared.Identity;
using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.Runtime;
using Kuroe.Kernel.Execution.Shared.State;

namespace Kuroe.Kernel.Tasks.Runtime.Tests;

/// <summary>代码定义流程使用执行端口与中性契约工具：输入、输出、状态读取、无法完成与存档往返。</summary>
public sealed class CodeFlowExecutionTests
{
    private static readonly ExecutionPortName Input = new("输入");
    private static readonly ExecutionPortName Conclusion = new("结论");

    [Fact(DisplayName = "代码流程提交输出端口并读取其契约")]
    public async Task A_code_flow_submits_output_ports_and_reads_its_contract()
    {
        Harness harness = Harness.Create();
        IExecution execution = harness.Open();

        await Harness.RunAsync(
            execution,
            new PortRunnerFactory(Conclusion, "得出结论"));

        Assert.Equal(ExecutionState.Succeeded, execution.State);

        ExecutionStatus status = ExecutionStatus.Of(execution.Request, execution.Contract, execution.State);
        Assert.Empty(status.OutstandingOutputs);
        Assert.Equal("得出结论：目标", execution.Contract.Outputs![Conclusion].Text);

        TaskSnapshot snapshot = harness.Task.Snapshot();
        FlowStepView step = Assert.Single(snapshot.View.Steps);
        Assert.Contains(step.Outputs, port => port.Name == Conclusion.Value);
    }

    [Fact(DisplayName = "缺少运行器把执行标记为失败")]
    public async Task A_missing_runner_marks_the_execution_failed()
    {
        Harness harness = Harness.Create();
        IExecution execution = harness.Open();

        ErrorOr<Success> run = await Harness.RunAsync(execution, new ThrowingRunnerFactory());

        Assert.True(run.IsError);
        Assert.Equal(ExecutionState.Failed, execution.State);
    }

    [Fact(DisplayName = "代码流程报告无法完成并保留原因")]
    public async Task A_code_flow_reports_cannot_complete_and_keeps_the_reason()
    {
        Harness harness = Harness.Create();
        IExecution execution = harness.Open();

        await Harness.RunAsync(execution, new CannotCompleteRunnerFactory());

        ExecutionStatus status = ExecutionStatus.Of(execution.Request, execution.Contract, execution.State);
        Assert.Equal("缺少资料", status.CannotCompleteReason);
        Assert.Equal([Conclusion], status.OutstandingOutputs);
    }

    [Fact(DisplayName = "代码流程捕获并恢复自己的记录")]
    public void A_code_flow_captures_and_restores_its_own_records()
    {
        Harness harness = Harness.Create();
        _ = harness.Open();

        TaskRecord record = harness.Task.CaptureRecord();
        TaskRecordSerializationMapper mapper = TaskRecordCodecs.Mapper();
        TaskRecord loaded = mapper.FromDocument(mapper.ToDocument(record).ThrowIfError()).ThrowIfError();

        CodeFlowDefinitionRecord definition = Assert.IsType<CodeFlowDefinitionRecord>(loaded.Flow.Definition);
        CodeFlowStateRecord state = Assert.IsType<CodeFlowStateRecord>(loaded.Flow.State);
        Assert.Equal(CodeIdentity.Id, definition.Implementation);
        Assert.Equal("test", definition.Values["mode"]);
        Assert.Equal("test", state.Values["mode"]);
        Assert.Equal("目标", state.Inputs.Text(new PortName("目标")));
        Assert.Equal("目标", loaded.Flow.Identity.Name.Value);
        Assert.Single(loaded.Executions);
    }

    private sealed class Harness
    {
        private Harness(WorkTask task, ExecutionIndex index, ExecutionRuntime runtime)
        {
            Task = task;
            Index = index;
            Runtime = runtime;
        }

        public WorkTask Task { get; }

        public ExecutionIndex Index { get; }

        public ExecutionRuntime Runtime { get; }

        public static Harness Create()
        {
            ExecutionIndex index = new();
            ExecutionRuntime runtime = new(new ExecutionDispatcher());
            StartInputs inputs = StartInputs.Single(new PortName("目标"), "目标");
            FlowProvider provider = new(
                new CodeIdentity(),
                new CodeSubmission(),
                null,
                new CodeControls(),
                new CodePresentation(),
                new CodeRecords());
            WorkTask task = new(
                new TaskRegistry(index),
                runtime,
                new TaskId(1),
                inputs,
                FlowTexts.Empty,
                "代码任务",
                provider,
                new CodeState(inputs),
                definition: null,
                observer: null);

            return new Harness(task, index, runtime);
        }

        public IExecution Open()
        {
            ExecutionRequest request = new(
                new ExecutionOwnerId(Task.Id.Value),
                new ExecutionStepId("回话"),
                [new ExecutionInputPort(Input, ExecutionPortKind.Text, ExecutionPortRole.Content, true, null)],
                [new ExecutionInputValue(Input, "目标", null)],
                new Dictionary<ExecutionPortName, string> { [Input] = "目标" },
                [new ExecutionOutputPort(Conclusion, ExecutionPortKind.Text, true, false)]);
            IExecution execution = Index.Open(Task, request, Runtime.Create);
            Task.UnderLock(() =>
            {
                Task.AttachExecution(execution);

                return true;
            });

            return execution;
        }

        public static Task<ErrorOr<Success>> RunAsync(
            IExecution execution,
            IExecutionRunnerFactory factory) =>
            new ExecutionRuntime(new ExecutionDispatcher())
                .RunAsync(
                    execution,
                    new ExecutionInvocation(
                        execution.Request.Step,
                        factory,
                        EmptyExecutionParameters.Instance));
    }
}
internal sealed class CodeIdentity : IFlowIdentity
{
    public const string Id = "code-test";

    public string DisplayName => "代码流程";

    public bool Handles(FlowName name) => name.Value == "目标";

    public bool Handles(FlowIdentity identity) =>
        identity.Kind == CodeFlowKind.Value && identity.Implementation == Id;
}

internal sealed class CodeState(StartInputs inputs) : IFlowState
{
    public StartInputs Inputs { get; } = inputs;
}

internal sealed class CodeSubmission : IFlowSubmission
{
    public ErrorOr<FlowSubmission> Prepare(TaskId id, FlowName name, StartInputs inputs, string? title) =>
        throw new NotSupportedException();

    public ErrorOr<FlowRestore> Restore(TaskRecord record) => throw new NotSupportedException();
}

internal sealed class CodeControls : IFlowControlFactory
{
    public IFlowControl CreateControl(ITaskFlowContext task) => throw new NotSupportedException();
}

internal sealed class CodeRecords : IFlowRecordSource
{
    public FlowRecord CaptureRecord(ITaskFlowContext task)
    {
        CodeState state = Assert.IsType<CodeState>(task.FlowState);

        return new FlowRecord(
            new FlowIdentity(new FlowName("目标"), CodeFlowKind.Value, CodeIdentity.Id),
            new CodeFlowDefinitionRecord(
                CodeIdentity.Id,
                new Dictionary<string, string> { ["mode"] = "test" }),
            new CodeFlowStateRecord(
                state.Inputs,
                FlowTexts.Empty,
                new Dictionary<string, string> { ["mode"] = "test" }));
    }
}

internal sealed class CodePresentation : IFlowPresentation
{
    public IFlowView CaptureView(ITaskFlowView task) =>
        new FlowView(
            "目标",
            CodeFlowKind.Value,
            null,
            [],
            [
                new FlowStepView(
                    "回话",
                    "回话",
                    "code",
                    FlowItemState.Done,
                    [new FlowPortView("输入", "Text", null)],
                    [new FlowPortView("结论", "Text", null)],
                    [],
                    10),
            ],
            []);

}

internal sealed class PortRunnerFactory(
    ExecutionPortName port,
    string text) : IExecutionRunnerFactory
{
    public IExecutionRunner Create(
        IExecutionParameters parameters,
        IExecutionRequestEntry requests) =>
        new PortRunner(requests, port, text);

    private sealed class PortRunner(
        IExecutionRequestEntry requests,
        ExecutionPortName port,
        string text) : IExecutionRunner
    {
        public IProcessJournal Journal { get; } = new ExecutionJournal();

        public async Task<ErrorOr<ExecutionOutput>> ExecuteAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            ErrorOr<ExecutionStatus> submitted = requests.SubmitOutputs(
                [
                    new ExecutionOutputValue(
                        port,
                        $"{text}：{requests.Request.Texts[new ExecutionPortName("输入")]}"),
                ]);

            return submitted.IsError
                ? submitted.ErrorsOrEmptyList
                : ExecutionOutput.FromPorts([]);
        }
    }
}

internal sealed class ThrowingRunnerFactory : IExecutionRunnerFactory
{
    public IExecutionRunner Create(
        IExecutionParameters parameters,
        IExecutionRequestEntry requests) =>
        throw new InvalidOperationException("测试执行器工厂失败。");
}

internal sealed class CannotCompleteRunnerFactory : IExecutionRunnerFactory
{
    public IExecutionRunner Create(
        IExecutionParameters parameters,
        IExecutionRequestEntry requests) =>
        new Runner(requests);

    private sealed class Runner(IExecutionRequestEntry requests) : IExecutionRunner
    {
        public IProcessJournal Journal { get; } = new ExecutionJournal();

        public async Task<ErrorOr<ExecutionOutput>> ExecuteAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            ErrorOr<ExecutionStatus> reported = requests.ReportCannotComplete("缺少资料");

            return reported.IsError
                ? reported.ErrorsOrEmptyList
                : ExecutionOutput.FromPorts([]);
        }
    }
}
