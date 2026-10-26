using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;
using Kuroe.Flow.Shared.RuntimeGraph;
using Xunit;

namespace Kuroe.Tests;

/// <summary>命令执行节点：按版本执行命令，成功输出进入下游，启动失败阻塞并记录原因。</summary>
public sealed class CommandNodeTests
{
    [Fact]
    public void Command_output_enters_the_downstream_file()
    {
        using TestEnvironment environment = TestEnvironment.Create(CommandFlow);

        TaskId id = environment.Submit("运行命令");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);

        string content = File.ReadAllText(Path.Combine(environment.Root, "out", "version.txt"));
        Assert.Matches(@"^\d+\.\d+\.\d+", content);
    }

    [Fact]
    public void Missing_program_blocks_the_task_and_records_the_reason()
    {
        using TestEnvironment environment = TestEnvironment.Create(MissingProgramFlow);

        TaskId id = environment.Submit("运行缺失命令");
        TaskSnapshot blocked = environment.Wait(id, snapshot => snapshot.State == TaskState.Blocked);

        Assert.Contains(blocked.Journal,
            entry => entry.Level == JournalLevel.Error && entry.Text.Contains("无法启动"));
    }

    /// <summary>命令节点执行 dotnet --version，输出经文件写入节点落到文件。</summary>
    private const string CommandFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "基准", "Type": "Text", "Value": "." }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    {
                      "Name": "版本",
                      "Kind": "Command",
                      "Program": "dotnet",
                      "Arguments": ["--version"],
                      "Inputs": [{ "Name": "BasePath" }, { "Name": "触发", "Type": "Signal" }],
                      "From": [{ "BasePath": "Start@基准", "触发": "Start@目标" }],
                      "Outputs": [{ "Name": "输出", "Type": "Text" }]
                    },
                    {
                      "Name": "写出",
                      "Kind": "FileWrite",
                      "Path": "out/version.txt",
                      "Inputs": [{ "Name": "正文" }, { "Name": "BasePath" }],
                      "From": [{ "正文": "版本@输出", "BasePath": "Start@基准" }]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>程序不存在，命令节点停在阻塞。</summary>
    private const string MissingProgramFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "基准", "Type": "Text", "Value": "." }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Nodes": [
                    {
                      "Name": "缺失",
                      "Kind": "Command",
                      "Program": "kuroe-command-does-not-exist",
                      "Inputs": [{ "Name": "BasePath" }, { "Name": "触发", "Type": "Signal" }],
                      "From": [{ "BasePath": "Start@基准", "触发": "Start@目标" }],
                      "Outputs": [{ "Name": "输出", "Type": "Text" }]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;
}