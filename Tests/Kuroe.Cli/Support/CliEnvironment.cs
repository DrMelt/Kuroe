using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;
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
        File.WriteAllText(Path.Combine(config, "flows.json"), flowsJson);

        ErrorOr<CliSession> created = CliSession.Create(
            root,
            services => services.AddSingleton<IExecutionRunner>(new StubRunner()));
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
internal sealed class StubRunner : IExecutionRunner
{
    public string Text { get; set; } = "输出";

    public IExecutionBody CreateBody(ExecutionInput input) => new EmptyBody();

    public async Task<ErrorOr<Success>> ExecuteAsync(
        IExecution execution,
        IExecutionBody body,
        CancellationToken cancellationToken)
    {
        await Task.Delay(5, cancellationToken);
        execution.Sink.OnText(Text);

        return Result.Success;
    }

    private sealed class EmptyBody : IExecutionBody
    {
        public IProcessJournal Journal { get; } = new EmptyJournal();

        private sealed class EmptyJournal : IProcessJournal
        {
            public IReadOnlyList<JournalEntry> Entries => [];

            public int DroppedEntries => 0;
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