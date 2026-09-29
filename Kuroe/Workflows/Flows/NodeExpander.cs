using ErrorOr;
using Flow = Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Flows;

/// <summary>把流程装配展开成具体节点树：引用按库定义实例化，端口绑定沿作用域链透传。
/// 装配层直接写的节点名保持原名，扁平唯一由校验保证；库定义实例化时整棵子树成员名带实例前缀，避免重名。</summary>
internal static class NodeExpander
{
    private const char PortPrefix = '@';

    /// <summary>展开作用域的一层：容器实例的成员原名到实例名的映射，以及端口绑定。
    /// 每层以 parent 串起，从内向外解析成员名，从内向外透传端口绑定。</summary>
    private sealed class Env(
        Env? parent,
        IReadOnlyDictionary<Flow.NodeName, Flow.NodeName> renames,
        IReadOnlyDictionary<Flow.NodeName, Flow.NodeName> bindings)
    {
        public Env? Parent { get; } = parent;
        public IReadOnlyDictionary<Flow.NodeName, Flow.NodeName> Renames { get; } = renames;
        public IReadOnlyDictionary<Flow.NodeName, Flow.NodeName> Bindings { get; } = bindings;
    }

    private static bool IsPort(Flow.NodeName name) => name.Value.StartsWith(PortPrefix);
    private static string PortName(Flow.NodeName name) => name.Value[1..];

    /// <summary>展开流程根节点。引用类错误（定义不存在、端口无绑定）在此一次给全。</summary>
    public static ErrorOr<Flow.NodeSpec> Expand(
        IReadOnlyDictionary<Flow.NodeName, Flow.NodeSpec> library,
        Flow.NodeSpec root,
        string flowName)
    {
        List<Error> errors = [];
        Flow.NodeSpec? expanded = ExpandNode(library, root, null, string.Empty, inInstance: false, flowName, errors);

        if (errors.Count > 0)
        {
            return errors;
        }

        return expanded!;
    }

    /// <summary>展开一个节点。prefix 是库实例链前缀；装配层内联容器成员保持原名（原与校验的扁平唯一联动）。
    /// 库定义实例化时，子树成员名都带实例前缀。</summary>
    private static Flow.NodeSpec? ExpandNode(
        IReadOnlyDictionary<Flow.NodeName, Flow.NodeSpec> library,
        Flow.NodeSpec node,
        Env? env,
        string prefix,
        bool inInstance,
        string flowName,
        List<Error> errors)
    {
        if (node.Use is { } use)
        {
            if (!library.TryGetValue(use, out Flow.NodeSpec? definition))
            {
                errors.Add(WorkflowErrors.Node(flowName, node.Name.Value, $"引用的节点 {use} 不在节点库。"));
                return null;
            }

            string invocation = node.Name.Value.Length > 0 ? node.Name.Value : definition.Name.Value;
            Flow.NodeName instance = new(prefix + invocation);
            if (definition.Nodes is { Count: > 0 })
            {
                if (node.From.Count > 0)
                {
                    errors.Add(WorkflowErrors.Node(flowName, node.Name.Value, "引用容器不能声明 From，容器自身不接线。"));
                }

                CheckBindings(node, definition, flowName, errors);
                Env childEnv = BuildContainerEnv(env, definition, instance, node.In);
                List<Flow.NodeSpec> members = ExpandMembers(library, definition.Nodes, childEnv, instance.Value + ".", flowName, errors);
                return new Flow.NodeSpec { Name = instance, Gate = definition.Gate, Nodes = members };
            }

            return new Flow.NodeSpec
            {
                Name = instance,
                Gate = definition.Gate,
                Execution = definition.Execution,
                From = ResolveAll(node.From, env, flowName, errors),
            };
        }

        Flow.NodeName own = new(prefix + node.Name.Value);
        if (node.Nodes is { Count: > 0 } children)
        {
            // 装配层内联容器成员不再叠加容器名，库实例内成员才叠加实例前缀
            string memberPrefix = inInstance ? own.Value + "." : prefix;
            List<Flow.NodeSpec> expanded = ExpandMembers(library, children, env, memberPrefix, flowName, errors);
            return new Flow.NodeSpec
            {
                Name = own,
                Gate = node.Gate,
                From = ResolveAll(node.From, env, flowName, errors),
                Nodes = expanded,
            };
        }

        return new Flow.NodeSpec
        {
            Name = own,
            Gate = node.Gate,
            Execution = node.Execution,
            From = ResolveAll(node.From, env, flowName, errors),
        };
    }

    private static List<Flow.NodeSpec> ExpandMembers(
        IReadOnlyDictionary<Flow.NodeName, Flow.NodeSpec> library,
        IReadOnlyList<Flow.NodeSpec> members,
        Env? env,
        string prefix,
        string flowName,
        List<Error> errors)
    {
        List<Flow.NodeSpec> expanded = [];
        foreach (Flow.NodeSpec member in members)
        {
            Flow.NodeSpec? item = ExpandNode(library, member, env, prefix, inInstance: prefix.Length > 0, flowName, errors);
            if (item is not null)
            {
                expanded.Add(item);
            }
        }

        return expanded;
    }

    /// <summary>端口绑定只能指向容器定义声明的输入端口。</summary>
    private static void CheckBindings(
        Flow.NodeSpec node,
        Flow.NodeSpec definition,
        string flowName,
        List<Error> errors)
    {
        if (node.In is not { } bindings)
        {
            return;
        }

        var declared = new HashSet<Flow.NodeName>([.. definition.Inputs]);
        foreach (Flow.NodeName port in bindings.Keys.Where(port => !declared.Contains(port)))
        {
            errors.Add(WorkflowErrors.Node(flowName, node.Name.Value, $"端口 {port} 不在容器 {definition.Name} 上。"));
        }
    }

    /// <summary>构造容器实例的子作用域：整棵子树成员名对齐到带实例前缀的实例名，端口绑定进作用域。
    /// parent 沿当前环境链挂上，便于 @更外层端口 透传。</summary>
    private static Env BuildContainerEnv(
        Env? parent,
        Flow.NodeSpec container,
        Flow.NodeName instanceName,
        IReadOnlyDictionary<Flow.NodeName, Flow.NodeName>? bindings)
    {
        Dictionary<Flow.NodeName, Flow.NodeName> renames = [];
        CollectRenames(container.Nodes!, renames, instanceName.Value + ".");

        return new Env(parent, renames, bindings ?? new Dictionary<Flow.NodeName, Flow.NodeName>());
    }

    /// <summary>递归收集容器子树全部成员的实例名映射：成员原名对齐到带实例名前缀的实例名。</summary>
    private static void CollectRenames(
        IReadOnlyList<Flow.NodeSpec> nodes,
        Dictionary<Flow.NodeName, Flow.NodeName> renames,
        string prefix)
    {
        foreach (Flow.NodeSpec node in nodes)
        {
            Flow.NodeName localName = node.Name.Value.Length > 0
                ? node.Name
                : new Flow.NodeName(node.Use?.Value ?? string.Empty);
            Flow.NodeName instanceName = new(prefix + localName.Value);
            renames[localName] = instanceName;
            if (node.Nodes is { Count: > 0 })
            {
                CollectRenames(node.Nodes, renames, instanceName.Value + ".");
            }
        }
    }

    /// <summary>逐个解析 From 引用：普通名沿映射链找实例名，@端口沿绑定链找绑定。</summary>
    private static IReadOnlyList<Flow.NodeName> ResolveAll(IReadOnlyList<Flow.NodeName> from, Env? env, string flowName, List<Error> errors) =>
        [.. from.Select(name => Resolve(name, env, flowName, errors))];

    /// <summary>解析一个引用名：@端口沿环境链找绑定，普通名沿环境链找实例映射。
    /// 都没有则保留原名交既有校验判断。</summary>
    private static Flow.NodeName Resolve(Flow.NodeName name, Env? env, string flowName, List<Error> errors)
    {
        if (!IsPort(name))
        {
            Env? scope = env;
            while (scope is not null)
            {
                if (scope.Renames.TryGetValue(name, out Flow.NodeName renamed))
                {
                    return renamed;
                }

                scope = scope.Parent;
            }

            return name;
        }

        string port = PortName(name);
        Env? chain = env;
        while (chain is not null)
        {
            if (chain.Bindings.TryGetValue(new Flow.NodeName(port), out Flow.NodeName bound))
            {
                // @绑定值沿更外层作用域透传；普通名绑定在所在作用域解析成实例名
                return IsPort(bound) ? Resolve(bound, chain.Parent, flowName, errors) : Resolve(bound, chain, flowName, errors);
            }

            chain = chain.Parent;
        }

        errors.Add(WorkflowErrors.Node(flowName, name.Value, $"端口 {name} 没有绑定来源。"));
        return name;
    }
}