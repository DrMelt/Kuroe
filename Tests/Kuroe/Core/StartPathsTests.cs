using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Xunit;

namespace Kuroe.Tests;

/// <summary>流程声明的宿主开始输入：HostStartInputs 绑定到 WORKSPACE_PATH 与 TASK_WORK_PATH，开始端口为静态端口，
/// 由容器静态转发并绑定到执行节点，值在任务启动时给出，不接受提交输入。</summary>
public sealed class StartPathsTests
{
    /// <summary>执行节点取开始的目标与两个宿主输入。</summary>
    private const string FixedPortFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": {
                "Outputs": [
                  { "Name": "目标", "Type": "Text" },
                  { "Name": "启动", "Type": "Text", "Constant": true },
                  { "Name": "流程", "Type": "Text", "Constant": true },
                  { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }
                ]
              },
              "HostStartInputs": {
                "启动": "WORKSPACE_PATH",
                "流程": "TASK_WORK_PATH"
              },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "启动", "Type": "Text", "Static": true }, { "Name": "流程", "Type": "Text", "Static": true }, { "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "启动": "Start@启动", "流程": "Start@流程", "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    {
                      "Name": "读取路径",
                      "Kind": "Llm",
                      "Static": { "MODEL_NAME": "@MODEL_NAME", "启动": "@启动", "流程": "@流程" },
                      "Inputs": [
                        { "Name": "目标" },
                        { "Name": "启动", "Type": "Text", "Static": true },
                        { "Name": "流程", "Type": "Text", "Static": true }
                      ],
                      "From": [
                        {
                          "目标": "@目标"
                        }
                      ]
                    }
                  ]
                }
              ]
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
            path => Path.GetFileName(path) != ".checkpoints");

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

    /// <summary>任务目录经静态端口转发到文件写入节点，相对路径落在本次任务目录下。</summary>
    private const string TaskPathFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [
                { "Name": "目标", "Type": "Text" },
                { "Name": "任务目录", "Type": "Text", "Constant": true },
                { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }
              ] },
              "HostStartInputs": { "任务目录": "TASK_WORK_PATH" },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [
                    { "Name": "目标" },
                    { "Name": "任务目录", "Type": "Text", "Static": true },
                    { "Name": "MODEL_NAME", "Type": "Text", "Static": true }
                  ],
                  "In": { "目标": "Start@目标", "任务目录": "Start@任务目录", "MODEL_NAME": "Start@MODEL_NAME" },
                  "Nodes": [
                    {
                      "Name": "实施",
                      "Kind": "Llm",
                      "Static": { "MODEL_NAME": "@MODEL_NAME" },
                      "Outputs": [{ "Name": "正文", "Type": "Text" }],
                      "From": [{ "目标": "@目标" }]
                    },
                    {
                      "Name": "保存",
                      "Kind": "FileWrite",
                      "Path": "{root}/out/result.txt",
                      "Variables": { "root": "任务目录" },
                      "Inputs": [{ "Name": "正文" }, { "Name": "任务目录", "Type": "Text", "Static": true }],
                      "Static": { "任务目录": "@任务目录" },
                      "From": [{ "正文": "实施@正文" }]
                    }
                  ]
                }
              ]
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
            path => Path.GetFileName(path) != ".checkpoints");

        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal("正文产出", File.ReadAllText(Path.Combine(taskDirectory, "out", "result.txt")));
    }
}