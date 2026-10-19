namespace Kuroe.Flow.Shared.Execution.Tasks;

/// <summary>执行节点与模型之间的契约定义：两个提交工具的名字。工具载体按它声明函数，派发执行的类型按输出端口放行契约能力。</summary>
public static class ContractTools
{
    /// <summary>交回命名端口内容的工具名，声明 Text 端口的执行节点放行。</summary>
    public const string Port = "SubmitPortValues";

    /// <summary>声明本轮状态的工具名，所有执行节点放行。</summary>
    public const string Status = "SubmitStatus";
}
