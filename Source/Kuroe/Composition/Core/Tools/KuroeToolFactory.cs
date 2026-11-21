using Kuroe.Runner.Llm;
using Kuroe.Tool;
using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Execution.Shared.Runtime;

namespace Kuroe.Composition.Tools;

/// <summary>按执行创建全部工具载体：合并节点的工具变量映射，按本轮工具面裁剪，交给各工具源创建。</summary>
internal sealed class KuroeToolFactory(
    IReadOnlyList<IToolSource> sources,
    string workspaceDirectory) : IToolFactory
{
    /// <inheritdoc/>
    public IReadOnlyList<ITool> Create(
        IExecutionRequestEntry requests,
        LlmExecutionParameters parameters)
    {
        IReadOnlyList<ToolPath> paths = ExecutionToolPaths.For(parameters.Tools, requests.Request.OutputPorts);
        List<(ToolPath Prefix, IReadOnlyDictionary<string, string> Ports)> mappings =
        [
            .. parameters.ToolPorts
                .Select(pair => (Prefix: new ToolPath(pair.Key), Ports: pair.Value))
                .OrderByDescending(mapping => mapping.Prefix.Value.Length),
        ];

        Dictionary<string, IReadOnlyDictionary<string, ExecutionPortName>> portsByTool = sources
            .SelectMany(source => source.Definitions)
            .Select(definition => (
                Definition: definition,
                Match: mappings.FirstOrDefault(mapping => mapping.Prefix.Allows(definition.Path.Value))))
            .Where(entry => entry.Match.Ports is not null)
            .ToDictionary(
                entry => entry.Definition.Path.Value,
                entry => (IReadOnlyDictionary<string, ExecutionPortName>)entry.Match.Ports.ToDictionary(
                    pair => pair.Key,
                    pair => new ExecutionPortName(pair.Value)));

        ToolContext context = new(
            new ExecutionPortAccess(requests.Request),
            portsByTool,
            workspaceDirectory,
            requests,
            parameters.Attempt,
            parameters.MaxAttempts,
            paths);

        return [.. sources.SelectMany(source => source.Create(context))];
    }
}
