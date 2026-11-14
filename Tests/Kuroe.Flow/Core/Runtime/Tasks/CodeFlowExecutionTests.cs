using ErrorOr;
using Kuroe.Control.Contract;
using Kuroe.Control.Shared;
using Kuroe.Execution.Shared;
using Kuroe.Execution;
using Kuroe.Flow.Persistence.Serialization.Shared;
using Kuroe.Presentation.Shared;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Tasks.Presentation.Shared;
using Kuroe.Tasks.Records.Shared;
using Kuroe.Tasks.Shared;
using Xunit;

namespace Kuroe.Flow.Tests.Runtime.Tasks;

/// <summary>代码定义流程使用执行端口与中性契约工具：输入、输出、状态读取、无法完成与存档往返。</summary>
public sealed class CodeFlowExecutionTests
{
    private static readonly ExecutionPortName Input = new("输入");
    private static readonly ExecutionPortName Conclusion = new("结论");

    [Fact]
    public async Task A_code_flow_submits_output_ports_and_reads_its_contract()
    {
        Harness harness = Harness.Create();
        IExecution execution = harness.Open();

        await harness.RunAsync(
            execution,
            new PortRunnerFactory(harness.Submitter, Conclusion, "得出结论"),
            new CodeTarget());

        Assert.Equal(ExecutionState.Succeeded, execution.State);

        ExecutionStatus status = harness.Status.Read(execution.Id).ThrowIfError();
        Assert.Empty(status.OutstandingOutputs);
        Assert.Equal(5, status.MaxAttempts);
        Assert.Equal("得出结论：目标", execution.Contract.Outputs![Conclusion].Text);

        TaskSnapshot snapshot = harness.Task.Snapshot();
        FlowStepView step = Assert.Single(snapshot.View.Steps);
        Assert.Contains(step.Outputs, port => port.Name == Conclusion.Value);
    }

    [Fact]
    public void A_code_flow_reports_cannot_complete_and_keeps_the_reason()
    {
        Harness harness = Harness.Create();
        IExecution execution = harness.Open();

        ExecutionStatus status = harness.CannotComplete.Report(execution.Id, "缺少资料").ThrowIfError();

        Assert.Equal("缺少资料", status.CannotCompleteReason);
        Assert.Equal([Conclusion], status.OutstandingOutputs);
    }

    [Fact]
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
            Submitter = new PortSubmitter(index, new ExecutionContractWriter());
            CannotComplete = new CannotCompleteSubmitter(index, new ExecutionContractWriter());
            Status = new ExecutionStatusReader(index);
        }

        public WorkTask Task { get; }

        public ExecutionIndex Index { get; }

        public ExecutionRuntime Runtime { get; }

        public PortSubmitter Submitter { get; }

        public CannotCompleteSubmitter CannotComplete { get; }

        public ExecutionStatusReader Status { get; }

        public static Harness Create()
        {
            ExecutionIndex index = new();
            ExecutionRuntime runtime = new(
                new ExecutionDispatcher(),
                new ExecutionRunnerRegistry(),
                new ExecutionRestorerRegistry());
            StartInputs inputs = StartInputs.Single(new PortName("目标"), "目标");
            FlowProvider provider = new(
                new CodeIdentity(),
                new CodeSubmission(),
                NoFlowStateBinder.Instance,
                new CodeControls(),
                new CodePresentation(),
                new CodeRecords(),
                NoFlowTemplates.Instance);
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
                new ExecutionTargetId("回话"),
                "code",
                1,
                [new ExecutionInputPort(Input, ExecutionPortKind.Text, ExecutionPortRole.Content, true, null)],
                [new ExecutionInputValue(Input, "目标", null)],
                new Dictionary<ExecutionPortName, string> { [Input] = "目标" },
                [new ExecutionOutputPort(Conclusion, ExecutionPortKind.Text, true, false)],
                MaxAttempts: 5);
            IExecution execution = Index.Open(Task, request, Runtime.Create);
            Task.UnderLock(() =>
            {
                Task.AttachExecution(execution);

                return true;
            });

            return execution;
        }

        public Task<ErrorOr<Success>> RunAsync(IExecution execution, IExecutionRunnerFactory runner, IExecutionTarget target) =>
            Runtime.RunAsync(execution, runner, target);
    }
}
internal sealed class CodeIdentity : IFlowIdentity
{
    public const string Id = "code-test";

    public string DisplayName => "代码流程";

    public bool Handles(FlowName name) => name.Value == "目标";

    public bool Handles(FlowIdentity identity) =>
        identity.Kind == FlowRecordKinds.Code && identity.Implementation == Id;
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
            new FlowIdentity(new FlowName("目标"), FlowRecordKinds.Code, CodeIdentity.Id),
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
    public IFlowView CaptureView(ITaskFlowContext task) =>
        new FlowView(
            "目标",
            FlowRecordKinds.Code,
            null,
            [],
            [
                new FlowStepView(
                    "回话",
                    "回话",
                    "code",
                    FlowNodeState.Done,
                    [new FlowPortView("输入", "Text", null)],
                    [new FlowPortView("结论", "Text", null)],
                    [],
                    10),
            ],
            []);

    public IReadOnlyList<FlowPendingStep> Pending(ITaskFlowContext task) => [];
}

internal sealed class CodeTarget : IExecutionTarget
{
    public ExecutionTargetId Id => new("回话");

    public string Kind => "code";

    public bool UsesDefaultRunner => false;

    public IExecutionParameters Parameters => EmptyExecutionParameters.Instance;
}

internal sealed class PortRunnerFactory(
    IExecutionPortSubmitter submitter,
    ExecutionPortName port,
    string text) : IExecutionRunnerFactory
{
    public string Kind => "code";

    public IExecutionRunner Create(IExecutionTarget target, ExecutionId execution) =>
        new PortRunner(submitter, execution, port, text);

    private sealed class PortRunner(
        IExecutionPortSubmitter submitter,
        ExecutionId execution,
        ExecutionPortName port,
        string text) : IExecutionRunner
    {
        public IProcessJournal Journal { get; } = new ExecutionJournal();

        public async Task<ErrorOr<ExecutionOutput>> ExecuteAsync(
            ExecutionRequest request,
            CancellationToken cancellationToken)
        {
            await Task.Yield();
            ErrorOr<ExecutionStatus> submitted = submitter.Submit(
                execution,
                [new ExecutionOutputValue(port, $"{text}：{request.Texts[new ExecutionPortName("输入")]}")]);

            return submitted.IsError
                ? submitted.ErrorsOrEmptyList
                : ExecutionOutput.FromPorts([]);
        }
    }
}