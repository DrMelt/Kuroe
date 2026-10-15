using ErrorOr;
using Kuroe.Flow.Json.Files;
using Kuroe.Flow.Json.Parameters;
using Kuroe.Flow.NodeLibrary;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.StaticGraph;

namespace Kuroe.Flow.Json;

/// <summary>流程文件的形状入口：文件 DTO 读成节点定义与流程模型，以及读入后交装配层校验与展开。</summary>
static class FlowFile
{
    /// <summary>把文件形状读成节点定义与流程模型，错误逐条收集。</summary>
    internal static ErrorOr<(IReadOnlyList<NodeSpec> Nodes, IReadOnlyList<FlowDefinition> Flows)> Read(
        FlowFileDto file,
        NodeTypeRegistry types,
        NodeParameterFileRegistry files) => FlowReader.ToContent(file, types, files);

    /// <summary>装入文件形状：读成模型后交装配层校验并展开。</summary>
    internal static ErrorOr<AssembledFlows> Assemble(
        FlowFileDto file,
        Kuroe.Flow.NodeLibrary.NodeLibrary library,
        NodeTypeRegistry types,
        NodeParameterFileRegistry files)
    {
        ErrorOr<(IReadOnlyList<NodeSpec> Nodes, IReadOnlyList<FlowDefinition> Flows)> content = FlowReader.ToContent(file, types, files);

        return content.IsError
            ? content.ErrorsOrEmptyList
            : FlowAssembler.Assemble(content.Value.Nodes, content.Value.Flows, library, types);
    }

    /// <summary>把节点定义与流程转回文件形状，保留引用形态。</summary>
    internal static FlowFileDto Write(
        IReadOnlyList<NodeSpec> fileNodes,
        IReadOnlyList<FlowDefinition> flows,
        NodeTypeRegistry types,
        NodeParameterFileRegistry files) => FlowWriter.ToDto(fileNodes, flows, types, files);
}