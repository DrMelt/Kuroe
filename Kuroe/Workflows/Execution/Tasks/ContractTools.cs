using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Execution.Tasks;

/// <summary>执行节点与模型之间的契约定义：三个提交工具的名字、说明与参数。工具载体按它声明函数，引擎按它的路径放行。</summary>
internal static class ContractTools
{
    /// <summary>交回条目拆分的工具定义，声明 Items 端口的执行节点放行。</summary>
    internal static ToolDefinition Plan { get; } = new(
        new ToolName("SubmitPlanItems"),
        "提交本执行节点的条目拆分。itemsJson 是对象数组的 JSON 文本，每项含 Title、Instruction、Acceptance。",
        [new ToolParameter(new ToolName("itemsJson"), "对象数组的 JSON 文本，每项含 Title、Instruction、Acceptance", Required: true)]);

    /// <summary>交回命名端口内容的工具定义，声明 Data 端口的执行节点放行。</summary>
    internal static ToolDefinition Port { get; } = new(
        new ToolName("SubmitPortValues"),
        "提交本执行节点命名输出端口的内容。valuesJson 是端口名到文本的 JSON 对象，端口必须与声明一一对应。",
        [new ToolParameter(new ToolName("valuesJson"), "端口名到文本的 JSON 对象，端口必须与声明一一对应", Required: true)]);

    /// <summary>声明本轮状态的工具定义，所有执行节点放行。</summary>
    internal static ToolDefinition Status { get; } = new(
        new ToolName("SubmitStatus"),
        "声明本执行节点这一轮的状态。status 取 completed 交付完毕或 blocked 无法完成，blocked 时必须给出 reason。",
        [
            new ToolParameter(new ToolName("status"), "completed 或 blocked", Required: true),
            new ToolParameter(new ToolName("reason"), "无法完成的原因，status 为 blocked 时必填"),
        ]);

    /// <summary>说明末尾附上本轮需交回的端口清单，未声明端口时原样返回。</summary>
    internal static ToolDefinition WithPorts(ToolDefinition definition, IReadOnlyList<Port>? ports) =>
        ports is null
            ? definition
            : definition with { Description = $"{definition.Description}本节点需要交回的端口：{ContractText.List(ports)}。" };
}
