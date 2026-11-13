using ErrorOr;
using Kuroe.Execution.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.TestSupport;
using NeutralExecutionId = Kuroe.Execution.Shared.ExecutionId;
using NeutralExecutionOutput = Kuroe.Execution.Shared.ExecutionOutput;
using NeutralExecutionOutputPort = Kuroe.Execution.Shared.ExecutionOutputPort;
using NeutralExecutionOutputValue = Kuroe.Execution.Shared.ExecutionOutputValue;
using NeutralExecutionPortName = Kuroe.Execution.Shared.ExecutionPortName;
using NeutralExecutionRequest = Kuroe.Execution.Shared.ExecutionRequest;
using NeutralExecutionRunner = Kuroe.Execution.Shared.IExecutionRunner;
using NeutralExecutionRunnerFactory = Kuroe.Execution.Shared.IExecutionRunnerFactory;
using NeutralExecutionTarget = Kuroe.Execution.Shared.IExecutionTarget;
using NeutralPortKind = Kuroe.Execution.Shared.ExecutionPortKind;

namespace Kuroe.Console.Tests.Support;

/// <summary>不接模型的执行器：按节点产出要求交回端口取值，并按声明收口。</summary>
public sealed class FakeRunner : NeutralExecutionRunnerFactory
{
    /// <inheritdoc/>
    public string Kind => "*";

    /// <summary>需交回端口节点的交回通道，端口节点由此提交命名段。</summary>
    public IExecutionPortSubmitter? PortSubmitter { get; set; }

    /// <summary>执行闸门：替身执行器在此登记进入并保持，测试等待进入并放行。</summary>
    public IExecutionGate Gate { get; } = new ExecutionGate();

    /// <summary>是否交回声明端口，关闭用于制造未收口阻塞。</summary>
    public bool SubmitsPorts { get; set; } = true;

    /// <summary>按声明的端口生成提交取值：Text 语义端口给同名校验文本，Signal 端口只给端口名。</summary>
    private static IReadOnlyList<NeutralExecutionOutputValue> PortValuesFor(
        IReadOnlyList<NeutralExecutionOutputPort> ports) =>
        [
            .. ports.Select(port => new NeutralExecutionOutputValue(
                new NeutralExecutionPortName(port.Name.Value),
                port.Kind == NeutralPortKind.Text ? $"{port.Name.Value}产出" : null,
                true)),
        ];

    public NeutralExecutionRunner Create(NeutralExecutionTarget target, NeutralExecutionId execution) =>
        new Run(this, execution);

    private async Task<ErrorOr<NeutralExecutionOutput>> ExecuteAsync(
        NeutralExecutionId execution,
        NeutralExecutionRequest request,
        OutputJournal journal,
        CancellationToken cancellationToken)
    {
        NodePath node = new(
            [.. request.Target.Value
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(level => new NodeLabel(Uri.UnescapeDataString(level)))]);
        ExecutionHandle handle = Gate.Arrive(execution, node, request.Attempt);
        try
        {
            await Gate.WaitOpen(handle).WaitAsync(cancellationToken);

            IReadOnlyList<NeutralExecutionOutputPort> ports =
                [.. request.OutputPorts.Where(port => port.Required)];
            if (ports.Count > 0 && SubmitsPorts)
            {
                IExecutionPortSubmitter submitter = PortSubmitter
                    ?? throw new InvalidOperationException("测试环境没有提供端口提交器。");
                _ = submitter.Submit(execution, PortValuesFor(ports));
            }

            journal.AppendContent(ProcessContentKind.Reply, $"{node}产出");

            return NeutralExecutionOutput.FromPorts([]);
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
    private sealed class Run(FakeRunner owner, NeutralExecutionId execution) : NeutralExecutionRunner
    {
        private readonly OutputJournal _journal = new();

        public IProcessJournal Journal => _journal;

        public Task<ErrorOr<NeutralExecutionOutput>> ExecuteAsync(
            NeutralExecutionRequest request,
            CancellationToken cancellationToken) =>
            owner.ExecuteAsync(execution, request, _journal, cancellationToken);
    }
}
