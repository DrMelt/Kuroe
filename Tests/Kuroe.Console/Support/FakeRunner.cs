using ErrorOr;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Runtime.Contract;

namespace Kuroe.Console.Tests.Support;

/// <summary>不接模型的执行器：按节点产出要求交回端口取值，并按声明收口。</summary>
public sealed class FakeRunner : IExecutionRunnerFactory
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

    public IExecutionRunner Create(ExecutionId execution) => new Run(this, execution);

    private async Task<ErrorOr<Success>> ExecuteAsync(
        ExecutionId execution,
        ExecutionInput input,
        OutputJournal journal,
        CancellationToken cancellationToken)
    {
        await Task.Delay(DelayMs, cancellationToken);

        IReadOnlyList<Port> ports = input.RequiredPorts;
        if (ports.Count > 0)
        {
            _ = PortSubmitter!.SubmitValues(execution, PortValuesFor(ports));
        }

        journal.AppendText($"{input.Node}产出");

        return Result.Success;
    }

    /// <summary>测试用的过程记录：保存条目并播发文本。</summary>
    private sealed class OutputJournal : IProcessJournal
    {
        private readonly Lock _gate = new();
        private readonly List<JournalEntry> _entries = [];
        private Action<JournalEntry>? _entryAppended;
        private Action<string>? _progressReported;

        /// <summary>文本追加后给出该增量。</summary>
        public event Action<string>? TextAppended;

        /// <summary>需要即时呈现的条目写入后给出该条目。</summary>
        public event Action<JournalEntry>? EntryAppended
        {
            add => _entryAppended += value;
            remove => _entryAppended -= value;
        }

        /// <summary>执行进度变化后给出进度文本。</summary>
        public event Action<string>? ProgressReported
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

        /// <summary>追加文本增量，随后播发该增量。</summary>
        public void AppendText(string delta)
        {
            if (delta.Length == 0)
            {
                return;
            }

            Append(new JournalEntry(DateTimeOffset.UtcNow, JournalLevel.Information, "回复", delta));
            TextAppended?.Invoke(delta);
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

        public int DroppedEntries => 0;
    }

    /// <summary>一次执行的运行器：过程记录随执行建立，运行委托给共享的假执行器状态。</summary>
    private sealed class Run(FakeRunner owner, ExecutionId execution) : IExecutionRunner
    {
        private readonly OutputJournal _journal = new();

        public IProcessJournal Journal => _journal;

        public Task<ErrorOr<Success>> ExecuteAsync(ExecutionInput input, CancellationToken cancellationToken) =>
            owner.ExecuteAsync(execution, input, _journal, cancellationToken);
    }
}
