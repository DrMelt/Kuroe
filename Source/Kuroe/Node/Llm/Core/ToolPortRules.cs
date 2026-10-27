using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Flow.Shared.Tools;
using Kuroe.Shared;
using Kuroe.Tool;

namespace Kuroe.Node.Llm;

/// <summary>Llm 节点工具路径与路径变量的装载期规则：映射必须落在白名单内，并覆盖每个可用工具的全部变量。</summary>
internal static class ToolPortRules
{
    /// <summary>按白名单与工具契约校验全部映射，错误经 fail 报出。</summary>
    internal static void Validate(
        LlmParameters? parameters,
        NodeSpec node,
        IToolContractCatalog? contracts,
        NodeScope scope,
        Action<Error> fail)
    {
        if (parameters is null)
        {
            return;
        }

        void Report(string message) => fail(scope.Node(node.Label.Value, message));

        List<Mapping> mappings = [];
        foreach ((string key, IReadOnlyDictionary<string, string> ports) in parameters.ToolPorts)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                Report("工具路径不能为空。");
                continue;
            }

            if (ToolPath.InvalidReason(key) is { } reason)
            {
                Report($"工具路径 {key} 不合法：{reason}");
                continue;
            }

            foreach ((string variable, string portName) in ports)
            {
                if (PathTemplate.InvalidNameReason(variable) is { } invalid)
                {
                    Report($"工具 {key} 的路径变量名不合法：{invalid}");
                    continue;
                }

                InputPort? port = node.Inputs.Find(new PortName(portName));
                if (port is null)
                {
                    Report($"工具 {key} 的路径变量 {variable} 映射到未声明的输入端口 {portName}。");
                }
                else if (!Ports.CarriesText(port.Value.Type))
                {
                    Report($"工具 {key} 的路径变量 {variable} 映射到的输入端口 {portName} 必须是 Text。");
                }
            }

            mappings.Add(new Mapping(new ToolPath(key), ports));
        }

        if (contracts is null)
        {
            return;
        }

        IReadOnlyList<ToolContract> all = contracts.Contracts;
        foreach (ToolPath prefix in mappings.Select(mapping => mapping.Prefix))
        {
            List<ToolContract> matched = [.. all.Where(contract => prefix.Allows(contract.Path))];
            if (matched.Count == 0)
            {
                Report($"工具路径 {prefix.Value} 没有匹配任何已注册工具。");
            }
            else if (!matched.Any(contract => parameters.Tools.Any(tool => tool.Allows(contract.Path))))
            {
                Report($"工具路径 {prefix.Value} 不在工具白名单内。");
            }
        }

        foreach (ToolContract contract in all)
        {
            if (!parameters.Tools.Any(tool => tool.Allows(contract.Path)))
            {
                continue;
            }

            List<Mapping> effective = [.. mappings.Where(mapping => mapping.Prefix.Allows(contract.Path))];
            if (contract.Variables.Count == 0)
            {
                if (effective.Count > 0)
                {
                    Report($"工具 {contract.Path} 没有路径变量，不能写映射。");
                }

                continue;
            }

            if (effective.Count == 0)
            {
                Report($"工具 {contract.Path} 缺少路径变量 {string.Join('、', contract.Variables)} 的映射。");
                continue;
            }

            Mapping selected = effective.OrderByDescending(mapping => mapping.Prefix.Value.Length).First();
            HashSet<string> expected = [.. contract.Variables];
            HashSet<string> actual = [.. selected.Ports.Keys];
            foreach (string variable in actual.Except(expected))
            {
                Report($"工具 {contract.Path} 的路径变量 {variable} 不属于工具定义。");
            }

            foreach (string variable in expected.Except(actual))
            {
                Report($"工具 {contract.Path} 缺少路径变量 {variable} 的映射。");
            }
        }
    }

    private sealed record Mapping(ToolPath Prefix, IReadOnlyDictionary<string, string> Ports);
}