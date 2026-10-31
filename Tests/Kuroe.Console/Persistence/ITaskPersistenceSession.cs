using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;

namespace Kuroe.Console.Tests.Persistence;

/// <summary>端到端测试的一次宿主会话。</summary>
public interface ITaskPersistenceSession : IAsyncDisposable
{
    TaskId Submit(string title);

    TaskSnapshot Snapshot(TaskId id);

    IReadOnlyList<TaskSnapshot> Tasks();

    Task<ErrorOr<Success>> PauseAsync(TaskId id);

    ErrorOr<Success> Resume(TaskId id);

    ErrorOr<Success> Answer(TaskId id, string input);

    ErrorOr<Success> Rework(TaskId id);

    TaskSnapshot WaitForInput(TaskId id);

    TaskSnapshot Wait(TaskId id, Func<TaskSnapshot, bool> condition);

    Task<ErrorOr<Success>> ShutdownAsync();
}
