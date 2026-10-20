using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Configuration.Files;
using Kuroe.Shared;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Workflows.Dialogue;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Pending;
using Kuroe.Tool.Contract;
using Kuroe.Workflows.Flows;
using Microsoft.Extensions.DependencyInjection;

namespace Kuroe.Tests.Support;

/// <summary>在临时工作目录里装配一套 Kuroe，用假执行器驱动流程。</summary>
public sealed class TestEnvironment : IDisposable
{
    private readonly ServiceProvider _provider;

    private TestEnvironment(string root, ServiceProvider provider, FakeRunner runner)
    {
        Root = root;
        _provider = provider;
        Runner = runner;
        Registry = provider.GetRequiredService<TaskRegistry>();
        Tasks = provider.GetRequiredService<TaskService>();
        Pendings = provider.GetRequiredService<PendingService>();
        PortTool = new PortTool(provider.GetRequiredService<PortSubmitter>(), Registry);
        CannotCompleteTool = new CannotCompleteTool(provider.GetRequiredService<CannotCompleteSubmitter>());
        runner.PortTool = PortTool;
        runner.CannotCompleteTool = CannotCompleteTool;
    }

    /// <summary>临时工作目录。</summary>
    public string Root { get; }

    public FakeRunner Runner { get; }

    public TaskRegistry Registry { get; }

    public TaskService Tasks { get; }

    /// <summary>等待型节点的宿主功能，回答与批准经它交付外部输入。</summary>
    public PendingService Pendings { get; }

    /// <summary>常驻对话任务的管理层宿主。</summary>
    public DialogueHost Dialogue => _provider.GetRequiredService<DialogueHost>();

    public SettingsProvider Settings => _provider.GetRequiredService<SettingsProvider>();

    public FlowService Flows => _provider.GetRequiredService<FlowService>();

    /// <summary>端口产出的交回通道，测试直接调用它验证提交校验。</summary>
    public PortSubmitter PortSubmitter => _provider.GetRequiredService<PortSubmitter>();

    /// <summary>交回端口产出的契约工具载体，假执行器按模型调用方式经它提交。</summary>
    public PortTool PortTool { get; }

    /// <summary>上报无法完成的契约工具载体，假执行器按模型调用方式经它上报。</summary>
    public CannotCompleteTool CannotCompleteTool { get; }

    public CatalogService Catalog => _provider.GetRequiredService<CatalogService>();

    public ModelService Models => _provider.GetRequiredService<ModelService>();

    public IReadOnlyList<Kuroe.Tool.Json.ConfiguredTool> ConfiguredTools =>
        _provider.GetRequiredService<IReadOnlyList<Kuroe.Tool.Json.ConfiguredTool>>();

    /// <summary>测试默认写入的流程文件，模型选择显式指向 fake，供按名提交的执行测试使用。</summary>
    private const string TestFlowFile = """
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
                    { "Name": "制定计划", "Kind": "Llm", "Model": "规划者", "Prompt": "把目标写成可实施的计划。", "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
                    { "Name": "交付", "Kind": "Container", "Nodes": [
                      { "Name": "分配执行", "Kind": "Llm", "Model": "执行者", "Tools": ["GetLocalTime"], "From": [ { "计划": "制定计划@计划" } ] }
                    ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>装配失败时返回错误，用于校验类断言。未给流程文件时写入测试自带流程，flowsJson 传空对象即空流程库。</summary>
    public static ErrorOr<TestEnvironment> TryCreate(string? flowsJson = null, bool seedCatalog = true)
    {
        string root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);

        KuroePaths paths = KuroePaths.At(root);
        string? flowsText = flowsJson ?? TestFlowFile;
        if (flowsText is not null)
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(paths.FlowsFile)!);
            File.WriteAllText(paths.FlowsFile, flowsText);
        }

        var services = new ServiceCollection();
        services.AddLogging();
        var runner = new FakeRunner();

        ErrorOr<KuroeStartup> startup = services.AddKuroe(paths, Path.Combine(root, "source"));
        if (startup.IsError)
        {
            return startup.ErrorsOrEmptyList;
        }

        // 后注册的同类型解析时胜出，据此把真实执行器换成假的
        services.AddSingleton<IExecutionRunner>(runner);
        ServiceProvider provider = services.BuildServiceProvider();

        if (seedCatalog)
        {
            CatalogService catalog = provider.GetRequiredService<CatalogService>();
            catalog.AddProvider("test", "https://example.invalid/v1", "key").ThrowIfError();
            catalog.AddModel(ModelName.Create("fake").Value, ProviderName.Create("test").Value).ThrowIfError();
            provider.GetRequiredService<ModelService>().Select(ModelName.Create("fake").Value).ThrowIfError();
        }

        return new TestEnvironment(root, provider, runner);
    }

    public static TestEnvironment Create(string? flowsJson = null, bool seedCatalog = true) =>
        TryCreate(flowsJson, seedCatalog).ThrowIfError();

    /// <summary>按流程名提交任务，未点名时用测试默认写入的那条流程。</summary>
    public TaskId Submit(string goal, FlowName? flow = null) =>
        Tasks.Submit(goal, flow ?? new FlowName("默认"), null).ThrowIfError().Id;

    public TaskSnapshot Snapshot(TaskId id) => Registry.Find(id).ThrowIfError().Snapshot();

    /// <summary>执行节点状态摘要，用于超时信息里的定位。</summary>
    private static string NodeStates(TaskSnapshot snapshot) =>
        string.Join("、", snapshot.ExecutableStates.Select(state => $"{state.Node} {state.State}"));

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

    /// <summary>等执行侧不再有任何在跑或待推进的执行。停在等待外部输入时要求连续两次采样一致，避免输入信号落地前的瞬态。</summary>
    public TaskSnapshot Settle(TaskId id)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(5000);
        bool previous = false;
        while (true)
        {
            TaskSnapshot snapshot = Snapshot(id);
            if (snapshot.LiveExecutions == 0
                && snapshot.State is TaskState.Done or TaskState.Blocked or TaskState.Canceled)
            {
                return snapshot;
            }

            bool waiting = snapshot.LiveExecutions == 0 && Awaiting(id);
            if (waiting && previous)
            {
                return snapshot;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"任务 {id} 未在超时内稳定，状态 {snapshot.State}、LiveExecutions {snapshot.LiveExecutions}、执行节点 [{NodeStates(snapshot)}]。");
            }

            previous = waiting;
            Thread.Sleep(20);
        }
    }

    /// <summary>任务里是否有停在等待的节点。</summary>
    public bool Awaiting(TaskId id) =>
        Pendings.Pending(id) is { IsError: false } pending && pending.Value.Count > 0;

    /// <summary>任务里停在给定等待种类的节点路径。</summary>
    public IReadOnlyList<NodePath> Awaiting(TaskId id, PendingKind kind) =>
        Pendings.Pending(id) is { IsError: false } pending
            ? [.. pending.Value.Where(node => node.Kind == kind).Select(node => node.Node)]
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
