using System.Text.Json.Nodes;
using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;

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

        return Task.FromResult<ITaskPersistenceSession>(
            new ConsoleTaskPersistenceSession(TestEnvironment.CreateAt(root, SessionFlows, seedCatalog)));
    }

    public TaskId Submit(string title)
    {
        FlowName flow = new(title == "阻塞任务" ? "阻塞" : "输入");

        return _environment.Submit(title, flow);
    }

    public TaskSnapshot Snapshot(TaskId id) => _environment.Snapshot(id);

    public IReadOnlyList<TaskSnapshot> Tasks() => _environment.Registry.Snapshots();

    public Task<ErrorOr<Success>> PauseAsync(TaskId id) => Task.FromResult(_environment.Tasks.Pause(id));

    public ErrorOr<Success> Resume(TaskId id) => _environment.Tasks.Resume(id);

    public ErrorOr<Success> Answer(TaskId id, string input) => _environment.Pendings.Answer(id, null, input);

    public ErrorOr<Success> Rework(TaskId id) => _environment.Tasks.Rework(id);

    public TaskSnapshot WaitForInput(TaskId id) =>
        _environment.Wait(id, _ => _environment.Awaiting(id));

    public TaskSnapshot Wait(TaskId id, Func<TaskSnapshot, bool> condition) =>
        _environment.Wait(id, condition);

    public Task<ErrorOr<Success>> ShutdownAsync() => Task.FromResult<ErrorOr<Success>>(Result.Success);

    public ValueTask DisposeAsync()
    {
        _environment.Dispose();

        return ValueTask.CompletedTask;
    }

    /// <summary>合并输入与阻塞流程，分别命名为「输入」与「阻塞」。</summary>
    private static string MergeFlows()
    {
        JsonObject document = new() { ["Flows"] = new JsonArray() };
        JsonArray flows = document["Flows"]!.AsArray();
        Add(flows, TestFlows.InputFlow, "输入");
        Add(flows, TestFlows.BlockingFlow, "阻塞");

        return document.ToJsonString();
    }

    private static void Add(JsonArray flows, string json, string name)
    {
        JsonObject flow = JsonNode.Parse(json)!["Flows"]![0]!.DeepClone().AsObject();
        flow["Name"] = name;
        flows.Add(flow);
    }
}
