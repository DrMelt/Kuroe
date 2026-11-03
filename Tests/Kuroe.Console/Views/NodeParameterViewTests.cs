using Kuroe.Console.Views;
using Kuroe.Flow.InputChannel.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Node.Approval;
using Kuroe.Node.Input;
using Xunit;

namespace Kuroe.Console.Tests.Views;

/// <summary>节点类型到宿主值的读取：等待型节点的提问取具体类型声明。</summary>
public sealed class NodeParameterViewTests
{
    [Fact]
    public void Question_reads_the_waiting_node_declaration()
    {
        INodeType input = InputNodeTypes.Create(new InputParameters { Question = "补充背景" }, new StubInputChannel());
        INodeType approval = ApprovalNodeTypes.Create(new ApprovalParameters { Question = "确认计划" }, new StubInputChannel());

        Assert.Equal("补充背景", NodeParameterView.Question(input));
        Assert.Equal("确认计划", NodeParameterView.Question(approval));
    }

    [Fact]
    public void Question_is_empty_for_other_types() =>
        Assert.Null(NodeParameterView.Question(new StubNodeType(new NodeKind("Llm"))));

    private sealed class StubInputChannel : IInputChannel
    {
        public void Accept(INodeContext task, NodePath node, string? question, Action<string> receive)
        {
        }

        public void Decline(INodeContext task, NodePath node)
        {
        }
    }
}