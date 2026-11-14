using Kuroe.Tasks.Presentation.Shared;
using Kuroe.Tasks.Shared;
using System.Text.Json.Nodes;
using ErrorOr;
using Kuroe.Flow.Persistence.Restoring.Shared;

namespace Kuroe.Console.Tests.Persistence;

/// <summary>用真实装配的宿主执行一次持久化验收会话。</summary>
internal sealed class ConsoleTaskPersistenceSession : ITaskPersistenceSession
{
    private static readonly string SessionFlows = MergeFlows();

    private readonly TestEnvironment _environment;

    private ConsoleTaskPersistenceSession(TestEnvironment environment) => _environment = environment;

    public static Task<ITaskPersistenceSession> CreateAsync(string root)
    {
        bool seedCatalog = !File.Exists(Path.Combine(root, ".kuroe", "catalog.json"));
        TestEnvironment environment = TestEnvironment.CreateAt(root, SessionFlows, seedCatalog);
        environment.Runner.SubmitsPorts = false;

        return Task.FromResult<ITaskPersistenceSession>(
            new ConsoleTaskPersistenceSession(environment));
    }

    public TaskLoadReport Loaded => _environment.Loaded;

    public TaskId Submit(string title)
    {
        FlowName flow = new(title == "阻塞任务" ? "阻塞" : "输入");

        return _environment.Submit(title, flow);
    }

    public TaskSnapshot Snapshot(TaskId id) => _environment.Snapshot(id);

    public IReadOnlyList<TaskSnapshot> Tasks() => _environment.TaskReader.Snapshots();

    public Task<ErrorOr<Success>> PauseAsync(TaskId id) => Task.FromResult(_environment.Tasks.Pause(id));

    public ErrorOr<Success> Resume(TaskId id) => _environment.Tasks.Resume(id);

    public ErrorOr<Success> Answer(TaskId id, string input) => _environment.Pendings.Answer(id, null, input);

    public ErrorOr<Success> Rerun(TaskId id) => _environment.Tasks.Rerun(id);

    public TaskSnapshot WaitForInput(TaskId id)
    {
        TaskSnapshot snapshot = _environment.Wait(id, _ => _environment.Awaiting(id));
        _environment.Tasks.FlushAsync().GetAwaiter().GetResult();

        return snapshot;
    }

    public TaskSnapshot Wait(TaskId id, Func<TaskSnapshot, bool> condition) =>
        _environment.Wait(id, snapshot => condition(snapshot));

    public Task FlushAsync(TaskId id) => _environment.Records.FlushAsync(id);

    public Task<ErrorOr<Success>> ShutdownAsync() => Task.FromResult<ErrorOr<Success>>(Result.Success);

    public ValueTask DisposeAsync()
    {
        _environment.Dispose();

        return ValueTask.CompletedTask;
    }

    /// <summary>合并输入与阻塞流程，分别命名为「输入」与「阻塞」。</summary>
    private static string MergeFlows()
    {
        JsonArray flows = [];
        JsonObject document = new() { ["Flows"] = flows };
        Add(flows, TestFlows.InputFlow, "输入");
        Add(flows, TestFlows.BlockingFlow, "阻塞");

        return document.ToJsonString();
    }

    private static void Add(JsonArray flows, string json, string name)
    {
        JsonObject file = JsonNode.Parse(json) as JsonObject
            ?? throw new InvalidOperationException($"流程文件不是对象：{json}");
        JsonArray array = file["Flows"] as JsonArray
            ?? throw new InvalidOperationException($"流程文件没有 Flows 数组：{json}");
        JsonObject flow = array[0]?.DeepClone().AsObject()
            ?? throw new InvalidOperationException($"流程文件没有第一个流程：{json}");
        flow["Name"] = name;
        flows.Add(flow);
    }
}
