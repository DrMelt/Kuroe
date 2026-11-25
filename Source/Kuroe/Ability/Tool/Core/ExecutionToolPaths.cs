using Kuroe.Kernel.Execution.Shared.Ports;



namespace Kuroe.Ability.Tool;

/// <summary>执行工具路径的组合：声明的白名单，加按需交回输出端口附上的交回能力，以及所有执行都有的上报与查看通道。</summary>
public static class ExecutionToolPaths
{
    /// <summary>按声明的工具路径与本次执行的输出端口给出可用工具路径。</summary>
    public static IReadOnlyList<ToolPath> For(
        IEnumerable<ToolPath> declared,
        IReadOnlyList<ExecutionOutputPort> outputs)
    {
        List<ToolPath> paths = [.. declared];
        if (outputs.Any(port => port.Required))
        {
            paths.Add(new ToolPath(ContractTools.Port));
        }

        paths.Add(new ToolPath(ContractTools.CannotComplete));
        paths.Add(new ToolPath(ContractTools.Status));

        return paths;
    }

    /// <summary>按声明的工具路径原文与本次执行的输出端口给出可用工具路径。</summary>
    public static IReadOnlyList<ToolPath> For(
        IReadOnlyList<string> declared,
        IReadOnlyList<ExecutionOutputPort> outputs) =>
        For(declared.Select(path => new ToolPath(path)), outputs);
}