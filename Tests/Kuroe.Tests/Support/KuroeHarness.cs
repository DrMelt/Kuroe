using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Configuration.Files;
using Kuroe.Shared;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Runs;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.Waiting;
using Kuroe.Workflows.Execution;
using Kuroe.Flow.Execution.Tasks;
using Kuroe.Waiting;
using Kuroe.Workflows.Flows;
using Microsoft.Extensions.DependencyInjection;

namespace Kuroe.Tests.Support;

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
        Waits = provider.GetRequiredService<WaitService>();
        executor.Submitter = provider.GetRequiredService<WorkItemSubmitter>();
        executor.PortSubmitter = provider.GetRequiredService<PortSubmitter>();
        executor.StatusSubmitter = provider.GetRequiredService<StatusSubmitter>();
    }

    /// <summary>临时工作目录。</summary>
    public string Root { get; }

    public FakeExecutor Executor { get; }

    public TaskRegistry Registry { get; }

    public TaskService Tasks { get; }

    /// <summary>等待型节点的宿主功能，回答与批准经它交付外部输入。</summary>
    public WaitService Waits { get; }

    /// <summary>常驻对话任务的管理层宿主。</summary>
    public DialogueHost Dialogue => _provider.GetRequiredService<DialogueHost>();

    public SettingsProvider Settings => _provider.GetRequiredService<SettingsProvider>();

    public FlowService Flows => _provider.GetRequiredService<FlowService>();

    public WorkItemSubmitter Submitter => _provider.GetRequiredService<WorkItemSubmitter>();

    public CatalogService Catalog => _provider.GetRequiredService<CatalogService>();

    public ModelService Models => _provider.GetRequiredService<ModelService>();

    public IReadOnlyList<Kuroe.Workflows.Tools.Command.Model.CommandToolDefinition> CommandTools =>
        _provider.GetRequiredService<IReadOnlyList<Kuroe.Workflows.Tools.Command.Model.CommandToolDefinition>>();

    /// <summary>测试用默认流程文件，模型选择显式指向 fake，供无参数装配的执行测试使用。</summary>
    private const string TestDefaultFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "制定计划", "Kind": "Executable", "Model": "规划者", "Prompt": "把目标拆成可独立实施的条目，逐项给出标题、要做什么和验收标准。", "Outputs": [{ "Name": "拆分", "Type": "WorkItems" }], "From": [ { "目标": "Start@目标" } ] },
                    { "Name": "交付", "Kind": "Container", "Nodes": [
                      { "Name": "分配执行", "Kind": "Executable", "Model": "执行者", "Tools": ["GetLocalTime"], "Mode": "PerItem", "From": [ { "拆分": "制定计划@拆分" } ] }
                    ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>装配失败时返回错误，用于校验类断言。未给流程文件时写入测试默认流程，flowsJson 传空对象即空流程库，
    /// defaultFlow 为 null 时不写入用户层默认流程。</summary>
    public static ErrorOr<KuroeHarness> TryCreate(string? flowsJson = null, bool seedCatalog = true, string? defaultFlow = "默认")
    {
        string root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);

        KuroePaths paths = KuroePaths.At(root);
        string? flowsText = flowsJson ?? TestDefaultFlow;
        if (flowsText is not null)
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(paths.FlowsFile)!);
            File.WriteAllText(paths.FlowsFile, flowsText);
        }

        var services = new ServiceCollection();
        services.AddLogging();
        var executor = new FakeExecutor();

        ErrorOr<KuroeStartup> startup = services.AddKuroe(paths, Path.Combine(root, "source"));
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
            catalog.AddModel(ModelName.Create("fake").Value, ProviderName.Create("test").Value).ThrowIfError();
            provider.GetRequiredService<ModelService>().Select(ModelName.Create("fake").Value).ThrowIfError();
        }

        if (defaultFlow is not null)
        {
            provider.GetRequiredService<SettingsProvider>().SetText("Runtime:DefaultFlow", defaultFlow).ThrowIfError();
        }

        return new KuroeHarness(root, provider, executor);
    }

    public static KuroeHarness Create(string? flowsJson = null, bool seedCatalog = true, string? defaultFlow = "默认") =>
        TryCreate(flowsJson, seedCatalog, defaultFlow).ThrowIfError();

    public TaskId Submit(string goal, Kuroe.Flow.Shared.StaticGraph.FlowName? flow = null) =>
        Tasks.Submit(goal, flow, null).ThrowIfError().Id;

    public TaskSnapshot Snapshot(TaskId id) => Registry.Find(id).ThrowIfError().Snapshot();

    /// <summary>执行节点状态摘要，用于超时信息里的定位。</summary>
    private static string NodeStates(TaskSnapshot snapshot) =>
        string.Join("、", snapshot.ExecutableStates.Select(state =>
            $"{state.Index} {state.State}" + (state.Items.Count > 0 ? $" {state.CompletedItems}/{state.Items.Count}" : string.Empty)));

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
                throw new TimeoutException($"任务 {id} 未在 {milliseconds} 毫秒内满足条件，状态 {snapshot.State}、执行节点 [{NodeStates(snapshot)}]。");
            }

            Thread.Sleep(20);
        }
    }

    /// <summary>等执行侧不再有任何在跑或待推进的 run。停在等待外部输入时要求连续两次采样一致，避免输入信号落地前的瞬态。</summary>
    public TaskSnapshot Settle(TaskId id)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(5000);
        bool previous = false;
        while (true)
        {
            TaskSnapshot snapshot = Snapshot(id);
            if (snapshot.LiveRuns == 0
                && snapshot.State is TaskState.Done or TaskState.Blocked or TaskState.Canceled)
            {
                return snapshot;
            }

            bool waiting = snapshot.LiveRuns == 0 && Awaiting(id);
            if (waiting && previous)
            {
                return snapshot;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"任务 {id} 未在超时内稳定，状态 {snapshot.State}、LiveRuns {snapshot.LiveRuns}、执行节点 [{NodeStates(snapshot)}]。");
            }

            previous = waiting;
            Thread.Sleep(20);
        }
    }

    /// <summary>任务里是否有停在等待的节点。</summary>
    public bool Awaiting(TaskId id) =>
        Waits.Pending(id) is { IsError: false } pending && pending.Value.Count > 0;

    /// <summary>任务里停在给定等待种类的节点序号。</summary>
    public IReadOnlyList<int> Awaiting(TaskId id, AwaitKind kind) =>
        Waits.Pending(id) is { IsError: false } pending
            ? [.. pending.Value.Where(node => node.Kind == kind).Select(node => node.Index)]
            : [];

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
            // 临时目录可能已被清理或占用，收尾不再上报
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
