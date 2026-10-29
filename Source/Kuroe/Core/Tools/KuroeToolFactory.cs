using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Node.Llm;
using Kuroe.Tool;

namespace Kuroe.Tools;

/// <summary>按执行创建全部工具载体：合并节点的工具变量映射，按本轮工具面裁剪，交给各工具源创建。</summary>
internal sealed class KuroeToolFactory(
    IReadOnlyList<IToolSource> sources,
    string workspaceDirectory) : IToolFactory
{
    /// <inheritdoc/>
    public IReadOnlyList<ITool> Create(ExecutionId execution, ExecutionInput input)
    {
        Dictionary<string, IReadOnlyDictionary<string, PortName>> portsByTool = [];
        IReadOnlyList<ToolPath> paths = input.Type is LlmNodeType type ? type.ToolPaths(input.OutputPorts) : [];
        if (input.Type is LlmNodeType { Parameters: { } parameters })
        {
            List<(ToolPath Prefix, IReadOnlyDictionary<string, string> Ports)> mappings =
            [
                .. parameters.ToolPorts
                    .Select(pair => (Prefix: new ToolPath(pair.Key), Ports: pair.Value))
                    .OrderByDescending(mapping => mapping.Prefix.Value.Length),
            ];

            portsByTool = sources
                .SelectMany(source => source.Definitions)
                .Select(definition => (
                    Definition: definition,
                    Match: mappings.FirstOrDefault(mapping => mapping.Prefix.Allows(definition.Path.Value))))
                .Where(entry => entry.Match.Ports is not null)
                .ToDictionary(
                    entry => entry.Definition.Path.Value,
                    entry => (IReadOnlyDictionary<string, PortName>)entry.Match.Ports.ToDictionary(
                        pair => pair.Key,
                        pair => new PortName(pair.Value)));
        }

        ToolContext context = new(new ExecutionPortAccess(input), portsByTool, workspaceDirectory, execution, paths);

        return [.. sources.SelectMany(source => source.Create(context))];
    }
}