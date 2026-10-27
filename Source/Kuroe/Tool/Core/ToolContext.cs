using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Shared;

namespace Kuroe.Tool;

/// <summary>一次执行的工具创建上下文：端口访问、执行身份、本轮可用工具面、各工具的变量到端口映射与工作区根。</summary>
/// <param name="Ports">执行输入的端口访问。</param>
/// <param name="PortsByTool">工具路径到变量映射的对应表。</param>
/// <param name="WorkspaceDirectory">工作区根目录。</param>
/// <param name="Execution">所属执行。</param>
/// <param name="Paths">本轮模型可用的工具路径。</param>
public sealed record ToolContext(
    IPortAccess Ports,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, PortName>> PortsByTool,
    string WorkspaceDirectory,
    ExecutionId Execution,
    IReadOnlyList<ToolPath> Paths)
{
    /// <summary>某个工具的绑定视图，变量映射只保留该工具的条目。</summary>
    public ToolBinding For(string toolPath) =>
        new(Ports, PortsFor(toolPath), WorkspaceDirectory);

    /// <summary>某个工具的变量映射，未声明时为空。</summary>
    public IReadOnlyDictionary<string, PortName> PortsFor(string toolPath) =>
        PortsByTool.TryGetValue(toolPath, out IReadOnlyDictionary<string, PortName>? ports)
            ? ports
            : new Dictionary<string, PortName>();

    /// <summary>该工具路径是否在本轮可用工具面内。</summary>
    public bool Allows(ToolPath path) => Paths.Any(entry => entry.Allows(path.Value));

    /// <summary>这些定义中是否有任一本轮可用。</summary>
    public bool AllowsAny(IEnumerable<ToolDefinition> definitions) =>
        definitions.Any(definition => Allows(definition.Path));

    /// <summary>该载体的函数中是否有任一本轮可用。</summary>
    public bool AllowsAny(ITool tool) =>
        AllowsAny(tool.Functions.Select(function => function.Definition));
}

/// <summary>单个工具的绑定视图：端口访问、变量到端口映射与工作区根。</summary>
/// <param name="Ports">执行输入的端口访问。</param>
/// <param name="PortsByName">变量名到输入端口名。</param>
/// <param name="WorkspaceDirectory">工作区根目录。</param>
public sealed record ToolBinding(
    IPortAccess Ports,
    IReadOnlyDictionary<string, PortName> PortsByName,
    string WorkspaceDirectory)
{
    /// <summary>无变量映射的绑定，root 变量指向给定目录。用于测试与不经过流程装配的载体。</summary>
    public static ToolBinding Root(string workspaceDirectory)
    {
        PortName port = new(PathVariables.RootName);

        return new(
            new FixedPortAccess(port, workspaceDirectory),
            new Dictionary<string, PortName> { [PathVariables.RootName] = port },
            workspaceDirectory);
    }

    /// <summary>某个变量的端口值，未映射或端口为空时为空。</summary>
    public string? Variable(string name) =>
        PortsByName.TryGetValue(name, out PortName port) ? Ports.Text(port) : null;
}

/// <summary>固定返回一个端口文本的访问，用于不经过流程装配的载体。</summary>
public sealed class FixedPortAccess(PortName fixedPort, string text) : IPortAccess
{
    /// <inheritdoc/>
    public IReadOnlyList<Port> Outputs => [];

    /// <inheritdoc/>
    public string? Text(PortName port) => port == fixedPort ? text : null;
}