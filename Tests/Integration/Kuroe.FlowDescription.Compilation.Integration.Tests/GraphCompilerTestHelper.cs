using ErrorOr;
using Kuroe.Flow.Compilation;
using Kuroe.Flow.Graph.Shared.StaticGraph;
using Kuroe.FlowDescription.Compilation.Library;
using Kuroe.FlowDescription.Description.Shared;
using Kuroe.FlowDescription.Compilation.Shared;
using Kuroe.FlowDescription.Compilation.Shared.NodeTypes;
using Kuroe.FlowDescription.Definition.Shared.Flow;
using Kuroe.FlowDescription.Semantics;
using Kuroe.FlowDescription.Semantics.Shared;

namespace Kuroe.FlowDescription.Compilation.Integration.Tests;

/// <summary>测试用的描述编译加图编译组合。</summary>
internal static class GraphCompilerTestHelper
{
    /// <summary>按类型注册表编译文档并建立图执行视图。</summary>
    public static ErrorOr<IReadOnlyList<FlowGraph>> Compile(
        INodeTypeDescriptionRegistry types,
        FlowDescriptionSet document)
    {
        IFlowSemanticsAnalyzer semantics = new FlowSemanticsAnalyzer();
        IFlowDescriptionCompiler descriptions = new FlowDescriptionCompiler(types, new NodeLibraryExpander(), semantics);
        ErrorOr<IReadOnlyList<FlowDefinition>> expanded = descriptions.Compile(document);
        if (expanded.IsError)
        {
            return expanded.ErrorsOrEmptyList;
        }

        FlowGraphCompiler graphs = new(semantics);
        List<FlowGraph> flows = [];
        List<Error> errors = [];
        foreach (FlowDefinition flow in expanded.Value)
        {
            ErrorOr<FlowGraph> compiled = graphs.Compile(flow);
            if (compiled.IsError)
            {
                errors.AddRange(compiled.ErrorsOrEmptyList);
                continue;
            }

            flows.Add(compiled.Value);
        }

        return errors.Count > 0 ? errors : flows;
    }
}
