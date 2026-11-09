using System.Text.Json;
using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Configuration.Files;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Persistence.Restoring.Shared;
using Kuroe.Flow.Persistence.Saving.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Assistant;
using Kuroe.Assistant.Dialogue;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Runtime.Contract;
using Kuroe.Pending;
using Kuroe.Process;
using Kuroe.Tool;
using Kuroe.Tool.Contract;
using Kuroe.Workflows.Flows;
using Kuroe.Flow.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace Kuroe.Integration.Tests.Support;

/// <summary>在临时工作目录里装配一套 Kuroe，用假执行器驱动流程。</summary>
public sealed class TestEnvironment : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly ContractToolSource _contractTools;
    private readonly NotifiedTaskWaiter _waiter;

    private TestEnvironment(string root, ServiceProvider provider, FakeRunner runner)
    {
        Root = root;
        _provider = provider;
        Runner = runner;
        Registry = provider.GetRequiredService<TaskRegistry>();
        TaskReader = provider.GetRequiredService<ITaskReader>();
        Executions = provider.GetRequiredService<ExecutionIndex>();
        runner.Executions = Executions;
        Tasks = provider.GetRequiredService<TaskService>();
        Pendings = provider.GetRequiredService<PendingService>();
        Records = provider.GetRequiredService<ITaskRecordCoordinator>();
        _waiter = new NotifiedTaskWaiter(
            TaskReader,
            provider.GetRequiredService<ITaskChangeSource>());
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

    /// <summary>任务索引的只读快照面，信息查询工具使用。</summary>
    public ITaskReader TaskReader { get; }

    /// <summary>全部任务域共享的执行索引。</summary>
    public ExecutionIndex Executions { get; }

    public TaskService Tasks { get; }

    /// <summary>等待型节点的宿主功能，回答与批准经它交付外部输入。</summary>
    public PendingService Pendings { get; }

    /// <summary>存档协调，用例据此把待写状态显式落盘。</summary>
    public ITaskRecordCoordinator Records { get; }

    /// <summary>常驻对话任务的管理层宿主。</summary>
    public DialogueHost Dialogue => _provider.GetRequiredService<DialogueHost>();

    public SettingsProvider Settings => _provider.GetRequiredService<SettingsProvider>();

    public FlowService Flows => _provider.GetRequiredService<FlowService>();

    /// <summary>宿主开始输入表，流程展示与任务提交共用。</summary>
    public IHostStartInputs HostStartInputs => _provider.GetRequiredService<IHostStartInputs>();

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

    /// <summary>测试默认写入的流程文件与宿主开始输入表，模型经宿主输入 MODEL 取当前选择，供按名提交的执行测试使用。</summary>
    private const string TestFlowFile = """
        {
          "HostStartInputs": { "默认": { "MODEL_NAME": "MODEL" } },
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [
                { "Name": "目标", "Type": "Text" },
                { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }
              ] },
              "Root": {
                "Name": "整体",
                "Kind": "Container",
                "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }],
                "From": [ { "目标": "Start@目标" } ],
                "Bindings": { "MODEL_NAME": "Start@MODEL_NAME" },
                "Nodes": [
                  { "Name": "制定计划", "Kind": "Llm", "Inputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Prompt": "把目标写成可实施的计划。", "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                  { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "计划" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "计划": "制定计划@计划" } ], "Nodes": [
                    { "Name": "分配执行", "Kind": "Llm", "Inputs": [{ "Name": "计划", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Tools": ["GetLocalTime"], "From": [ { "计划": "@计划" } ] }
                  ] }
                ]
              }
            }
          ]
        }
        """;

    /// <summary>完整装配的选项：注册全部节点类型，流程失败即启动失败。默认不写存档，持久化用例显式打开。</summary>
    internal static KuroeCoreOptions Options => CreateOptions(null);

    private static KuroeCoreOptions CreateOptions(IProcessRunner? process) => new()
    {
        RegisterNodeTypes = TestTypeFiles.RegisterAll,
        PersistTasks = false,
        CommandNodeProcessRunner = process,
    };

    /// <summary>装配失败时返回错误，用于校验类断言。未给流程文件时写入测试自带流程，flowsJson 传空对象即空流程库。</summary>
    public static ErrorOr<TestEnvironment> TryCreate(
        string? flowsJson = null,
        bool seedCatalog = true,
        bool persistTasks = false,
        IProcessRunner? process = null)
    {
        string root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(root);

        KuroePaths paths = KuroePaths.At(root);
        string? flowsText = flowsJson ?? TestFlowFile;
        if (flowsText is not null)
        {
            WriteFlowFiles(paths, flowsText);
        }

        var services = new ServiceCollection();
        services.AddLogging();
        var runner = new FakeRunner();

        ErrorOr<KuroeComposition> composition = services.AddKuroeCore(
            paths,
            CreateOptions(process) with { PersistTasks = persistTasks });
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
        services.AddSingleton<ITaskIdSource>(new SequentialTaskIdSource());
        ServiceProvider provider = services.BuildServiceProvider();
        // 不写存档的装配不注册恢复服务
        if (persistTasks)
        {
            provider.GetRequiredService<ITaskRestoreService>().Restore();
        }

        if (seedCatalog)
        {
            CatalogService catalog = provider.GetRequiredService<CatalogService>();
            catalog.AddProvider("test", "https://example.invalid/v1", "key").ThrowIfError();
            catalog.AddModel(
                ModelName.Create("fake").Value,
                ProviderName.Create("test").Value,
                ProviderModelName.Create("fake").Value).ThrowIfError();
            provider.GetRequiredService<ModelService>().Select(ModelName.Create("fake").Value).ThrowIfError();
        }

        return new TestEnvironment(root, provider, runner);
    }

    public static TestEnvironment Create(
        string? flowsJson = null,
        bool seedCatalog = true,
        bool persistTasks = false,
        IProcessRunner? process = null) =>
        TryCreate(flowsJson, seedCatalog, persistTasks, process).ThrowIfError();

    /// <summary>把测试夹具按顶层字段写成节点库、流程与宿主开始输入表，空对象不写文件。</summary>
    private static void WriteFlowFiles(KuroePaths paths, string text)
    {
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(paths.FlowsFile)
            ?? throw new InvalidOperationException($"流程文件 {paths.FlowsFile} 没有所在目录。"));

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
                    if (string.Equals(property.Name, "Nodes", StringComparison.OrdinalIgnoreCase))
                    {
                        File.WriteAllText(paths.NodesFile, $"{{\"Nodes\":{property.Value.GetRawText()}}}");
                        written = true;
                    }
                    else if (string.Equals(property.Name, "Flows", StringComparison.OrdinalIgnoreCase))
                    {
                        File.WriteAllText(paths.FlowsFile, $"{{\"Flows\":{property.Value.GetRawText()}}}");
                        written = true;
                    }
                    else if (string.Equals(property.Name, "HostStartInputs", StringComparison.OrdinalIgnoreCase))
                    {
                        File.WriteAllText(paths.HostStartInputsFile, $"{{\"HostStartInputs\":{property.Value.GetRawText()}}}");
                        written = true;
                    }
                }
            }

            if (!written && text.Trim() is not "{}")
            {
                File.WriteAllText(paths.FlowsFile, text);
            }
        }
        catch (JsonException)
        {
            File.WriteAllText(paths.FlowsFile, text);
        }
    }

    /// <summary>按 目标 端口提交任务，未点名时用测试默认写入的那条流程。</summary>
    public TaskId Submit(string goal, FlowName? flow = null) =>
        Submit(StartInputs.Single(new PortName("目标"), goal), flow);

    /// <summary>按开始输入表提交任务，未点名时用测试默认写入的那条流程。</summary>
    public TaskId Submit(StartInputs inputs, FlowName? flow = null)
    {
        FlowName name = flow ?? new FlowName("默认");
        ErrorOr<TaskSnapshot> submitted = Tasks.Submit(inputs, name, null);
        if (submitted.IsSuccess)
        {
            return submitted.Value.Id;
        }

        FlowFailure? failure = Flows.Failures.FirstOrDefault(candidate => candidate.Name == name);
        if (failure is not null)
        {
            throw new InvalidOperationException(string.Join("；", failure.Errors.Select(error => error.Description)));
        }

        return submitted.ThrowIfError().Id;
    }

    public TaskSnapshot Snapshot(TaskId id) => TaskReader.Find(id).ThrowIfError();

    /// <summary>等到任务满足条件为止。</summary>
    public TaskSnapshot Wait(TaskId id, TaskPredicate condition) => _waiter.Wait(id, condition);

    /// <summary>等到任务满足条件为止。等待期间让出测试线程。</summary>
    public Task<TaskSnapshot> WaitAsync(TaskId id, TaskPredicate condition) => _waiter.WaitAsync(id, condition);

    /// <summary>等任务不再自动推进：已完成、阻塞、终止或暂停，且没有在跑执行。停在等待节点不构成收口。</summary>
    public TaskSnapshot Settle(TaskId id) => _waiter.Settle(id);

    /// <summary>等任务不再自动推进：已完成、阻塞、终止或暂停，且没有在跑执行。停在等待节点不构成收口。</summary>
    public Task<TaskSnapshot> SettleAsync(TaskId id) => _waiter.SettleAsync(id);

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
        _waiter.Dispose();
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
