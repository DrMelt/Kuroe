using Kuroe.Flow.Shared.Execution.Tasks;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Node.Llm.Tools;

/// <summary>契约工具的定义：名字、说明与参数。引擎按名字放行，说明末尾附上本轮需交回的端口清单。</summary>
static class ContractToolDefinitions
{
    /// <summary>交回条目拆分的工具定义。</summary>
    public static ToolDefinition WorkItems { get; } = new(
        new ToolName(ContractTools.WorkItems),
        "提交本执行节点的条目拆分。itemsJson 是对象数组的 JSON 文本，每项含 Title、Instruction、Acceptance。",
        [new ToolParameter(new ToolName("itemsJson"), "对象数组的 JSON 文本，每项含 Title、Instruction、Acceptance", Required: true)]);

    /// <summary>交回命名端口内容的工具定义，有端口清单时写进说明。</summary>
    public static ToolDefinition Port(IReadOnlyList<Port>? ports) => WithPorts(new ToolDefinition(
        new ToolName(ContractTools.Port),
        "提交本执行节点命名输出端口的内容。valuesJson 是端口名到文本的 JSON 对象，端口必须与声明一一对应。",
        [new ToolParameter(new ToolName("valuesJson"), "端口名到文本的 JSON 对象，端口必须与声明一一对应", Required: true)]), ports);

    /// <summary>声明本轮状态的工具定义，有端口清单时写进说明。</summary>
    public static ToolDefinition Status(IReadOnlyList<Port>? ports) => WithPorts(new ToolDefinition(
        new ToolName(ContractTools.Status),
        "声明本执行节点这一轮的状态。status 取 completed 交付完毕或 blocked 无法完成，blocked 时必须给出 reason。",
        [
            new ToolParameter(new ToolName("status"), "completed 或 blocked", Required: true),
            new ToolParameter(new ToolName("reason"), "无法完成的原因，status 为 blocked 时必填"),
        ]), ports);

    /// <summary>说明末尾附上本轮需交回的端口清单，未声明端口时原样返回。</summary>
    private static ToolDefinition WithPorts(ToolDefinition definition, IReadOnlyList<Port>? ports) =>
        ports is null
            ? definition
            : definition with { Description = $"{definition.Description}本节点需要交回的端口：{Ports.List(ports)}。" };
}
