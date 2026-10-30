namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>任务键的生成。运行期给跨进程唯一键，测试可给出可预期的固定序列。</summary>
public interface ITaskIdSource
{
    /// <summary>取下一个任务键。</summary>
    TaskId Next();
}
