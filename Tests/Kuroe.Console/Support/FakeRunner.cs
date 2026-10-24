using ErrorOr;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Runtime.Contract;

namespace Kuroe.Console.Tests.Support;

/// <summary>不接模型的执行器：按节点产出要求交回端口取值，并按声明收口。</summary>
public sealed class FakeRunner : IExecutionRunner
{
    /// <summary>需交回端口节点的交回通道，端口节点由此提交命名段。</summary>
    public PortSubmitter? PortSubmitter { get; set; }

    /// <summary>单个执行的人为耗时，用于取消断言。</summary>
    public int DelayMs { get; set; } = 10;

    /// <summary>按声明的端口生成提交取值：Text 语义端口给同名校验文本，Signal 端口只给端口名。</summary>
    private static Dictionary<string, PortValue> PortValuesFor(IReadOnlyList<Port> ports) =>
        ports.ToDictionary(
            port => port.Name.Value,
            port => port.Type.Semantics == PortSemantics.Text
                ? new PortValue($"{port.Name.Value}产出")
                : new PortValue(null));

    public IExecutionBody CreateBody(ExecutionInput input) => new FakeBody();

    public async Task<ErrorOr<Success>> ExecuteAsync(IExecution execution, IExecutionBody body, CancellationToken cancellationToken)
    {
        await Task.Delay(DelayMs, cancellationToken);

        IReadOnlyList<Port> ports = execution.Input.RequiredPorts;
        if (ports.Count > 0)
        {
            _ = PortSubmitter!.SubmitValues(execution.Id, PortValuesFor(ports));
        }

        execution.Sink.OnText($"{execution.Input.Node}产出");

        return Result.Success;
    }

    /// <summary>测试用的执行体：本组测试不看过程记录。</summary>
    private sealed class FakeBody : IExecutionBody
    {
        IProcessJournal IExecutionBody.Journal { get; } = new EmptyJournal();

        private sealed class EmptyJournal : IProcessJournal
        {
            public IReadOnlyList<JournalEntry> Entries => [];

            public int DroppedEntries => 0;
        }
    }
}
