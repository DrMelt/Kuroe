using ErrorOr;
using Kuroe.Shared.Workflows.Graph;
using Flow = Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Flows;

/// <summary>把流程装配展开成具体节点树：引用按库定义实例化，端口绑定沿作用域链透传。
/// 装配层直接写的节点名保持原名，扁平唯一由校验保证；库定义实例化时整棵子树成员名带实例前缀，避免重名。</summary>
internal static class NodeExpander
{
    private const char PortPrefix = '@';

    /// <summary>执行次数上限的默认值：整条覆盖链都没有配置时生效。</summary>
    private const int DefaultMaxRuns = ExecutableNode.DefaultMaxRuns;

    /// <summary>展开作用域的一层：容器实例的成员原名到实例名的映射，以及端口绑定与模型槽位绑定。
    /// 每层以 parent 串起，从内向外解析成员名，从内向外透传端口与模型绑定。</summary>
    private sealed class Env(
        Env? parent,
        IReadOnlyDictionary<Flow.NodeName, Flow.NodeName> renames,
        IReadOnlyDictionary<Flow.NodeName, Flow.NodeName> bindings,
        IReadOnlyDictionary<Flow.ModelRef, Flow.ModelRef>? modelSlots)
    {
        public Env? Parent { get; } = parent;
        public IReadOnlyDictionary<Flow.NodeName, Flow.NodeName> Renames { get; } = renames;
        public IReadOnlyDictionary<Flow.NodeName, Flow.NodeName> Bindings { get; } = bindings;

        /// <summary>引用节点组时的模型槽位绑定：槽位名到流程模型配置名或外层槽位名的映射。</summary>
        public IReadOnlyDictionary<Flow.ModelRef, Flow.ModelRef>? ModelSlots { get; } = modelSlots;
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
        Flow.NodeSpec? expanded = ExpandNode(library, root, null, string.Empty, inInstance: false, currentLimit: null, flowName, errors);

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
        int? currentLimit,
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

                if (node.Model is not null)
                {
                    errors.Add(WorkflowErrors.Node(flowName, node.Name.Value, "引用节点组不能声明模型，用 Models 绑定组内成员的模型。"));
                }

                if (node.AnyOf.Count > 0)
                {
                    errors.Add(WorkflowErrors.Node(flowName, node.Name.Value, "引用节点组不能声明 AnyOf，起点条件组只属于执行节点。"));
                }

                if (node.Validate is not null)
                {
                    errors.Add(WorkflowErrors.Node(flowName, node.Name.Value, "引用节点组不能声明 Validate，输出校验只属于执行节点。"));
                }

                CheckBindings(node, definition, flowName, errors);
                Env childEnv = BuildContainerEnv(env, definition, instance, node.In, node.Models);
                int? memberLimit = node.MaxRuns ?? definition.MaxRuns ?? currentLimit;
                List<Flow.NodeSpec> members = ExpandMembers(library, definition.Nodes, childEnv, instance.Value + ".", flowName, errors, memberLimit);
                return new Flow.NodeSpec { Name = instance, Gate = definition.Gate, Nodes = members };
            }

            if (node.Models is not null)
            {
                errors.Add(WorkflowErrors.Node(flowName, node.Name.Value, "引用执行节点不能声明模型绑定，用 Model 指定模型配置。"));
            }

            return new Flow.NodeSpec
            {
                Name = instance,
                Gate = definition.Gate,
                Execution = definition.Execution! with
                {
                    AnyOf = node.AnyOf.Count > 0
                        ? ResolveGroups(node.AnyOf, env, flowName, errors)
                        : ResolveGroups(definition.Execution.AnyOf, env, flowName, errors),
                    Validate = node.Validate ?? definition.Execution.Validate,
                    MaxRuns = ResolveMaxRuns(node.MaxRuns, definition.Execution.MaxRuns, currentLimit),
                },
                Model = ResolveModel(node.Model, env, inInstance, flowName, node.Name.Value, errors),
                From = ResolveAll(node.From, env, flowName, errors),
            };
        }

        Flow.NodeName own = new(prefix + node.Name.Value);
        if (node.Nodes is { Count: > 0 } children)
        {
            // 装配层内联容器成员不再叠加容器名，库实例内成员才叠加实例前缀
            string memberPrefix = inInstance ? own.Value + "." : prefix;
            List<Flow.NodeSpec> expanded = ExpandMembers(library, children, env, memberPrefix, flowName, errors, node.MaxRuns ?? currentLimit);
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
            Execution = node.Execution! with
            {
                AnyOf = ResolveGroups(node.Execution.AnyOf, env, flowName, errors),
                Validate = node.Execution.Validate,
                MaxRuns = ResolveMaxRuns(node.Execution.MaxRuns, null, currentLimit),
            },
            Model = ResolveModel(node.Model, env, inInstance, flowName, node.Name.Value, errors),
            From = ResolveAll(node.From, env, flowName, errors),
        };
    }

    /// <summary>执行节点的生效执行上限：自身显式、库定义、外层容器统一值依次取先，都没有时落到默认值。</summary>
    private static int ResolveMaxRuns(int? own, int? library, int? currentLimit) =>
        own ?? library ?? currentLimit ?? DefaultMaxRuns;

    private static List<Flow.NodeSpec> ExpandMembers(
        IReadOnlyDictionary<Flow.NodeName, Flow.NodeSpec> library,
        IReadOnlyList<Flow.NodeSpec> members,
        Env? env,
        string prefix,
        string flowName,
        List<Error> errors,
        int? currentLimit)
    {
        List<Flow.NodeSpec> expanded = [];
        foreach (Flow.NodeSpec member in members)
        {
            Flow.NodeSpec? item = ExpandNode(library, member, env, prefix, inInstance: prefix.Length > 0, currentLimit, flowName, errors);
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

    /// <summary>构造容器实例的子作用域：整棵子树成员名对齐到带实例前缀的实例名，端口绑定与模型槽位绑定进作用域。
    /// parent 沿当前环境链挂上，便于 @更外层端口 与更外层模型槽位透传。</summary>
    private static Env BuildContainerEnv(
        Env? parent,
        Flow.NodeSpec container,
        Flow.NodeName instanceName,
        IReadOnlyDictionary<Flow.NodeName, Flow.NodeName>? bindings,
        IReadOnlyDictionary<Flow.ModelRef, Flow.ModelRef>? modelSlots)
    {
        Dictionary<Flow.NodeName, Flow.NodeName> renames = [];
        CollectRenames(container.Nodes!, renames, instanceName.Value + ".");

        return new Env(parent, renames, bindings ?? new Dictionary<Flow.NodeName, Flow.NodeName>(), modelSlots);
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

    /// <summary>逐个解析 AnyOf 组：每组内的引用名沿作用域解析，规则同 From。</summary>
    private static IReadOnlyList<IReadOnlyList<Flow.NodeName>> ResolveGroups(
        IReadOnlyList<IReadOnlyList<Flow.NodeName>> groups,
        Env? env,
        string flowName,
        List<Error> errors) =>
        [.. groups.Select(group => ResolveAll(group, env, flowName, errors))];

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

    /// <summary>执行节点的模型引用：装配层节点直接是流程模型配置名，节点组实例内成员是模型槽位，沿绑定链解析成配置名。
    /// 装配层引用执行节点未写模型时留空，交 WorkflowRules 报错。</summary>
    private static Flow.ModelRef? ResolveModel(
        Flow.ModelRef? model,
        Env? env,
        bool inInstance,
        string flowName,
        string nodeName,
        List<Error> errors)
    {
        if (model is null || !inInstance)
        {
            return model;
        }

        return ResolveSlot(model.Value, env, mustBind: true, flowName, nodeName, errors);
    }

    /// <summary>沿模型绑定链解析槽位：当前层命中取绑定值继续向更外层解析，直到装配层拿到流程模型配置名。
    /// 节点组内执行节点的模型必须从引用处绑定，未命中报错。</summary>
    private static Flow.ModelRef? ResolveSlot(
        Flow.ModelRef name,
        Env? env,
        bool mustBind,
        string flowName,
        string nodeName,
        List<Error> errors)
    {
        if (env is null || env.ModelSlots is null)
        {
            if (mustBind)
            {
                errors.Add(WorkflowErrors.Node(flowName, nodeName, $"模型槽位 {name} 没有外部绑定，引用节点组时用 Models 提供。"));
                return null;
            }

            return name;
        }

        if (env.ModelSlots.TryGetValue(name, out Flow.ModelRef bound))
        {
            return ResolveSlot(bound, env.Parent, mustBind: false, flowName, nodeName, errors);
        }

        return ResolveSlot(name, env.Parent, mustBind, flowName, nodeName, errors);
    }
}