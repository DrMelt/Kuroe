using ErrorOr;
using Kuroe.Configuration;

namespace Kuroe.Workflows.Flows;

/// <summary>运行期的流程模板集合。加载即校验，全部非法项一次给出；导入合并后落盘。</summary>
public sealed class WorkflowService
{
    private readonly WorkflowStore _store;
    private readonly SettingsProvider _settings;
    private readonly Lock _gate = new();
    private List<Workflow> _flows;

    private WorkflowService(WorkflowStore store, SettingsProvider settings, List<Workflow> flows)
    {
        _store = store;
        _settings = settings;
        _flows = flows;
    }

    /// <summary>从流程文件装配。任一条流程不合法时返回全部错误，装配失败。</summary>
    internal static ErrorOr<WorkflowService> Create(WorkflowStore store, SettingsProvider settings)
    {
        ErrorOr<List<Workflow>> loaded = store.Load();
        if (loaded.IsError)
        {
            return loaded.ErrorsOrEmptyList;
        }

        List<Error> errors = Validate(loaded.Value, settings.Current.Agent.MaxAttempts, out List<Workflow> valid);
        if (WorkflowRules.Duplicated(loaded.Value) is { } duplicated)
        {
            errors.Add(WorkflowErrors.Body(duplicated, "流程名重复。"));
        }

        return errors.Count > 0 ? errors : new WorkflowService(store, settings, valid);
    }

    /// <summary>提交任务未指定流程时用的流程名，取自用户层；未设置时为内置流程。</summary>
    public string DefaultName => _settings.Current.Agent.DefaultFlow ?? Workflow.BuiltinName;

    /// <summary>全部流程，按文件顺序。</summary>
    public IReadOnlyList<Workflow> All()
    {
        lock (_gate)
        {
            return [.. _flows];
        }
    }

    public ErrorOr<Workflow> Find(string name)
    {
        lock (_gate)
        {
            Workflow? flow = _flows.Find(candidate => candidate.Name == name);

            return flow is null ? [WorkflowErrors.FlowNotFound(name)] : flow;
        }
    }

    public ErrorOr<Workflow> Default() => Find(DefaultName);

    /// <summary>从文件导入流程：逐条校验，任一条不合法就整体不生效；同名整条覆盖，新的追加，成功后落盘。</summary>
    public ErrorOr<WorkflowImport> Import(string source)
    {
        ErrorOr<string> resolved = _store.Resolve(source);
        if (resolved.IsError)
        {
            return resolved.ErrorsOrEmptyList;
        }

        ErrorOr<List<Workflow>> parsed = WorkflowStore.Read(resolved.Value);
        if (parsed.IsError)
        {
            return parsed.ErrorsOrEmptyList;
        }

        List<Error> errors = Validate(parsed.Value, _settings.Current.Agent.MaxAttempts, out List<Workflow> candidates);
        if (errors.Count > 0)
        {
            return errors;
        }

        lock (_gate)
        {
            List<string> notes = [];
            List<Workflow> merged = [.. _flows];
            foreach (Workflow flow in candidates)
            {
                int index = merged.FindIndex(candidate => candidate.Name == flow.Name);
                notes.Add(index < 0 ? $"新增流程 {flow.Name}" : $"覆盖流程 {flow.Name}");
                if (index < 0)
                {
                    merged.Add(flow);
                }
                else
                {
                    merged[index] = flow;
                }
            }

            ErrorOr<Success> saved = _store.Save(merged);
            if (saved.IsError)
            {
                return saved.ErrorsOrEmptyList;
            }

            _flows = merged;

            return new WorkflowImport(resolved.Value, [.. candidates.Select(flow => flow.Name)], notes);
        }
    }

    /// <summary>逐条校验，合法流程经 valid 交回，返回值是全部错误。</summary>
    private static List<Error> Validate(IReadOnlyList<Workflow> flows, int attemptLimit, out List<Workflow> valid)
    {
        List<Error> errors = [];
        List<Workflow> accepted = [];
        foreach (Workflow flow in flows)
        {
            ErrorOr<Success> checkedFlow = WorkflowRules.Validate(flow, attemptLimit);
            if (checkedFlow.IsError)
            {
                errors.AddRange(checkedFlow.ErrorsOrEmptyList);
            }
            else
            {
                accepted.Add(flow);
            }
        }

        valid = accepted;

        return errors;
    }
}

/// <summary>一次流程导入的结果：来源文件的绝对路径与逐条说明。</summary>
public sealed record WorkflowImport(string Source, IReadOnlyList<string> Names, IReadOnlyList<string> Notes);
