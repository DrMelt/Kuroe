using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Tests.Support;
using Xunit;

namespace Kuroe.Tests;

/// <summary>开始节点的宿主固定端口：WORKSPACE_PATH 与 TASK_WORK_PATH 由任务启动时给出，不接受提交输入。</summary>
public sealed class StartPathsTests
{
    /// <summary>执行节点同时取开始的目标与两个固定端口。</summary>
    private const string FixedPortFlow = """
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
                    {
                      "Name": "读取路径",
                      "Kind": "Llm",
                      "Model": "执行者",
                      "Inputs": [
                        { "Name": "目标" },
                        { "Name": "启动" },
                        { "Name": "流程" }
                      ],
                      "From": [
                        {
                          "目标": "Start@目标",
                          "启动": "Start@WORKSPACE_PATH",
                          "流程": "Start@TASK_WORK_PATH"
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
    public void Host_paths_reach_the_execution_inputs()
    {
        using TestEnvironment environment = TestEnvironment.Create(FixedPortFlow);

        TaskId id = environment.Submit("整理目录");
        TaskSnapshot task = environment.Settle(id);
        IExecution execution = Assert.Single(environment.Runner.Started);
        string taskDirectory = Assert.Single(Directory.GetDirectories(Path.Combine(environment.Root, ".kuroe_tasks")));

        Assert.Equal(TaskState.Done, task.State);
        Assert.Contains(execution.Input.Inputs, value => value.Text == environment.Root);
        Assert.Contains(execution.Input.Inputs, value => value.Text == taskDirectory);
    }

    [Fact]
    public void A_host_port_rejects_submitted_input()
    {
        using TestEnvironment environment = TestEnvironment.Create(FixedPortFlow);

        ErrorOr<TaskSnapshot> submitted = environment.Tasks.Submit(
            new StartInputs(
            [
                new StartInput(new PortName("目标"), "整理目录"),
                new StartInput(StartNode.WorkspacePath.Port.Name, environment.Root),
            ]),
            new FlowName("默认"),
            null);

        Assert.True(submitted.IsError);
        Assert.Equal(FlowErrorCodes.TaskInput, submitted.FirstError.Code);
        Assert.Equal("开始端口 WORKSPACE_PATH 由宿主提供，不接受提交输入。", submitted.FirstError.Description);
    }
}