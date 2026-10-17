using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph;
namespace Kuroe.Flow.NodeLibrary;
/// <summary>把流程装配展开成具体节点树：引用按库定义实例化，端口绑定沿作用域链透传。
/// 节点名按作者写下的原文保留，层级由树给出，重名只需同一容器内避免；引用由编译期按作用域链解析。</summary>
internal static class NodeExpander
{
    /// <summary>展开作用域的一层：容器实例的端口绑定与模型槽位绑定。
    /// 每层以 parent 串起，从内向外透传端口与模型绑定。</summary>
    private sealed class Env(
        Env? parent,
        IReadOnlyDictionary<PortName, PortRef> bindings,
        IReadOnlyDictionary<ModelRef, ModelRef>? modelSlots)
    {
        public Env? Parent { get; } = parent;
        public IReadOnlyDictionary<PortName, PortRef> Bindings { get; } = bindings;

        /// <summary>引用节点组时的模型槽位绑定：槽位名到流程模型选择名或外层槽位名的映射。</summary>
        public IReadOnlyDictionary<ModelRef, ModelRef>? ModelSlots { get; } = modelSlots;
    }

    /// <summary>展开流程根节点。引用类错误（定义不存在、端口无绑定）在此一次给全。</summary>
    public static ErrorOr<NodeSpec> Expand(
        NodeLibrary library,
        NodeSpec root,
        NodeScope scope)
    {
        List<Error> errors = [];
        NodeSpec? expanded = ExpandNode(library, root, null, inInstance: false, currentLimit: null, scope, errors);

        if (errors.Count > 0)
        {
            return errors;
        }

        return expanded!;
    }

    /// <summary>展开一个节点：引用按库定义实例化，实例根采用引用处写下的名字，库定义内成员沿用定义里的名字。</summary>
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
                errors.Add(scope.Node(node.Name.Value, $"引用的节点 {use} 不在节点库。"));
                return null;
            }

            NodeName invocation = node.Name.Value.Length > 0 ? node.Name : definition.Name;
            if (definition.Nodes is { Count: > 0 })
            {
                if (node.From.Count > 0)
                {
                    errors.Add(scope.Node(node.Name.Value, "引用容器不能声明 From，容器自身不接线。"));
                }

                if (node.Model is not null)
                {
                    errors.Add(scope.Node(node.Name.Value, "引用节点组不能声明模型，用 Models 绑定组内成员的模型。"));
                }

                if (node.Validate is not null)
                {
                    errors.Add(scope.Node(node.Name.Value, "引用节点组不能声明 Validate，输出校验只属于执行节点。"));
                }

                CheckBindings(node, definition, scope, errors);
                Env childEnv = BuildContainerEnv(env, node.In, node.Models);
                int? memberLimit = node.MaxRuns ?? definition.MaxRuns ?? currentLimit;
                List<NodeSpec> members = ExpandMembers(library, definition.Nodes, childEnv, inInstance: true, scope, errors, memberLimit);
                return new NodeSpec
                {
                    Name = invocation,
                    Type = definition.Type,
                    Inputs = definition.Inputs,
                    Nodes = members,
                    ContainerOutputs = ResolveContainerOutputs(definition.ContainerOutputs, childEnv, scope, errors),
                };
            }

            if (node.Models is not null)
            {
                errors.Add(scope.Node(node.Name.Value, "引用执行节点不能声明模型绑定，用 Model 指定模型选择。"));
            }

            if (node.In is { Count: > 0 })
            {
                errors.Add(scope.Node(node.Name.Value, "引用执行节点不写输入端口绑定，接线一律经 From。"));
            }

            ExecutableSpec libraryExecution = definition.Execution!;

            return new NodeSpec
            {
                Name = invocation,
                Type = definition.Type,
                Inputs = definition.Inputs,
                Execution = libraryExecution with
                {
                    Validate = node.Validate ?? libraryExecution.Validate,
                    MaxRuns = ResolveMaxRuns(node.MaxRuns, libraryExecution.MaxRuns, currentLimit),
                },
                Model = ResolveModel(node.Model, env, inInstance, scope, node.Name.Value, errors),
                From = ResolveAll(node.From, env, scope, errors),
                Outputs = definition.Outputs,
            };
        }

        if (node.Nodes is { Count: > 0 } children)
        {
            List<NodeSpec> expanded = ExpandMembers(library, children, env, inInstance, scope, errors, node.MaxRuns ?? currentLimit);
            return new NodeSpec
            {
                Name = node.Name,
                Type = node.Type,
                Inputs = node.Inputs,
                From = ResolveAll(node.From, env, scope, errors),
                ContainerOutputs = ResolveContainerOutputs(node.ContainerOutputs, env, scope, errors),
                Nodes = expanded,
            };
        }

        ExecutableSpec execution = node.Execution!;

        return new NodeSpec
        {
            Name = node.Name,
            Type = node.Type,
            Inputs = node.Inputs,
            Execution = execution with
            {
                Validate = execution.Validate,
                MaxRuns = ResolveMaxRuns(execution.MaxRuns, null, currentLimit),
            },
            Model = ResolveModel(node.Model, env, inInstance, scope, node.Name.Value, errors),
            From = ResolveAll(node.From, env, scope, errors),
            Outputs = node.Outputs,
        };
    }

    /// <summary>执行节点的生效执行上限：自身显式、库定义、外层容器统一值依次取先，都没有时落到默认值。</summary>
    private static int ResolveMaxRuns(int? own, int? library, int? currentLimit) =>
        own ?? library ?? currentLimit ?? ExecutableNode.DefaultMaxRuns;

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

    /// <summary>引用容器的绑定域：端口绑定只能指向定义声明的输入端口，模型绑定只能指向定义声明的槽位。</summary>
    private static void CheckBindings(
        NodeSpec node,
        NodeSpec definition,
        NodeScope scope,
        List<Error> errors)
    {
        if (node.In is { } bindings)
        {
            var declared = new HashSet<PortName>([.. definition.Inputs.Select(port => port.Name)]);
            foreach (PortName port in bindings.Keys.Where(port => !declared.Contains(port)))
            {
                errors.Add(scope.Node(node.Name.Value, $"端口 {port} 不在容器 {definition.Name} 上。"));
            }
        }

        if (node.Models is { } modelBindings)
        {
            var slots = new HashSet<ModelRef>(definition.ModelSlots ?? []);
            foreach (ModelRef slot in modelBindings.Keys.Where(slot => !slots.Contains(slot)))
            {
                errors.Add(scope.Node(node.Name.Value, $"模型槽位 {slot} 不在容器 {definition.Name} 上。"));
            }
        }
    }

    /// <summary>构造容器实例的子作用域：端口绑定与模型槽位绑定进作用域。
    /// parent 沿当前环境链挂上，便于 @更外层端口 与更外层模型槽位透传。</summary>
    private static Env BuildContainerEnv(
        Env? parent,
        IReadOnlyDictionary<PortName, PortRef>? bindings,
        IReadOnlyDictionary<ModelRef, ModelRef>? modelSlots) =>
        new(parent, bindings ?? new Dictionary<PortName, PortRef>(), modelSlots);

    /// <summary>逐个解析上游启动组：来源名沿实例映射链与端口绑定链解析，输入名与类型保留。</summary>
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

    /// <summary>解析一个端口引用：来源为空（@端口）沿绑定链找实际来源，写明来源的引用原样保留，由编译期按作用域解析。
    /// 绑定链找不到来源时保留原引用并报错，交既有校验判断。</summary>
    private static PortRef ResolveRef(PortRef reference, Env? env, NodeScope scope, List<Error> errors)
    {
        if (reference.Source is not null)
        {
            return reference;
        }

        // @端口：沿绑定链找到绑定来源，绑定值沿更外层作用域透传，来源名保持作者写下的原文
        Env? chain = env;
        while (chain is not null)
        {
            if (chain.Bindings.TryGetValue(reference.Port, out PortRef bound))
            {
                return bound.Source is null
                    ? ResolveRef(bound, chain.Parent, scope, errors)
                    : bound;
            }

            chain = chain.Parent;
        }

        errors.Add(scope.Node(reference.Port.Value, $"端口 @{reference.Port.Value} 没有绑定来源。"));
        return reference;
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
            entry => new ContainerOutput(ResolveRef(entry.Value.Ref, env, scope, errors), entry.Value.Description));
    }

    /// <summary>执行节点的模型引用：装配层节点直接是流程模型选择名，节点组实例内成员是模型槽位，沿绑定链解析成选择名。
    /// 装配层引用执行节点未写模型时留空，交 FlowRules 报错。</summary>
    private static ModelRef? ResolveModel(
        ModelRef? model,
        Env? env,
        bool inInstance,
        NodeScope scope,
        string nodeName,
        List<Error> errors)
    {
        if (model is null || !inInstance)
        {
            return model;
        }

        return ResolveSlot(model.Value, env, mustBind: true, scope, nodeName, errors);
    }

    /// <summary>沿模型绑定链解析槽位：当前层命中取绑定值继续向更外层解析，直到装配层拿到流程模型选择名。
    /// 节点组内执行节点的模型必须从引用处绑定，未命中报错。</summary>
    private static ModelRef? ResolveSlot(
        ModelRef name,
        Env? env,
        bool mustBind,
        NodeScope scope,
        string nodeName,
        List<Error> errors)
    {
        if (env is null || env.ModelSlots is null)
        {
            if (mustBind)
            {
                errors.Add(scope.Node(nodeName, $"模型槽位 {name} 没有外部绑定，引用节点组时用 Models 提供。"));
                return null;
            }

            return name;
        }

        if (env.ModelSlots.TryGetValue(name, out ModelRef bound))
        {
            return ResolveSlot(bound, env.Parent, mustBind: false, scope, nodeName, errors);
        }

        return ResolveSlot(name, env.Parent, mustBind, scope, nodeName, errors);
    }
}
