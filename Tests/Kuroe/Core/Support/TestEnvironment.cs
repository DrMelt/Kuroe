using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Configuration.Files;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Assistant;
using Kuroe.Assistant.Dialogue;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Runtime.Contract;
using Kuroe.Pending;
using Kuroe.Node.Llm.Tools;
using Kuroe.Tool;
using Kuroe.Tool.Contract;
using Kuroe.Workflows.Flows;
using Microsoft.Extensions.DependencyInjection;

namespace Kuroe.Tests.Support;

/// <summary>在临时工作目录里装配一套 Kuroe，用假执行器驱动流程。</summary>
public sealed class TestEnvironment : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly ContractToolSource _contractTools;

    private TestEnvironment(string root, ServiceProvider provider, FakeRunner runner)
    {
        Root = root;
        _provider = provider;
        Runner = runner;
        Registry = provider.GetRequiredService<TaskRegistry>();
        Executions = provider.GetRequiredService<ExecutionIndex>();
        runner.Executions = Executions;
        Tasks = provider.GetRequiredService<TaskService>();
        Pendings = provider.GetRequiredService<PendingService>();
        _contractTools = new ContractToolSource(
            provider.GetRequiredService<PortSubmitter>(),
            provider.GetRequiredService<CannotCompleteSubmitter>(),
            new ExecutionStatusReader(Executions));
        runner.ContractToolFactory = (execution, input) => CreateContractTools(execution, input);
    }

    /// <summary>临时工作目录。</summary>
    public string Root { get; }

    public FakeRunner Runner { get; }

    public TaskRegistry Registry { get; }

    /// <summary>全部任务域共享的执行索引。</summary>
    public ExecutionIndex Executions { get; }

    public TaskService Tasks { get; }

    /// <summary>等待型节点的宿主功能，回答与批准经它交付外部输入。</summary>
    public PendingService Pendings { get; }

    /// <summary>常驻对话任务的管理层宿主。</summary>
    public DialogueHost Dialogue => _provider.GetRequiredService<DialogueHost>();

    public SettingsProvider Settings => _provider.GetRequiredService<SettingsProvider>();

    public FlowService Flows => _provider.GetRequiredService<FlowService>();

    /// <summary>端口产出的交回通道，测试直接调用它验证提交校验。</summary>
    public PortSubmitter PortSubmitter => _provider.GetRequiredService<PortSubmitter>();

    /// <summary>交回端口产出的契约工具，按执行创建。</summary>
    public ITool PortToolFor(ExecutionId execution) => ContractTool(execution, ContractTools.Port);

    /// <summary>上报无法完成的契约工具，按执行创建。</summary>
    public ITool CannotCompleteToolFor(ExecutionId execution) => ContractTool(execution, ContractTools.CannotComplete);

    /// <summary>查看执行契约状态的契约工具，按执行创建。</summary>
    public ITool StatusToolFor(ExecutionId execution) => ContractTool(execution, ContractTools.Status);

    /// <summary>按执行创建全部契约工具。</summary>
    public IReadOnlyList<ITool> CreateContractTools(ExecutionId execution, ExecutionInput input) =>
        _contractTools.Create(new ToolContext(
            new ExecutionPortAccess(input),
            new Dictionary<string, IReadOnlyDictionary<string, PortName>>(),
            Root,
            execution,
            [
                new ToolPath(ContractTools.Port),
                new ToolPath(ContractTools.CannotComplete),
                new ToolPath(ContractTools.Status),
            ]));

    /// <summary>取指定路径的契约工具。</summary>
    private ITool ContractTool(ExecutionId execution, string path)
    {
        Execution found = Executions.Find(execution).ThrowIfError();
        IReadOnlyList<ITool> tools = CreateContractTools(execution, found.Input);

        return tools.Single(tool => tool.Functions.Any(function => function.Definition.Path.Value == path));
    }

    public CatalogService Catalog => _provider.GetRequiredService<CatalogService>();

    public ModelService Models => _provider.GetRequiredService<ModelService>();

    public IReadOnlyList<Kuroe.Tool.Json.ConfiguredTool> ConfiguredTools =>
        _provider.GetRequiredService<IReadOnlyList<Kuroe.Tool.Json.ConfiguredTool>>();

    /// <summary>测试默认写入的流程文件，模型经宿主输入 MODEL 取当前选择，供按名提交的执行测试使用。</summary>
    private const string TestFlowFile = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [
                { "Name": "目标", "Type": "Text" },
                { "Name": "MODEL_NAME", "Type": "Text", "Static": true }
              ] },
              "HostStartInputs": { "MODEL_NAME": "MODEL" },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }],
                  "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" },
                  "Nodes": [
                    { "Name": "制定计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Prompt": "把目标写成可实施的计划。", "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                    { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "计划" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "计划": "制定计划@计划", "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
                      { "Name": "分配执行", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Tools": ["GetLocalTime"], "From": [ { "计划": "@计划" } ] }
                    ] }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>完整装配的选项：注册全部节点类型，流程失败即启动失败。</summary>
    internal static KuroeCoreOptions Options => new() { RegisterNodeTypes = TestTypeFiles.RegisterAll };

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

        ErrorOr<KuroeComposition> composition = services.AddKuroeCore(paths, Options);
        if (composition.IsError)
        {
            return composition.ErrorsOrEmptyList;
        }

        ErrorOr<Success> assistant = services.AddKuroeAssistant(composition.Value, Path.Combine(root, "source"));
        if (assistant.IsError)
        {
            return assistant.ErrorsOrEmptyList;
        }

        // 后注册的同类型解析时胜出，据此把真实执行器换成假的
        services.AddSingleton<IExecutionRunnerFactory>(runner);
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

    /// <summary>按 目标 端口提交任务，未点名时用测试默认写入的那条流程。</summary>
    public TaskId Submit(string goal, FlowName? flow = null) =>
        Submit(StartInputs.Single(new PortName("目标"), goal), flow);

    /// <summary>按开始输入表提交任务，未点名时用测试默认写入的那条流程。</summary>
    public TaskId Submit(StartInputs inputs, FlowName? flow = null) =>
        Tasks.Submit(inputs, flow ?? new FlowName("默认"), null).ThrowIfError().Id;

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
