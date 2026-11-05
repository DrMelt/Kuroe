using Microsoft.Extensions.AI;

namespace Kuroe.Node.Llm.Executions;

/// <summary>一轮工具调用的执行面：按调用名执行函数，为每个调用给出结果。</summary>
internal interface IToolInvoker
{
    /// <summary>按调用顺序执行函数，调用失败与未知工具以结果文本交回模型。</summary>
    Task<IReadOnlyList<FunctionResultContent>> InvokeAsync(
        IReadOnlyList<FunctionCallContent> calls,
        CancellationToken cancellationToken);
}

/// <summary>按函数名在本轮工具面里取函数并执行。</summary>
internal sealed class ToolInvoker(IReadOnlyList<AIFunction> functions) : IToolInvoker
{
    public async Task<IReadOnlyList<FunctionResultContent>> InvokeAsync(
        IReadOnlyList<FunctionCallContent> calls,
        CancellationToken cancellationToken)
    {
        List<FunctionResultContent> results = [];
        foreach (FunctionCallContent call in calls)
        {
            results.Add(new FunctionResultContent(call.CallId, await InvokeAsync(call, cancellationToken)));
        }

        return results;
    }

    private async Task<string> InvokeAsync(FunctionCallContent call, CancellationToken cancellationToken)
    {
        AIFunction? function = functions.FirstOrDefault(entry => entry.Name == call.Name);
        if (function is null)
        {
            return $"工具 {call.Name} 不存在。";
        }

        try
        {
            object? outcome = await function.InvokeAsync(new AIFunctionArguments(call.Arguments), cancellationToken);

            return outcome?.ToString() ?? string.Empty;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return $"工具 {call.Name} 调用失败：{ex.Message}";
        }
    }
}