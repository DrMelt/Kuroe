using Kuroe.Executor.Shared.Nodes;

namespace Kuroe.Executor.Compilation;

/// <summary>执行器描述注册表的默认实现：种类名到描述的唯一入口，同名覆盖，按注册序保留种类名。</summary>
public sealed class ExecutorRegistry : IExecutorRegistry
{
    private readonly Dictionary<string, IExecutorDescription> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ExecutorKind> _ordered = [];

    /// <summary>注册一个描述，同名覆盖。同名注册在原位替换，种类序不变。</summary>
    public void Register(IExecutorDescription description)
    {
        if (!_byName.ContainsKey(description.Kind.Value))
        {
            _ordered.Add(description.Kind);
        }

        _byName[description.Kind.Value] = description;
    }

    /// <summary>按种类取描述，未注册时为空。</summary>
    public IExecutorDescription? Find(ExecutorKind kind) => _byName.GetValueOrDefault(kind.Value);

    /// <summary>按文本种类名取描述，名字不区分大小写，未注册时为空。</summary>
    public IExecutorDescription? Find(string name) => _byName.GetValueOrDefault(name);

    /// <summary>已注册的种类名，按注册序。</summary>
    public IReadOnlyList<ExecutorKind> Kinds => _ordered;
}