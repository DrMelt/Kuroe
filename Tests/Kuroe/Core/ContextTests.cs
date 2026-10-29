using ApiHub.Shared.Models;
using Kuroe.Node.Llm;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
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

        InputValue goal = Assert.Single(plan.Input.Inputs);
        Assert.Equal("补齐 README", goal.Value.Text);
        Assert.IsType<StartSource>(goal.Value.Origin);
        Assert.Equal(ModelName.Create("fake").Value, (plan.Input.Declaration as KuroeNodeDeclaration)!.Model);
    }

    [Fact]
    public void Implement_context_carries_the_plan_output_and_its_origin()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        TaskSnapshot done = environment.Settle(environment.Submit("补齐 README"));
        ExecutionSnapshot plan = Assert.Single(done.Executables[0].Executions);
        ExecutionSnapshot implement = Assert.Single(done.Executables[1].Executions);

        InputValue value = Assert.Single(implement.Input.Inputs);
        Assert.Equal("计划产出", value.Value.Text);
        Assert.Equal(plan.Id, Assert.IsType<ExecutionSource>(value.Value.Origin).FromExecution);
    }

    [Fact]
    public void Start_constant_port_feeds_downstream_instead_of_goal()
    {
        using TestEnvironment environment = TestEnvironment.Create(StartConstantFlow);

        TaskSnapshot done = environment.Settle(environment.Submit("补齐 README"));
        ExecutionSnapshot execution = Assert.Single(done.Executables[0].Executions);

        InputValue seed = Assert.Single(execution.Input.Inputs);
        Assert.Equal("固定背景", seed.Value.Text);
        Assert.Equal(new PortName("背景"), Assert.IsType<StartSource>(seed.Value.Origin).Port);
    }

    [Fact]
    public void Multiple_start_ports_feed_downstream_in_the_declared_order()
    {
        using TestEnvironment environment = TestEnvironment.Create(MultiStartFlow);

        TaskSnapshot done = environment.Settle(environment.Submit(new StartInputs(
        [
            new StartInput(new PortName("目标"), "补齐 README"),
            new StartInput(new PortName("风格"), "技术文档风格"),
        ])));

        ExecutionSnapshot execution = Assert.Single(done.Executables[0].Executions);
        Assert.Equal(["补齐 README", "技术文档风格"], execution.Input.Inputs.Select(value => value.Text));
        Assert.Equal(new PortName("风格"), Assert.IsType<StartSource>(execution.Input.Inputs[1].Origin).Port);
    }

    [Fact]
    public void Submit_without_one_start_input_is_rejected()
    {
        using TestEnvironment environment = TestEnvironment.Create(MultiStartFlow);

        var submitted = environment.Tasks.Submit(
            StartInputs.Single(new PortName("目标"), "补齐 README"), new FlowName("默认"), null);

        Assert.Contains(submitted.ErrorsOrEmptyList,
            error => error.Description.Contains("开始端口 风格 没有输入。"));
    }

    [Fact]
    public void Submit_rejects_duplicate_start_input()
    {
        using TestEnvironment environment = TestEnvironment.Create(MultiStartFlow);

        var submitted = environment.Tasks.Submit(new StartInputs(
        [
            new StartInput(new PortName("目标"), "甲"),
            new StartInput(new PortName("目标"), "乙"),
            new StartInput(new PortName("风格"), "丙"),
        ]), new FlowName("默认"), null);

        Assert.Contains(submitted.ErrorsOrEmptyList,
            error => error.Description.Contains("开始端口 目标 的输入重复。"));
    }

    [Fact]
    public void Submit_rejects_input_for_a_start_port_with_a_value()
    {
        using TestEnvironment environment = TestEnvironment.Create(StartConstantFlow);

        var submitted = environment.Tasks.Submit(
            StartInputs.Single(new PortName("背景"), "覆盖"), new FlowName("默认"), null);

        Assert.Contains(submitted.ErrorsOrEmptyList,
            error => error.Description.Contains("开始端口 背景 带固定值"));
    }

    /// <summary>组内省略带默认值的输入端口时，执行输入追加声明值。</summary>
    [Fact]
    public void Omitted_input_port_falls_back_to_declared_default()
    {
        using TestEnvironment environment = TestEnvironment.Create(DefaultInputFlow);

        TaskSnapshot done = environment.Settle(environment.Submit("补齐 README"));
        ExecutionSnapshot execution = Implement(done);

        InputValue[] values = [.. execution.Input.Inputs];
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
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    { "Name": "制定计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                    {
                      "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" },
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
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    { "Name": "制定计划", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "计划", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                    {
                      "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" },
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
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    { "Name": "准备甲", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                    { "Name": "准备乙", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] },
                    {
                      "Name": "实施", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "MaxExecutions": 1,
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

    /// <summary>开始声明两个不带固定值的端口，执行节点按声明序接两个输入。</summary>
    private const string MultiStartFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "风格", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }, { "Name": "风格" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "目标": "Start@目标", "风格": "Start@风格", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    {
                      "Name": "干活",
                      "Kind": "Llm",
                      "Static": { "MODEL_NAME": "@MODEL_NAME" },
                      "Inputs": [{ "Name": "目标" }, { "Name": "风格" }],
                      "From": [{ "目标": "@目标", "风格": "@风格" }]
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
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "背景", "Type": "Text", "Value": "固定背景" }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Static": true }] },
              "Nodes": [
                { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "背景" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "背景": "Start@背景", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                  { "Name": "干活", "Kind": "Llm", "Static": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "背景": "@背景" } ] }
                ] }
              ]
            }
          ]
        }
        """;
}
