using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Xunit;

namespace Kuroe.Tests;

/// <summary>文件读取与写入节点：按版本读取工作目录内文件，内容写入下游或落到文件，失败阻塞后返工恢复。</summary>
public sealed class FileNodeTests
{
    [Fact]
    public void Read_then_write_copies_the_file()
    {
        using TestEnvironment environment = TestEnvironment.Create(ReadWriteFlow);
        File.WriteAllText(Path.Combine(environment.Root, "in.txt"), "文件内容");

        TaskId id = environment.Submit("复制文件");
        TaskSnapshot done = environment.Settle(id);

        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal("文件内容", File.ReadAllText(Path.Combine(environment.Root, "out", "result.txt")));
    }

    [Fact]
    public void Read_content_enters_the_downstream_execution_input()
    {
        using TestEnvironment environment = TestEnvironment.Create(ReadThenLlmFlow);
        File.WriteAllText(Path.Combine(environment.Root, "in.txt"), "文件内容");

        TaskId id = environment.Submit("读取后实施");
        environment.Settle(id);

        IExecution started = environment.Runner.Started
            .Single(execution => execution.Input.Node.Value == "整体/实施");
        Assert.Contains(started.Input.Inputs,
            value => value.Text == "文件内容" && value.Origin is NodeSource);
    }

    [Fact]
    public void Missing_file_blocks_the_task_and_rework_resumes_it()
    {
        using TestEnvironment environment = TestEnvironment.Create(ReadWriteFlow);

        TaskId id = environment.Submit("复制文件");
        TaskSnapshot blocked = environment.Wait(id, snapshot => snapshot.State == TaskState.Blocked);

        Assert.Contains(blocked.Journal,
            entry => entry.Level == JournalLevel.Error && entry.Text.Contains("读取 {root}/in.txt 失败"));

        File.WriteAllText(Path.Combine(environment.Root, "in.txt"), "恢复内容");
        environment.Tasks.Rework(id).ThrowIfError();

        TaskSnapshot done = environment.Settle(id);
        Assert.Equal(TaskState.Done, done.State);
        Assert.Equal("恢复内容", File.ReadAllText(Path.Combine(environment.Root, "out", "result.txt")));
    }

    [Fact]
    public void Configured_max_chars_truncates_the_file_content()
    {
        using TestEnvironment environment = TestEnvironment.Create(LimitedReadFlow);
        File.WriteAllText(Path.Combine(environment.Root, "in.txt"), "文件内容");

        TaskId id = environment.Submit("限长复制");
        environment.Settle(id);

        Assert.Equal("文件…内容已截断", File.ReadAllText(Path.Combine(environment.Root, "out", "result.txt")));
    }

    [Fact]
    public void Negative_max_chars_fails_loading()
    {
        ErrorOr<TestEnvironment> created = TestEnvironment.TryCreate(NegativeLimitFlow);

        Assert.True(created.IsError);
        Assert.Contains(created.ErrorsOrEmptyList,
            error => error.Description.Contains("MaxChars 不能是负数。"));
    }

    [Fact]
    public void Missing_variable_mapping_fails_loading()
    {
        ErrorOr<TestEnvironment> created = TestEnvironment.TryCreate(MissingVariableFlow);

        Assert.True(created.IsError);
        Assert.Contains(created.ErrorsOrEmptyList,
            error => error.Description.Contains("路径占位符 {root} 没有对应的路径变量映射"));
    }

    [Fact]
    public void Unconnected_variable_port_fails_loading()
    {
        ErrorOr<TestEnvironment> created = TestEnvironment.TryCreate(UnconnectedVariableFlow);

        Assert.True(created.IsError);
        Assert.Contains(created.ErrorsOrEmptyList,
            error => error.Description.Contains("From 组没有覆盖输入端口 基准"));
    }

    [Fact]
    public void Path_outside_the_working_directory_blocks_the_task()
    {
        using TestEnvironment environment = TestEnvironment.Create(EscapePathFlow);

        TaskId id = environment.Submit("逃逸路径");
        TaskSnapshot blocked = environment.Wait(id, snapshot => snapshot.State == TaskState.Blocked);

        Assert.Contains(blocked.Journal,
            entry => entry.Level == JournalLevel.Error && entry.Text.Contains("不是工作区内的合法路径"));
    }

    /// <summary>读入后写出：文件内容直接落到另一个文件。</summary>
    private const string ReadWriteFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "基准", "Type": "Text", "Constant": true }] },
              "HostStartInputs": { "基准": "WORKSPACE_PATH" },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "基准", "Type": "Text", "Static": true }, { "Name": "目标" }], "In": { "基准": "Start@基准", "目标": "Start@目标" }, "Nodes": [
                    {
                      "Name": "读入",
                      "Kind": "FileRead",
                      "Path": "{root}/in.txt",
                      "Variables": { "root": "基准" },
                      "Inputs": [{ "Name": "基准", "Type": "Text", "Static": true }, { "Name": "触发", "Type": "Signal" }],
                      "Static": { "基准": "@基准" },
                      "From": [{ "触发": "@目标" }],
                      "Outputs": [{ "Name": "正文", "Type": "Text" }]
                    },
                    {
                      "Name": "写出",
                      "Kind": "FileWrite",
                      "Path": "{root}/out/result.txt",
                      "Variables": { "root": "基准" },
                      "Inputs": [{ "Name": "正文" }, { "Name": "基准", "Type": "Text", "Static": true }],
                      "Static": { "基准": "@基准" },
                      "From": [{ "正文": "读入@正文" }]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>读入后交给执行节点：读取内容进入下游执行输入。</summary>
    private const string ReadThenLlmFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "基准", "Type": "Text", "Constant": true }, { "Name": "MODEL_NAME", "Type": "Text", "Value": "fake", "Constant": true }] },
              "HostStartInputs": { "基准": "WORKSPACE_PATH" },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "基准", "Type": "Text", "Static": true }, { "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Static": true }], "In": { "基准": "Start@基准", "目标": "Start@目标", "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
                    {
                      "Name": "读入",
                      "Kind": "FileRead",
                      "Path": "{root}/in.txt",
                      "Variables": { "root": "基准" },
                      "Inputs": [{ "Name": "基准", "Type": "Text", "Static": true }, { "Name": "触发", "Type": "Signal" }],
                      "Static": { "基准": "@基准" },
                      "From": [{ "触发": "@目标" }],
                      "Outputs": [{ "Name": "正文", "Type": "Text" }]
                    },
                    {
                      "Name": "实施",
                      "Kind": "Llm",
                      "Static": { "MODEL_NAME": "@MODEL_NAME" },
                      "Inputs": [{ "Name": "正文" }],
                      "From": [{ "正文": "读入@正文" }]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>读入按声明的字符上限截断，结果经写出落到文件。</summary>
    private const string LimitedReadFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "基准", "Type": "Text", "Constant": true }] },
              "HostStartInputs": { "基准": "WORKSPACE_PATH" },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "基准", "Type": "Text", "Static": true }, { "Name": "目标" }], "In": { "基准": "Start@基准", "目标": "Start@目标" }, "Nodes": [
                    {
                      "Name": "读入",
                      "Kind": "FileRead",
                      "Path": "{root}/in.txt",
                      "Variables": { "root": "基准" },
                      "MaxChars": 2,
                      "Inputs": [{ "Name": "基准", "Type": "Text", "Static": true }, { "Name": "触发", "Type": "Signal" }],
                      "Static": { "基准": "@基准" },
                      "From": [{ "触发": "@目标" }],
                      "Outputs": [{ "Name": "正文", "Type": "Text" }]
                    },
                    {
                      "Name": "写出",
                      "Kind": "FileWrite",
                      "Path": "{root}/out/result.txt",
                      "Variables": { "root": "基准" },
                      "Inputs": [{ "Name": "正文" }, { "Name": "基准", "Type": "Text", "Static": true }],
                      "Static": { "基准": "@基准" },
                      "From": [{ "正文": "读入@正文" }]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>字符上限为负数：装载期即报错。</summary>
    private const string NegativeLimitFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "基准", "Type": "Text", "Constant": true }] },
              "HostStartInputs": { "基准": "WORKSPACE_PATH" },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "基准", "Type": "Text", "Static": true }, { "Name": "目标" }], "In": { "基准": "Start@基准", "目标": "Start@目标" }, "Nodes": [
                    {
                      "Name": "读入",
                      "Kind": "FileRead",
                      "Path": "{root}/in.txt",
                      "Variables": { "root": "基准" },
                      "MaxChars": -1,
                      "Inputs": [{ "Name": "基准", "Type": "Text", "Static": true }, { "Name": "触发", "Type": "Signal" }],
                      "Static": { "基准": "@基准" },
                      "From": [{ "触发": "@目标" }],
                      "Outputs": [{ "Name": "正文", "Type": "Text" }]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>Path 含路径变量但没有映射：装载期即报错。</summary>
    private const string MissingVariableFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }], "In": { "目标": "Start@目标" }, "Nodes": [
                    {
                      "Name": "读入",
                      "Kind": "FileRead",
                      "Path": "{root}/in.txt",
                      "Inputs": [{ "Name": "触发", "Type": "Signal" }],
                      "From": [{ "触发": "@目标" }],
                      "Outputs": [{ "Name": "正文", "Type": "Text" }]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>声明了路径变量端口但 From 没有连接：装载期即报错。</summary>
    private const string UnconnectedVariableFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "基准", "Type": "Text", "Value": "." }] },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "目标" }], "In": { "目标": "Start@目标" }, "Nodes": [
                    {
                      "Name": "读入",
                      "Kind": "FileRead",
                      "Path": "{root}/in.txt",
                      "Variables": { "root": "基准" },
                      "Inputs": [{ "Name": "基准" }, { "Name": "触发", "Type": "Signal" }],
                      "From": [{ "触发": "@目标" }],
                      "Outputs": [{ "Name": "正文", "Type": "Text" }]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>路径越出工作区：运行期解析失败并停在阻塞。</summary>
    private const string EscapePathFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "基准", "Type": "Text", "Constant": true }] },
              "HostStartInputs": { "基准": "WORKSPACE_PATH" },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "基准", "Type": "Text", "Static": true }, { "Name": "目标" }], "In": { "基准": "Start@基准", "目标": "Start@目标" }, "Nodes": [
                    {
                      "Name": "读入",
                      "Kind": "FileRead",
                      "Path": "{root}/../escape.txt",
                      "Variables": { "root": "基准" },
                      "Inputs": [{ "Name": "基准", "Type": "Text", "Static": true }, { "Name": "触发", "Type": "Signal" }],
                      "Static": { "基准": "@基准" },
                      "From": [{ "触发": "@目标" }],
                      "Outputs": [{ "Name": "正文", "Type": "Text" }]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    /// <summary>静态路径端口没有绑定来源：装载期即报错。</summary>
    private const string UnboundStaticVariableFlow = """
        {
          "Flows": [
            {
              "Name": "默认",
              "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "基准", "Type": "Text", "Constant": true }] },
              "HostStartInputs": { "基准": "WORKSPACE_PATH" },
              "Nodes": [
                {
                  "Name": "整体",
                  "Kind": "Container",
                  "Inputs": [{ "Name": "基准", "Type": "Text", "Static": true }, { "Name": "目标" }], "In": { "基准": "Start@基准", "目标": "Start@目标" }, "Nodes": [
                    {
                      "Name": "读入",
                      "Kind": "FileRead",
                      "Path": "{root}/in.txt",
                      "Variables": { "root": "基准" },
                      "Inputs": [{ "Name": "基准", "Type": "Text", "Static": true }, { "Name": "触发", "Type": "Signal" }],
                      "From": [{ "触发": "@目标" }],
                      "Outputs": [{ "Name": "正文", "Type": "Text" }]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void Unbound_static_variable_port_fails_loading()
    {
        ErrorOr<TestEnvironment> created = TestEnvironment.TryCreate(UnboundStaticVariableFlow);

        Assert.True(created.IsError);
        Assert.Contains(created.ErrorsOrEmptyList,
            error => error.Description.Contains("静态输入端口 基准 没有绑定来源或默认值"));
    }
}