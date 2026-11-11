using ErrorOr;
using Kuroe.Shared;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Xunit;

namespace Kuroe.Integration.Tests.Runtime.Engine;

/// <summary>宿主开始输入表把开始端口绑定到 WORKSPACE_PATH 与 TASK_WORK_PATH，开始端口为静态端口，
/// 由容器静态转发并绑定到执行节点，值在任务启动时给出，不接受提交输入。</summary>
public sealed class StartPathsTests
{
    /// <summary>执行节点取开始的目标与两个宿主输入。</summary>
    private const string FixedPortFlow = """
        {
          "HostStartInputs": { "默认": { "启动": "WORKSPACE_PATH", "流程": "TASK_WORK_PATH" } },
          "Flows": [
            {
              "Name": "默认",
              "Ports": [
                  { "Name": "目标", "Type": "Text" },
                  { "Name": "启动", "Type": "Text", "Mode": "Constant" },
                  { "Name": "流程", "Type": "Text", "Mode": "Constant" },
                  { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }
                ],
              "Root": {
                "Name": "整体",
                "Kind": "Container",
                "Inputs": [{ "Name": "启动", "Type": "Text", "Mode": "Constant" }, { "Name": "流程", "Type": "Text", "Mode": "Constant" }, { "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "启动": "@启动", "流程": "@流程", "MODEL_NAME": "@MODEL_NAME" }, "Nodes": [
                  {
                    "Name": "读取路径",
                    "Kind": "Llm",
                    "Bindings": { "MODEL_NAME": "@MODEL_NAME", "启动": "@启动", "流程": "@流程" },
                    "Inputs": [
                      { "Name": "目标" },
                      { "Name": "启动", "Type": "Text", "Mode": "Constant" },
                      { "Name": "流程", "Type": "Text", "Mode": "Constant" }
                    ],
                    "From": [
                      {
                        "目标": "@目标"
                      }
                    ]
                  }
                ]
              }
            }
          ]
        }
        """;

    [Fact]
    public void Host_paths_reach_the_execution_static_inputs()
    {
        using TestEnvironment environment = TestEnvironment.Create(FixedPortFlow);

        TaskId id = environment.Submit("整理目录");
        TaskSnapshot task = environment.Settle(id);
        IExecution execution = Assert.Single(environment.Runner.Started);
        string taskDirectory = Assert.Single(
            Directory.GetDirectories(Path.Combine(environment.Root, ".kuroe_tasks")),
            path => Path.GetFileName(path) != ".records");

        Assert.Equal(TaskState.Done, task.State);
        Assert.NotNull(task.StartTexts.Text(new PortName("启动")));
        Assert.Equal(environment.Root, execution.Input.Text(new PortName("启动")));
        Assert.Equal(taskDirectory, execution.Input.Text(new PortName("流程")));
        Assert.DoesNotContain(execution.Input.Inputs, value => value.Text == environment.Root);
        Assert.DoesNotContain(execution.Input.Inputs, value => value.Text == taskDirectory);
    }

    [Fact]
    public void A_host_port_rejects_submitted_input()
    {
        using TestEnvironment environment = TestEnvironment.Create(FixedPortFlow);

        ErrorOr<TaskSnapshot> submitted = environment.Tasks.Submit(
            new StartInputs(
            [
                new StartInput(new PortName("目标"), "整理目录"),
                new StartInput(new PortName("启动"), environment.Root),
            ]),
            new FlowName("默认"),
            null);

        Assert.True(submitted.IsError);
        Assert.Equal(FlowErrorCodes.TaskInput, submitted.FirstError.Code);
        Assert.Equal("开始端口 启动 由宿主提供，不接受提交输入。", submitted.FirstError.Description);
    }

    /// <summary>开始端口名与宿主输入名相同但不在表里时按普通用户输入处理，不接受宿主值。</summary>
    [Fact]
    public void A_port_named_after_a_host_input_takes_submitted_input()
    {
        using TestEnvironment environment = TestEnvironment.Create(NamedPortFlow);

        ErrorOr<TaskSnapshot> missing = environment.Tasks.Submit(
            StartInputs.Single(new PortName("目标"), "整理目录"), new FlowName("默认"), null);
        Assert.True(missing.IsError);
        Assert.Equal("开始端口 WORKSPACE_PATH 没有输入。", missing.FirstError.Description);

        TaskId id = environment.Submit(new StartInputs(
        [
            new StartInput(new PortName("目标"), "整理目录"),
            new StartInput(new PortName("WORKSPACE_PATH"), "用户给的路径"),
        ]));
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal("用户给的路径", done.Inputs.Text(new PortName("WORKSPACE_PATH")));
        Assert.Null(done.StartTexts.Text(new PortName("WORKSPACE_PATH")));
    }

    /// <summary>带默认值的开始端口进表后由宿主给值，默认值不生效。</summary>
    [Fact]
    public void A_defaulted_port_in_the_form_takes_the_host_value()
    {
        using TestEnvironment environment = TestEnvironment.Create(DefaultedPortFlow);

        TaskSnapshot done = environment.Settle(environment.Submit("整理目录"));

        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal(environment.Root, done.StartTexts.Text(new PortName("启动")));
        Assert.DoesNotContain(done.Inputs.Values, value => value.Text == "fallback");
    }

    /// <summary>表里的开始端口必须在流程开始入口声明，提交时报告。</summary>
    [Fact]
    public void An_undeclared_form_port_is_rejected_at_submit()
    {
        using TestEnvironment environment = TestEnvironment.Create(UndeclaredFormPortFlow);

        ErrorOr<TaskSnapshot> submitted = environment.Tasks.Submit(
            StartInputs.Single(new PortName("目标"), "整理目录"), new FlowName("默认"), null);

        Assert.True(submitted.IsError);
        Assert.Equal(ErrorCodes.TaskStartInput, submitted.FirstError.Code);
        Assert.Equal("流程 默认 的开始入口没有声明开始输入表里的端口 未声明。", submitted.FirstError.Description);
    }

    /// <summary>表里的宿主输入名必须受支持，提交时报告。</summary>
    [Fact]
    public void An_unsupported_form_input_is_rejected_at_submit()
    {
        using TestEnvironment environment = TestEnvironment.Create(UnsupportedFormInputFlow);

        ErrorOr<TaskSnapshot> submitted = environment.Tasks.Submit(
            StartInputs.Single(new PortName("目标"), "整理目录"), new FlowName("默认"), null);

        Assert.True(submitted.IsError);
        Assert.Equal(ErrorCodes.TaskStartInput, submitted.FirstError.Code);
        Assert.Equal("流程 默认 的开始端口 启动 引用了不受支持的宿主输入 NOPE。", submitted.FirstError.Description);
    }

    /// <summary>非 Text 开始端口不能由宿主给值，提交时报告而不是丢弃。</summary>
    [Fact]
    public void A_non_text_form_port_is_rejected_at_submit()
    {
        using TestEnvironment environment = TestEnvironment.Create(SignalFormPortFlow);

        ErrorOr<TaskSnapshot> submitted = environment.Tasks.Submit(
            StartInputs.Single(new PortName("目标"), "整理目录"), new FlowName("默认"), null);

        Assert.True(submitted.IsError);
        Assert.Equal(ErrorCodes.TaskStartInput, submitted.FirstError.Code);
        Assert.Equal("流程 默认 的开始端口 触发 不是 Text 端口，不能由宿主给值。", submitted.FirstError.Description);
    }

    /// <summary>带固定值的开始端口不能进表，提交时报告。</summary>
    [Fact]
    public void A_constant_port_in_the_form_is_rejected_at_submit()
    {
        using TestEnvironment environment = TestEnvironment.Create(ConstantFormPortFlow);

        ErrorOr<TaskSnapshot> submitted = environment.Tasks.Submit(
            StartInputs.Single(new PortName("目标"), "整理目录"), new FlowName("默认"), null);

        Assert.True(submitted.IsError);
        Assert.Equal("流程 默认 的开始端口 环境 带固定值，不能由宿主给值。", submitted.FirstError.Description);
    }

    /// <summary>开始端口名取宿主输入名，但表里没有该流程的表项，端口按用户输入处理。</summary>
    private const string NamedPortFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Ports": [
                { "Name": "目标", "Type": "Text" },
                { "Name": "WORKSPACE_PATH", "Type": "Text", "Mode": "Constant" },
                { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }
              ],
              "Root": {
                "Name": "实施",
                "Kind": "Llm",
                "Bindings": { "MODEL_NAME": "@MODEL_NAME", "WORKSPACE_PATH": "@WORKSPACE_PATH" },
                "Inputs": [
                  { "Name": "目标" },
                  { "Name": "WORKSPACE_PATH", "Type": "Text", "Mode": "Constant" }
                ],
                "From": [ { "目标": "@目标" } ]
              }
            }
          ]
        }
        """;

    /// <summary>带默认值的开始端口进表，由宿主给值。</summary>
    private const string DefaultedPortFlow = """
        {
          "HostStartInputs": { "默认": { "启动": "WORKSPACE_PATH" } },
          "Flows": [
            {
              "Name": "默认",
              "Ports": [
                { "Name": "目标", "Type": "Text" },
                { "Name": "启动", "Type": "Text", "Mode": "Constant", "Default": "fallback" },
                { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }
              ],
              "Root": {
                "Name": "实施",
                "Kind": "Llm",
                "Bindings": { "MODEL_NAME": "@MODEL_NAME", "启动": "@启动" },
                "Inputs": [
                  { "Name": "目标" },
                  { "Name": "启动", "Type": "Text", "Mode": "Constant" }
                ],
                "From": [ { "目标": "@目标" } ]
              }
            }
          ]
        }
        """;

    /// <summary>表里写了流程没有声明的开始端口。</summary>
    private const string UndeclaredFormPortFlow = """
        {
          "HostStartInputs": { "默认": { "未声明": "WORKSPACE_PATH" } },
          "Flows": [
            {
              "Name": "默认",
              "Ports": [{ "Name": "目标", "Type": "Text" }],
              "Root": { "Name": "收话", "Kind": "Input", "Parameters": { "Question": "请补充背景" }, "Inputs": [{ "Name": "目标" }], "From": [ { "目标": "@目标" } ] }
            }
          ]
        }
        """;

    /// <summary>表里写了带固定值的开始端口。</summary>
    private const string ConstantFormPortFlow = """
        {
          "HostStartInputs": { "默认": { "环境": "WORKSPACE_PATH" } },
          "Flows": [
            {
              "Name": "默认",
              "Ports": [
                { "Name": "目标", "Type": "Text" },
                { "Name": "环境", "Type": "Text", "Mode": "Constant", "Value": "固定" }
              ],
              "Root": { "Name": "收话", "Kind": "Input", "Parameters": { "Question": "请补充背景" }, "Inputs": [{ "Name": "目标" }], "From": [ { "目标": "@目标" } ] }
            }
          ]
        }
        """;

    /// <summary>表里写了不受支持的宿主输入名。</summary>
    private const string UnsupportedFormInputFlow = """
        {
          "HostStartInputs": { "默认": { "启动": "NOPE" } },
          "Flows": [
            {
              "Name": "默认",
              "Ports": [
                { "Name": "目标", "Type": "Text" },
                { "Name": "启动", "Type": "Text", "Mode": "Constant" }
              ],
              "Root": { "Name": "收话", "Kind": "Input", "Parameters": { "Question": "请补充背景" }, "Inputs": [{ "Name": "目标" }], "From": [ { "目标": "@目标" } ] }
            }
          ]
        }
        """;

    /// <summary>表里写了 Signal 开始端口。</summary>
    private const string SignalFormPortFlow = """
        {
          "HostStartInputs": { "默认": { "触发": "WORKSPACE_PATH" } },
          "Flows": [
            {
              "Name": "默认",
              "Ports": [
                { "Name": "目标", "Type": "Text" },
                { "Name": "触发", "Type": "Signal", "Mode": "Plain" }
              ],
              "Root": { "Name": "收话", "Kind": "Input", "Parameters": { "Question": "请补充背景" }, "Inputs": [{ "Name": "目标" }], "From": [ { "目标": "@目标" } ] }
            }
          ]
        }
        """;

    /// <summary>任务目录经静态端口转发到文件写入节点，相对路径落在本次任务目录下。</summary>
    private const string TaskPathFlow = """
        {
          "HostStartInputs": { "默认": { "任务目录": "TASK_WORK_PATH" } },
          "Flows": [
            {
              "Name": "默认",
              "Ports": [
                { "Name": "目标", "Type": "Text" },
                { "Name": "任务目录", "Type": "Text", "Mode": "Constant" },
                { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }
              ],
              "Root": {
                "Name": "整体",
                "Kind": "Container",
                "Inputs": [
                  { "Name": "目标" },
                  { "Name": "任务目录", "Type": "Text", "Mode": "Constant" },
                  { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }
                ],
                "From": [ { "目标": "@目标" } ],
                "Bindings": { "任务目录": "@任务目录", "MODEL_NAME": "@MODEL_NAME" },
                "Nodes": [
                  {
                    "Name": "实施",
                    "Kind": "Llm",
                    "Bindings": { "MODEL_NAME": "@MODEL_NAME" },
                    "Outputs": [{ "Name": "正文", "Type": "Text" }],
                    "From": [{ "目标": "@目标" }]
                  },
                  {
                    "Name": "保存",
                    "Kind": "FileWrite",
                    "Parameters": { "Path": "{root}/out/result.txt", "Variables": { "root": "任务目录" } },
                    "Inputs": [{ "Name": "正文" }, { "Name": "任务目录", "Type": "Text", "Mode": "Constant" }],
                    "Bindings": { "任务目录": "@任务目录" },
                    "From": [{ "正文": "实施@正文" }]
                  }
                ]
              }
            }
          ]
        }
        """;

    [Fact]
    public void A_static_task_path_is_the_base_of_file_nodes()
    {
        using TestEnvironment environment = TestEnvironment.Create(TaskPathFlow);

        TaskId id = environment.Submit("写入任务目录");
        TaskSnapshot done = environment.Settle(id);
        string taskDirectory = Assert.Single(
            Directory.GetDirectories(Path.Combine(environment.Root, ".kuroe_tasks")),
            path => Path.GetFileName(path) != ".records");

        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal("正文产出", File.ReadAllText(Path.Combine(taskDirectory, "out", "result.txt")));
    }
}