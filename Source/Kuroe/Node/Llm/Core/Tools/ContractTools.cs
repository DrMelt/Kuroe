using Kuroe.Flow.Shared.StaticGraph.Ports;
namespace Kuroe.Node.Llm.Tools;

/// <summary>执行节点与模型之间的契约定义：交回产出、上报无法完成与查看本轮状态三个工具的路径。
/// 工具载体按它声明函数，派发执行的类型按需交回的输出端口放行交回能力，上报与查看所有执行节点放行。</summary>
public static class ContractTools
{
    /// <summary>交回命名端口产出的工具路径，声明 Text 或 Signal 输出端口的执行节点放行。</summary>
    public const string Port = "SubmitPortValues";

    /// <summary>上报本轮无法完成的工具路径，所有执行节点放行。</summary>
    public const string CannotComplete = "ReportCannotComplete";

    /// <summary>查看本轮执行契约状态的工具路径，所有执行节点放行。</summary>
    public const string Status = "GetStatus";
}
