using Kuroe.Runner.Llm;
using Kuroe.Tool;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Ports;
using Kuroe.Execution.Shared.Request;

namespace Kuroe.Tools;

/// <summary>按执行创建全部工具载体：合并节点的工具变量映射，按本轮工具面裁剪，交给各工具源创建。</summary>
internal sealed class KuroeToolFactory(
    IReadOnlyList<IToolSource> sources,
    string workspaceDirectory) : IToolFactory
{
    /// <inheritdoc/>
    public IReadOnlyList<ITool> Create(
        ExecutionId execution,
        ExecutionRequest request,
        LlmExecutionParameters parameters)
    {
        IReadOnlyList<ToolPath> paths = ExecutionToolPaths.For(parameters.Tools, request.OutputPorts);
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
            new ExecutionPortAccess(request),
            portsByTool,
            workspaceDirectory,
            execution,
            parameters.Attempt,
            parameters.MaxAttempts,
            paths);

        return [.. sources.SelectMany(source => source.Create(context))];
    }
}
