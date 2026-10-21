using ApiHub.Shared.Models;
using Kuroe.Node.Llm;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
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
        Assert.Equal("计划产出", value.Text);
        Assert.Equal(plan.Id, Assert.IsType<ExecutionSource>(value.Origin).FromExecution);
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

    /// <summary>组内省略带默认值的输入端口时，执行输入追加声明值。</summary>
    [Fact]
    public void Omitted_input_port_falls_back_to_declared_default()
    {
        using TestEnvironment environment = TestEnvironment.Create(DefaultInputFlow);

        TaskSnapshot done = environment.Settle(environment.Submit("补齐 README"));
        ExecutionSnapshot execution = Implement(done);

        ContextValue[] values = [.. execution.Input.Inputs];
        Assert.Equal(["计划产出", "固定格式"], values.Select(value => value.Text));
        Assert.IsType<ExecutionSource>(values[0].Origin);
        DefaultSource fallback = Assert.IsType<DefaultSource>(values[1].Origin);
        Assert.Equal(new PortName("格式"), fallback.Port);
    }

    /// <summary>显式连接优先，默认值不进入执行输入。</summary>
    [Fact]
    public void Connected_input_wins_over_default()
    {
        using TestEnvironment environment = TestEnvironment.Create(ConnectedDefaultInputFlow);

        TaskSnapshot done = environment.Settle(environment.Submit("补齐 README"));
        ExecutionSnapshot execution = Implement(done);

        Assert.Equal(["计划产出", "计划产出"], execution.Input.Inputs.Select(value => value.Text));
        Assert.DoesNotContain(execution.Input.Inputs, value => value.Origin is DefaultSource);
    }

    /// <summary>多组时只取生效组的连接，默认值补齐该组未连接的端口。</summary>
    [Fact]
    public void Effective_group_limits_inputs_to_its_own_connections()
    {
        using TestEnvironment environment = TestEnvironment.Create(ActiveGroupDefaultFlow);
        environment.Runner.DelayFor = execution =>
            execution.Input.Node.Value.Contains("准备甲", StringComparison.Ordinal) ? 250 : 10;
        environment.Runner.PortValuesJsonFor = execution =>
        {
            if (execution.Input.Node.Value.Contains("准备甲", StringComparison.Ordinal))
            {
                return "{ \"结论\": \"甲产出\" }";
            }

            return execution.Input.Node.Value.Contains("准备乙", StringComparison.Ordinal)
                ? "{ \"结论\": \"乙产出\" }"
                : null;
        };

        TaskSnapshot done = environment.Settle(environment.Submit("补齐 README"));
        ExecutionSnapshot execution = Implement(done);

        Assert.Equal(["乙产出", "默认依据", "默认附加"], execution.Input.Inputs.Select(value => value.Text));
        Assert.DoesNotContain(execution.Input.Inputs, value => value.Text.Contains("甲产出", StringComparison.Ordinal));
        Assert.All(execution.Input.Inputs.Skip(1), value => Assert.IsType<DefaultSource>(value.Origin));
    }

    /// <summary>取实施节点的唯一一次执行。</summary>
    private static ExecutionSnapshot Implement(TaskSnapshot done) =>
        Assert.Single(done.Executables
            .Single(executable => executable.Executable.Path.Value.EndsWith("实施", StringComparison.Ordinal))
            .Executions);

    /// <summary>实施节点声明一个默认值端口，From 只连接计划。</summary>
    private const string DefaultInputFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "制定计划", "Kind": "Llm", "Model": "规划者", "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
                    {
                      "Name": "实施", "Kind": "Llm", "Model": "执行者",
                      "Inputs": [{ "Name": "任务" }, { "Name": "格式", "Default": "固定格式" }],
                      "From": [ { "任务": "制定计划@计划" } ]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>实施节点的两个输入都显式连接，默认值不生效。</summary>
    private const string ConnectedDefaultInputFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [
                { "Name": "规划者", "Model": "fake" },
                { "Name": "执行者", "Model": "fake" }
              ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "制定计划", "Kind": "Llm", "Model": "规划者", "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
                    {
                      "Name": "实施", "Kind": "Llm", "Model": "执行者",
                      "Inputs": [{ "Name": "任务" }, { "Name": "格式", "Default": "固定格式" }],
                      "From": [ { "任务": "制定计划@计划", "格式": "制定计划@计划" } ]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>两套启动组：第一组连接附加端口，第二组省略两个默认端口。</summary>
    private const string ActiveGroupDefaultFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Models": [ { "Name": "执行者", "Model": "fake" } ],
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    { "Name": "准备甲", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
                    { "Name": "准备乙", "Kind": "Llm", "Model": "执行者", "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "Start@目标" } ] },
                    {
                      "Name": "实施", "Kind": "Llm", "Model": "执行者", "MaxExecutions": 1,
                      "Inputs": [
                        { "Name": "结论" },
                        { "Name": "依据", "Default": "默认依据" },
                        { "Name": "附加", "Default": "默认附加" }
                      ],
                      "From": [
                        { "结论": "准备甲@结论", "附加": "准备甲@结论" },
                        { "结论": "准备乙@结论" }
                      ]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

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
