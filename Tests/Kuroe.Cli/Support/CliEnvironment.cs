using System.Text.Json;
using ApiHub.Shared.Models;
using ErrorOr;

using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Microsoft.Extensions.DependencyInjection;

namespace Kuroe.Cli.Tests.Support;

/// <summary>在临时工作目录里装配一次 CLI 会话，用替身执行器驱动流程。</summary>
internal sealed class CliEnvironment : IAsyncDisposable
{
    private CliEnvironment(string root, CliSession session, StubRunner runner)
    {
        Root = root;
        Session = session;
        Runner = runner;
    }

    /// <summary>临时工作目录。</summary>
    public string Root { get; }

    /// <summary>本次测试的 CLI 会话。</summary>
    public CliSession Session { get; }

    /// <summary>替身执行器。</summary>
    public StubRunner Runner { get; }

    /// <summary>按给定流程文件装配会话，并注册一个已选中的 fake 模型。顶层同时给出 HostStartInputs 时按宿主开始输入表另存一个文件。</summary>
    public static CliEnvironment Create(string flowsJson)
    {
        string root = Path.Combine(Path.GetTempPath(), "kuroe-cli-tests", Guid.NewGuid().ToString("N"));
        string config = Path.Combine(root, ".kuroe");
        Directory.CreateDirectory(config);
        WriteFlowFiles(config, flowsJson);

        StubRunner runner = new();
        ErrorOr<CliSession> created = CliSession.Create(
            root,
            services => services.AddSingleton<IExecutionRunnerFactory>(runner));
        if (created.IsError)
        {
            throw new InvalidOperationException(string.Join('；', created.ErrorsOrEmptyList.Select(error => error.Description)));
        }

        CliSession session = created.Value;
        session.Catalog.AddProvider("test", "https://example.invalid/v1", "key").ThrowIfError();
        session.Catalog.AddModel(
            ModelName.Create("fake").Value,
            ProviderName.Create("test").Value,
            ProviderModelName.Create("fake").Value).ThrowIfError();
        session.Models.Select(ModelName.Create("fake").Value).ThrowIfError();

        return new CliEnvironment(root, session, runner);
    }

    /// <summary>把测试夹具按顶层字段写成流程文件与宿主开始输入表，其余内容整体写进流程文件。</summary>
    private static void WriteFlowFiles(string config, string text)
    {
        string flowsFile = Path.Combine(config, "flows.json");
        try
        {
            using JsonDocument document = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
            JsonElement root = document.RootElement;
            bool written = false;
            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in root.EnumerateObject())
                {
                    if (string.Equals(property.Name, "Flows", StringComparison.OrdinalIgnoreCase))
                    {
                        File.WriteAllText(flowsFile, $"{{\"Flows\":{property.Value.GetRawText()}}}");
                        written = true;
                    }
                    else if (string.Equals(property.Name, "HostStartInputs", StringComparison.OrdinalIgnoreCase))
                    {
                        File.WriteAllText(
                            Path.Combine(config, "hostStartInputs.json"),
                            $"{{\"HostStartInputs\":{property.Value.GetRawText()}}}");
                        written = true;
                    }
                }
            }

            if (!written)
            {
                File.WriteAllText(flowsFile, text);
            }
        }
        catch (JsonException)
        {
            File.WriteAllText(flowsFile, text);
        }
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

    /// <summary>执行器上报的模型请求用量，不设置时表示没有请求。</summary>
    public ExecutionUsage Usage { get; set; } = ExecutionUsage.None;

    public IExecutionRunner Create(ExecutableNode node, ExecutionId execution) => new Run(this);

    /// <summary>一次执行的运行器：过程记录随执行建立，运行委托给共享的替身状态。</summary>
    private sealed class Run(StubRunner owner) : IExecutionRunner, IExecutionUsageReader
    {
        private readonly StubJournal _journal = new();

        public IProcessJournal Journal => _journal;

        public ExecutionUsage Usage => owner.Usage;

        public Task<ErrorOr<ExecutionOutput>> ExecuteAsync(ExecutionInput input, CancellationToken cancellationToken)
        {
            _journal.AppendContent(ProcessContentKind.Reply, owner.Text);

            return Task.FromResult<ErrorOr<ExecutionOutput>>(ExecutionOutput.Ports);
        }
    }

    /// <summary>测试用的过程记录：保存条目并播发内容。</summary>
    private sealed class StubJournal : IProcessJournal
    {
        private static readonly DateTimeOffset At = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

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

            Append(new JournalEntry(At, JournalLevel.Information, label, delta));
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