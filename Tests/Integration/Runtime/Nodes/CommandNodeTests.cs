using Kuroe.Flow.Node.RuntimeGraph.Execution;
using Kuroe.Flow.Node.RuntimeGraph.Tasks;
using Xunit;

namespace Kuroe.Integration.Tests.Runtime.Nodes;

/// <summary>命令执行节点：按版本执行命令，输出与完成信号进入下游，启动失败阻塞并记录原因。</summary>
public sealed class CommandNodeTests
{
    [Fact]
    public void Command_output_and_completion_enter_the_downstream_files()
    {
        using TestEnvironment environment = TestEnvironment.Create(CommandFlow);

        TaskId id = environment.Submit("运行命令");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);

        Assert.Matches(@"^\d+\.\d+\.\d+", File.ReadAllText(Path.Combine(environment.Root, "out", "version.txt")));
        Assert.Matches(@"^\d+\.\d+\.\d+", File.ReadAllText(Path.Combine(environment.Root, "out", "completed.txt")));
    }

    [Fact]
    public void Missing_program_blocks_the_task_and_records_the_reason()
    {
        using TestEnvironment environment = TestEnvironment.Create(MissingProgramFlow);

        TaskId id = environment.Submit("运行缺失命令");
        TaskSnapshot blocked = environment.Wait(id, snapshot => snapshot.State == TaskState.Blocked);

        Assert.Contains(blocked.Journal,
            entry => entry.Level == JournalLevel.Error && entry.Text.Contains("无法启动"));
        Assert.False(File.Exists(Path.Combine(environment.Root, "out", "completed.txt")));
    }

    /// <summary>命令节点执行 dotnet --version，输出经文件写入节点落到文件。</summary>
    private const string CommandFlow = """
        {
          "HostStartInputs": { "默认": { "基准": "WORKSPACE_PATH" } },
          "Flows": [
            {
              "Name": "默认",
              "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "基准", "Type": "Text", "Mode": "Constant" }],
              
              "Root": {
                "Name": "整体",
                "Kind": "Container",
                "Inputs": [{ "Name": "基准", "Type": "Text", "Mode": "Constant" }, { "Name": "目标" }], "From": [ { "目标": "@目标" } ], "Bindings": { "基准": "@基准" }, "Nodes": [
                  {
                    "Name": "版本",
                    "Kind": "Command",
                    "Program": "dotnet",
                    "Arguments": ["--version"],
                    "Inputs": [{ "Name": "触发", "Type": "Signal" }],
                    "From": [{ "触发": "@目标" }]
                  },
                  {
                    "Name": "再次",
                    "Kind": "Command",
                    "Program": "dotnet",
                    "Arguments": ["--version"],
                    "Inputs": [{ "Name": "触发", "Type": "Signal" }],
                    "From": [{ "触发": "版本@Completed" }]
                  },
                  {
                    "Name": "写出",
                    "Kind": "FileWrite",
                    "Path": "{root}/out/version.txt",
                    "Variables": { "root": "基准" },
                    "Inputs": [{ "Name": "正文" }, { "Name": "基准", "Type": "Text", "Mode": "Constant" }],
                    "Bindings": { "基准": "@基准" },
                    "From": [{ "正文": "版本@Output" }]
                  },
                  {
                    "Name": "写出完成",
                    "Kind": "FileWrite",
                    "Path": "{root}/out/completed.txt",
                    "Variables": { "root": "基准" },
                    "Inputs": [{ "Name": "正文" }, { "Name": "基准", "Type": "Text", "Mode": "Constant" }],
                    "Bindings": { "基准": "@基准" },
                    "From": [{ "正文": "再次@Output" }]
                  }
                ]
              }
            }
          ]
        }
        """;

    /// <summary>程序不存在，命令节点停在阻塞，完成信号不触发下游。</summary>
    private const string MissingProgramFlow = """
        {
          "HostStartInputs": { "默认": { "基准": "WORKSPACE_PATH" } },
          "Flows": [
            {
              "Name": "默认",
              "Ports": [{ "Name": "目标", "Type": "Text" }, { "Name": "基准", "Type": "Text", "Mode": "Constant" }],
              
              "Root": {
                "Name": "整体",
                "Kind": "Container",
                "Inputs": [{ "Name": "目标" }, { "Name": "基准", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "@目标" } ], "Bindings": { "基准": "@基准" }, "Nodes": [
                  {
                    "Name": "缺失",
                    "Kind": "Command",
                    "Program": "kuroe-command-does-not-exist",
                    "Inputs": [{ "Name": "触发", "Type": "Signal" }],
                    "From": [{ "触发": "@目标" }]
                  },
                  {
                    "Name": "再次",
                    "Kind": "Command",
                    "Program": "dotnet",
                    "Arguments": ["--version"],
                    "Inputs": [{ "Name": "触发", "Type": "Signal" }],
                    "From": [{ "触发": "缺失@Completed" }]
                  },
                  {
                    "Name": "写出完成",
                    "Kind": "FileWrite",
                    "Path": "{root}/out/completed.txt",
                    "Variables": { "root": "基准" },
                    "Inputs": [{ "Name": "正文" }, { "Name": "基准", "Type": "Text", "Mode": "Constant" }],
                    "Bindings": { "基准": "@基准" },
                    "From": [{ "正文": "再次@Output" }]
                  }
                ]
              }
            }
          ]
        }
        """;
}