using ErrorOr;
using Kuroe.Flow.Graph.Shared.StaticGraph;
using Kuroe.Flow.NodeType.Container;
using Kuroe.FlowDescription.Compilation.Library;
using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.FlowDescription.Definition.Shared.Flow;
using Kuroe.FlowDescription.Definition.Shared.Nodes;
using Kuroe.FlowDescription.Definition.Shared.Ports;
using Kuroe.Node.Llm;
using Kuroe.Kernel.Tasks.Shared;
using Xunit;

namespace Kuroe.Flow.Compilation.Tests;

/// <summary>图编译从展开描述建立执行视图。</summary>
public sealed class FlowGraphCompilerTests
{
    [Fact(DisplayName = "单执行节点的流程可以建立图视图")]
    public void A_single_executable_flow_builds_a_graph()
    {
        NodeSpec root = new()
        {
            Label = new NodeLabel("整体"),
            Type = ContainerNodeTypes.Type,
            Inputs = [new InputPort(LlmNodeTypes.ModelPort, PortTypes.Text, mode: PortMode.Constant)],
            Bindings = new Dictionary<PortName, PortRef> { [LlmNodeTypes.ModelPort] = PortRef.Start(LlmNodeTypes.ModelPort) },
            Nodes =
            [
                new NodeSpec
                {
                    Label = new NodeLabel("干活"),
                    Type = LlmNodeTypes.Create(null),
                    Inputs = [new InputPort(LlmNodeTypes.ModelPort, PortTypes.Text, mode: PortMode.Constant)],
                    Bindings = new Dictionary<PortName, PortRef> { [LlmNodeTypes.ModelPort] = PortRef.Bound(LlmNodeTypes.ModelPort) },
                    Spec = new ExecutableSpec(),
                },
            ],
        };
        FlowDefinition flow = new(
            new FlowName("测试"),
            null,
            [new Port(LlmNodeTypes.ModelPort, PortTypes.Text, mode: PortMode.Constant)],
            root);

        ErrorOr<IReadOnlyList<FlowDefinition>> expanded = new NodeLibraryExpander().Expand([flow], []);
        Assert.False(expanded.IsError, string.Join("；", expanded.ErrorsOrEmptyList.Select(error => error.Description)));

        ErrorOr<FlowGraph> compiled = new FlowGraphCompiler().Compile(expanded.Value[0]);

        Assert.False(compiled.IsError, string.Join("；", compiled.ErrorsOrEmptyList.Select(error => error.Description)));
        Assert.Single(compiled.Value.Graph.ExecutableNodes);
    }
}
