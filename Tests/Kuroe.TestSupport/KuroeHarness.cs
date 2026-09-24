using ErrorOr;
using Kuroe.Agent.Runs;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Shared;
using Kuroe.Shared.Agent;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.Workflows.Flows;
using Kuroe.Workflows.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Kuroe.TestSupport;

/// <summary>在临时工作目录里装配一套 Kuroe，用假执行器驱动流程。</summary>
public sealed class KuroeHarness : IDisposable
{
    private readonly ServiceProvider _provider;

    private KuroeHarness(string root, ServiceProvider provider, FakeExecutor executor)
    {
        Root = root;
        _provider = provider;
        Executor = executor;
        Registry = provider.GetRequiredService<TaskRegistry>();
        Tasks = provider.GetRequiredService<TaskService>();
        executor.Submitter = provider.GetRequiredService<UnitSubmitter>();
    }

    /// <summary>临时工作目录。</summary>
    public string Root { get; }

    public FakeExecutor Executor { get; }

    public TaskRegistry Registry { get; }

    public TaskService Tasks { get; }

    public SettingsProvider Settings => _provider.GetRequiredService<SettingsProvider>();

    public WorkflowService Flows => _provider.GetRequiredService<WorkflowService>();

    public UnitSubmitter Submitter => _provider.GetRequiredService<UnitSubmitter>();

    public CatalogService Catalog => _provider.GetRequiredService<CatalogService>();

    public ModelService Models => _provider.GetRequiredService<ModelService>();

    /// <summary>装配失败时返回错误，用于校验类断言。</summary>
    public static ErrorOr<KuroeHarness> TryCreate(string? flowsJson = null, bool seedCatalog = true)
    {
        string root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);
        if (flowsJson is not null)
        {
            File.WriteAllText(Path.Combine(root, "flows.json"), flowsJson);
        }

        var services = new ServiceCollection();
        services.AddLogging();
        var executor = new FakeExecutor();

        ErrorOr<KuroeStartup> startup = services.AddKuroe(KuroePaths.At(root));
        if (startup.IsError)
        {
            return startup.ErrorsOrEmptyList;
        }

        // 后注册的同类型解析时胜出，据此把真实执行器换成假的
        services.AddSingleton<IRunExecutor>(executor);
        ServiceProvider provider = services.BuildServiceProvider();

        if (seedCatalog)
        {
            CatalogService catalog = provider.GetRequiredService<CatalogService>();
            catalog.AddProvider("test", "https://example.invalid/v1", "key").ThrowIfError();
            catalog.AddModel("fake", "test").ThrowIfError();
            provider.GetRequiredService<ModelService>().Select("fake").ThrowIfError();
        }

        return new KuroeHarness(root, provider, executor);
    }

    public static KuroeHarness Create(string? flowsJson = null, bool seedCatalog = true) =>
        TryCreate(flowsJson, seedCatalog).ThrowIfError();

    public TaskId Submit(string goal, string? flow = null) =>
        Tasks.Submit(goal, flow, null).ThrowIfError().Id;

    public TaskSnapshot Snapshot(TaskId id) => Registry.Find(id).ThrowIfError().Snapshot();

    /// <summary>等到任务满足条件为止，超时即失败。</summary>
    public TaskSnapshot Wait(TaskId id, Func<TaskSnapshot, bool> condition, int milliseconds = 5000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (true)
        {
            TaskSnapshot snapshot = Snapshot(id);
            if (condition(snapshot))
            {
                return snapshot;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"任务 {id} 未在 {milliseconds} 毫秒内满足条件，当前状态 {snapshot.State}。");
            }

            Thread.Sleep(20);
        }
    }

    /// <summary>等执行侧不再有任何在跑或待推进的 agent。</summary>
    public TaskSnapshot Settle(TaskId id) => Wait(id, snapshot =>
        snapshot.LiveRuns == 0 && snapshot.State is TaskState.Done or TaskState.Blocked or TaskState.Canceled
            or TaskState.AwaitingApproval);

    public void Dispose()
    {
        Tasks.ShutdownAsync().GetAwaiter().GetResult();
        _provider.Dispose();

        try
        {
            System.IO.Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

public static class ErrorOrExtensions
{
    /// <summary>出错时把全部错误说明拼进异常信息。</summary>
    public static T ThrowIfError<T>(this ErrorOr<T> result) =>
        result.IsError
            ? throw new InvalidOperationException(string.Join("；", result.ErrorsOrEmptyList.Select(error => error.Description)))
            : result.Value;

    public static void ThrowIfError(this ErrorOr<Success> result)
    {
        if (result.IsError)
        {
            throw new InvalidOperationException(string.Join("；", result.ErrorsOrEmptyList.Select(error => error.Description)));
        }
    }
}
