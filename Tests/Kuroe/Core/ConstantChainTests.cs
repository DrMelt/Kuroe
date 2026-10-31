using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Xunit;

namespace Kuroe.Tests;

/// <summary>常量链：静态输入绑定到上游唯一一次执行的产出，上游发布后下游按依赖启动。</summary>
public sealed class ConstantChainTests
{
    [Fact]
    public void Second_node_starts_after_first_constant_is_published()
    {
        using TestEnvironment environment = TestEnvironment.Create(ConstantChainFlow);

        TaskId id = environment.Submit("目标内容");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        IReadOnlyList<IExecution> executions = environment.Runner.Started;
        Assert.Equal(2, executions.Count);
        Assert.Equal("第一步", executions[0].Input.Node.Label.Value);
        Assert.Equal("第二步", executions[1].Input.Node.Label.Value);
        Assert.Contains(executions[0].Input.Inputs,
            value => value.Port.Value == "目标" && value.Text == "目标内容");
        Assert.Contains(executions[1].Input.Inputs,
            value => value.Port.Value == "结果" && value.Text == "结果产出");
        Assert.All(done.Executables, node => Assert.Single(node.Executions));
    }

    [Fact]
    public void Container_referenced_twice_publishes_one_constant_per_instance()
    {
        using TestEnvironment environment = TestEnvironment.Create(ContainerConstantFlow);

        TaskId id = environment.Submit("目标内容");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(6, environment.Runner.Started.Count);
        Assert.All(done.Executables, node => Assert.Single(node.Executions));
        IExecution compare = environment.Runner.Started.Single(execution => execution.Input.Node.Label.Value == "比选");
        Assert.Equal(2, compare.Input.Inputs.Count(value => value.Port.Value is "甲稿" or "乙稿"));
    }

    [Fact]
    public void Once_node_can_retry_after_a_failed_execution()
    {
        using TestEnvironment environment = TestEnvironment.Create(RetryOnceFlow);
        environment.Runner.FailsWhen = execution =>
            execution.Input.Node.Label.Value == "重试" && execution.Input.ExecutionCount == 1;

        TaskId id = environment.Submit("目标内容");
        environment.Wait(id, snapshot => snapshot.State == TaskState.Blocked);
        environment.Tasks.Rework(id).ThrowIfError();
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(2, environment.Runner.Started.Count(execution => execution.Input.Node.Label.Value == "重试"));
    }

    /// <summary>容器引用两次，各实例的成员链只执行一次，下游静态读取两份常量。</summary>
    private const string ContainerConstantFlow = """
        {
          "Nodes": [
            {
              "Name": "成稿",
              "Kind": "Container",
              "Inputs": [
                { "Name": "大纲", "Type": "Text", "Mode": "Constant", "Role": "Content" },
                { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }
              ],
              "Outputs": [{ "Name": "全文", "Type": "Text", "Mode": "Constant", "Ref": "合成@全文" }],
              "Nodes": [
                {
                  "Name": "撰写",
                  "Kind": "Llm",
                  "Emission": "Once",
                  "Static": { "大纲": "@大纲", "MODEL_NAME": "@MODEL_NAME" },
                  "Inputs": [{ "Name": "大纲", "Type": "Text", "Mode": "Constant", "Role": "Content" }],
                  "Outputs": [{ "Name": "正文", "Type": "Text", "Mode": "Constant" }]
                },
                {
                  "Name": "合成",
                  "Kind": "Llm",
                  "Emission": "Once",
                  "Static": { "正文": "撰写@正文", "MODEL_NAME": "@MODEL_NAME" },
                  "Inputs": [{ "Name": "正文", "Type": "Text", "Mode": "Constant", "Role": "Content" }],
                  "Outputs": [{ "Name": "全文", "Type": "Text", "Mode": "Constant" }]
                }
              ]
            }
          ],
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [
                { "Name": "目标", "Type": "Text", "Mode": "Constant" },
                { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }
              ] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [
                    { "Name": "目标", "Type": "Text", "Mode": "Constant", "Role": "Content" },
                    { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }
                  ],
                  "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" },
                  "Nodes": [
                    {
                      "Name": "制定大纲",
                      "Kind": "Llm",
                      "Emission": "Once",
                      "Static": { "目标": "@目标", "MODEL_NAME": "@MODEL_NAME" },
                      "Inputs": [{ "Name": "目标", "Type": "Text", "Mode": "Constant", "Role": "Content" }],
                      "Outputs": [{ "Name": "大纲", "Type": "Text", "Mode": "Constant" }]
                    },
                    {
                      "Label": "甲稿",
                      "Use": "成稿",
                      "In": { "大纲": "制定大纲@大纲", "MODEL_NAME": "@MODEL_NAME" }
                    },
                    {
                      "Label": "乙稿",
                      "Use": "成稿",
                      "In": { "大纲": "制定大纲@大纲", "MODEL_NAME": "@MODEL_NAME" }
                    },
                    {
                      "Name": "比选",
                      "Kind": "Llm",
                      "Emission": "Once",
                      "Static": {
                        "甲稿": "甲稿@全文",
                        "乙稿": "乙稿@全文",
                        "MODEL_NAME": "@MODEL_NAME"
                      },
                      "Inputs": [
                        { "Name": "甲稿", "Type": "Text", "Mode": "Constant", "Role": "Content" },
                        { "Name": "乙稿", "Type": "Text", "Mode": "Constant", "Role": "Content" }
                      ],
                      "Outputs": [{ "Name": "定稿", "Type": "Text", "Mode": "Constant" }]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void Dynamic_node_waits_for_a_runtime_constant_and_keeps_its_trigger()
    {
        using TestEnvironment environment = TestEnvironment.Create(MixedInputFlow);

        TaskId id = environment.Submit(new StartInputs(
        [
            new StartInput(new PortName("触发"), "触发内容"),
            new StartInput(new PortName("目标"), "目标内容"),
        ]));
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        IExecution second = environment.Runner.Started.Single(execution => execution.Input.Node.Label.Value == "第二步");
        Assert.Contains(second.Input.Inputs, value => value.Port.Value == "触发" && value.Text == "触发内容");
        Assert.Contains(second.Input.Inputs, value => value.Port.Value == "结果" && value.Text == "结果产出");
    }

    /// <summary>第二步有动态触发与运行期常量输入，常量产出前不消耗触发版本。</summary>
    private const string MixedInputFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [
                { "Name": "触发", "Type": "Text" },
                { "Name": "目标", "Type": "Text", "Mode": "Constant" },
                { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }
              ] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [
                    { "Name": "触发" },
                    { "Name": "目标", "Type": "Text", "Mode": "Constant", "Role": "Content" },
                    { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }
                  ],
                  "In": { "触发": "Start@触发", "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" },
                  "Nodes": [
                    {
                      "Name": "第一步",
                      "Emission": "Once",

                      "Kind": "Llm",
                      "Static": { "目标": "@目标", "MODEL_NAME": "@MODEL_NAME" },
                      "Inputs": [{ "Name": "目标", "Type": "Text", "Mode": "Constant", "Role": "Content" }],
                      "Outputs": [{ "Name": "结果", "Type": "Text", "Mode": "Constant" }]
                    },
                    {
                      "Name": "第二步",
                      "Kind": "Llm",
                      "Static": { "结果": "第一步@结果", "MODEL_NAME": "@MODEL_NAME" },
                      "Inputs": [
                        { "Name": "触发", "Type": "Text" },
                        { "Name": "结果", "Type": "Text", "Mode": "Constant", "Role": "Content" }
                      ],
                      "Outputs": [{ "Name": "定稿", "Type": "Text" }],
                      "From": [{ "触发": "@触发" }]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>第一步是常量开始输入驱动的静态节点，第二步等待第一步的常量产出。</summary>
    private const string ConstantChainFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [
                { "Name": "目标", "Type": "Text", "Mode": "Constant" },
                { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }
              ] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [
                    { "Name": "目标", "Type": "Text", "Mode": "Constant", "Role": "Content" },
                    { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }
                  ],
                  "In": { "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" },
                  "Nodes": [
                    {
                      "Name": "第一步",
                      "Emission": "Once",

                      "Kind": "Llm",
                      "Static": { "目标": "@目标", "MODEL_NAME": "@MODEL_NAME" },
                      "Inputs": [
                        { "Name": "目标", "Type": "Text", "Mode": "Constant", "Role": "Content" }
                      ],
                      "Outputs": [{ "Name": "结果", "Type": "Text", "Mode": "Constant" }]
                    },
                    {
                      "Name": "第二步",
                      "Kind": "Llm",
                      "Emission": "Once",

                      "Static": { "结果": "第一步@结果", "MODEL_NAME": "@MODEL_NAME" },
                      "Inputs": [
                        { "Name": "结果", "Type": "Text", "Mode": "Constant", "Role": "Content" }
                      ],
                      "Outputs": [{ "Name": "定稿", "Type": "Text", "Mode": "Constant" }]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;
    [Fact]
    public void Context_constant_static_input_enters_context_content()
    {
        using TestEnvironment environment = TestEnvironment.Create(ContextConstantFlow);

        TaskId id = environment.Submit(new StartInputs(
        [
            new StartInput(new PortName("目标"), "目标内容"),
            new StartInput(new PortName("上下文"), "上下文值"),
        ]));
        environment.Settle(id);

        IExecution execution = environment.Runner.Started.Single(candidate => candidate.Input.Node.Label.Value == "读取");
        InputValue first = execution.Input.Inputs[0];
        Assert.Equal(new PortName("上下文"), first.Port);
        Assert.Equal("上下文值", first.Text);
    }

    /// <summary>单个 Once 节点首次执行失败，返工后可以再次执行并完成发布。</summary>
    private const string RetryOnceFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [
                { "Name": "目标", "Type": "Text" },
                { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }
              ] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }],
                  "In": { "MODEL_NAME": "Start@MODEL_NAME" },
                  "Nodes": [
                    {
                      "Name": "重试",
                      "Kind": "Llm",
                      "Emission": "Once",
                      "Static": { "MODEL_NAME": "@MODEL_NAME" },
                      "Inputs": [{ "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }],
                      "Outputs": [{ "Name": "结果", "Type": "Text", "Mode": "Constant" }]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;


    /// <summary>Context 常量作为静态内容输入时进入上下文内容，排在其他内容之前。</summary>
    private const string ContextConstantFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [
                { "Name": "目标", "Type": "Text" },
                { "Name": "上下文", "Type": "Context", "Mode": "Constant" },
                { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }
              ] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [
                    { "Name": "上下文", "Type": "Context", "Mode": "Constant" },
                    { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }
                  ],
                  "In": { "上下文": "Start@上下文", "MODEL_NAME": "Start@MODEL_NAME" },
                  "Nodes": [
                    {
                      "Name": "读取",
                      "Kind": "Llm",
                      "Static": { "上下文": "@上下文", "MODEL_NAME": "@MODEL_NAME" },
                      "Inputs": [
                        { "Name": "上下文", "Type": "Context", "Mode": "Constant", "Role": "Content" },
                        { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }
                      ],
                      "Outputs": [{ "Name": "结果", "Type": "Text" }]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;


}
