using ApiHub.Shared.Models;
using Kuroe.Node.Llm;
using Kuroe.Node.Llm.Sessions;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using Xunit;

namespace Kuroe.Tests;

/// <summary>输入取值只由上游装配：每个执行看到的内容与出处都可追溯。</summary>
public sealed class ContextTests
{
    [Fact]
    public void First_step_gets_goal_from_start_port_and_nothing_else()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        TaskSnapshot done = environment.Settle(environment.Submit("补齐 README"));
        ExecutionSnapshot plan = Assert.Single(done.Executables[0].Executions);

        ContextValue goal = Assert.Single(plan.Input.Inputs);
        Assert.Equal("补齐 README", goal.Text);
        Assert.IsType<StartSource>(goal.Origin);
        Assert.Equal(ModelName.Create("fake").Value, (plan.Input.Declaration as KuroeNodeDeclaration)!.Model);
    }

    [Fact]
    public void Implement_context_carries_the_plan_output_and_its_origin()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        TaskSnapshot done = environment.Settle(environment.Submit("补齐 README"));
        ExecutionSnapshot plan = Assert.Single(done.Executables[0].Executions);
        ExecutionSnapshot implement = Assert.Single(done.Executables[1].Executions);

        ContextValue value = Assert.Single(implement.Input.Inputs);
        Assert.Equal("节点「整体/制定计划」的端口「计划」产出：\n计划产出", LlmHistoryValue(implement));
        Assert.Equal(plan.Id, Assert.IsType<ExecutionSource>(value.Origin).FromExecution);
    }

    /// <summary>本轮请求历史里的一条取值：由 Llm 侧按取值与出处渲染。</summary>
    private static string LlmHistoryValue(ExecutionSnapshot execution)
    {
        List<Microsoft.Extensions.AI.ChatMessage> messages = [];
        new ExecutionHistory(execution.Input).LayOut(messages);

        return Assert.Single(messages).Text;
    }

    [Fact]
    public void Start_constant_port_feeds_downstream_instead_of_goal()
    {
        using TestEnvironment environment = TestEnvironment.Create(StartConstantFlow);

        TaskSnapshot done = environment.Settle(environment.Submit("补齐 README"));
        ExecutionSnapshot execution = Assert.Single(done.Executables[0].Executions);

        ContextValue seed = Assert.Single(execution.Input.Inputs);
        Assert.Equal("固定背景", seed.Text);
        Assert.Equal(new PortName("背景"), Assert.IsType<StartSource>(seed.Origin).Port);
    }

    /// <summary>开始除目标外声明一个带固定值的端口，下游取固定值端口。</summary>
    private const string StartConstantFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "背景", "Type": "Text", "Value": "固定背景" }] },
              "Nodes": [
                { "Name": "整体", "Kind": "Container", "Nodes": [
                  { "Name": "干活", "Kind": "Llm", "Model": "执行者", "From": [ { "背景": "Start@背景" } ] }
                ] }
              ]
            }
          ]
        }
        """;
}
