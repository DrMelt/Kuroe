using Kuroe.Flow.Node.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Tasks.Shared;

namespace Kuroe.Flow.Compilation.Shared.Validation;

/// <summary>节点接口的静态规则：输入与输出端口名在同一节点内唯一，端口类型落在类型形状的值域，模式落在类型注册支持范围，
/// 输出按 Value、Ref 与运行期常量判定来源。流程校验与节点库校验共用这一份，报错出口由调用方给出。</summary>
public static partial class NodeSpecRules
{
    /// <summary>逐条检查一个节点的端口声明，每条错误经 fail 报出。shape 给出端口类型值域与推进方式，
    /// 端口模式由类型注册声明支持范围。</summary>
    public static void CheckInterface(
        this NodeSpec node,
        NodeTypeShape? shape,
        ValidationMessageHandler fail)
    {
        bool container = shape?.Organizes ?? node.IsContainer;
        bool organizes = node.IsContainer;
        IReadOnlyList<InputPort> inputs = node.EffectiveInputs;
        foreach (PortName port in Duplicates([.. inputs.Select(port => port.Name)]))
        {
            fail($"输入端口 {port.Value} 重复。");
        }

        foreach (InputPort port in inputs.Where(port => !PortTypes.Supports(port.Type, port.Mode)))
        {
            fail($"输入端口 {port.Name.Value} 的类型 {port.Type} 不支持模式 {port.Mode}。");
        }

        foreach (InputPort port in inputs.Where(port => port.Role == InputRole.Control && port.IsStatic))
        {
            fail($"控制输入端口 {port.Name.Value} 不能是静态输入。");
        }

        foreach (InputPort port in inputs.Where(port =>
            port.IsStatic && port.Type.Semantics == PortSemantics.Signal && port.Role != InputRole.Configuration))
        {
            fail($"Signal 静态输入端口 {port.Name.Value} 只能作为配置。");
        }

        if (shape is { } declared)
        {
            foreach (InputPort port in inputs.Where(port => !declared.InputTypes.Contains(port.Type)))
            {
                fail($"输入端口 {port.Name.Value} 的类型只能是 {Types(declared.InputTypes)}。");
            }
        }

        if (container)
        {
            foreach (InputPort port in inputs.Where(port => port.IsStatic && port.Default is not null))
            {
                fail($"静态输入端口 {port.Name.Value} 在容器上不能声明默认值，值由引用处的绑定提供。");
            }
        }

        foreach (PortName port in Duplicates([.. node.Outputs.Select(port => port.Name)]))
        {
            fail($"输出端口 {port.Value} 重复。");
        }

        foreach (string name in node.Outputs
            .Select(port => port.Name)
            .Where(IsInvalidPortName)
            .Select(name => name.Value))
        {
            fail(string.IsNullOrWhiteSpace(name)
                ? "输出端口名不能为空。"
                : $"输出端口名 {name} 不能含 @。");
        }

        foreach (Port port in node.Outputs.Where(port => !PortTypes.Supports(port.Type, port.Mode)))
        {
            fail($"输出端口 {port.Name.Value} 的类型 {port.Type} 不支持模式 {port.Mode}。");
        }

        if (shape is { } declaredOutput)
        {
            if (declaredOutput.Outputs is OutputDeclaration.Given given && !node.Outputs.SequenceEqual(given.Ports))
            {
                fail("输出端口必须与类型给出的端口一致。");
            }

            IReadOnlyList<PortType> allowed = declaredOutput.AllowedOutputTypes;
            foreach (string port in node.Outputs
                .Where(port => !allowed.Contains(port.Type))
                .Select(port => port.Name.Value))
            {
                fail($"输出端口 {port} 的类型只能是 {Types(allowed)}。");
            }
        }

        foreach (Port port in node.Outputs)
        {
            CheckOutputSource(node, port, organizes, fail);
        }

        NodeEmission emission = node.Spec?.Emission ?? NodeEmission.Repeated;
        bool runtimeConstant = !organizes
            && node.Outputs.Any(port => port.IsConstant && port.Value is null && port.Ref is null);
        if (runtimeConstant && emission != NodeEmission.Once)
        {
            fail("运行期常量输出需要节点声明 Emission 为 Once。");
        }

        if (emission == NodeEmission.Once)
        {
            if (organizes)
            {
                fail("容器不能声明 Emission。");
            }

            if (!runtimeConstant)
            {
                fail("Emission 为 Once 的节点至少要有一个由执行产出的常量输出。");
            }

            if (node.Outputs.Any(port => !port.IsConstant))
            {
                fail("Emission 为 Once 的节点的输出必须都是常量端口。");
            }

            if (node.From.Count > 0 || node.EffectiveInputs.Any(port => !port.IsStatic))
            {
                fail("Emission 为 Once 的节点不能声明动态输入或 From。");
            }
        }

        if (shape is { Organizes: false } executable)
        {
            foreach (InputPort port in inputs.Where(port => port.Default is not null))
            {
                if (port.IsStatic)
                {
                    if (string.IsNullOrWhiteSpace(port.Default))
                    {
                        fail($"静态输入端口 {port.Name.Value} 的默认值不能为空。");
                    }

                    continue;
                }

                if (!executable.DispatchesExecutions || port.Type != PortTypes.Text)
                {
                    fail($"输入端口 {port.Name.Value} 的默认值只用于派发执行的节点的 Text 输入端口。");
                }
                else if (string.IsNullOrWhiteSpace(port.Default))
                {
                    fail($"输入端口 {port.Name.Value} 的默认值不能为空。");
                }
            }
        }

        if (node.Spec is null || shape is { DispatchesExecutions: false })
        {
            return;
        }

        if (node.Outputs.Count(port => port.Type.Semantics == PortSemantics.Context && !port.IsConstant) > 1)
        {
            fail("Context 输出端口至多声明一个。");
        }
    }

    /// <summary>输出端口来源：常量输出只接受固定值或常量来源，动态输出不接受固定值，转发来源只用于组织子节点的节点。
    /// 引用本节点静态输入时在此完成存在性、常量性与类型校验。</summary>
    private static void CheckOutputSource(NodeSpec node, Port port, bool organizes, ValidationMessageHandler fail)
    {
        string name = port.Name.Value;
        if (port.Value is not null && port.Ref is not null)
        {
            fail($"输出端口 {name} 不能同时写 Value 与 Ref。");
            return;
        }

        if (port.IsConstant)
        {
            if (port.Value is { } value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    fail($"常量输出端口 {name} 的 Value 不能为空。");
                }

                return;
            }

            if (port.Ref is not { } reference)
            {
                if (organizes)
                {
                    fail($"容器常量输出端口 {name} 必须写 Value 或 Ref。");
                }

                return;
            }

            if (reference.Source is null)
            {
                if (node.FindInput(reference.Port) is not { IsStatic: true } input)
                {
                    fail($"常量输出端口 {name} 的来源 {reference.Display} 不是本节点的静态输入。");
                    return;
                }

                foreach (string message in OutputSourceRules.Check(port, input.Type, true, reference.Display))
                {
                    fail(message);
                }

                return;
            }

            if (!organizes)
            {
                fail($"常量输出端口 {name} 的 Ref 只能引用本节点的静态输入，写 @端口。");
            }

            return;
        }

        if (port.Value is not null)
        {
            fail($"动态输出端口 {name} 不能写 Value。");
        }

        if (organizes)
        {
            if (port.Ref is not { } reference)
            {
                fail($"输出端口 {name} 必须写 Value 或 Ref。");
                return;
            }

            if (reference.Source is null)
            {
                if (node.FindInput(reference.Port) is not { IsStatic: true } input)
                {
                    fail($"动态输出端口 {name} 的来源 {reference.Display} 不是本节点的静态输入。");
                    return;
                }

                foreach (string message in OutputSourceRules.Check(port, input.Type, true, reference.Display))
                {
                    fail(message);
                }
            }

            return;
        }

        if (port.Ref is not null)
        {
            fail($"动态输出端口 {name} 不能写 Ref，执行产出由节点交回。");
        }
    }

    /// <summary>端口类型值域的展示文本。</summary>
    private static string Types(IReadOnlyList<PortType> types) => types.Count switch
    {
        0 => "（无）",
        1 => types[0].ToString(),
        _ => $"{string.Join("、", types.Take(types.Count - 1).Select(type => type.ToString()))} 或 {types[^1]}",
    };

    /// <summary>端口名不能为空或含 @。</summary>
    private static bool IsInvalidPortName(PortName name) =>
        string.IsNullOrWhiteSpace(name.Value) || name.Value.Contains('@');

    /// <summary>重复出现的端口名。</summary>
    private static IEnumerable<PortName> Duplicates(IReadOnlyList<PortName> names) =>
        names.GroupBy(name => name).Where(group => group.Count() > 1).Select(group => group.Key);
}
