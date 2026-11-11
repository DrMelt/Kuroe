using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Tool.Contract;

/// <summary>契约工具的定义：路径、说明与参数。引擎按路径放行，交回能力的端口与形状由参数 schema 给出。</summary>
static class ContractToolDefinitions
{
    /// <summary>交回命名端口产出的工具定义，有端口清单时给出参数 schema。</summary>
    public static ToolDefinition Port(IReadOnlyList<Port>? ports) => new(
        new ToolPath(ContractTools.Port),
        "提交本执行节点命名输出端口的值。",
        [new ToolParameter(new ToolName("valuesJson"), "端口名到值的 JSON 对象", Required: true,
            Schema: ports is { Count: > 0 } ? ContractPortSchemas.Of(ports) : null)]);

    /// <summary>上报本轮无法完成的工具定义。</summary>
    public static ToolDefinition CannotComplete() => new(
        new ToolPath(ContractTools.CannotComplete),
        "上报本轮无法完成并给出原因。只在确实做不了时调用，能完成时直接结束本轮即可，不要调用。",
        [new ToolParameter(new ToolName("reason"), "无法完成的原因", Required: true)]);

    /// <summary>查看本轮执行契约状态的工具定义。</summary>
    public static ToolDefinition Status() => new(
        new ToolPath(ContractTools.Status),
        "查看本轮执行契约状态：节点、轮次、节点状态、尚缺的输出端口与已上报的无法完成原因。",
        []);
}
