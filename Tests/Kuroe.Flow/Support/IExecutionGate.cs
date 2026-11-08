using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.TestSupport;

/// <summary>执行进出的闸门：替身执行器登记进入并在此保持，测试等待进入并放行。
/// 保持与放行由测试控制，观察窗口由在场事实决定。</summary>
public interface IExecutionGate
{
    /// <summary>替身执行器登记进入运行，返回句柄。</summary>
    ExecutionHandle Arrive(ExecutionId execution, NodePath node, int round);

    /// <summary>替身执行器在此保持运行，直到测试放行或调用方取消。</summary>
    Task WaitOpen(ExecutionHandle handle);

    /// <summary>替身执行器登记已离开运行。</summary>
    void Leave(ExecutionHandle handle);

    /// <summary>保持给定节点的执行，进入后不自动放行。</summary>
    void Hold(NodePath node);

    /// <summary>保持全部执行，进入后不自动放行。</summary>
    void HoldAll();

    /// <summary>放开全部保持并放行等待中的执行。</summary>
    void ClearHolds();

    /// <summary>等到给定节点的一次执行进入运行。</summary>
    Task<ExecutionHandle> Entered(NodePath node);

    /// <summary>等到这些节点同时各有一次执行进入运行。</summary>
    Task<ExecutionHandle[]> Entered(IReadOnlyList<NodePath> nodes);

    /// <summary>放行一次在场执行。</summary>
    void Open(ExecutionHandle handle);

    /// <summary>取消该节点的保持并放行它的全部在场执行。</summary>
    void Open(NodePath node);
}