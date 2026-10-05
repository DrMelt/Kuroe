using ErrorOr;
using Kuroe.Shared.Workflows.Graph;
using Flow = Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Workflows.Flows;

/// <summary>流程模板的校验规则。任务推进依赖这些不变量成立，加载与导入时逐条校验。
/// 一条流程内的错误一次给全。</summary>
static class FlowRules
{
    /// <summary>校验一条流程。</summary>
    public static ErrorOr<Success> Validate(Flow.FlowDefinition flow)
    {
        List<Error> errors = [];

        var modelNames = new HashSet<Flow.ModelRef>();
        foreach (Flow.ModelRef name in flow.Models.Select(model => model.Name))
        {
            if (string.IsNullOrWhiteSpace(name.Value))
            {
                errors.Add(FlowErrors.Model(flow.Name, "(未命名)", "模型配置名不能为空。"));
            }
            else if (!modelNames.Add(name))
            {
                errors.Add(FlowErrors.Model(flow.Name, name.Value, "模型配置名重复。"));
            }
        }

        var containerNames = new HashSet<Flow.NodeName>();
        CollectContainers(flow.RootNode, containerNames);

        var executableNames = new HashSet<Flow.NodeName>();
        CollectExecutables(flow.RootNode, executableNames);

        var names = new HashSet<Flow.NodeName>();
        CheckTree(flow.RootNode, names, executableNames, modelNames, containerNames, flow.Name, errors);

        // 引用类错误不存在时才展平，避免编译时的模型配置查表落空
        if (errors.Count == 0)
        {
            CheckShape(FlowCompiler.Compile(flow), flow.Name, errors);
        }

        return errors.Count > 0 ? errors : Result.Success;
    }

    /// <summary>收集全部容器名，From 引用允许指向容器。</summary>
    private static void CollectContainers(Flow.NodeSpec node, HashSet<Flow.NodeName> names)
    {
        if (node.Nodes is not { Count: > 0 } children)
        {
            return;
        }

        names.Add(node.Name);
        foreach (Flow.NodeSpec child in children)
        {
            CollectContainers(child, names);
        }
    }

    /// <summary>收集全部执行节点名，From 引用据此判定存在。</summary>
    private static void CollectExecutables(Flow.NodeSpec node, HashSet<Flow.NodeName> names)
    {
        if (node.Execution is not null)
        {
            names.Add(node.Name);
        }
        else if (node.Nodes is { Count: > 0 } children)
        {
            foreach (Flow.NodeSpec child in children)
            {
                CollectExecutables(child, names);
            }
        }
    }

    /// <summary>递归校验名字、From 引用、容器与模型配置引用。执行先后由拓扑排序保证，这里只检查引用落在执行节点或容器上。</summary>
    private static void CheckTree(
        Flow.NodeSpec node,
        HashSet<Flow.NodeName> names,
        HashSet<Flow.NodeName> executableNames,
        HashSet<Flow.ModelRef> modelNames,
        HashSet<Flow.NodeName> containerNames,
        string flowName,
        List<Error> errors)
    {
        foreach (Flow.NodeName from in node.From)
        {
            if (node.Nodes is { Count: > 0 })
            {
                break;
            }

            if (!executableNames.Contains(from) && !containerNames.Contains(from))
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, $"From 引用的节点 {from} 不在流程里。"));
            }
        }

        foreach (Flow.NodeName source in (node.Execution?.AnyOf ?? [])
            .SelectMany(group => group)
            .Distinct()
            .Where(source => !executableNames.Contains(source) && !containerNames.Contains(source)))
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, $"AnyOf 引用的节点 {source} 不在流程里。"));
        }

        CheckValidation(node, flowName, errors);

        if (string.IsNullOrWhiteSpace(node.Name.Value))
        {
            errors.Add(FlowErrors.Node(flowName, "(未命名)", "节点名不能为空。"));
        }
        else if (!names.Add(node.Name))
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, "节点名重复。"));
        }

        if (node.Nodes is { Count: > 0 } children)
        {
            if (node.MaxRuns is { } containerLimit && containerLimit < 1)
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, "MaxRuns 必须是正整数。"));
            }

            if (node.Model is not null || node.Models is not null)
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, "容器节点不是执行节点，不声明模型。"));
            }

            if (!HasExecutable(children))
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, "容器里至少要有一个执行节点。"));
            }
            else
            {
                foreach (Flow.NodeSpec child in children)
                {
                    CheckTree(child, names, executableNames, modelNames, containerNames, flowName, errors);
                }
            }

            if (node.From.Count > 0)
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, "容器节点不是执行节点，不支持 From。"));
            }
        }
        else if (node.Execution is not null)
        {
            if (node.Model is not { } model)
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, "执行节点必须声明模型配置。"));
            }
            else if (!modelNames.Contains(model))
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, $"引用的模型配置 {model.Value} 不存在。"));
            }

            CheckExpansion(node, flowName, errors);
            ValidateSplit(node, flowName, errors);
        }
        else if (node.Use is null)
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, "节点必须声明执行配置或子节点。"));
        }
    }

    /// <summary>子树里是否至少有一个执行节点。</summary>
    private static bool HasExecutable(IReadOnlyList<Flow.NodeSpec> nodes)
    {
        foreach (Flow.NodeSpec node in nodes)
        {
            if (node.Execution is not null)
            {
                return true;
            }

            if (node.Nodes is { Count: > 0 } children && HasExecutable(children))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>拆分配置的静态规则：只能写在规划执行节点上，条目与补充上限的取值边界。</summary>
    private static void ValidateSplit(Flow.NodeSpec node, string flowName, List<Error> errors)
    {
        Flow.ExecutableSpec? executable = node.Execution;
        if (executable?.Split is not { } split)
        {
            return;
        }

        if (executable.Output != Flow.NodeOutput.Plan)
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, "Split 只能写在规划节点上。"));
        }

        if (split.Items is null && split.ExtrasMax is null)
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, "Split 至少要声明 Items 或 ExtrasMax。"));
        }

        if (split.Items is null && split.ExtrasMax is 0)
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, "Split.ExtrasMax 为 0 时要求声明至少一条 Items。"));
        }

        if (split.Items is not null && split.Items.Count is 0 or > 20)
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, "Split.Items 要有 1 到 20 条。"));
        }

        if (split.Items is { } items
            && items.Any(item => string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Instruction)))
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, "Split.Items 每条的 Title 与 Instruction 不能为空。"));
        }

        if (split.ExtrasMax is < 0 or > 20)
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, "Split.ExtrasMax 必须是 0 到 20 的整数。"));
        }

        if (executable.IsStaticSplit)
        {
            if (executable.Prompt is not null)
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, "纯静态拆分节点不支持 Prompt。"));
            }

            if (node.From.Count > 0)
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, "纯静态拆分节点不支持 From。"));
            }

            if (node.Gate == Flow.NodeGate.Review)
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, "纯静态拆分节点不支持待批准门控。"));
            }
        }
    }

    /// <summary>AnyOf 与 Validate 的组合约束：两个能力都要求整节点执行，且不与 From、纯静态拆分叠用。</summary>
    private static void CheckExpansion(Flow.NodeSpec node, string flowName, List<Error> errors)
    {
        Flow.ExecutableSpec execution = node.Execution!;
        if (execution.MaxRuns is { } limit && limit < 1)
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, "MaxRuns 必须是正整数。"));
        }

        if (execution.Validate is not null)
        {
            if (execution.Mode == Flow.NodeMode.PerItem)
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, "声明 Validate 的执行节点不能按条目展开，必须是整节点执行。"));
            }

            if (execution.IsStaticSplit)
            {
                errors.Add(FlowErrors.Node(flowName, node.Name.Value, "纯静态拆分节点不支持 Validate。"));
            }
        }

        if (execution.AnyOf.Count == 0)
        {
            return;
        }

        if (execution.Mode == Flow.NodeMode.PerItem)
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, "声明 AnyOf 的执行节点不能按条目展开，必须是整节点执行。"));
        }

        if (execution.IsStaticSplit)
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, "纯静态拆分节点不支持 AnyOf。"));
        }

        // 组间判定以成员顺序为准：任两组完全相同视为重复，组内与组间成员允许重复
        if (execution.AnyOf.Any(group => group.Count == 0))
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, "AnyOf 组不能为空。"));
        }

        bool duplicated = false;
        for (int i = 0; i < execution.AnyOf.Count && !duplicated; i++)
        {
            for (int j = i + 1; j < execution.AnyOf.Count; j++)
            {
                if (execution.AnyOf[i].SequenceEqual(execution.AnyOf[j]))
                {
                    errors.Add(FlowErrors.Node(flowName, node.Name.Value, "AnyOf 各组必须整体不同。"));
                    duplicated = true;
                    break;
                }
            }
        }

        // 来源与 From 不得重叠，组内与跨组重复不影响判定
        IReadOnlyList<Flow.NodeName> anySources = [.. execution.AnyOf.SelectMany(group => group).Distinct()];
        foreach (Flow.NodeName source in node.From.Where(anySources.Contains))
        {
            errors.Add(FlowErrors.Node(flowName, node.Name.Value, $"节点 {source} 不能同时出现在 From 与 AnyOf。"));
        }
    }

    /// <summary>输出校验的取值边界：参数必要性、正则合法性。</summary>
    private static void CheckValidation(Flow.NodeSpec node, string flowName, List<Error> errors)
    {
        Flow.OutputValidation? validation = node.Execution?.Validate;
        if (validation is null)
        {
            return;
        }

        switch (validation.Predicate)
        {
            case Flow.ValidationPredicate.NonEmpty:
                if (validation.Argument is not null)
                {
                    errors.Add(FlowErrors.Node(flowName, node.Name.Value, "NonEmpty 校验不接受参数。"));
                }

                break;

            case Flow.ValidationPredicate.Pattern:
                if (string.IsNullOrEmpty(validation.Argument))
                {
                    errors.Add(FlowErrors.Node(flowName, node.Name.Value, "Pattern 校验需要正则参数。"));
                }
                else
                {
                    try
                    {
                        _ = new System.Text.RegularExpressions.Regex(
                            validation.Argument, System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1));
                    }
                    catch (System.Text.RegularExpressions.RegexParseException)
                    {
                        errors.Add(FlowErrors.Node(flowName, node.Name.Value, $"Pattern 校验的正则不合法：{validation.Argument}。"));
                    }
                }

                break;

            default:
                if (string.IsNullOrEmpty(validation.Argument))
                {
                    errors.Add(FlowErrors.Node(flowName, node.Name.Value, $"{validation.Predicate} 校验需要指定文本参数。"));
                }

                break;
        }
    }

    /// <summary>展平后的图规则：边组合、分支归属与拆分条目。执行直接按边推进。</summary>
    private static void CheckShape(NodeGraph graph, string flowName, List<Error> errors)
    {
        CheckLoops(graph, flowName, errors);
        CheckAlignment(graph, flowName, errors);

        foreach (FlowEdge edge in graph.Edges)
        {
            GraphNode source = graph[edge.From];
            if (graph[edge.To] is not ExecutableNode target)
            {
                continue;
            }

            if (target.Mode == Flow.NodeMode.Single
                && source is ExecutableNode { Mode: Flow.NodeMode.PerItem }
                && target.Output == Flow.NodeOutput.Plan)
            {
                errors.Add(FlowErrors.Node(flowName, target.Name.Value, "规划执行节点不能从按条目展开的执行节点取输入。"));
            }

            if (edge.Feed == EdgeFeed.Items && source is not ExecutableNode { Output: Flow.NodeOutput.Plan })
            {
                errors.Add(FlowErrors.Node(flowName, target.Name.Value, "按条目展开的执行节点只能从规划执行节点取拆分。"));
            }

            if (edge.Feed == EdgeFeed.Aligned
                && graph.ItemSpace(edge.From) is { } fromSpace
                && graph.ItemSpace(edge.To) is { } toSpace
                && fromSpace != toSpace)
            {
                errors.Add(FlowErrors.Node(flowName, target.Name.Value, "逐条对齐的两端必须来自同一个拆分。"));
            }

            if (target.Branch is { } branch
                && (target.Mode != Flow.NodeMode.PerItem || graph.ItemSource(target.Index) is null))
            {
                errors.Add(FlowErrors.Node(flowName, target.Name.Value, $"声明分支 {branch} 的执行节点必须按条目展开并从规划执行节点取拆分。"));
            }
        }

        // 按条目展开必须能确定实例集：从规划执行节点取拆分，或从其它展开执行节点取对齐
        foreach (ExecutableNode executable in graph.ExecutableNodes)
        {
            if (executable.Mode != Flow.NodeMode.PerItem)
            {
                continue;
            }

            bool expandable = graph.Incoming(executable.Index).Any(edge =>
                edge.Feed is EdgeFeed.Items or EdgeFeed.Aligned);
            if (!expandable)
            {
                // 容器来源只供整份上下文，不提供实例集，单独出现时节点永远无法展开
                string reason = graph.Incoming(executable.Index).Any(edge => graph[edge.From] is ContainerNode)
                    ? "按条目展开的执行节点不能从容器取实例集，请引用规划执行节点或其它展开执行节点。"
                    : "按条目展开的执行节点必须从规划执行节点或其它展开执行节点取输入。";
                errors.Add(FlowErrors.Node(flowName, executable.Name.Value, reason));
            }
        }

        // 分支声明必须落在引用它的静态拆分条目里
        foreach (ExecutableNode executable in graph.ExecutableNodes)
        {
            if (!executable.IsStaticSplit || executable.Split?.Items is not { } fixedItems)
            {
                continue;
            }

            foreach (Flow.SplitItem item in fixedItems)
            {
                if (item.Branch is not { } branch)
                {
                    continue;
                }

                bool owned = graph.Outgoing(executable.Index).Any(edge =>
                    edge.Feed == EdgeFeed.Items
                    && graph[edge.To] is ExecutableNode target
                    && target.Branch == branch);
                if (!owned)
                {
                    errors.Add(FlowErrors.Node(flowName, executable.Name.Value,
                        $"Split.Items 的分支“{branch}”没有对应的分支执行节点。"));
                }
            }
        }
    }

    /// <summary>引用环的启动不变量：每个环都必须有环外来源，否则环内节点永远等不到输入齐备，任务无法推进。
    /// 只做一次强连通检查，不产生环对象。整节点依赖构成的迭代环若可从外部启动则合法。</summary>
    private static void CheckLoops(NodeGraph graph, string flowName, List<Error> errors)
    {
        int count = graph.TotalExecutables;
        int[] global = [.. graph.ExecutableNodes.Select(executable => executable.Index)];
        var rank = new Dictionary<int, int>();
        for (int i = 0; i < global.Length; i++)
        {
            rank[global[i]] = i;
        }

        var adjacent = new List<List<int>>(count);
        for (int i = 0; i < count; i++)
        {
            adjacent.Add([]);
        }

        foreach (FlowEdge edge in graph.Edges)
        {
            // 成员从自身所在容器取输入会让容器永远等不到齐备，是启动即死的环
            if (graph[edge.From] is ContainerNode container
                && graph.ExecutablesIn(container.Index).Contains(edge.To))
            {
                errors.Add(FlowErrors.Node(flowName, graph[edge.To].Name.Value,
                    "执行节点不能从自身所在容器取输入，容器会永远等不到齐备。"));
            }

            foreach (int source in graph.ExecutablesIn(edge.From))
            {
                adjacent[rank[source]].Add(rank[edge.To]);
            }
        }

        var components = new List<List<int>>();
        var index = new int[count];
        var low = new int[count];
        Array.Fill(index, -1);
        var stack = new Stack<int>();
        var onStack = new bool[count];
        int next = 0;
        for (int start = 0; start < count; start++)
        {
            if (index[start] == -1)
            {
                Tarjan(start, adjacent, index, low, ref next, stack, onStack, components);
            }
        }

        foreach (List<int> component in components)
        {
            bool selfLoop = component.Any(member => adjacent[member].Contains(member));
            if (component.Count < 2 && !selfLoop)
            {
                continue;
            }

            bool externalEntry = component.Any(member =>
                graph.Incoming(global[member]).Any(edge =>
                    graph.ExecutablesIn(edge.From).Any(source => !component.Contains(rank[source]))));
            if (!externalEntry)
            {
                ExecutableNode representative = (ExecutableNode)graph[global[component[0]]];
                errors.Add(FlowErrors.Node(flowName, representative.Name.Value, "引用环没有环外来源，任务无法启动。"));
            }
        }
    }

    /// <summary>递归式 Tarjan 求强连通分量，图小型，递归深度受节点数限制。</summary>
    private static void Tarjan(
        int node,
        List<List<int>> adjacent,
        int[] index,
        int[] low,
        ref int next,
        Stack<int> stack,
        bool[] onStack,
        List<List<int>> components)
    {
        index[node] = next;
        low[node] = next;
        next++;
        stack.Push(node);
        onStack[node] = true;

        foreach (int successor in adjacent[node])
        {
            if (index[successor] == -1)
            {
                Tarjan(successor, adjacent, index, low, ref next, stack, onStack, components);
                low[node] = Math.Min(low[node], low[successor]);
            }
            else if (onStack[successor])
            {
                low[node] = Math.Min(low[node], index[successor]);
            }
        }

        if (low[node] != index[node])
        {
            return;
        }

        List<int> component = [];
        while (true)
        {
            int member = stack.Pop();
            onStack[member] = false;
            component.Add(member);
            if (member == node)
            {
                break;
            }
        }

        components.Add(component);
    }

    /// <summary>逐条对齐的引用必须无环：按条目展开的实例空间沿对齐边递推，环里的对齐会让它递归不止。
    /// 整节点依赖构成的环由运行时自然迭代，不在此拒绝。</summary>
    private static void CheckAlignment(NodeGraph graph, string flowName, List<Error> errors)
    {
        var indegree = new Dictionary<int, int>();
        var outgoing = new Dictionary<int, List<int>>();
        foreach (int index in graph.ExecutableNodes.Select(node => node.Index))
        {
            indegree[index] = 0;
            outgoing[index] = [];
        }

        foreach (FlowEdge edge in graph.Edges.Where(edge => edge.Feed is EdgeFeed.Items or EdgeFeed.Aligned))
        {
            foreach (int source in graph.ExecutablesIn(edge.From))
            {
                indegree[edge.To]++;
                outgoing[source].Add(edge.To);
            }
        }

        var ready = new Queue<int>(indegree.Where(entry => entry.Value == 0).Select(entry => entry.Key));
        int visited = 0;
        while (ready.TryDequeue(out int current))
        {
            visited++;
            foreach (int to in outgoing[current])
            {
                if (--indegree[to] == 0)
                {
                    ready.Enqueue(to);
                }
            }
        }

        if (visited < graph.ExecutableNodes.Count)
        {
            errors.Add(FlowErrors.Body(flowName, "逐条对齐的引用关系存在环，请检查 From。"));
        }
    }

    /// <summary>一组流程里重复的流程名，没有时为空。</summary>
    public static string? Duplicated(IReadOnlyList<Flow.FlowDefinition> flows) =>
        flows.GroupBy(flow => flow.Name).FirstOrDefault(group => group.Count() > 1)?.Key;
}
