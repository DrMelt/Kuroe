using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Executions.Runs;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Shared;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.Workflows;
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
        executor.Submitter = provider.GetRequiredService<PlanSubmitter>();
        executor.PortSubmitter = provider.GetRequiredService<PortSubmitter>();
    }

    /// <summary>临时工作目录。</summary>
    public string Root { get; }

    public FakeExecutor Executor { get; }

    public TaskRegistry Registry { get; }

    public TaskService Tasks { get; }

    /// <summary>常驻对话任务的管理层宿主。</summary>
    public DialogueHost Dialogue => _provider.GetRequiredService<DialogueHost>();

    public SettingsProvider Settings => _provider.GetRequiredService<SettingsProvider>();

    public FlowService Flows => _provider.GetRequiredService<FlowService>();

    public PlanSubmitter Submitter => _provider.GetRequiredService<PlanSubmitter>();

    public CatalogService Catalog => _provider.GetRequiredService<CatalogService>();

    public ModelService Models => _provider.GetRequiredService<ModelService>();

    public IReadOnlyList<Kuroe.Tools.CommandTools.CommandToolDefinition> CommandTools =>
        _provider.GetRequiredService<IReadOnlyList<Kuroe.Tools.CommandTools.CommandToolDefinition>>();

    /// <summary>测试默认流程：结构同内置「默认」流程，模型选择显式指向 fake，供无参数装配的执行测试使用。</summary>
    private const string TestDefaultFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" }
              ],
              "Nodes": [
                {
                  "Name": "整体",
                  "Nodes": [
                    { "Name": "制定计划", "Model": "规划者", "Output": "Plan", "Prompt": "把目标拆成可独立实施的条目，逐项给出标题、要做什么和验收标准。" },
                    { "Name": "交付", "Nodes": [
                      { "Name": "分配执行", "Model": "执行者", "Tools": ["GetLocalTime"], "Mode": "PerItem", "From": ["制定计划"] }
                    ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>装配失败时返回错误，用于校验类断言。未给流程文件时写入与内置流程同形的测试默认流程，
    /// writeDefaultFlow 为 false 时不写文件，走真实内置流程。</summary>
    public static ErrorOr<KuroeHarness> TryCreate(string? flowsJson = null, bool seedCatalog = true, bool writeDefaultFlow = true)
    {
        string root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);

        KuroePaths paths = KuroePaths.At(root);
        string? flowsText = flowsJson ?? (writeDefaultFlow ? TestDefaultFlow : null);
        if (flowsText is not null)
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(paths.FlowsFile)!);
            File.WriteAllText(paths.FlowsFile, flowsText);
        }

        var services = new ServiceCollection();
        services.AddLogging();
        var executor = new FakeExecutor();

        ErrorOr<KuroeStartup> startup = services.AddKuroe(paths);
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

        return new KuroeHarness(root, provider, executor);
    }

    public static KuroeHarness Create(string? flowsJson = null, bool seedCatalog = true, bool writeDefaultFlow = true) =>
        TryCreate(flowsJson, seedCatalog, writeDefaultFlow).ThrowIfError();

    public TaskId Submit(string goal, Kuroe.Shared.Workflows.Flows.FlowName? flow = null) =>
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

    /// <summary>等执行侧不再有任何在跑或待推进的 run。待批准状态要求连续两次采样一致，避免批准信号落地前的瞬态。</summary>
    public TaskSnapshot Settle(TaskId id)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(5000);
        TaskState? previous = null;
        while (true)
        {
            TaskSnapshot snapshot = Snapshot(id);
            if (snapshot.LiveRuns == 0
                && snapshot.State is TaskState.Done or TaskState.Blocked or TaskState.Canceled)
            {
                return snapshot;
            }

            if (snapshot.LiveRuns == 0
                && snapshot.State == TaskState.AwaitingApproval
                && previous == TaskState.AwaitingApproval)
            {
                return snapshot;
            }

            if (snapshot.LiveRuns == 0
                && snapshot.State == TaskState.AwaitingInput
                && previous == TaskState.AwaitingInput)
            {
                return snapshot;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"任务 {id} 未在超时内稳定，状态 {snapshot.State}、LiveRuns {snapshot.LiveRuns}、执行节点 [{NodeStates(snapshot)}]。");
            }

            previous = snapshot.State;
            Thread.Sleep(20);
        }
    }

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
