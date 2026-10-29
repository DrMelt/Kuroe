using ApiHub.Shared.Models;
using ErrorOr;

using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace Kuroe.Cli.Tests.Support;

/// <summary>在临时工作目录里装配一次 CLI 会话，用替身执行器驱动流程。</summary>
internal sealed class CliEnvironment : IAsyncDisposable
{
    private CliEnvironment(string root, CliSession session)
    {
        Root = root;
        Session = session;
    }

    /// <summary>临时工作目录。</summary>
    public string Root { get; }

    /// <summary>本次测试的 CLI 会话。</summary>
    public CliSession Session { get; }

    /// <summary>按给定流程文件装配会话，并注册一个已选中的 fake 模型。</summary>
    public static CliEnvironment Create(string flowsJson)
    {
        string root = Path.Combine(Path.GetTempPath(), "kuroe-cli-tests", Guid.NewGuid().ToString("N"));
        string config = Path.Combine(root, ".kuroe");
        Directory.CreateDirectory(config);
        File.WriteAllText(Path.Combine(config, "flows.json"), LegacyFlow.Normalize(flowsJson));

        ErrorOr<CliSession> created = CliSession.Create(
            root,
            services => services.AddSingleton<IExecutionRunnerFactory>(new StubRunner()));
        if (created.IsError)
        {
            throw new InvalidOperationException(string.Join('；', created.ErrorsOrEmptyList.Select(error => error.Description)));
        }

        CliSession session = created.Value;
        session.Catalog.AddProvider("test", "https://example.invalid/v1", "key").ThrowIfError();
        session.Catalog.AddModel(ModelName.Create("fake").Value, ProviderName.Create("test").Value).ThrowIfError();
        session.Models.Select(ModelName.Create("fake").Value).ThrowIfError();

        return new CliEnvironment(root, session);
    }

    public async ValueTask DisposeAsync()
    {
        await Session.DisposeAsync();

        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // 临时目录可能已被清理或占用，收尾不再上报
        }
    }
}

/// <summary>不接模型的执行器：只播发固定文本，不提交端口产出。</summary>
internal sealed class StubRunner : IExecutionRunnerFactory
{
    /// <summary>每次执行播发的文本。</summary>
    public string Text { get; set; } = "输出";

    public IExecutionRunner Create(ExecutionId execution) => new Run(this);

    private async Task<ErrorOr<Success>> ExecuteAsync(
        StubJournal journal,
        CancellationToken cancellationToken)
    {
        await Task.Delay(5, cancellationToken);
        journal.AppendText(Text);

        return Result.Success;
    }

    /// <summary>一次执行的运行器：过程记录随执行建立，运行委托给共享的替身状态。</summary>
    private sealed class Run(StubRunner owner) : IExecutionRunner
    {
        private readonly StubJournal _journal = new();

        public IProcessJournal Journal => _journal;

        public Task<ErrorOr<Success>> ExecuteAsync(ExecutionInput input, CancellationToken cancellationToken) =>
            owner.ExecuteAsync(_journal, cancellationToken);
    }

    /// <summary>测试用的过程记录：保存条目并播发文本。</summary>
    private sealed class StubJournal : IProcessJournal
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
}

internal static class ErrorOrExtensions
{
    public static T ThrowIfError<T>(this ErrorOr<T> result) =>
        result.IsError
            ? throw new InvalidOperationException(string.Join('；', result.ErrorsOrEmptyList.Select(error => error.Description)))
            : result.Value;

    public static void ThrowIfError(this ErrorOr<Success> result)
    {
        if (result.IsError)
        {
            throw new InvalidOperationException(string.Join('；', result.ErrorsOrEmptyList.Select(error => error.Description)));
        }
    }
}