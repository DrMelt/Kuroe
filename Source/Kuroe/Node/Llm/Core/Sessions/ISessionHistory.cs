using ApiHub.Shared.Models;
using Kuroe.Execution.Shared;
using Kuroe.Tool;
using Microsoft.Extensions.AI;

namespace Kuroe.Node.Llm.Sessions;

/// <summary>会话历史的来源：决定用哪个模型、本轮请求的模型侧内容与历史起点。</summary>
internal interface ISessionHistory
{
    /// <summary>本轮使用的模型，未选择时为空。</summary>
    ModelName? Model { get; }

    /// <summary>本轮系统指令，没有内容时为空。</summary>
    string? SystemPrompt { get; }

    /// <summary>本轮可用的工具路径，没有能力时为空集合。</summary>
    IReadOnlyList<ToolPath> ToolPaths { get; }

    /// <summary>按来源重铺历史。</summary>
    void LayOut(List<ChatMessage> messages);
}

/// <summary>一次执行的会话：历史起点由执行的输入端口给出，执行期间来源不再改变。</summary>
internal sealed class ExecutionHistory(ExecutionRequest request, LlmExecutionParameters parameters) : ISessionHistory
{
    public ModelName? Model =>
        parameters.Model is { Length: > 0 } model && ModelName.Create(model) is { IsError: false } parsed
            ? parsed.Value
            : null;

    /// <summary>该执行的系统指令：节点声明的恒定块，置于请求最前。</summary>
    public string? SystemPrompt => parameters.SystemPrompt();

    /// <summary>该执行的能力面：节点声明的白名单与按输出端口附上的契约能力。</summary>
    public IReadOnlyList<ToolPath> ToolPaths => parameters.ToolPaths(request.OutputPorts);

    public void LayOut(List<ChatMessage> messages)
    {
        messages.Clear();
        foreach (ExecutionInputValue value in request.Inputs)
        {
            messages.Add(new ChatMessage(ChatRole.User, Text(value)));
        }
    }

    /// <summary>一条取值在请求里的写法：有来源标识时标明来源，没有时原样给出。</summary>
    private static string Text(ExecutionInputValue value) =>
        value.Source is { Length: > 0 } source ? $"{source}：\n{value.Text}" : value.Text;
}
