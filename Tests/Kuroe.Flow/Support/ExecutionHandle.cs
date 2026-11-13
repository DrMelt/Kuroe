using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Execution;

namespace Kuroe.Flow.TestSupport;

/// <summary>一次在场执行的句柄：执行号、节点与轮次。测试据此放行与等待，不必猜测执行号。</summary>
public readonly record struct ExecutionHandle(ExecutionId Id, NodePath Node, int Round);