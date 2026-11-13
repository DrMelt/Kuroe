using Kuroe.Execution.Shared;
using Kuroe.Node.Llm.Executions;
using Kuroe.Node.Llm.Tools;

namespace Kuroe.Node.Llm.Sessions;

/// <summary>会话装配入口。执行器经它取会话，内部实现类型不外露。</summary>
/// <remarks>按模型接入与工具面建立会话装配。</remarks>
public sealed class SessionFactory(IModelAccess access, IToolFactory tools, IToolJson json) : IDisposable
{
    private readonly ClientProvider _clients = new(access);
    private readonly IToolFactory _tools = tools;
    private readonly IToolJson _json = json;

    /// <summary>一次执行的会话，工具载体与历史都由执行输入给出。带恢复数据时恢复历史。</summary>
    internal Session ForExecution(
        ExecutionId execution,
        ExecutionRequest request,
        LlmExecutionParameters parameters,
        ProcessJournal journal,
        LlmExecutionState? restored)
    {
        ExecutionHistory history = new(request, parameters);
        if (restored is null)
        {
            return new Session(
                _clients,
                new ToolCollection(_tools.Create(execution, request, parameters), _json),
                history,
                journal);
        }

        string? model = parameters.Model;
        if (restored.Model != model)
        {
            throw new InvalidOperationException(
                $"会话恢复数据属于模型 {restored.Model}，当前执行使用 {model ?? "未声明模型"}。");
        }

        return new Session(
            _clients,
            new ToolCollection(_tools.Create(execution, request, parameters), _json),
            history,
            journal,
            [.. restored.Messages.Select(LlmExecutionStateCodec.ToMessage)]);
    }

    /// <summary>释放缓存的模型客户端。</summary>
    public void Dispose() => _clients.Dispose();
}
