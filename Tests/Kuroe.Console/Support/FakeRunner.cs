using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Kuroe.Flow.Runtime.Contract;
using Kuroe.Flow.TestSupport;

namespace Kuroe.Console.Tests.Support;

/// <summary>不接模型的执行器：按节点产出要求交回端口取值，并按声明收口。</summary>
public sealed class FakeRunner : IExecutionRunnerFactory
{
    /// <summary>需交回端口节点的交回通道，端口节点由此提交命名段。</summary>
    public PortSubmitter? PortSubmitter { get; set; }

    /// <summary>执行闸门：替身执行器在此登记进入并保持，测试等待进入并放行。</summary>
    public IExecutionGate Gate { get; } = new ExecutionGate();

    /// <summary>是否交回声明端口，关闭用于制造未收口阻塞。</summary>
    public bool SubmitsPorts { get; set; } = true;

    /// <summary>按声明的端口生成提交取值：Text 语义端口给同名校验文本，Signal 端口只给端口名。</summary>
    private static Dictionary<string, PortValue> PortValuesFor(IReadOnlyList<Port> ports) =>
        ports.ToDictionary(
            port => port.Name.Value,
            port => port.Type.Semantics == PortSemantics.Text
                ? new PortValue($"{port.Name.Value}产出")
                : new PortValue(null));

    public IExecutionRunner Create(ExecutableNode node, ExecutionId execution) =>
        new Run(this, execution);

    private async Task<ErrorOr<ExecutionOutput>> ExecuteAsync(
        ExecutionId execution,
        ExecutionInput input,
        OutputJournal journal,
        CancellationToken cancellationToken)
    {
        ExecutionHandle handle = Gate.Arrive(execution, input.Node, input.ExecutionCount);
        try
        {
            await Gate.WaitOpen(handle).WaitAsync(cancellationToken);

            IReadOnlyList<Port> ports = input.RequiredPorts;
            if (ports.Count > 0 && SubmitsPorts)
            {
                PortSubmitter submitter = PortSubmitter
                    ?? throw new InvalidOperationException("测试环境没有提供端口提交器。");
                _ = submitter.SubmitValues(execution, PortValuesFor(ports));
            }

            journal.AppendContent(ProcessContentKind.Reply, $"{input.Node}产出");

            return ExecutionOutput.Ports;
        }
        finally
        {
            Gate.Leave(handle);
        }
    }

    /// <summary>测试用的过程记录：保存条目并播发内容。</summary>
    private sealed class OutputJournal : IProcessJournal
    {
        private readonly Lock _gate = new();
        private readonly List<JournalEntry> _entries = [];
        private JournalEntryHandler? _entryAppended;
        private ProgressTextHandler? _progressReported;

        /// <summary>流式内容追加后给出该增量与种类。</summary>
        public event ProcessContentHandler? ContentAppended;

        /// <summary>需要即时呈现的条目写入后给出该条目。</summary>
        public event JournalEntryHandler? EntryAppended
        {
            add => _entryAppended += value;
            remove => _entryAppended -= value;
        }

        /// <summary>执行进度变化后给出进度文本。</summary>
        public event ProgressTextHandler? ProgressReported
        {
            add => _progressReported += value;
            remove => _progressReported -= value;
        }

        /// <summary>追加一条过程记录。</summary>
        public void Append(JournalEntry entry)
        {
            lock (_gate)
            {
                _entries.Add(entry);
            }
        }

        /// <summary>追加一段流式内容，随后播发该增量。</summary>
        public void AppendContent(ProcessContentKind kind, string delta)
        {
            if (delta.Length == 0)
            {
                return;
            }

            string label = kind switch
            {
                ProcessContentKind.Reply => "回复",
                ProcessContentKind.Reasoning => "思考",
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };

            Append(new JournalEntry(TestTime.At, JournalLevel.Information, label, delta));
            ContentAppended?.Invoke(new ProcessContent(kind, delta));
        }

        public IReadOnlyList<JournalEntry> Entries
        {
            get
            {
                lock (_gate)
                {
                    return [.. _entries];
                }
            }
        }
    }

    /// <summary>一次执行的运行器：过程记录随执行建立，运行委托给共享的假执行器状态。</summary>
    private sealed class Run(FakeRunner owner, ExecutionId execution) : IExecutionRunner
    {
        private readonly OutputJournal _journal = new();

        public IProcessJournal Journal => _journal;

        public Task<ErrorOr<ExecutionOutput>> ExecuteAsync(ExecutionInput input, CancellationToken cancellationToken) =>
            owner.ExecuteAsync(execution, input, _journal, cancellationToken);
    }
}
