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
        IReadOnlyDictionary<Flow.PortName, Flow.NodeName> bindings,
        IReadOnlyDictionary<Flow.ModelRef, Flow.ModelRef>? modelSlots)
    {
        public Env? Parent { get; } = parent;
        public IReadOnlyDictionary<Flow.NodeName, Flow.NodeName> Renames { get; } = renames;
        public IReadOnlyDictionary<Flow.PortName, Flow.NodeName> Bindings { get; } = bindings;

        /// <summary>引用节点组时的模型槽位绑定：槽位名到流程模型选择名或外层槽位名的映射。</summary>
        public IReadOnlyDictionary<Flow.ModelRef, Flow.ModelRef>? ModelSlots { get; } = modelSlots;
    }

    private static bool IsPort(Flow.NodeName name) => name.Value.StartsWith(PortPrefix);
    private static string PortOf(Flow.NodeName name) => name.Value[1..];

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
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, $"引用的节点 {use} 不在节点库。"));
                return null;
            }

            string invocation = node.Name.Value.Length > 0 ? node.Name.Value : definition.Name.Value;
            Flow.NodeName instance = new(prefix + invocation);
            if (definition.Nodes is { Count: > 0 })
            {
                if (node.From.Count > 0)
                {
                    errors.Add(FlowErrors.Node(flowName, node.Name.Value, "引用容器不能声明 From，容器自身不接线。"));
                }

                if (node.Model is not null)
                {
                    errors.Add(FlowErrors.Node(flowName, node.Name.Value, "引用节点组不能声明模型，用 Models 绑定组内成员的模型。"));
                }

                if (node.Validate is not null)
                {
                    errors.Add(FlowErrors.Node(flowName, node.Name.Value, "引用节点组不能声明 Validate，输出校验只属于执行节点。"));
                }

                CheckBindings(node, definition, flowName, errors);
                Env childEnv = BuildContainerEnv(env, definition, instance, node.In, node.Models);
                int? memberLimit = node.MaxRuns ?? definition.MaxRuns ?? currentLimit;
                List<Flow.NodeSpec> members = ExpandMembers(library, definition.Nodes, childEnv, instance.Value + ".", flowName, errors, memberLimit);
                return new Flow.NodeSpec
                {
                    Name = instance,
                    Gate = definition.Gate,
                    Nodes = members,
                    Out = ResolveOuts(definition.Out, childEnv, flowName, errors),
                };
            }

            if (node.Models is not null)
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, "引用执行节点不能声明模型绑定，用 Model 指定模型选择。"));
            }

            if (node.In is { Count: > 0 })
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, "引用执行节点不写输入端口绑定，ContextInput 用 From 条目的 Context 标记。"));
            }

            return new Flow.NodeSpec
            {
                Name = instance,
                Gate = definition.Gate,
                Execution = definition.Execution! with
                {
                    Validate = node.Validate ?? definition.Execution.Validate,
                    MaxRuns = ResolveMaxRuns(node.MaxRuns, definition.Execution.MaxRuns, currentLimit),
                },
                Model = ResolveModel(node.Model, env, inInstance, flowName, node.Name.Value, errors),
                From = ResolveAll(node.From, env, flowName, errors),
                Outputs = definition.Outputs,
                SystemPrompt = definition.SystemPrompt,
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
                Validate = node.Execution.Validate,
                MaxRuns = ResolveMaxRuns(node.Execution.MaxRuns, null, currentLimit),
            },
            Model = ResolveModel(node.Model, env, inInstance, flowName, node.Name.Value, errors),
            From = ResolveAll(node.From, env, flowName, errors),
            Outputs = node.Outputs,
            SystemPrompt = node.SystemPrompt,
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

        var declared = new HashSet<Flow.PortName>([.. definition.Inputs]);
        foreach (Flow.PortName port in bindings.Keys.Where(port => !declared.Contains(port)))
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, $"端口 {port} 不在容器 {definition.Name} 上。"));
        }
    }

    /// <summary>构造容器实例的子作用域：整棵子树成员名对齐到带实例前缀的实例名，端口绑定与模型槽位绑定进作用域。
    /// parent 沿当前环境链挂上，便于 @更外层端口 与更外层模型槽位透传。</summary>
    private static Env BuildContainerEnv(
        Env? parent,
        Flow.NodeSpec container,
        Flow.NodeName instanceName,
        IReadOnlyDictionary<Flow.PortName, Flow.NodeName>? bindings,
        IReadOnlyDictionary<Flow.ModelRef, Flow.ModelRef>? modelSlots)
    {
        Dictionary<Flow.NodeName, Flow.NodeName> renames = [];
        CollectRenames(container.Nodes!, renames, instanceName.Value + ".");

        return new Env(parent, renames, bindings ?? new Dictionary<Flow.PortName, Flow.NodeName>(), modelSlots);
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

    /// <summary>逐个解析上游接线条目：来源名沿实例映射链与端口绑定链解析，Or 与 Signal 标记保留。</summary>
    private static IReadOnlyList<Flow.SourceRef> ResolveAll(
        IReadOnlyList<Flow.SourceRef> from,
        Env? env,
        string flowName,
        List<Error> errors) =>
        [.. from.Select(source => Resolve(source, env, flowName, errors))];

    /// <summary>解析一条上游接线条目：来源名按引用名规则解析，标记保留。</summary>
    private static Flow.SourceRef Resolve(Flow.SourceRef source, Env? env, string flowName, List<Error> errors)
    {
        Flow.NodeName name = Resolve(source.Name, env, flowName, errors);
        return source with { Name = name };
    }

    /// <summary>解析一个引用名：@端口沿绑定链找来源，来源@端口 拆来源沿实例映射解析并保留端口，普通名沿环境链找实例映射。
    /// 都没有则保留原名交既有校验判断。</summary>
    private static Flow.NodeName Resolve(Flow.NodeName name, Env? env, string flowName, List<Error> errors)
    {
        if (name.Value.StartsWith(PortPrefix))
        {
            string port = PortOf(name);
            Env? chain = env;
            while (chain is not null)
            {
                if (chain.Bindings.TryGetValue(new Flow.PortName(port), out Flow.NodeName bound))
                {
                    // @绑定值沿更外层作用域透传；普通名绑定在所在作用域解析成实例名
                    return IsPort(bound) ? Resolve(bound, chain.Parent, flowName, errors) : ResolveName(bound, chain);
                }

                chain = chain.Parent;
            }

            errors.Add(FlowErrors.Node(flowName, name.Value, $"端口 {name} 没有绑定来源。"));
            return name;
        }

        if (PortRef.Split(name) is { } reference)
        {
            Flow.NodeName source = ResolveName(reference.Source, env);
            if (reference.Port.Value.Contains(PortPrefix))
            {
                errors.Add(FlowErrors.Node(flowName, name.Value, $"输出端口名 {reference.Port.Value} 不能含 @。"));
            }

            return new Flow.NodeName($"{source.Value}@{reference.Port.Value}");
        }

        return ResolveName(name, env);
    }

    /// <summary>普通节点引用沿环境链找实例映射，找不到时保留原名。</summary>
    private static Flow.NodeName ResolveName(Flow.NodeName name, Env? env)
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

    /// <summary>容器输出端口的实例化：绑定值沿子作用域解析成实例成员引用，前缀由此加上。</summary>
    private static Dictionary<Flow.PortName, Flow.NodeName>? ResolveOuts(
        IReadOnlyDictionary<Flow.PortName, Flow.NodeName>? outs,
        Env? env,
        string flowName,
        List<Error> errors)
    {
        if (outs is not { Count: > 0 })
        {
            return null;
        }

        return outs.ToDictionary(
            entry => entry.Key,
            entry => Resolve(entry.Value, env, flowName, errors));
    }

    /// <summary>执行节点的模型引用：装配层节点直接是流程模型选择名，节点组实例内成员是模型槽位，沿绑定链解析成选择名。
    /// 装配层引用执行节点未写模型时留空，交 FlowRules 报错。</summary>
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

    /// <summary>沿模型绑定链解析槽位：当前层命中取绑定值继续向更外层解析，直到装配层拿到流程模型选择名。
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
                errors.Add(FlowErrors.Node(flowName, nodeName, $"模型槽位 {name} 没有外部绑定，引用节点组时用 Models 提供。"));
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
