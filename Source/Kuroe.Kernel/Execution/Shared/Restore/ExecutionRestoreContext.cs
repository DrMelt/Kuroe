using Kuroe.Execution.Shared.Contract;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Execution.Shared.Restore;

/// <summary>建立恢复计划时交给执行实现的输入：本次执行的标识、请求、装配结果、恢复负载与本轮契约。</summary>
public sealed record ExecutionRestoreContext(
    ExecutionId Execution,
    ExecutionRequest Request,
    ExecutionInvocation Invocation,
    ExecutionRestoreState Payload,
    ExecutionContract Contract);