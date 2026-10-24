using Kuroe.Node.Llm.Tools;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Tool.Contract;

/// <summary>契约工具的定义：路径、说明与参数。引擎按路径放行，交回能力的说明末尾附上本轮需交回的端口清单。</summary>
static class ContractToolDefinitions
{
    /// <summary>交回命名端口产出的工具定义，有端口清单时写进说明。</summary>
    public static ToolDefinition Port(IReadOnlyList<Port>? ports) => WithPorts(new ToolDefinition(
        new ToolPath(ContractTools.Port),
        "提交本执行节点命名输出端口的值。valuesJson 是端口名到值的 JSON 对象，端口必须与需交回的端口一一对应。"
        + "Text 端口给文本；Signal 端口给空文本或端口名；TextTrigger 端口给文本，或带 text 与 trigger 的对象；"
        + "SignalTrigger 端口给 true 或 false，或带 trigger 的对象。trigger 省略时为 true，false 表示本次产出只被下游启动组消耗、不触发下游节点。",
        [new ToolParameter(new ToolName("valuesJson"), "端口名到值的 JSON 对象，端口必须与需交回的端口一一对应", Required: true)]), ports);

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

    /// <summary>说明末尾附上本轮需交回的端口清单，未声明端口时原样返回。</summary>
    private static ToolDefinition WithPorts(ToolDefinition definition, IReadOnlyList<Port>? ports) =>
        ports is null
            ? definition
            : definition with { Description = $"{definition.Description}本节点需要交回的端口：{Ports.List(ports)}。" };
}
