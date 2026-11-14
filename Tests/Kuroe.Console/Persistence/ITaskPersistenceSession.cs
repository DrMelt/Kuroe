using ErrorOr;
using Kuroe.Flow.Persistence.Restoring.Shared;
using Kuroe.Tasks.Presentation.Shared;
using Kuroe.Tasks.Shared;

namespace Kuroe.Console.Tests.Persistence;

/// <summary>端到端测试的一次宿主会话。</summary>
public interface ITaskPersistenceSession : IAsyncDisposable
{
    /// <summary>本次会话启动加载的报告。</summary>
    TaskLoadReport Loaded { get; }

    TaskId Submit(string title);

    TaskSnapshot Snapshot(TaskId id);

    IReadOnlyList<TaskSnapshot> Tasks();

    Task<ErrorOr<Success>> PauseAsync(TaskId id);

    ErrorOr<Success> Resume(TaskId id);

    ErrorOr<Success> Answer(TaskId id, string input);

    ErrorOr<Success> Rerun(TaskId id);

    TaskSnapshot WaitForInput(TaskId id);

    TaskSnapshot Wait(TaskId id, Func<TaskSnapshot, bool> condition);

    /// <summary>把该任务当前状态写入存档，返回时存档已落盘。</summary>
    Task FlushAsync(TaskId id);

    Task<ErrorOr<Success>> ShutdownAsync();
}
