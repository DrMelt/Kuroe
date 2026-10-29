using ErrorOr;
using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
namespace Kuroe.Flow.NodeLibrary;
/// <summary>把流程装配展开成具体节点树：引用按库定义实例化，容器的 In 在所在作用域建立绑定域，成员的 From、In 与 Static 引用都在该绑定域里取 @端口。
/// 节点按作者写下的原文保留，定义条目沿用节点名，引用条目用标签，层级由树给出；引用由编译期按作用域链解析。</summary>
internal static class NodeExpander
{
    /// <summary>展开作用域的一层：容器实例的端口绑定。每层以 parent 串起，从内向外透传端口绑定。</summary>
    private sealed class Env(
        Env? parent,
        IReadOnlyDictionary<PortName, PortRef> bindings)
    {
        public Env? Parent { get; } = parent;
        public IReadOnlyDictionary<PortName, PortRef> Bindings { get; } = bindings;
    }

    /// <summary>展开流程根节点。引用类错误（定义不存在、端口无绑定）在此一次给全。</summary>
    public static ErrorOr<NodeSpec> Expand(
        NodeLibrary library,
        NodeSpec root,
        NodeScope scope)
    {
        List<Error> errors = [];
        CheckRootBindings(library, root, scope, errors);
        NodeSpec? expanded = ExpandNode(library, root, null, inInstance: false, currentLimit: null, scope, errors);

        if (errors.Count > 0)
        {
            return errors;
        }

        return expanded!;
    }

    /// <summary>根容器的绑定来源：流程作用域里只有开始节点，根容器的传入端口只能绑定开始端口。</summary>
    private static void CheckRootBindings(
        NodeLibrary library,
        NodeSpec root,
        NodeScope scope,
        List<Error> errors)
    {
        bool container = root.Nodes is { Count: > 0 }
            || (root.Use is { } use
                && library.TryFind(use, out NodeSpec? definition)
                && definition.Nodes is { Count: > 0 });
        if (!container || root.In is not { Count: > 0 } bindings)
        {
            return;
        }

        foreach ((PortName port, PortRef reference) in bindings)
        {
            if (reference.Source is null)
            {
                errors.Add(scope.Node(root.Label.Value, $"根容器的端口 {port} 不能引用更外层端口。"));
            }
            else if (reference.Source != StartNode.ReservedLabel)
            {
                errors.Add(scope.Node(root.Label.Value,
                    $"根容器的端口 {port} 引用的节点 {reference.Source.Value} 不在流程作用域里。"));
            }
        }
    }

    /// <summary>展开一个节点：引用按库定义实例化，实例根采用引用处写下的标签，库定义内成员沿用定义里的标签。</summary>
    private static NodeSpec? ExpandNode(
        NodeLibrary library,
        NodeSpec node,
        Env? env,
        bool inInstance,
        int? currentLimit,
        NodeScope scope,
        List<Error> errors)
    {
        if (node.Use is { } use)
        {
            if (!library.TryFind(use, out NodeSpec? definition))
            {
                errors.Add(scope.Node(node.Label.Value, $"引用的节点 {use} 不在节点库。"));
                return null;
            }

            if (definition.Nodes is { Count: > 0 })
            {
                node.CheckWiring(NodeRuleContext.Reference, message =>
                    errors.Add(scope.Node(node.Label.Value, message)), container: true);

                CheckBindings(node, definition.EffectiveInputs, definition.Label, scope, errors);
                Env childEnv = BuildContainerEnv(env, node.In);
                int? memberLimit = node.MaxExecutions ?? definition.MaxExecutions ?? currentLimit;
                List<NodeSpec> members = ExpandMembers(library, definition.Nodes, childEnv, inInstance: true, scope, errors, memberLimit);
                return new NodeSpec
                {
                    Label = node.Label,
                    Type = definition.Type,
                    Inputs = definition.Inputs,
                    Nodes = members,
                    ContainerOutputs = ResolveContainerOutputs(definition.ContainerOutputs, childEnv, scope, errors),
                    MaxExecutions = node.MaxExecutions,
                };
            }

            node.CheckWiring(NodeRuleContext.Reference, message =>
                errors.Add(scope.Node(node.Label.Value, message)), container: false);

            ExecutableSpec librarySpec = definition.Spec!;

            return new NodeSpec
            {
                Label = node.Label,
                Type = definition.Type,
                Inputs = definition.Inputs,
                Spec = librarySpec with
                {
                    Validate = node.Validate ?? librarySpec.Validate,
                    MaxExecutions = ResolveMaxExecutions(node.MaxExecutions, librarySpec.MaxExecutions, currentLimit),
                },
                Static = ResolveStatic(node.Static, env, scope, errors),
                From = ResolveAll(node.From, env, scope, errors),
                Outputs = definition.Outputs,
            };
        }

        if (node.Nodes is { Count: > 0 } children)
        {
            if (!inInstance)
            {
                ContainerScope.Check(node, children, name => Find(library, name), (member, message) =>
                    errors.Add(scope.Node(member.Label.Value, message)));
            }

            CheckBindings(node, node.EffectiveInputs, node.Label, scope, errors);
            Env childEnv = BuildContainerEnv(env, node.In);
            List<NodeSpec> expanded = ExpandMembers(library, children, childEnv, inInstance, scope, errors, node.MaxExecutions ?? currentLimit);
            return new NodeSpec
            {
                Name = node.Name,
                Label = node.Label,
                Type = node.Type,
                Inputs = node.Inputs,
                From = ResolveAll(node.From, env, scope, errors),
                Static = ResolveStatic(node.Static, env, scope, errors),
                ContainerOutputs = ResolveContainerOutputs(node.ContainerOutputs, childEnv, scope, errors),
                MaxExecutions = node.MaxExecutions,
                Nodes = expanded,
            };
        }

        if (node.Spec is null)
        {
            // 结构不完整时保留原节点，由校验给出诊断
            return node;
        }

        ExecutableSpec spec = node.Spec;

        return new NodeSpec
        {
            Name = node.Name,
            Label = node.Label,
            Type = node.Type,
            Inputs = node.Inputs,
            Spec = spec with
            {
                MaxExecutions = ResolveMaxExecutions(spec.MaxExecutions, null, currentLimit),
            },
            Static = ResolveStatic(node.Static, env, scope, errors),
            From = ResolveAll(node.From, env, scope, errors),
            Outputs = node.Outputs,
        };
    }

    /// <summary>执行节点的生效执行上限：自身显式、库定义、外层容器统一值依次取先，都没有时落到默认值。</summary>
    private static int ResolveMaxExecutions(int? own, int? library, int? currentLimit) =>
        own ?? library ?? currentLimit ?? ExecutableNode.DefaultMaxExecutions;

    private static List<NodeSpec> ExpandMembers(
        NodeLibrary library,
        IReadOnlyList<NodeSpec> members,
        Env? env,
        bool inInstance,
        NodeScope scope,
        List<Error> errors,
        int? currentLimit)
    {
        List<NodeSpec> expanded = [];
        foreach (NodeSpec member in members)
        {
            NodeSpec? item = ExpandNode(library, member, env, inInstance, currentLimit, scope, errors);
            if (item is not null)
            {
                expanded.Add(item);
            }
        }

        return expanded;
    }

    /// <summary>容器的绑定域：端口绑定必须覆盖声明的输入端口且不能指向未声明的端口。</summary>
    private static void CheckBindings(
        NodeSpec node,
        IReadOnlyList<InputPort> inputs,
        NodeLabel container,
        NodeScope scope,
        List<Error> errors)
    {
        var declaredPorts = new HashSet<PortName>([.. inputs.Select(port => port.Name)]);
        IReadOnlyDictionary<PortName, PortRef> bound = node.In ?? new Dictionary<PortName, PortRef>();
        foreach (PortName port in bound.Keys.Where(port => !declaredPorts.Contains(port)))
        {
            errors.Add(scope.Node(node.Label.Value, $"端口 {port} 不在容器 {container} 上。"));
        }

        foreach (InputPort port in inputs.Where(port => !bound.ContainsKey(port.Name)))
        {
            errors.Add(scope.Node(node.Label.Value, $"端口 {port.Name} 没有绑定。"));
        }

    }

    /// <summary>按名字取库定义，未注册时给空。</summary>
    private static NodeSpec? Find(NodeLibrary library, NodeName name) =>
        library.TryFind(name, out NodeSpec? definition) ? definition : null;

    /// <summary>构造容器实例的子作用域：端口绑定进作用域。
    /// parent 沿当前环境链挂上，便于 @更外层端口 逐层透传。</summary>
    private static Env BuildContainerEnv(
        Env? parent,
        IReadOnlyDictionary<PortName, PortRef>? bindings) =>
        new(parent, bindings ?? new Dictionary<PortName, PortRef>());

    /// <summary>解析静态绑定：@端口 沿容器的端口绑定链展开，写明来源的引用由编译期按作用域解析。</summary>
    private static Dictionary<PortName, PortRef>? ResolveStatic(
        IReadOnlyDictionary<PortName, PortRef>? bindings,
        Env? env,
        NodeScope scope,
        List<Error> errors) =>
        bindings is { Count: > 0 }
            ? bindings.ToDictionary(pair => pair.Key, pair => ResolveRef(pair.Value, env, scope, errors))
            : null;

    /// <summary>逐个解析上游启动组：来源标签沿实例映射链与端口绑定链解析，输入名与类型保留。</summary>
    private static IReadOnlyList<FromGroup> ResolveAll(
        IReadOnlyList<FromGroup> from,
        Env? env,
        NodeScope scope,
        List<Error> errors) =>
        [
            .. from.Select(group => group with
            {
                Links = [.. group.Links.Select(link => link with { Ref = ResolveRef(link.Ref, env, scope, errors) })],
            }),
        ];

    /// <summary>解析一个端口引用：来源为空（@端口）在所属容器的绑定域里找实际来源，写明来源的引用原样保留，由编译期按作用域解析。
    /// 绑定值沿更外层绑定域透传；成员只看自己容器的绑定域，外层容器的端口不可见。</summary>
    private static PortRef ResolveRef(PortRef reference, Env? env, NodeScope scope, List<Error> errors)
    {
        if (reference.Source is not null)
        {
            return reference;
        }

        if (env is not null && env.Bindings.TryGetValue(reference.Port, out PortRef bound))
        {
            return bound.Source is null
                ? ResolveBound(bound, env.Parent, scope, errors)
                : bound;
        }

        errors.Add(scope.Node(reference.Port.Value, $"端口 @{reference.Port.Value} 没有绑定来源。"));
        return reference;
    }

    /// <summary>绑定值沿更外层绑定域解析，@更外层端口 因此逐层透传，来源标签保持作者写下的原文。</summary>
    private static PortRef ResolveBound(PortRef bound, Env? env, NodeScope scope, List<Error> errors)
    {
        if (env is not null && env.Bindings.TryGetValue(bound.Port, out PortRef outer))
        {
            return outer.Source is null
                ? ResolveBound(outer, env.Parent, scope, errors)
                : outer;
        }

        errors.Add(scope.Node(bound.Port.Value, $"端口 @{bound.Port.Value} 没有绑定来源。"));
        return bound;
    }

    /// <summary>容器输出端口的实例化：绑定值按原文保留，引用在编译期按作用域解析，说明原样保留。</summary>
    private static Dictionary<PortName, ContainerOutput>? ResolveContainerOutputs(
        IReadOnlyDictionary<PortName, ContainerOutput>? outs,
        Env? env,
        NodeScope scope,
        List<Error> errors)
    {
        if (outs is not { Count: > 0 })
        {
            return null;
        }

        return outs.ToDictionary(
            entry => entry.Key,
            entry => new ContainerOutput(
                ResolveRef(entry.Value.Ref, env, scope, errors),
                entry.Value.Description,
                entry.Value.IsStatic));
    }

}
