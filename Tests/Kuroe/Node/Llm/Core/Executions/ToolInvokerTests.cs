using Kuroe.Node.Llm.Executions;
using Microsoft.Extensions.AI;
using Xunit;

namespace Kuroe.Node.Llm.Tests.Executions;

/// <summary>工具调用执行：按调用名取函数，未知工具与执行失败都以结果文本交回模型。</summary>
public sealed class ToolInvokerTests
{
    [Fact]
    public async Task A_known_tool_runs_and_returns_its_result()
    {
        ToolInvoker invoker = new([AIFunctionFactory.Create(() => "结果", "GetLocalTime")]);

        IReadOnlyList<FunctionResultContent> results = await invoker.InvokeAsync(
            [Call("call-1", "GetLocalTime")], CancellationToken.None);

        Assert.Equal("call-1", Assert.Single(results).CallId);
        Assert.Equal("结果", results[0].Result?.ToString());
    }

    [Fact]
    public async Task An_unknown_tool_becomes_an_error_result()
    {
        ToolInvoker invoker = new([]);

        IReadOnlyList<FunctionResultContent> results = await invoker.InvokeAsync(
            [Call("call-1", "bash_read_Bash")], CancellationToken.None);

        Assert.Equal("工具 bash_read_Bash 不存在。", results[0].Result?.ToString());
    }

    [Fact]
    public async Task A_failing_tool_becomes_an_error_result()
    {
        ToolInvoker invoker = new([AIFunctionFactory.Create(Fail, "GetLocalTime")]);

        IReadOnlyList<FunctionResultContent> results = await invoker.InvokeAsync(
            [Call("call-1", "GetLocalTime")], CancellationToken.None);

        Assert.Equal("工具 GetLocalTime 调用失败：坏了", results[0].Result?.ToString());
    }

    private static string Fail() => throw new InvalidOperationException("坏了");

    private static FunctionCallContent Call(string callId, string name) => new(callId, name);
}