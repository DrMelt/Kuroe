using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Runtime.Tasks;
using Xunit;

namespace Kuroe.Integration.Tests.Runtime.Engine;

/// <summary>单节点最高执行次数：路径达上限后不再启动新执行，未成功的停在阻塞且重新执行不放行。</summary>
public sealed class ExecutionLimitTests
{
    /// <summary>执行上限到点后不再重跑，未成功的停在阻塞且重新执行不放行。</summary>
    [Theory]
    [InlineData("failed")]
    [InlineData("uncollected")]
    public void Exhausted_limit_blocks_and_rejects_rerun(string reason)
    {
        string flow = reason == "uncollected" ? SingleNodeLimitFlow : FailingNodeFlow;

        using TestEnvironment environment = TestEnvironment.Create(flow);
        switch (reason)
        {
            case "failed":
                environment.Runner.FailsWhen = _ => true;
                break;
            case "uncollected":
                environment.Runner.SubmitsPorts = false;
                break;
        }

        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);
        environment.Tasks.Rerun(id).ThrowIfError();
        TaskSnapshot blocked = environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(2, Executions(task, "干活"));
        Assert.True(environment.Tasks.Rerun(id).IsError, "达到上限的节点不能再重新执行");

        if (reason == "failed")
        {
            Assert.Contains(blocked.Journal,
                entry => entry.Level == JournalLevel.Error && entry.Text.Contains("已达上限"));
        }
    }

    /// <summary>容器上限是发布次数，不限制成员执行次数。</summary>
    [Fact]
    public void Container_limit_does_not_cap_member_retries()
    {
        using TestEnvironment environment = TestEnvironment.Create(SingleExecutionContainerFlow);
        environment.Runner.FailsWhen = _ => true;

        TaskId id = environment.Submit("补齐 README");
        TaskSnapshot blocked = environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(TaskState.Blocked, blocked.State);
        Assert.Equal(1, Executions(task, "干活"));
        Assert.False(environment.Tasks.Rerun(id).IsError);
    }

    /// <summary>引用容器时覆盖库定义上限，未覆盖时继承库定义。上限属于容器自身，不传给成员。</summary>
    [Fact]
    public void Reference_overrides_and_inherits_container_limit()
    {
        using TestEnvironment environment = TestEnvironment.Create(ReferenceContainerLimitFlow);
        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(5, task.Graph.Containers
            .Single(node => node.Path.Value == "整体/较重").MaxExecutions);
        Assert.Equal(3, task.Graph.Containers
            .Single(node => node.Path.Value == "整体/较轻").MaxExecutions);
        Assert.Equal(ExecutableNode.DefaultMaxExecutions, task.Graph.ExecutableNodes
            .Single(node => node.Path.Value == "整体/较重/干活").MaxExecutions);
    }

    /// <summary>引用库执行定义时覆盖库上限，未覆盖时继承库定义。</summary>
    [Fact]
    public void Reference_overrides_and_inherits_library_limit()
    {
        using TestEnvironment environment = TestEnvironment.Create(LibraryLimitFlow);
        TaskId id = environment.Submit("补齐 README");
        environment.Settle(id);

        WorkTask task = environment.Registry.Find(id).ThrowIfError();
        Assert.Equal(5, task.Graph.ExecutableNodes
            .Single(node => node.Label.Value == "覆盖干活").MaxExecutions);
        Assert.Equal(3, task.Graph.ExecutableNodes
            .Single(node => node.Label.Value == "继承干活").MaxExecutions);
    }

    /// <summary>恒失败的单个节点，满 2 次后停在阻塞。</summary>
    private const string FailingNodeFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Root": { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "Start@目标" } ], "Bindings": { "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
            { "Name": "干活", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "MaxExecutions": 2, "From": [ { "目标": "@目标" } ] }
          ] } } ]
        }
        """;

    /// <summary>声明输出端口却不交回的单个节点，限两次执行。</summary>
    private const string SingleNodeLimitFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Root": { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "Start@目标" } ], "Bindings": { "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
            { "Name": "干活", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "MaxExecutions": 2, "Outputs": [{ "Name": "结论", "Type": "Text" }], "From": [ { "目标": "@目标" } ] }
          ] } } ]
        }
        """;

    /// <summary>容器发布上限 1 次，不限制成员执行次数，成员失败后停驻。</summary>
    private const string SingleExecutionContainerFlow = """
        {
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Root": { "Name": "整体", "Kind": "Container", "MaxExecutions": 1, "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "Start@目标" } ], "Bindings": { "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
            { "Name": "干活", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] }
          ] } } ]
        }
        """;

    /// <summary>库容器定义声明发布次数上限，引用处可覆盖或继承。</summary>
    private const string ReferenceContainerLimitFlow = """
        {
          "Nodes": [
            { "Name": "干活组", "Kind": "Container", "MaxExecutions": 3, "Inputs": [{ "Name": "入口" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "Nodes": [
              { "Name": "干活", "Kind": "Llm", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "入口": "@入口" } ] }
            ] }
          ],
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Root": { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "Start@目标" } ], "Bindings": { "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
            { "Label": "较重", "Use": "干活组", "MaxExecutions": 5, "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "入口": "@目标" } ] },
            { "Label": "较轻", "Use": "干活组", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "入口": "@目标" } ] }
          ] } } ]
        }
        """;

    /// <summary>库执行定义声明上限，引用处可覆盖或继承。</summary>
    private const string LibraryLimitFlow = """
        {
          "Nodes": [
            { "Name": "干活库", "Kind": "Llm", "Tools": [], "MaxExecutions": 3 }
          ],
          "Flows": [ { "Name": "默认", "Start": { "Outputs": [{ "Name": "目标", "Type": "Text" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant", "Value": "fake" }] }, "Root": { "Name": "整体", "Kind": "Container", "Inputs": [{ "Name": "目标" }, { "Name": "MODEL_NAME", "Type": "Text", "Mode": "Constant" }], "From": [ { "目标": "Start@目标" } ], "Bindings": { "MODEL_NAME": "Start@MODEL_NAME" }, "Nodes": [
            { "Label": "覆盖干活", "Use": "干活库", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "MaxExecutions": 5, "From": [ { "目标": "@目标" } ] },
            { "Label": "继承干活", "Use": "干活库", "Bindings": { "MODEL_NAME": "@MODEL_NAME" }, "From": [ { "目标": "@目标" } ] }
          ] } } ]
        }
        """;

    /// <summary>执行节点按启动的执行记录计数。</summary>
    private static int Executions(WorkTask task, string label) =>
        task.Snapshot().Executables
            .Single(executable => executable.Executable.Path == task.Graph.PathOf(label))
            .Executions.Count;
}
