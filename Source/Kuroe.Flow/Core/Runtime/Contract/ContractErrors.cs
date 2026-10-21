using ErrorOr;

namespace Kuroe.Flow.Runtime.Contract;

/// <summary>契约提交域的错误构造。</summary>
static class ContractErrors
{
    /// <summary>端口产出的形状或内容不合法，原因要能直接回给模型改正。</summary>
    public static Error Ports(string reason) => Error.Validation(
        "Task.Ports", $"{reason}。要提交端口名到文本的 JSON 对象。");
}