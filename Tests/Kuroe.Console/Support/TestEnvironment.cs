using Kuroe.Flow.Control.Shared;
using Kuroe.Flow.Tasks.Presentation.Shared;
using Kuroe.Flow.Tasks.Shared;
using System.Text.Json;
using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Configuration.Files;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Persistence.Restoring.Shared;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Persistence.Saving.Shared;
using Kuroe.Node.Llm.Tools;
using Kuroe.Assistant;
using Kuroe.Flow.TestSupport;
using Kuroe.Assistant.Dialogue;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Control.Graph.Runtime.Engine;
using Kuroe.Pending;
using Kuroe.Workflows.Flows;
using Microsoft.Extensions.DependencyInjection;

namespace Kuroe.Console.Tests.Support;

/// <summary>在临时工作目录里装配一套 Kuroe，用假执行器驱动流程。</summary>
public sealed class TestEnvironment : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly bool _deleteOnDispose;
    private readonly NotifiedTaskWaiter _waiter;

    private TestEnvironment(
        string root,
        ServiceProvider provider,
        FakeRunner runner,
        bool deleteOnDispose,
        TaskLoadReport loaded)
    {
        Root = root;
        _provider = provider;
        _deleteOnDispose = deleteOnDispose;
        Loaded = loaded;
        Runner = runner;
        Registry = provider.GetRequiredService<TaskRegistry>();
        TaskReader = provider.GetRequiredService<ITaskReader>();
        Tasks = provider.GetRequiredService<TaskService>();
        Pendings = provider.GetRequiredService<PendingService>();
        _waiter = new NotifiedTaskWaiter(
            TaskReader,
            provider.GetRequiredService<ITaskChangeSource>());
        runner.PortSubmitter = provider.GetRequiredService<Kuroe.Flow.Execution.Shared.IExecutionPortSubmitter>();
    }

    /// <summary>临时工作目录。</summary>
    public string Root { get; }

    /// <summary>启动加载的报告，被其他实例占用的任务在这里列出。</summary>
    public TaskLoadReport Loaded { get; }

    public FakeRunner Runner { get; }

    public TaskRegistry Registry { get; }

    /// <summary>任务索引的只读快照面，用例读取任务状态时使用。</summary>
    public ITaskReader TaskReader { get; }

    public TaskService Tasks { get; }

    /// <summary>等待型节点的宿主功能，回答与批准经它交付外部输入。</summary>
    public PendingService Pendings { get; }

    /// <summary>存档协调，用例据此把待写状态显式落盘。</summary>
    public ITaskRecordCoordinator Records => _provider.GetRequiredService<ITaskRecordCoordinator>();

    /// <summary>常驻对话任务的管理层宿主。</summary>
    public DialogueHost Dialogue => _provider.GetRequiredService<DialogueHost>();

    public SettingsProvider Settings => _provider.GetRequiredService<SettingsProvider>();

    public FlowService Flows => _provider.GetRequiredService<FlowService>();

    /// <summary>流程库操作。</summary>
    public IFlowLibrary Library => _provider.GetRequiredService<IFlowLibrary>();

    /// <summary>流程模板目录。</summary>
    public IFlowTemplateCatalog Templates => _provider.GetRequiredService<IFlowTemplateCatalog>();

    /// <summary>宿主开始输入表，流程展示与任务提交共用。</summary>
    public IHostStartInputs HostStartInputs => _provider.GetRequiredService<IHostStartInputs>();

    public CatalogService Catalog => _provider.GetRequiredService<CatalogService>();

    public ModelService Models => _provider.GetRequiredService<ModelService>();

    public ToolCollection Tools => _provider.GetRequiredService<ToolCollection>();

    public IReadOnlyList<Kuroe.Tool.Json.ConfiguredTool> ConfiguredTools =>
        _provider.GetRequiredService<IReadOnlyList<Kuroe.Tool.Json.ConfiguredTool>>();

    /// <summary>测试默认写入的流程文件与宿主开始输入表，模型经宿主输入 MODEL 取当前选择，供按名提交的执行测试使用。</summary>
    private const string TestFlowFile = """
        {
          "HostStartInputs": { "默认": { "MODEL_NAME": "MODEL" } },
          "Flows": [
            {
              "Name": "默认",
              "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }],
              "Root": {
                "Name": "整体",
                "Kind": "Container",
                "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
                  { "Name": "制定计划", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Prompt": "把目标写成可实施的计划。", "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                  { "Name": "交付", "Kind": "Container", "Inputs": [{ "Name": "计划" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "计划": "制定计划@计划" } ], "Nodes": [
                    { "Name": "分配执行", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "Tools": ["GetLocalTime"], "From": [ { "计划": "@计划" } ] }
                  ] }
                ]
              }
            }
          ]
        }
        """;

    /// <summary>装配失败时返回错误，用于校验类断言。未给流程文件时写入测试自带流程，flowsJson 传空对象即空流程库。</summary>
    public static ErrorOr<TestEnvironment> TryCreate(string? flowsJson = null, bool seedCatalog = true) =>
        TryCreateAt(
            Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N")),
            flowsJson,
            seedCatalog);

    /// <summary>在指定工作目录装配，用于跨会话重载存档。</summary>
    public static ErrorOr<TestEnvironment> TryCreateAt(
        string root,
        string? flowsJson = null,
        bool seedCatalog = true,
        bool deleteOnDispose = true)
    {
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

        ErrorOr<KuroeComposition> composition = services.AddKuroeCore(paths, new KuroeCoreOptions { RegisterNodeTypes = NodeTypes.Register });
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
        services.AddSingleton<Kuroe.Flow.Execution.Shared.IExecutionRunnerFactory>(runner);
        services.AddSingleton<ITaskIdSource>(new SequentialTaskIdSource());
        ServiceProvider provider = services.BuildServiceProvider();
        TaskLoadReport loaded = provider.GetRequiredService<ITaskRestoreService>().Restore();

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

        return new TestEnvironment(root, provider, runner, deleteOnDispose, loaded);
    }

    public static TestEnvironment Create(string? flowsJson = null, bool seedCatalog = true) =>
        TryCreate(flowsJson, seedCatalog).ThrowIfError();

    /// <summary>在指定工作目录装配，用于跨会话重载存档。</summary>
    public static TestEnvironment CreateAt(string root, string? flowsJson = null, bool seedCatalog = true) =>
        TryCreateAt(root, flowsJson, seedCatalog, deleteOnDispose: false).ThrowIfError();

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
    public TaskId Submit(StartInputs inputs, FlowName? flow = null) =>
        Tasks.Submit(inputs, flow ?? new FlowName("默认"), null).ThrowIfError().Id;

    public TaskSnapshot Snapshot(TaskId id) => TaskReader.Find(id).ThrowIfError();

    /// <summary>等到任务满足条件为止。</summary>
    public TaskSnapshot Wait(TaskId id, TaskPredicate condition) => _waiter.Wait(id, condition);

    /// <summary>等任务不再自动推进：已完成、阻塞、终止或暂停，且没有在跑执行。停在等待节点不构成收口。</summary>
    public TaskSnapshot Settle(TaskId id) => _waiter.Settle(id);

    /// <summary>任务里是否有停在等待的节点。</summary>
    public bool Awaiting(TaskId id) =>
        Pendings.Pending(id) is { IsError: false } pending && pending.Value.Count > 0;

    /// <summary>任务里停在给定等待种类的节点路径。</summary>
    public IReadOnlyList<NodePath> Awaiting(TaskId id, PendingKind kind) =>
        Pendings.Pending(id) is { IsError: false } pending
            ? [.. pending.Value.Where(node => node.Kind == kind).Select(node => FlowStepPaths.ToNodePath(node.Step))]
            : [];

    public void Dispose()
    {
        _waiter.Dispose();
        Tasks.ShutdownAsync().GetAwaiter().GetResult();
        _provider.Dispose();

        try
        {
            if (_deleteOnDispose)
            {
                System.IO.Directory.Delete(Root, recursive: true);
            }
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
